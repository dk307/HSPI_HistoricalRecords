﻿using System;
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
using static SQLitePCL.raw;

#nullable enable

namespace Hspi.Database
{
    internal class SqliteDatabaseCollector : IDisposable
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
            getOldestRecordCommand = CreateStatement(OldestRecordSql);
            allRefOldestRecordsCommand = CreateStatement(AllRefOldestRecordsSql);
            deleteOldRecordByRefCommand = CreateStatement(DeleteOldRecordByRefSql);
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

        //used in tests for mocks
        protected virtual DateTimeOffset TimeNow => DateTimeOffset.UtcNow;

        public void Dispose()
        {
            getHistoryCommand?.Dispose();
            getOldestRecordCommand?.Dispose();
            getRecordHistoryCountCommand?.Dispose();
            getTimeAndValueCommand?.Dispose();
            insertCommand?.Dispose();
            allRefOldestRecordsCommand?.Dispose();
            deleteOldRecordByRefCommand.Dispose();
            sqliteConnection?.Dispose();
        }

        public IDictionary<string, string> GetDatabaseStats()
        {
            return new Dictionary<string, string>()
            {
                { "Path", settings.DBPath },
                { "Sqlite version",  raw.sqlite3_libversion().utf8_to_string() },
                { "Sqlite memory used",  raw.sqlite3_memory_used().Bytes().Humanize() },
                { "Size",   GetTotalFileSize().Bytes().Humanize() },
                { "Total records",  GetTotalRecords().ToString("N0") },
                { "Total records from last 24 hr",  GetTotalRecordsInLastDay().ToString("N0") },
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
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history");
            }

            long GetTotalRecordsInLastDay()
            {
                return ugly.query_scalar<long>(sqliteConnection, "SELECT COUNT(*) FROM history WHERE ts>(STRFTIME('%s')-86400)");
            }
        }

        /// <summary>
        /// Returns the values between the range and one above and below the range
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="minUnixTimeSeconds"></param>
        /// <param name="maxUnixTimeSeconds"></param>
        /// <returns></returns>
        public async Task<IList<TimeAndValue>> GetGraphValues(int refId, long minUnixTimeSeconds, long maxUnixTimeSeconds)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getTimeAndValueCommand;
            ugly.reset(stmt);
            ugly.bind_int(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, minUnixTimeSeconds);
            ugly.bind_int64(stmt, 3, maxUnixTimeSeconds);

            List<TimeAndValue> records = new();
            while (ugly.step(stmt) != SQLITE_DONE)
            {
                // order: SELECT (time, value) FROM history
                var record = new TimeAndValue(ugly.column_int64(stmt, 0), ugly.column_double(stmt, 1));
                records.Add(record);
            };

            return records;
        }

        public async Task<DateTimeOffset> GetOldestRecordTimeDate(int refId)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            var stmt = getOldestRecordCommand;

            ugly.reset(stmt);
            ugly.bind_int(stmt, 1, refId);
            ugly.step(stmt);

            return DateTimeOffset.FromUnixTimeSeconds(stmt.column<long>(0));
        }

        public async Task<IList<RecordData>> GetRecords(long refId, long minUnixTimeSeconds, long maxUnixTimeSeconds,
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

            List<RecordData> records = new();

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                // order: SELECT (time, value, string) FROM history
                var record = new RecordData(
                        refId,
                        ugly.column_double(stmt, 1),
                        ugly.column_text(stmt, 2),
                        ugly.column_int64(stmt, 0)
                    );

                records.Add(record);
            };

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

        public IList<KeyValuePair<long, long>> GetTop10RecordsStats()
        {
            var records = new List<KeyValuePair<long, long>>();
            using var stmt = CreateStatement("SELECT ref, COUNT(*) as rcount FROM history GROUP BY ref ORDER BY rcount DESC LIMIT 10");

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                records.Add(new KeyValuePair<long, long>(ugly.column_int64(stmt, 0), ugly.column_int64(stmt, 1)));
            };
            return records;
        }

        public void PruneNow()
        {
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

                    List<RecordData> records = new();
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
                    };

                    Log.Information("Finished pruning database");
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

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

            if (sqlite3_threadsafe() == 0)
            {
                throw new SystemException("Sqlite is not thread safe");
            }

            ugly.exec(sqliteConnection, "PRAGMA page_size=4096");
            ugly.exec(sqliteConnection, "PRAGMA journal_mode=WAL");
            ugly.exec(sqliteConnection, "PRAGMA wal_autocheckpoint=100");
            ugly.exec(sqliteConnection, "PRAGMA synchronous=normal");
            ugly.exec(sqliteConnection, "PRAGMA locking_mode=EXCLUSIVE");
            ugly.exec(sqliteConnection, "PRAGMA temp_store=MEMORY");
            ugly.exec(sqliteConnection, "PRAGMA auto_vacuum=INCREMENTAL");
            ugly.exec(sqliteConnection, "PRAGMA integrity_check");

            ugly.exec(sqliteConnection, "BEGIN TRANSACTION");

            try
            {
                ugly.exec(sqliteConnection, "CREATE TABLE IF NOT EXISTS history(ts NUMERIC NOT NULL, ref INT NOT NULL, value DOUBLE NOT NULL, str VARCHAR(1024), PRIMARY KEY(ts,ref));");
                ugly.exec(sqliteConnection, "CREATE INDEX IF NOT EXISTS history_time_index ON history (ts);");
                ugly.exec(sqliteConnection, "CREATE INDEX IF NOT EXISTS history_ref_index ON history (ref);");
                ugly.exec(sqliteConnection, "CREATE INDEX IF NOT EXISTS history_time_ref_index ON history (ts, ref);");
                ugly.exec(sqliteConnection, "COMMIT");
            }
            catch (Exception)
            {
                ugly.exec(sqliteConnection, "ABORT");
                throw;
            }
        }

        private async Task UpdateRecords()
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                var record = await queue.DequeueAsync(shutdownToken).ConfigureAwait(false);
                try
                {
                    Log.Debug("Adding to database: {@record}", record);
                    await InsertRecord(record).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    Log.Warning("Failed to update {record} with {error}}", record, ExceptionHelper.GetFullMessage(ex));

                    await queue.EnqueueAsync(record, shutdownToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(30), shutdownToken).ConfigureAwait(false);
                }
            }
        }

        private const string AllRefOldestRecordsSql = "SELECT ref, MIN(ts), COUNT(*) FROM history GROUP BY ref";

        private const string DeleteOldRecordByRefSql = "DELETE from history where ref=$ref and ts<$time AND ts NOT IN ( SELECT ts FROM history WHERE ref=$ref ORDER BY ts DESC LIMIT $limit)";

        // 1 record before the time range and one after
        private const string GetTimeValueSql =
            @"SELECT * FROM (SELECT ts, value FROM history WHERE ref=$ref AND ts<$min ORDER BY ts DESC LIMIT 1) UNION
              SELECT * FROM (SELECT ts, value FROM history WHERE ref=$ref AND ts>$max ORDER BY ts LIMIT 1) UNION
              SELECT ts, value FROM history WHERE ref=$ref AND ts>=$min AND ts<=$max ORDER BY ts";

        private const string InsertSql = "INSERT OR REPLACE INTO history(ts, ref, value, str) VALUES(?,?,?,?)";
        private const string OldestRecordSql = "SELECT MIN(ts) FROM history WHERE ref=?";
        private const string RecordsHistoryCountSql = "SELECT COUNT(*) FROM history WHERE ref=? AND ts>=? AND ts<=?";

        private const string RecordsHistorySql = @"
                SELECT ts, value, str FROM history
                WHERE ref=$refid AND ts>=$minV AND ts<=$maxV
                ORDER BY
                    CASE WHEN $order = 0 THEN ts END DESC,
                    CASE WHEN $order = 1 THEN value END DESC,
                    CASE WHEN $order = 2 THEN str END DESC,
                    CASE WHEN $order = 3 THEN ts END ASC,
                    CASE WHEN $order = 4 THEN value END ASC,
                    CASE WHEN $order = 5 THEN str END ASC
                LIMIT $limit OFFSET $offset";

        private readonly sqlite3_stmt allRefOldestRecordsCommand;
        private readonly AsyncLock connectionLock = new();
        private readonly sqlite3_stmt deleteOldRecordByRefCommand;
        private readonly sqlite3_stmt getHistoryCommand;
        private readonly sqlite3_stmt getOldestRecordCommand;
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