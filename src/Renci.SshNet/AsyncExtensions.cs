#if FEATURE_TAP
using System;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet.Common;

namespace Renci.SshNet
{
    /// <summary>
    /// Task Async Parallel (TAP) Extensions for functions that are not natively implemented via TAP
    /// </summary>
    public static class AsyncExtensions
    {
        /// <summary>
        /// Wrapper to convert a function from APM to TAP and supports cancellation tokens as seen here:
        /// https://stackoverflow.com/questions/24980427/task-factory-fromasync-with-cancellationtokensource
        /// </summary>
        /// <param name="asyncResult">Async result we need to await on</param>
        /// <param name="endMethod">End method for the async function</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="factory">Optional task factory to use</param>
        /// <param name="creationOptions">Optional task creation options to use</param>
        /// <param name="scheduler">Optional task schedule to use</param>
        internal static async Task FromAsync(
            IAsyncResult asyncResult,
            Action<IAsyncResult> endMethod,
            CancellationToken cancellationToken,
            TaskFactory factory,
            TaskCreationOptions creationOptions,
            TaskScheduler scheduler)
        {
            // Execute the task via APM
            factory = factory ?? Task.Factory;
            scheduler = scheduler ?? factory.Scheduler ?? TaskScheduler.Current;
            var asyncTask = factory.FromAsync(asyncResult, endMethod, creationOptions, scheduler);

            // Create another task that completes as soon as cancellation is requested.
            // http://stackoverflow.com/a/18672893/1149773
            var tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            var cancellationTask = tcs.Task;

            // Create a task that completes when either the async operation completes,
            // or cancellation is requested.
            var readyTask = await Task.WhenAny(asyncTask, cancellationTask).ConfigureAwait(false);

            // In case of cancellation, register a continuation to observe any unhandled
            // exceptions from the asynchronous operation (once it completes).
            // In .NET 4.0, unobserved task exceptions would terminate the process.
            if (readyTask == cancellationTask)
            {
                // Mark the async result as completed so it will terminate
                ((AsyncResult)asyncResult).SetAsCompleted(null, false);

                // Now register the continuation as mentioned above
                await asyncTask.ContinueWith(_ => asyncTask.Exception,
                    TaskContinuationOptions.OnlyOnFaulted |
                    TaskContinuationOptions.ExecuteSynchronously).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wrapper to convert a function from APM to TAP and supports cancellation tokens as seen here:
        /// https://stackoverflow.com/questions/24980427/task-factory-fromasync-with-cancellationtokensource
        /// </summary>
        /// <param name="asyncResult">Async result we need to await on</param>
        /// <param name="endMethod">End method for the async function</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="factory">Optional task factory to use</param>
        /// <param name="creationOptions">Optional task creation options to use</param>
        /// <param name="scheduler">Optional task schedule to use</param>
        internal static async Task<TResult> FromAsync<TResult>(
            IAsyncResult asyncResult,
            Func<IAsyncResult, TResult> endMethod,
            CancellationToken cancellationToken,
            TaskFactory<TResult> factory,
            TaskCreationOptions creationOptions,
            TaskScheduler scheduler)
        {
            // Execute the task via APM
            factory = factory ?? Task<TResult>.Factory;
            scheduler = scheduler ?? factory.Scheduler ?? TaskScheduler.Current;
            var asyncTask = factory.FromAsync(asyncResult, endMethod, creationOptions, scheduler);

            // Create another task that completes as soon as cancellation is requested.
            // http://stackoverflow.com/a/18672893/1149773
            var tcs = new TaskCompletionSource<TResult>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), false);
            var cancellationTask = tcs.Task;

            // Create a task that completes when either the async operation completes,
            // or cancellation is requested.
            var readyTask = await Task.WhenAny(asyncTask, cancellationTask).ConfigureAwait(false);

            // In case of cancellation, register a continuation to observe any unhandled
            // exceptions from the asynchronous operation (once it completes).
            // In .NET 4.0, unobserved task exceptions would terminate the process.
            if (readyTask == cancellationTask)
            {
                // Mark the async result as completed so it will terminate
                ((AsyncResult<TResult>)asyncResult).SetAsCompleted(default(TResult), false);

                // Now register the continuation as mentioned above
                await asyncTask.ContinueWith(_ => asyncTask.Exception,
                    TaskContinuationOptions.OnlyOnFaulted |
                    TaskContinuationOptions.ExecuteSynchronously).ConfigureAwait(false);
            }

            return await readyTask.ConfigureAwait(false);
        }
    }
}
#endif