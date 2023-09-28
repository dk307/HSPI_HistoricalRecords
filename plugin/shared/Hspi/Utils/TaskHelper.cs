using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.Utils
{
    internal static class TaskHelper
    {
        public static T ResultForSync<T>(this Task<T> @this)
        {
            // https://blogs.msdn.microsoft.com/pfxteam/2012/04/13/should-i-expose-synchronous-wrappers-for-asynchronous-methods/
            return Task.Run(() => @this).Result;
        }

        public static void ResultForSync(this Task @this)
        {
            Task.Run(() => @this).Wait();
        }

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