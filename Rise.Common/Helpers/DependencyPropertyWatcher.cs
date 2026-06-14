using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace Rise.Common.Helpers
{
    /// <summary>
    /// Listens for changes to a dependency property.
    /// </summary>
    /// <typeparam name="T">Type of value returned by the property.</typeparam>
    public sealed class DependencyPropertyWatcher<T> : IDisposable
    {
        /// <summary>
        /// The object being watched for changes.
        /// </summary>
        public DependencyObject WatchedObject { get; }

        /// <summary>
        /// The property that's currently being watched.
        /// </summary>
        public DependencyProperty WatchedProperty { get; }

        private readonly long _token;

        /// <summary>
        /// Represents the method that will handle events that occur when a
        /// <see cref="DependencyPropertyWatcher{T}"/> fires a change notification.
        /// </summary>
        public delegate void PropertyWatcherFiredEventHandler(DependencyPropertyWatcher<T> sender, T newValue);

        /// <summary>
        /// Occurs when the watched property's value changes.
        /// </summary>
        public event PropertyWatcherFiredEventHandler PropertyChanged;

        /// <summary>
        /// Creates a new watcher for the provided property's value
        /// in the provided object.
        /// </summary>
        public DependencyPropertyWatcher(DependencyObject obj, DependencyProperty property)
        {
            WatchedObject = obj;
            WatchedProperty = property;

            _token = obj.RegisterPropertyChangedCallback(property, PropertyChangedCallback);
        }

        private void PropertyChangedCallback(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is FrameworkElement fe && fe.DispatcherQueue is DispatcherQueue dq)
            {
                dq.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    object val = sender.GetValue(dp);
                    PropertyChanged?.Invoke(this, (T)val);
                });
            }
            else
            {
                object val = sender.GetValue(dp);
                PropertyChanged?.Invoke(this, (T)val);
            }
        }

        /// <summary>
        /// Unhooks the property change callback from the watched object.
        /// </summary>
        public void Dispose()
        {
            WatchedObject.UnregisterPropertyChangedCallback(WatchedProperty, _token);
        }
    }
}
