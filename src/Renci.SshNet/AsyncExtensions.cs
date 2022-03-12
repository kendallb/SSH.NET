#if FEATURE_TAP
using System;
using System.Collections.Generic;
using System.IO;
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
        private static async Task FromAsync(
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
        private static async Task<TResult> FromAsync<TResult>(
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

        /// <summary>
        /// Asynchronously download the file into the stream.
        /// </summary>
        /// <param name="client">The <see cref="SftpClient"/> instance</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="output">Data output stream.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="downloadCallback">The download progress callback.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Returns <see cref="Task"/> for the resulting task</returns>
        public static Task DownloadFileAsync(
            this SftpClient client,
            string path,
            Stream output,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<ulong> downloadCallback = null,
            TaskFactory factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler scheduler = null)
        {
            return FromAsync(
                client.BeginDownloadFile(path, output, null, null, downloadCallback),
                client.EndDownloadFile,
                cancellationToken,
                factory,
                creationOptions,
                scheduler);
        }

        /// <summary>
        /// Asynchronously upload the stream into the remote file.
        /// </summary>
        /// <param name="client">The <see cref="SftpClient"/> instance</param>
        /// <param name="input">Data input stream.</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="canOverride">if set to <c>true</c> then existing file will be overwritten.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="uploadCallback">The upload callback.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Returns <see cref="Task"/> for the resulting task</returns>
        public static Task UploadAsync(
            this SftpClient client,
            Stream input,
            string path,
            bool canOverride = true,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<ulong> uploadCallback = null,
            TaskFactory factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler scheduler = null)
        {
            return FromAsync(
                client.BeginUploadFile(input, path, canOverride, null, null, uploadCallback),
                client.EndUploadFile,
                cancellationToken,
                factory,
                creationOptions,
                scheduler);
        }

        /// <summary>
        /// Asynchronously synchronizes the directories.
        /// </summary>
        /// <param name="client">The <see cref="SftpClient"/> instance</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="searchPattern">The search pattern.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Returns <see cref="Task{IEnumerable}"/> as a list of uploaded files</returns>
        public static Task<IEnumerable<FileInfo>> SynchronizeDirectoriesAsync(
            this SftpClient client,
            string sourcePath,
            string destinationPath,
            string searchPattern,
            CancellationToken cancellationToken = default(CancellationToken),
            TaskFactory<IEnumerable<FileInfo>> factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler scheduler = null)
        {
            return FromAsync(
                client.BeginSynchronizeDirectories(sourcePath, destinationPath, searchPattern, null, null),
                client.EndSynchronizeDirectories,
                cancellationToken,
                factory,
                creationOptions,
                scheduler);
        }

        /// <summary>
        /// Asynchronously run a command.
        /// </summary>
        /// <param name="command">The <see cref="SshCommand"/> instance</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Returns <see cref="Task{String}"/> as command execution result.</returns>
        public static Task<string> ExecuteAsync(
            this SshCommand command,
            CancellationToken cancellationToken = default(CancellationToken),
            TaskFactory<string> factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler scheduler = null)
        {
            return FromAsync(
                command.BeginExecute(),
                command.EndExecute,
                cancellationToken,
                factory,
                creationOptions,
                scheduler);
        }

        /// <summary>
        /// Asynchronously run a command.
        /// </summary>
        /// <param name="command">The <see cref="SshCommand"/> instance</param>
        /// <param name="commandText">The command text to execute</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Returns <see cref="Task{String}"/> as command execution result.</returns>
        public static Task<string> ExecuteAsync(
            this SshCommand command,
            string commandText,
            CancellationToken cancellationToken = default(CancellationToken),
            TaskFactory<string> factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler scheduler = null)
        {
            return FromAsync(
                command.BeginExecute(commandText, null, null),
                command.EndExecute,
                cancellationToken,
                factory,
                creationOptions,
                scheduler);
        }
    }
}
#endif