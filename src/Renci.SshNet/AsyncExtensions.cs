#if FEATURE_TAP
using System;
using System.Threading;
using System.Threading.Tasks;

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
        /// <param name="cancellationCallback">Callback to call when the task is cancelled</param>
        internal static async Task<TResult> FromAsync<TResult>(
            IAsyncResult asyncResult,
            Func<IAsyncResult, TResult> endMethod,
            CancellationToken cancellationToken,
            Action cancellationCallback)
        {
            // Execute the task via APM
            var asyncTask = Task<TResult>.Factory.FromAsync(asyncResult, endMethod);

            // Create another task that completes as soon as cancellation is requested.
            // http://stackoverflow.com/a/18672893/1149773
            var tcs = new TaskCompletionSource<TResult>();
            var cancellationTask = tcs.Task;
            using (cancellationToken.Register(() => tcs.TrySetCanceled(), false))
            {
                // Create a task that completes when either the async operation completes,
                // or cancellation is requested.
                var readyTask = await Task.WhenAny(asyncTask, cancellationTask).ConfigureAwait(false);

                // Call the cancellation callback if provided to cancel the operation
                if (readyTask == cancellationTask)
                {
                    cancellationCallback();
                }

                return await readyTask.ConfigureAwait(false);
            }
        }
    }
}
#endif