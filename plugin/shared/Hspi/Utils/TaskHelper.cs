using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

#nullable enable

namespace Hspi.Utils
{
    internal static class TaskHelper
    {
        public static void StartAsyncWithErrorChecking(string taskName,
                                                       Func<Task> taskAction,
                                                       CancellationToken token,
                                                       TimeSpan? delayAfterError = null)
        {
            _ = Task.Factory.StartNew(() => RunInLoop(taskName, taskAction, delayAfterError, token), token,
                                          TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                          TaskScheduler.Current);
        }

        private static async Task RunInLoop(string taskName,
                                            Func<Task> taskAction,
                                            TimeSpan? delayAfterError,
                                            CancellationToken token)
        {
            bool loop = true;
            while (loop && !token.IsCancellationRequested)
            {
                try
                {
                    Log.Debug("{taskName} starting", taskName);
                    await taskAction().ConfigureAwait(false);
                    Log.Debug("{taskName} finished", taskName);
                    loop = false;  //finished sucessfully
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    if (delayAfterError.HasValue)
                    {
                        Log.Error("{taskName} failed with {error}. Restarting after {time}s ...",
                                    taskName, ex.GetFullMessage(), delayAfterError.Value.TotalSeconds);
                        await Task.Delay(delayAfterError.Value, token).ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Error("{taskName} failed with {error}. Restarting ...",
                                    taskName, ex.GetFullMessage());
                    }
                }
            }
        }
    }
}