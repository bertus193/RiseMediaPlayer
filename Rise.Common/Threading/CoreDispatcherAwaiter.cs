using Microsoft.UI.Dispatching;
using System;
using System.Runtime.CompilerServices;

namespace Rise.Common.Threading
{
    /// <summary>
    /// A custom awaiter for <see cref="DispatcherQueue"/> objects,
    /// dispatching its continuation with normal priority.
    /// </summary>
    public struct DispatcherQueueAwaiter : INotifyCompletion
    {
        private readonly DispatcherQueue dispatcher;

        internal DispatcherQueueAwaiter(DispatcherQueue dispatcher)
            => this.dispatcher = dispatcher;

        public DispatcherQueueAwaiter GetAwaiter() => this;
        public bool IsCompleted => dispatcher.HasThreadAccess;

        public void GetResult() { }
        public void OnCompleted(Action continuation)
            => dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => continuation());
    }

    /// <summary>
    /// A custom awaiter for <see cref="DispatcherQueue"/> objects,
    /// dispatching its continuation with the provided priority.
    /// </summary>
    public struct ConfiguredDispatcherQueueAwaiter : INotifyCompletion
    {
        private readonly DispatcherQueue dispatcher;
        private readonly DispatcherQueuePriority priority;

        internal ConfiguredDispatcherQueueAwaiter(DispatcherQueue dispatcher, DispatcherQueuePriority priority)
        {
            this.dispatcher = dispatcher;
            this.priority = priority;
        }

        public ConfiguredDispatcherQueueAwaiter GetAwaiter() => this;
        public bool IsCompleted => dispatcher.HasThreadAccess;

        public void GetResult() { }
        public void OnCompleted(Action continuation)
            => dispatcher.TryEnqueue(priority, () => continuation());
    }
}
