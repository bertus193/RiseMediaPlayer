using Microsoft.UI.Dispatching;
using System.ComponentModel;
using Windows.System.Threading;

namespace Rise.Common.Threading
{
    /// <summary>
    /// A helper class that provides awaiters for multiple WinRT
    /// and WinUI 3 dispatcher objects.
    /// </summary>
    public static class ThreadSwitcher
    {
        public static DispatcherQueueAwaiter ResumeForegroundAsync(DispatcherQueue dispatcher)
            => new(dispatcher);

        /// <summary>
        /// Configures a dispatcher awaiter with the provided priority.
        /// </summary>
        public static ConfiguredDispatcherQueueAwaiter ConfigureAwait(this DispatcherQueue dispatcher, DispatcherQueuePriority priority)
            => new(dispatcher, priority);

        public static ThreadPoolAwaiter ResumeBackgroundAsync()
            => new();

        /// <summary>
        /// Configures the awaiter with the provided priority.
        /// </summary>
        public static ConfiguredThreadPoolAwaiter ResumeBackgroundAsync(WorkItemPriority priority)
            => new(priority, WorkItemOptions.None);

        /// <summary>
        /// Configures the awaiter with the provided priority and options.
        /// </summary>
        public static ConfiguredThreadPoolAwaiter ResumeBackgroundAsync(WorkItemPriority priority, WorkItemOptions options)
            => new(priority, options);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static DispatcherQueueAwaiter GetAwaiter(this DispatcherQueue dispatcher)
            => new(dispatcher);
    }
}
