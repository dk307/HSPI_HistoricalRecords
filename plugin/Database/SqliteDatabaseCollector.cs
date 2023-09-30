﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hspi.Utils;
using Nito.AsyncEx;
using Serilog;
using SQLitePCL;
using SQLitePCL.Ugly;
using static SQLitePCL.raw;

#nullable enable

namespace Hspi.Database
{
    public record TimeAndValue(DateTimeOffset TimeStamp, double DeviceValue);

    public enum ResultSortBy
    {
        TimeDesc = 0,
        ValueDesc = 1,
        StringDesc = 2,
        TimeAsc = 3,
        ValueAsc = 4,
        StringAsc = 5,
    }

    internal sealed class SqliteDatabaseCollector : IDisposable
    {
        static SqliteDatabaseCollector()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SQLitePCL.Batteries_V2.Init();
            }
            else
            {
                SQLitePCL.raw.SetProvider(new SQLite3Provider_sqlite3());
            }
        }

        public SqliteDatabaseCollector(string dbPath, CancellationToken shutdownToken)
        {
            this.dbPath = dbPath;
            this.shutdownToken = shutdownToken;
            CreateDBDirectory(dbPath);

            const int OpenFlags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_PRIVATECACHE;

            sqliteConnection = ugly.open_v2(dbPath, OpenFlags, null);
            Log.Information("System SQLite version: {version}", sqlite3_libversion().utf8_to_string());

            ConfigureDatabase();
            SetupDatabase();

            insertCommand = CreateStatement(InsertSql);
            getHistoryCommand = CreateStatement(RecordsHistorySql);
            getRecordHistoryCountCommand = CreateStatement(RecordsHistoryCountSql);
            getTimeAndValueCommand = CreateStatement(GetTimeValueSql);
            Utils.TaskHelper.StartAsyncWithErrorChecking("DB Update Records", UpdateRecords, shutdownToken);

            static void CreateDBDirectory(string dbPath)
            {
                string dirPath = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
            }

            void ConfigureDatabase()
            {
                if (sqlite3_threadsafe() == 0)
                {
                    throw new Exception("Sqlite is not thread safe");
                }

                sqlite3_extended_result_codes(sqliteConnection, 1);
            }
        }

        public async Task<IList<TimeAndValue>> GetGraphValues(int refId, DateTimeOffset min, DateTimeOffset max)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getTimeAndValueCommand;
            ugly.reset(stmt);
            ugly.bind_int(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, min.ToUnixTimeSeconds());
            ugly.bind_int64(stmt, 3, max.ToUnixTimeSeconds());

            List<TimeAndValue> records = new();
            while (ugly.step(stmt) != SQLITE_DONE)
            {
                // order: SELECT (time, value) FROM history
                var record = new TimeAndValue(
                        DateTimeOffset.FromUnixTimeSeconds(ugly.column_int64(stmt, 0)),
                        ugly.column_double(stmt, 1)
                    );

                records.Add(record);
            };

            return records;
        }

        public async Task<IList<RecordData>> GetRecords(int refId, TimeSpan timeSpan,
                                                    int start, int length, ResultSortBy sortBy)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getHistoryCommand;

            var fromTime = DateTimeOffset.UtcNow.Subtract(timeSpan).ToUnixTimeSeconds();

            ugly.reset(stmt);
            ugly.bind_int(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, fromTime);
            ugly.bind_int(stmt, 3, (int)sortBy);
            ugly.bind_int64(stmt, 4, length);
            ugly.bind_int64(stmt, 5, start);

            List<RecordData> records = new();

            while (ugly.step(stmt) != SQLITE_DONE)
            {
                // order: SELECT (time, value, string) FROM history
                var record = new RecordData(
                        refId,
                        ugly.column_double(stmt, 1),
                        ugly.column_text(stmt, 2),
                        DateTimeOffset.FromUnixTimeSeconds(ugly.column_int64(stmt, 0))
                    );

                records.Add(record);
            };

            return records;
        }

        public async Task<long> GetRecordsCount(int refId, TimeSpan timeSpan)
        {
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);

            var stmt = getRecordHistoryCountCommand;
            var fromTime = DateTimeOffset.UtcNow.Subtract(timeSpan).ToUnixTimeSeconds();

            ugly.reset(stmt);
            ugly.bind_int(stmt, 1, refId);
            ugly.bind_int64(stmt, 2, fromTime);
            ugly.step(stmt);

            return stmt.column<long>(0);
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
            sqlite3_stmt stmt = insertCommand;
            using var dbLock = await connectionLock.LockAsync(shutdownToken).ConfigureAwait(false);
            ugly.reset(stmt);
            ugly.bind_int64(stmt, 1, record.TimeStamp.ToUnixTimeSeconds());
            ugly.bind_int(stmt, 2, record.DeviceRefId);
            ugly.bind_double(stmt, 3, record.DeviceValue);
            ugly.bind_text(stmt, 4, record.DeviceString);
            ugly.step_done(stmt);
        }

        private void SetupDatabase()
        {
            Log.Information("Connecting to database: {dbPath}", dbPath);
            ugly.exec(sqliteConnection, "PRAGMA journal_mode=WAL");

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
            CancellationToken token = shutdownToken;
            while (!token.IsCancellationRequested)
            {
                var record = await queue.DequeueAsync(token).ConfigureAwait(false);
                try
                {
                    Log.Debug("Adding to database: {record}", record);
                    await InsertRecord(record).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    Log.Warning("Failed to update {record} with {error}}", record, ExceptionHelper.GetFullMessage(ex));

                    await queue.EnqueueAsync(record, token).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            getHistoryCommand.Dispose();
            getRecordHistoryCountCommand.Dispose();
            getTimeAndValueCommand.Dispose();
            insertCommand.Dispose();
            sqliteConnection.Dispose();
        }

        private const string GetTimeValueSql = "SELECT ts, value FROM history WHERE ref=? AND ts>=? AND ts<=? ORDER BY [ts] desc";
        private const string InsertSql = "INSERT OR REPLACE INTO history(ts, ref, value, str) VALUES(?,?,?,?)";
        private const string RecordsHistoryCountSql = "SELECT COUNT(*) FROM history WHERE ref=? AND ts>=?";

        private const string RecordsHistorySql = @"
                SELECT ts, value, str FROM history
                WHERE ref=$refid AND ts>=$ts
                ORDER BY
                    CASE WHEN $order = 0 THEN ts END DESC,
                    CASE WHEN $order = 1 THEN value END DESC,
                    CASE WHEN $order = 2 THEN str END DESC,
                    CASE WHEN $order = 3 THEN ts END ASC,
                    CASE WHEN $order = 4 THEN value END ASC,
                    CASE WHEN $order = 5 THEN str END ASC
                LIMIT $limit OFFSET $offset";

        private readonly AsyncLock connectionLock = new();
        private readonly string dbPath;
        private readonly sqlite3_stmt getHistoryCommand;
        private readonly sqlite3_stmt getRecordHistoryCountCommand;
        private readonly sqlite3_stmt getTimeAndValueCommand;
        private readonly sqlite3_stmt insertCommand;
        private readonly AsyncProducerConsumerQueue<RecordData> queue = new();
        private readonly sqlite3 sqliteConnection;
        private readonly CancellationToken shutdownToken;
    }
}