using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hspi.Utils;
using Humanizer;
using Nito.AsyncEx;
using Serilog;
using SQLitePCL;
using SQLitePCL.Ugly;
using static System.FormattableString;
using static SQLitePCL.raw;

#nullable enable

namespace Hspi.Database
{
    public sealed class SqliteDatabaseCollector : IDisposable
    {
        static SqliteDatabaseCollector()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Batteries_V2.Init();
            }
            else
            {
                SetProvider(new SQLite3Provider_sqlite3());
            }
        }

        public SqliteDatabaseCollector(IDBSettings settings, ISystemClock systemClock, CancellationToken shutdownToken)
        {
            this.settings = settings;
            this.systemClock = systemClock;
            this.shutdownToken = shutdownToken;
            CreateDBDirectory(settings.DBPath);

            const int OpenFlags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_PRIVATECACHE;

            sqliteConnection = ugly.open_v2(settings.DBPath, OpenFlags, null);
            Log.Information("{dll} version:{version}", GetNativeLibraryName(), sqlite3_libversion().utf8_to_string());

            SetupDatabase();

            insertCommand = CreateStatement(InsertSql);
            getHistoryCommand = CreateStatement(RecordsHistorySql);
            getRecordHistoryCountCommand = CreateStatement(RecordsHistoryCountSql);
            getTimeAndValueCommand = CreateStatement(GetTimeValueSql);
            getEarliestAndOldestRecordCommand = CreateStatement(EarliestAndOldestRecordSql);
            allRefOldestRecordsCommand = CreateStatement(AllRefOldestRecordsSql);
            deleteOldRecordByRefCommand = CreateStatement(DeleteOldRecordByRefSql);
            deleteAllRecordByRefCommand = CreateStatement(DeleteAllRecordByRefSql);
            Utils.TaskHelper.StartAsyncWithErrorChecking("DB update records", UpdateRecords, shutdownToken);
            Utils.TaskHelper.StartAsyncWithErrorChecking("Prune DB records", PruneRecords, shutdownToken);

            static void CreateDBDirectory(string dbPath)
            {
                string dirPath = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
            }
        }

        public async Task<long> DeleteAllRecordsForRef(long refId)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = deleteAllRecordByRefCommand;
            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.step_done(stmt);

            var changesCount = ugly.changes(sqliteConnection);
            Log.Information("Removed {rows} row(s) for device:{refId} in database", changesCount, refId);
            return changesCount;
        }

        public void Dispose()
        {
            getHistoryCommand?.Dispose();
            getEarliestAndOldestRecordCommand?.Dispose();
            getRecordHistoryCountCommand?.Dispose();
            getTimeAndValueCommand?.Dispose();
            insertCommand?.Dispose();
            allRefOldestRecordsCommand?.Dispose();
            deleteOldRecordByRefCommand.Dispose();
            deleteAllRecordByRefCommand.Dispose();
            sqliteConnection?.Dispose();
        }

        public async Task<IDictionary<string, string>> GetDatabaseStats()
        {
            return new Dictionary<string, string>()
            {
                { "Path", settings.DBPath },
                { "Sqlite version",  raw.sqlite3_libversion().utf8_to_string() },
                { "Sqlite memory used",  raw.sqlite3_memory_used().Bytes().Humanize() },
                { "Size",   GetTotalFileSize().Bytes().Humanize() },
                { "Total records",  (await GetTotalRecords().ConfigureAwait(false)).ToString("N0") },
                { "Total records from last 24 hr",  (await GetTotalRecordsInLastDay().ConfigureAwait(false)).ToString("N0") },
            };

            long GetTotalFileSize()
            {
                return GetFileSizeIfExists(settings.DBPath) +
                       GetFileSizeIfExists(settings.DBPath + "-shm") +
                       GetFileSizeIfExists(settings.DBPath + "-wal");
            }

            long GetFileSizeIfExists(string dBPath)
            {
                try
                {
                    var info = new FileInfo(dBPath);
                    return info.Length;
                }
                catch (FileNotFoundException)
                {
                    return 0;
                }
            }

            async Task<long> GetTotalRecords()
            {
                using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history");
            }

            async Task<long> GetTotalRecordsInLastDay()
            {
                using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history WHERE ts>(STRFTIME('%s')-86400)");
            }
        }

        public async Task<Tuple<DateTimeOffset, DateTimeOffset>> GetEarliestAndOldestRecordTimeDate(long refId)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            var stmt = getEarliestAndOldestRecordCommand;

            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.step(stmt);

            return new Tuple<DateTimeOffset, DateTimeOffset>(
                        DateTimeOffset.FromUnixTimeSeconds(stmt.column<long>(0)),
                        DateTimeOffset.FromUnixTimeSeconds(stmt.column<long>(1)));
        }

        /// <summary>
        /// Returns the values between the range and one above and below the range.  Order is time stamp ascending.
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="minUnixTimeSeconds"></param>
        /// <param name="maxUnixTimeSeconds"></param>
        /// <returns></returns>
        public async Task<IList<TimeAndValue>> GetGraphValues(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            List<TimeAndValue> records = new();
            await IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, (x) => records.AddRange(x)).ConfigureAwait(false);
            return records;
        }

        public async Task<IList<RecordDataAndDuration>> GetRecords(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds,
                                                                long start, long length, ResultSortBy sortBy)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            var stmt = getHistoryCommand;

            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);
            ugly.bind_int64(stmt, 4, (long)sortBy);
            ugly.bind_int64(stmt, 5, length);
            ugly.bind_int64(stmt, 6, start);

            List<RecordDataAndDuration> records = new();

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                // order: SELECT (time, value, string, duration) FROM history
                var record = new RecordDataAndDuration(
                        refId,
                        ugly.column_double(stmt, 1),
                        ugly.column_text(stmt, 2),
                        ugly.column_int64(stmt, 0),
                        ugly.column_type(stmt, 3) == SQLITE_NULL ? null : ugly.column_int64(stmt, 3)
                    );

                records.Add(record);
            }

            return records;
        }

        public async Task<long> GetRecordsCount(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getRecordHistoryCountCommand;
            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);
            ugly.step(stmt);

            return stmt.column<long>(0);
        }

        public async Task<IList<KeyValuePair<long, long>>> GetRecordsWithCount(int limit)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            var records = new List<KeyValuePair<long, long>>();
            using var stmt = CreateStatement("SELECT ref, COUNT(*) as rcount FROM history GROUP BY ref ORDER BY rcount DESC LIMIT ?");

            ugly.bind_int64(stmt, 1, limit);
            while (ugly.step(stmt) != SQLITE_DONE)
            {
                records.Add(new KeyValuePair<long, long>(ugly.column_int64(stmt, 0), ugly.column_int64(stmt, 1)));
            }

            return records;
        }

        public async Task<IList<long>> GetRefIdsWithRecords()
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var records = new List<long>();
            using var stmt = CreateStatement("SELECT DISTINCT ref FROM history");

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                records.Add(ugly.column_int64(stmt, 0));
            }

            return records;
        }

        /// <summary>
        /// Iterates the values between the range and one above and below the range. Order is time stamp ascending.
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="minUnixTimeSeconds"></param>
        /// <param name="maxUnixTimeSeconds"></param>
        /// <param name="iterator">Function called for iteration</param>
        public async Task IterateGraphValues(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds, Action<IEnumerable<TimeAndValue>> iterator)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getTimeAndValueCommand;
            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);

            iterator(Iterate(stmt));

            static IEnumerable<TimeAndValue> Iterate(sqlite3_stmt stmt)
            {
                while (ugly.step(stmt) != SQLITE_DONE)
                {
                    // order: SELECT (time, value) FROM history
                    var record = new TimeAndValue(ugly.column_int64(stmt, 0), ugly.column_double(stmt, 1));
                    yield return record;
                }
            }
        }

        public void PruneNow()
        {
            Log.Information("Pruning for database triggered");
            pruneNowEvent.Set();
        }

        public async Task Record(RecordData recordData)
        {
            await queue.EnqueueAsync(recordData).ConfigureAwait(false);
        }

        private sqlite3_stmt CreateStatement(string sql)
        {
            var command = ugly.prepare_v3(sqliteConnection, sql, SQLITE_PREPARE_PERSISTENT);
            return command;
        }

        private async Task InsertRecord(RecordData record)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            sqlite3_stmt stmt = insertCommand;
            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, record.TimeStamp.ToUnixTimeSeconds());
            ugly.bind_int64(stmt, 2, record.DeviceRefId);
            ugly.bind_double(stmt, 3, record.DeviceValue);
            ugly.bind_text(stmt, 4, record.DeviceString);
            ugly.step_done(stmt);
        }

        private async Task PruneRecords()
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                try
                {
                    Log.Information("Starting pruning database");

                    using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

                    var recordsToKeep = settings.MinRecordsToKeep;
                    allRefOldestRecordsCommand.reset();

                    DateTimeOffset now = systemClock.Now;
                    while (ugly.step(allRefOldestRecordsCommand) != SQLITE_DONE)
                    {
                        // order: SELECT time, MIN(TS) FROM history
                        var refId = ugly.column_int64(allRefOldestRecordsCommand, 0);
                        var oldestRecordUnixTimeSeconds = ugly.column_int64(allRefOldestRecordsCommand, 1);
                        var totalRecords = ugly.column_int64(allRefOldestRecordsCommand, 2);

                        var cutoffTime = now - settings.GetDeviceRetentionPeriod(refId);
                        var cutoffUnixTimeSeconds = cutoffTime.ToUnixTimeSeconds();
                        bool hasRecordsNeedingPruning = oldestRecordUnixTimeSeconds <= cutoffUnixTimeSeconds && totalRecords > recordsToKeep;
                        if (hasRecordsNeedingPruning)
                        {
                            Log.Debug("Pruning device:{refId} in database", refId);
                            PruneRecord(refId, recordsToKeep, cutoffUnixTimeSeconds);
                        }
                    }

                    Log.Information("Finished pruning database");
                }
                catch (Exception ex) when (!ex.IsCancelException())
                {
                    Log.Warning("Failed to prune with {error}}", ExceptionHelper.GetFullMessage(ex));
                }

                var eventWaitTask = pruneNowEvent.WaitAsync(shutdownToken);
                await Task.WhenAny(Task.Delay(TimeSpan.FromHours(1), shutdownToken), eventWaitTask).ConfigureAwait(false);
            }

            void PruneRecord(long refId, long recordsToKeep, long cutoffUnixTimeSeconds)
            {
                var stmt = deleteOldRecordByRefCommand;
                ugly.reset(stmt);
                ugly.bind_int64(stmt, 1, refId);
                ugly.bind_int64(stmt, 2, cutoffUnixTimeSeconds);
                ugly.bind_int64(stmt, 3, recordsToKeep);
                ugly.step_done(stmt);

                var changesCount = ugly.changes(sqliteConnection);
                Log.Information("Removed {rows} row(s) for device:{refId} in database", changesCount, refId);
            }
        }

        private void SetupDatabase()
        {
            Log.Information("Connecting to database: {dbPath}", settings.DBPath);

            CheckSqliteValid();

            ugly.exec(sqliteConnection, "PRAGMA page_size=4096");
            ugly.exec(sqliteConnection, "PRAGMA journal_mode=WAL");
            ugly.exec(sqliteConnection, Invariant($"PRAGMA journal_size_limit={10 * 1024 * 1024}")); //10 MB
            ugly.exec(sqliteConnection, "PRAGMA synchronous=normal");
            ugly.exec(sqliteConnection, "PRAGMA locking_mode=EXCLUSIVE");
            ugly.exec(sqliteConnection, "PRAGMA temp_store=MEMORY");
            ugly.exec(sqliteConnection, "PRAGMA auto_vacuum=INCREMENTAL");
            ugly.exec(sqliteConnection, "PRAGMA integrity_check");

            ugly.wal_autocheckpoint(sqliteConnection, 100);

            ugly.exec(sqliteConnection, "BEGIN TRANSACTION");

            try
            {
                ugly.exec(sqliteConnection, "CREATE TABLE IF NOT EXISTS history(ts INTEGER NOT NULL, ref INTEGER NOT NULL, value REAL NOT NULL, str TEXT, PRIMARY KEY(ts,ref)) WITHOUT ROWID, STRICT");
                ugly.exec(sqliteConnection, "CREATE INDEX IF NOT EXISTS history_time_index ON history (ts);");
                ugly.exec(sqliteConnection, "CREATE INDEX IF NOT EXISTS history_ref_index ON history (ref);");
                ugly.exec(sqliteConnection, "CREATE UNIQUE INDEX IF NOT EXISTS history_time_ref_index ON history (ts, ref);");
                ugly.exec(sqliteConnection, "CREATE VIEW IF NOT EXISTS history_with_duration as select ts, value, str, (lag(ts, 1) OVER ( PARTITION BY ref ORDER BY ts desc) - ts) as duration, ref from history order by ref, ts desc");
                ugly.exec(sqliteConnection, "PRAGMA user_version=1");
                ugly.exec(sqliteConnection, "COMMIT");
            }
            catch (Exception)
            {
                ugly.exec(sqliteConnection, "ROLLBACK");
                throw;
            }

            static void CheckSqliteValid()
            {
                if (sqlite3_threadsafe() == 0)
                {
                    throw new SystemException(@"Sqlite is not thread safe");
                }

                if (Version.TryParse(sqlite3_libversion().utf8_to_string(), out var version))
                {
                    var minSupportedVersion = new Version(3, 37);
                    if (version < minSupportedVersion)
                    {
                        throw new SystemException("Sqlite version on machine is too old. Need 3.37+");
                    }
                }
            }
        }

        private async Task UpdateRecords()
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                var record = await queue.DequeueAsync(shutdownToken).ConfigureAwait(false);
                try
                {
                    Log.Information("Adding to database: {@record}", record);
                    await InsertRecord(record).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCancelException())
                {
                    Log.Warning("Failed to update {record} with {error}}", record, ExceptionHelper.GetFullMessage(ex));

                    await queue.EnqueueAsync(record, shutdownToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(30), shutdownToken).ConfigureAwait(false);
                }
            }
        }

        private const string AllRefOldestRecordsSql = "SELECT ref, MIN(ts), COUNT(*) FROM history GROUP BY ref";

        private const string DeleteAllRecordByRefSql = "DELETE from history where ref=$ref";
        private const string DeleteOldRecordByRefSql = "DELETE from history where ref=$ref and ts<$time AND ts NOT IN ( SELECT ts FROM history WHERE ref=$ref ORDER BY ts DESC LIMIT $limit)";
        private const string EarliestAndOldestRecordSql = "SELECT MIN(ts), MAX(ts) FROM history WHERE ref=?";

        // 1 record before the time range and one after
        private const string GetTimeValueSql =
            @"SELECT * FROM (SELECT ts, value FROM history WHERE ref=$ref AND ts<$min ORDER BY ts DESC LIMIT 1) UNION
              SELECT * FROM (SELECT ts, value FROM history WHERE ref=$ref AND ts>$max ORDER BY ts LIMIT 1) UNION
              SELECT ts, value FROM history WHERE ref=$ref AND ts>=$min AND ts<=$max ORDER BY ts";

        private const string InsertSql = "INSERT OR REPLACE INTO history(ts, ref, value, str) VALUES(?,?,?,?)";
        private const string RecordsHistoryCountSql = "SELECT COUNT(*) FROM history WHERE ref=? AND ts>=? AND ts<=?";

        private const string RecordsHistorySql = @"
                SELECT ts, value, str, duration FROM history_with_duration
                WHERE ref=$refid AND ts>=$minV AND ts<=$maxV
                ORDER BY
                    CASE WHEN $order = 0 THEN ts END DESC,
                    CASE WHEN $order = 1 THEN value END DESC,
                    CASE WHEN $order = 2 THEN str END DESC,
                    CASE WHEN $order = 3 THEN duration END DESC,
                    CASE WHEN $order = 4 THEN ts END ASC,
                    CASE WHEN $order = 5 THEN value END ASC,
                    CASE WHEN $order = 6 THEN str END ASC,
                    CASE WHEN $order = 7 THEN duration END ASC
                LIMIT $limit OFFSET $offset";

        private readonly sqlite3_stmt allRefOldestRecordsCommand;
        private readonly AsyncLock connectionLock = new();
        private readonly sqlite3_stmt deleteAllRecordByRefCommand;
        private readonly sqlite3_stmt deleteOldRecordByRefCommand;
        private readonly sqlite3_stmt getEarliestAndOldestRecordCommand;
        private readonly sqlite3_stmt getHistoryCommand;
        private readonly sqlite3_stmt getRecordHistoryCountCommand;
        private readonly sqlite3_stmt getTimeAndValueCommand;
        private readonly sqlite3_stmt insertCommand;
        private readonly AsyncAutoResetEvent pruneNowEvent = new(false);
        private readonly AsyncProducerConsumerQueue<RecordData> queue = new();
        private readonly IDBSettings settings;
        private readonly CancellationToken shutdownToken;
        private readonly sqlite3 sqliteConnection;
        private readonly ISystemClock systemClock;
    }
}