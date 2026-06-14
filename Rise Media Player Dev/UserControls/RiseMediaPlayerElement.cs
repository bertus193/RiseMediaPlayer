using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rise.Common.Helpers;
using System;
using Windows.Media.Playback;

namespace Rise.App.UserControls
{
    /// <summary>
    /// Custom media player element implementation for RiseMP.
    /// </summary>
    public sealed partial class RiseMediaPlayerElement : MediaPlayerElement
    {
        public Visibility MediaPlayerVisibility
        {
            get => (Visibility)GetValue(MediaPlayerVisibilityProperty);
            set => SetValue(MediaPlayerVisibilityProperty, value);
        }
    }

    // Dependency Properties
    public sealed partial class RiseMediaPlayerElement : MediaPlayerElement
    {
        public static readonly DependencyProperty MediaPlayerVisibilityProperty =
            DependencyProperty.Register(nameof(MediaPlayerVisibility), typeof(Visibility),
                typeof(RiseMediaPlayerElement), new PropertyMetadata(Visibility.Visible));
    }

    // Event handlers
    public sealed partial class RiseMediaPlayerElement : MediaPlayerElement
    {
        private void OnVolumeChanged(MediaPlayer sender, object args)
        {
            if (!sender.IsMuted)
                HandleVolumeChanged(sender.Volume);
        }

        private void OnIsMutedChanged(MediaPlayer sender, object args)
        {
            if (!sender.IsMuted)
                HandleVolumeChanged(sender.Volume);
            else
                HandleMuted();
        }

        /// <summary>
        /// Dispatches a VisualState change for the volume icon onto the UI thread.
        /// Replaces IAsyncAction Dispatcher.RunAsync(CoreDispatcherPriority.Normal, …).
        /// </summary>
        private void HandleVolumeChanged(double newVolume)
        {
            var state = newVolume switch
            {
                0 => "NoVolumeState",
                < 0.33 => "LowVolumeState",
                < 0.66 => "MidVolumeState",
                _ => "HighVolumeState",
            };

            // DispatcherQueue replaces CoreDispatcher.RunAsync
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                _ = VisualStateManager.GoToState(TransportControls, state, true));
        }

        private void HandleMuted()
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                _ = VisualStateManager.GoToState(TransportControls, "NoVolumeState", true));
        }

        private void RegisterVolumeChanged()
        {
            MediaPlayer.VolumeChanged += OnVolumeChanged;
            MediaPlayer.IsMutedChanged += OnIsMutedChanged;
            HandleVolumeChanged(MediaPlayer.Volume);
        }
    }

    // Constructor
    public sealed partial class RiseMediaPlayerElement : MediaPlayerElement
    {
        private readonly DependencyPropertyWatcher<MediaPlayer> _playerWatcher;

        public RiseMediaPlayerElement()
        {
            DefaultStyleKey = typeof(RiseMediaPlayerElement);

            _playerWatcher = new(this, MediaPlayerProperty);
            _playerWatcher.PropertyChanged += OnMediaPlayerChanged;

            Unloaded += OnUnloaded;
        }

        private void OnMediaPlayerChanged(DependencyPropertyWatcher<MediaPlayer> sender, MediaPlayer newValue)
        {
            RegisterVolumeChanged();
            _playerWatcher.Dispose();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.VolumeChanged -= OnVolumeChanged;
                MediaPlayer.IsMutedChanged -= OnIsMutedChanged;
            }

            _playerWatcher.PropertyChanged -= OnMediaPlayerChanged;
            _playerWatcher.Dispose();
        }
    }
}
