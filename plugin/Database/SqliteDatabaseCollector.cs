using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Hspi.Utils;
using Humanizer;
using Nito.Disposables;
using Serilog;
using SQLitePCL;
using SQLitePCL.Ugly;
using static System.FormattableString;
using static SQLitePCL.raw;

#nullable enable

namespace Hspi.Database
{
    internal sealed class SqliteDatabaseCollector : IDisposable
    {
        public SqliteDatabaseCollector(IDBSettings settings,
                                       IGlobalTimerAndClock globalTimerAndClock,
                                       RecordDataProducerConsumerQueue queue,
                                       CancellationToken shutdownToken)
        {
            this.settings = settings;
            this.globalTimerAndClock = globalTimerAndClock;
            this.MaintainanceIntervalMs = (int)globalTimerAndClock.MaintenanceInterval.TotalMilliseconds;
            this.queue = queue;
            this.shutdownToken = shutdownToken;
            CreateDBDirectory(settings.DBPath);

            const int SQLITE_OPEN_EXRESCODE = 0x02000000;
            const int OpenFlags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE |
                                  SQLITE_OPEN_PRIVATECACHE | SQLITE_OPEN_NOMUTEX | SQLITE_OPEN_EXRESCODE;

            sqliteConnection = ugly.open_v2(settings.DBPath, OpenFlags, null);
            Log.Information("{dll} version:{version}", GetNativeLibraryName(), sqlite3_libversion().utf8_to_string());

            SetupDatabase();

            insertCommand = CreateStatement(InsertSql);
            getHistoryCommand = CreateStatement(RecordsHistorySql);
            getRecordHistoryCountCommand = CreateStatement(RecordsHistoryCountSql);
            getTimeAndValueCommand = CreateStatement(GetTimeValuesSql);
            getEarliestAndOldestRecordCommand = CreateStatement(EarliestAndOldestRecordSql);
            allRefOldestRecordsCommand = CreateStatement(AllRefOldestRecordsSql);
            deleteOldRecordByRefCommand = CreateStatement(DeleteOldRecordByRefSql);
            deleteAllRecordByRefCommand = CreateStatement(DeleteAllRecordByRefSql);
            getMaxValueCommand = CreateStatement(GetMaxValuesSql);
            getMinValueCommand = CreateStatement(GetMinValuesSql);
            getStrForRefAndValueCommand = CreateStatement(GetStrForRefAndValueSql);

            var recordUpdateThread = new Thread(UpdateRecords);
            recordUpdateThread.Start();

            maintainanceTimer = new Timer(Maintenance, null, 0, MaintainanceIntervalMs);

            static void CreateDBDirectory(string dbPath)
            {
                string dirPath = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
            }
        }

        public Exception? RecordUpdateException { get; private set; }

        public long DeleteAllRecordsForRef(long refId)
        {
            using var lock2 = CreateLockForDBConnection();

            var stmt = deleteAllRecordByRefCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.step_done(stmt);

            var changesCount = ugly.changes(sqliteConnection);
            Log.Information("Removed {rows} row(s) for device:{refId} in database", changesCount, refId);
            return changesCount;
        }

        public void DeleteRecordsOutsideRangeForRef(int refId, double? minValue, double? maxValue)
        {
            if (minValue.HasValue)
            {
                using var lock2 = CreateLockForDBConnection();
                using var stmt = CreateStatement("DELETE FROM history where ref=$ref and value<$min");
                RemoveInvalidValues(stmt, refId, minValue.Value);
            }

            if (maxValue.HasValue)
            {
                using var lock2 = CreateLockForDBConnection();
                using var stmt = CreateStatement("DELETE FROM history where ref=$ref and value>$max");
                RemoveInvalidValues(stmt, refId, maxValue.Value);
            }

            void RemoveInvalidValues(sqlite3_stmt stmt, int refId, double value)
            {
                ugly.bind_int64(stmt, 1, refId);
                ugly.bind_double(stmt, 2, value);
                ugly.step_done(stmt);

                var changesCount = ugly.changes(sqliteConnection);
                if (changesCount > 0)
                {
                    Log.Information("Removed {rows} row(s) for device:{refId} in database for being invalid values", changesCount, refId);
                }
            }
        }

        public void Dispose()
        {
            Log.Debug("Disposing Sqlite connection");
            getHistoryCommand?.Dispose();
            getEarliestAndOldestRecordCommand?.Dispose();
            getRecordHistoryCountCommand?.Dispose();
            getTimeAndValueCommand?.Dispose();
            insertCommand?.Dispose();
            allRefOldestRecordsCommand?.Dispose();
            deleteOldRecordByRefCommand.Dispose();
            deleteAllRecordByRefCommand.Dispose();
            getMaxValueCommand?.Dispose();
            getMinValueCommand?.Dispose();
            getStrForRefAndValueCommand?.Dispose();
            if (sqliteConnection != null && SQLITE_OK != sqliteConnection.manual_close_v2())
            {
                Log.Warning("Sqlite has open handles during close");
            }

            sqliteConnection?.Dispose();
            maintainanceTimer?.Dispose();
            connectionMutex?.Dispose();
        }

        public void DoMaintainance()
        {
            Log.Information("Maintainance for database triggered");
            maintainanceTimer.Change(0, MaintainanceIntervalMs);
        }

        public IList<IDictionary<string, object?>> ExecSql(string sql)
        {
            using var lock2 = CreateLockForDBConnection();
            using var stmt = CreateStatement(sql);

            List<IDictionary<string, object?>> list = new();

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                Dictionary<string, object?> records = new();
                for (var i = 0; i < ugly.column_count(stmt); i++)
                {
                    var name = ugly.column_name(stmt, i);
                    var type = ugly.column_type(stmt, i);
                    object? value = type switch
                    {
                        SQLITE_INTEGER => ugly.column_int64(stmt, i),
                        SQLITE_FLOAT => ugly.column_double(stmt, i),
                        SQLITE_TEXT => ugly.column_text(stmt, i),
                        SQLITE_BLOB => ugly.column_bytes(stmt, i),
                        _ => null,
                    };
                    records.Add(name, value);
                }

                list.Add(records);
            }

            return list;
        }

        public IDictionary<string, string> GetDatabaseStats()
        {
            return new Dictionary<string, string>()
            {
                { "Path", settings.DBPath },
                { "Sqlite version", raw.sqlite3_libversion().utf8_to_string() },
                { "Size", GetTotalFileSize().Bytes().Humanize() },
                { "Total records", GetTotalRecords().ToString("N0") },
                { "Total records from last 24 hr", GetTotalRecordsInLastDay().ToString("N0") },
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

            long GetTotalRecords()
            {
                using var lock2 = CreateLockForDBConnection();
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history");
            }

            long GetTotalRecordsInLastDay()
            {
                using var lock2 = CreateLockForDBConnection();
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history WHERE ts>(STRFTIME('%s')-86400)");
            }
        }

        public Tuple<DateTimeOffset, DateTimeOffset> GetEarliestAndOldestRecordTimeDate(long refId)
        {
            using var lock2 = CreateLockForDBConnection();
            var stmt = getEarliestAndOldestRecordCommand;

            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.step(stmt);

            return new Tuple<DateTimeOffset, DateTimeOffset>(
                        DateTimeOffset.FromUnixTimeSeconds(stmt.column<long>(0)),
                        DateTimeOffset.FromUnixTimeSeconds(stmt.column<long>(1)));
        }

        public double? GetMaxValue(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            return ExecRefIdMinMaxStatement(refId, minUnixTimeSeconds, maxUnixTimeSeconds, getMaxValueCommand);
        }

        public double? GetMinValue(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            return ExecRefIdMinMaxStatement(refId, minUnixTimeSeconds, maxUnixTimeSeconds, getMinValueCommand);
        }

        public IList<RecordDataAndDuration> GetRecords(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds,
                                                                        long start, long length, ResultSortBy sortBy)
        {
            using var lock2 = CreateLockForDBConnection();

            var stmt = getHistoryCommand;

            using var stmtRest = CreateStatementAutoReset(stmt);
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

        public long GetRecordsCount(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            using var lock2 = CreateLockForDBConnection();
            var stmt = getRecordHistoryCountCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);
            ugly.step(stmt);

            return stmt.column<long>(0);
        }

        public IList<KeyValuePair<long, long>> GetRecordsWithCount(int limit)
        {
            using var lock2 = CreateLockForDBConnection();

            var records = new List<KeyValuePair<long, long>>();
            using var stmt = CreateStatement("SELECT ref, COUNT(*) as rcount FROM history GROUP BY ref ORDER BY rcount DESC LIMIT ?");

            ugly.bind_int64(stmt, 1, limit);
            while (ugly.step(stmt) != SQLITE_DONE)
            {
                records.Add(new KeyValuePair<long, long>(ugly.column_int64(stmt, 0), ugly.column_int64(stmt, 1)));
            }

            return records;
        }

        public IList<long> GetRefIdsWithRecords()
        {
            var records = new List<long>();
            using var lock2 = CreateLockForDBConnection();
            using var stmt = CreateStatement("SELECT DISTINCT ref FROM history");

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                records.Add(ugly.column_int64(stmt, 0));
            }

            return records;
        }

        public string? GetStringForValue(long refId, double value)
        {
            using var lock2 = CreateLockForDBConnection();

            var stmt = getStrForRefAndValueCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_double(stmt, 2, value);

            if (ugly.step(stmt) != SQLITE_DONE)
            {
                return ugly.column_text(stmt, 0);
            }

            return null;
        }

        /// <summary>
        /// Iterates the values between the range and one above and below the range. Order is time stamp ascending.
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="minUnixTimeSeconds"></param>
        /// <param name="maxUnixTimeSeconds"></param>
        /// <param name="iterator">Function called for iteration</param>
        public void IterateGraphValues(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds, Action<IEnumerable<TimeAndValue>> iterator)
        {
            using var lock2 = CreateLockForDBConnection();
            var stmt = getTimeAndValueCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
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

        private static Disposable CreateStatementAutoReset(sqlite3_stmt stmt)
        {
            ugly.reset(stmt);
            return Disposable.Create(() => ugly.reset(stmt));
        }

        private Disposable CreateLockForDBConnection()
        {
            connectionMutex.Wait(shutdownToken);
            return Disposable.Create(() => connectionMutex.Release());
        }

        private sqlite3_stmt CreateStatement(string sql)
        {
            var command = ugly.prepare_v3(sqliteConnection, sql, SQLITE_PREPARE_PERSISTENT);
            return command;
        }

        private double? ExecRefIdMinMaxStatement(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds, sqlite3_stmt stmt)
        {
            using var lock2 = CreateLockForDBConnection();
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);
            ugly.step(stmt);

            return ugly.column_type(stmt, 0) == SQLITE_NULL ? null : ugly.column_double(stmt, 0);
        }

        private void InsertRecord(in RecordData record)
        {
            using var lock2 = CreateLockForDBConnection();
            sqlite3_stmt stmt = insertCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, record.TimeStamp.ToUnixTimeSeconds());
            ugly.bind_int64(stmt, 2, record.DeviceRefId);
            ugly.bind_double(stmt, 3, record.DeviceValue);
            ugly.bind_text(stmt, 4, record.DeviceString);
            ugly.step_done(stmt);
        }

        private void Maintenance(object state)
        {
            try
            {
                Log.Information("Starting maintaining database");

                using var lock2 = CreateLockForDBConnection();
                var deletedCount = PruneRecords();
                if (deletedCount > 0)
                {
                    VacuumFreePages();
                }

                ugly.exec(sqliteConnection, "PRAGMA optimize");

                Log.Information("Finished maintaining database");
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    Log.Warning("Failed to do maintainance with {error}", ExceptionHelper.GetFullMessage(ex));
                }
            }
        }

        private int PruneRecord(long refId, long recordsToKeep, long cutoffUnixTimeSeconds)
        {
            var stmt = deleteOldRecordByRefCommand;
            using var stmtRest = CreateStatementAutoReset(stmt);
            ugly.bind_int64(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, cutoffUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, recordsToKeep);
            ugly.step_done(stmt);

            var changesCount = ugly.changes(sqliteConnection);
            Log.Information("Removed {rows} row(s) for device:{refId} in database", changesCount, refId);
            return changesCount;
        }

        private long PruneRecords()
        {
            try
            {
                Log.Debug("Start Pruning older records");
                var recordsToKeep = settings.MinRecordsToKeep;
                allRefOldestRecordsCommand.reset();

                long deletedCount = 0;
                DateTimeOffset now = globalTimerAndClock.Now;
                while ((ugly.step(allRefOldestRecordsCommand) != SQLITE_DONE) &&
                       !shutdownToken.IsCancellationRequested)
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
                        deletedCount += PruneRecord(refId, recordsToKeep, cutoffUnixTimeSeconds);
                    }
                }

                Log.Debug("Finished pruning older records");
                return deletedCount;
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                Log.Warning("Failed to do pruning with {error}}", ExceptionHelper.GetFullMessage(ex));
                return 0;
            }
        }

        private void SetupDatabase()
        {
            Log.Information("Connecting to database: {dbPath}", settings.DBPath);

            CheckSqliteValid();

            ugly.exec(sqliteConnection, "PRAGMA page_size=4096");
            ugly.exec(sqliteConnection, "PRAGMA journal_mode=WAL");
            ugly.exec(sqliteConnection, Invariant($"PRAGMA journal_size_limit={32 * 1024 * 1024}")); //32 MB
            ugly.exec(sqliteConnection, "PRAGMA synchronous=normal");
            ugly.exec(sqliteConnection, "PRAGMA locking_mode=EXCLUSIVE");
            ugly.exec(sqliteConnection, "PRAGMA temp_store=MEMORY");
            ugly.exec(sqliteConnection, "PRAGMA auto_vacuum=INCREMENTAL");
            ugly.exec(sqliteConnection, "PRAGMA integrity_check");

            ugly.wal_autocheckpoint(sqliteConnection, 512);

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
                    throw new SqliteInvalidException(@"Sqlite is not thread safe");
                }

                if (Version.TryParse(sqlite3_libversion().utf8_to_string(), out var version))
                {
                    var minSupportedVersion = new Version(3, 37);
                    if (version < minSupportedVersion)
                    {
                        throw new SqliteInvalidException("Sqlite version on machine is too old. Need 3.37+");
                    }
                }
            }
        }

        private void UpdateRecords()
        {
            Log.Debug("Starting records update thread");
            while (!shutdownToken.IsCancellationRequested)
            {
                try
                {
                    var record = queue.Take(shutdownToken);
                    Log.Verbose("Adding to database: {@record}", record);
                    InsertRecord(record);
                    RecordUpdateException = null;
                }
                catch (Exception ex)
                {
                    if (!ex.IsCancelException())
                    {
                        Log.Warning("Error in records update thread : {error}. Retrying in 30s.", ex.GetFullMessage());
                        this.RecordUpdateException = ex;
                        shutdownToken.WaitHandle.WaitOne(30 * 1000);
                    }
                }
            }

            Log.Debug("Finish records update thread");
        }

        private void VacuumFreePages()
        {
            var freePages = ugly.query_scalar<long>(sqliteConnection, "PRAGMA freelist_count");

            if (freePages > 0)
            {
                Log.Debug("Vacuuming {count} Pages from database", freePages);
                ugly.exec(sqliteConnection, "PRAGMA incremental_vacuum");
            }
        }

        private const string AllRefOldestRecordsSql = "SELECT ref, MIN(ts), COUNT(*) FROM history GROUP BY ref";
        private const string DeleteAllRecordByRefSql = "DELETE from history where ref=$ref";
        private const string DeleteOldRecordByRefSql = "DELETE from history where ref=$ref and ts<$time AND ts NOT IN ( SELECT ts FROM history WHERE ref=$ref ORDER BY ts DESC LIMIT $limit)";
        private const string EarliestAndOldestRecordSql = "SELECT MIN(ts), MAX(ts) FROM history WHERE ref=?";

        private const string GetMaxValuesSql = @"SELECT MAX(value) FROM history WHERE ref=$ref AND ts>=$min AND ts<=$max";

        private const string GetMinValuesSql = @"SELECT MIN(value) FROM history WHERE ref=$ref AND ts>=$min AND ts<=$max";

        private const string GetStrForRefAndValueSql = "SELECT str FROM history WHERE ref=? AND value=? ORDER BY ts DESC LIMIT 1";

        // 1 record before the time range and one after
        private const string GetTimeValuesSql =
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
        private readonly SemaphoreSlim connectionMutex = new(1, 1);
        private readonly sqlite3_stmt deleteAllRecordByRefCommand;
        private readonly sqlite3_stmt deleteOldRecordByRefCommand;
        private readonly sqlite3_stmt getEarliestAndOldestRecordCommand;
        private readonly sqlite3_stmt getHistoryCommand;
        private readonly sqlite3_stmt getMaxValueCommand;
        private readonly sqlite3_stmt getMinValueCommand;
        private readonly sqlite3_stmt getRecordHistoryCountCommand;
        private readonly sqlite3_stmt getStrForRefAndValueCommand;
        private readonly sqlite3_stmt getTimeAndValueCommand;
        private readonly IGlobalTimerAndClock globalTimerAndClock;
        private readonly sqlite3_stmt insertCommand;
        private readonly int MaintainanceIntervalMs;
        private readonly Timer maintainanceTimer;
        private readonly RecordDataProducerConsumerQueue queue;
        private readonly IDBSettings settings;
        private readonly CancellationToken shutdownToken;
        private readonly sqlite3 sqliteConnection;
    }
}