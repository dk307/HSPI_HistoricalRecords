using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hspi.Utils;
using Nito.AsyncEx;
using Serilog;

#nullable enable

namespace Hspi.Database
{
    internal sealed class SqliteDatabaseCollector : IDatabaseCollector
    {
        public SqliteDatabaseCollector(CancellationToken shutdownToken)
        {
            tokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            dbPath = Path.Combine(Path.GetTempPath(), "test.db");
            sqliteConnection = new SQLiteConnection($"Data Source='{dbPath}';PRAGMA journal_mode=WAL;");
            SetupDatabase();
            insertCommand = CreateInsertCommand();
            getHistoryCommand = CreateHistoryCommand();
            getTimeAndValueCommand = CreateTimeAndValueCommand();
            getTimeAndValueCountCommand = CreateHistoryCountCommand();
            Utils.TaskHelper.StartAsyncWithErrorChecking("DB Update Records", UpdateRecords, tokenSource.Token);
        }

        public IList<RecordData> GetRecords(int refId, TimeSpan timeSpan,
                                            int start, int length, ResultSortBy sortBy)
        {
            var fromTime = DateTimeOffset.UtcNow.Subtract(timeSpan).ToUnixTimeSeconds();

            getHistoryCommand.Parameters["$refid"].Value = refId;
            getHistoryCommand.Parameters["$time"].Value = fromTime;
            getHistoryCommand.Parameters["$order"].Value = (int)sortBy;
            getHistoryCommand.Parameters["$limit"].Value = length;
            getHistoryCommand.Parameters["$offset"].Value = start;

            List<RecordData> records = new();
            using var reader = getHistoryCommand.ExecuteReader();
            while (reader.Read())
            {
                // order: SELECT (time, value, string) FROM history
                var record = new RecordData(
                        refId,
                        reader.GetDouble(1),
                        reader.GetString(2),
                        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0))
                    );

                records.Add(record);
            };

            return records;
        }

        public long GetRecordsCount(int refId, TimeSpan timeSpan)
        {
            var fromTime = DateTimeOffset.UtcNow.Subtract(timeSpan).ToUnixTimeSeconds();

            getTimeAndValueCountCommand.Parameters[0].Value = refId;
            getTimeAndValueCountCommand.Parameters[1].Value = fromTime;
            return Convert.ToInt64(getTimeAndValueCountCommand.ExecuteScalar());
        }

        public Task Record(RecordData recordData)
        {
            return RecordImpl(recordData);
            async Task RecordImpl(RecordData recordData)
            {
                await queue.EnqueueAsync(recordData).ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        private static void ExecQuery(string sql, SQLiteConnection connection)
        {
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQueryAsync();
        }

        private SQLiteCommand CreateHistoryCommand()
        {
            SQLiteCommand command = new(@"
                SELECT time, value, string FROM history
                WHERE ref=$refid AND time>=$time
                ORDER BY
                    CASE WHEN $order = 0 THEN time END DESC,
                    CASE WHEN $order = 1 THEN value END DESC,
                    CASE WHEN $order = 2 THEN string END DESC,
                    CASE WHEN $order = 3 THEN time END ASC,
                    CASE WHEN $order = 4 THEN value END ASC,
                    CASE WHEN $order = 5 THEN string END ASC
                LIMIT $limit OFFSET $offset", sqliteConnection);
            command.Parameters.Add(new SQLiteParameter("$refid", DbType.Int32));
            command.Parameters.Add(new SQLiteParameter("$time", DbType.Int64));
            command.Parameters.Add(new SQLiteParameter("$order", DbType.Int16));
            command.Parameters.Add(new SQLiteParameter("$limit", DbType.Int64));
            command.Parameters.Add(new SQLiteParameter("$offset", DbType.Int64));
            command.Prepare();
            return command;
        }

        private SQLiteCommand CreateHistoryCountCommand()
        {
            SQLiteCommand command = new("SELECT COUNT(*) FROM history WHERE ref=? AND time>=?", sqliteConnection);
            command.Parameters.Add(new SQLiteParameter(DbType.Int32));
            command.Parameters.Add(new SQLiteParameter(DbType.Int64));
            command.Prepare();
            return command;
        }

        private SQLiteCommand CreateTimeAndValueCommand()
        {
            SQLiteCommand command = new("SELECT [time], value FROM history WHERE ref=? AND time>=? AND time<=? ORDER BY [time] desc", sqliteConnection);
            command.Parameters.Add(new SQLiteParameter(DbType.Int32));
            command.Parameters.Add(new SQLiteParameter(DbType.Int64));
            command.Parameters.Add(new SQLiteParameter(DbType.Int64));
            command.Prepare();
            return command;
        }

        private SQLiteCommand CreateInsertCommand()
        {
            SQLiteCommand command = new("INSERT OR REPLACE INTO history(time, ref, value, string) VALUES(?,?,?,?)", sqliteConnection);
            command.Parameters.Add(new SQLiteParameter(DbType.Int64));
            command.Parameters.Add(new SQLiteParameter(DbType.Int32));
            command.Parameters.Add(new SQLiteParameter(DbType.Double));
            command.Parameters.Add(new SQLiteParameter(DbType.String));
            command.Prepare();
            return command;
        }

        private void InsertRecord(RecordData record)
        {
            insertCommand.Parameters[0].Value = record.TimeStamp.ToUnixTimeSeconds();
            insertCommand.Parameters[1].Value = record.DeviceRefId;
            insertCommand.Parameters[2].Value = record.DeviceValue;
            insertCommand.Parameters[3].Value = record.DeviceString;
            insertCommand.ExecuteNonQuery();
        }

        private void SetupDatabase()
        {
            Log.Information("Connecting to database: {dbPath}", dbPath);
            sqliteConnection.Open();

            using var tx = sqliteConnection.BeginTransaction();
            ExecQuery("CREATE TABLE IF NOT EXISTS history(time NUMERIC NOT NULL, ref INT NOT NULL, value DOUBLE NOT NULL, string VARCHAR(1024), PRIMARY KEY(time,ref));", sqliteConnection);
            ExecQuery("CREATE INDEX history_time_index ON history (time);", sqliteConnection);
            ExecQuery("CREATE INDEX history_ref_index ON history (ref);", sqliteConnection);
            ExecQuery("CREATE INDEX history_time_ref_index ON history (time, ref);", sqliteConnection);
            tx.Commit();
        }

        private async Task UpdateRecords()
        {
            CancellationToken token = tokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                var record = await queue.DequeueAsync(token).ConfigureAwait(false);
                try
                {
                    Log.Debug("Adding to database: {record}", record);

                    InsertRecord(record);
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

        public IList<TimeAndValue> GetGraphValues(int refId, DateTimeOffset min, DateTimeOffset max)
        {
            getTimeAndValueCommand.Parameters[0].Value = refId;
            getTimeAndValueCommand.Parameters[1].Value = min.ToUnixTimeSeconds();
            getTimeAndValueCommand.Parameters[2].Value = max.ToUnixTimeSeconds();

            List<TimeAndValue> records = new();
            using var reader = getTimeAndValueCommand.ExecuteReader();
            while (reader.Read())
            {
                // order: SELECT (time, value) FROM history
                var record = new TimeAndValue(
                        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                        reader.GetDouble(1)
                    );

                records.Add(record);
            };

            return records;
        }

        private readonly string dbPath;

        // private readonly SQLiteCommand getHistoryCountCommand;
        private readonly SQLiteCommand getHistoryCommand;

        private readonly SQLiteCommand getTimeAndValueCountCommand;
        private readonly SQLiteCommand getTimeAndValueCommand;
        private readonly SQLiteCommand insertCommand;
        private readonly AsyncProducerConsumerQueue<RecordData> queue = new();
        private readonly SQLiteConnection sqliteConnection;
        private readonly CancellationTokenSource tokenSource;
    }
}