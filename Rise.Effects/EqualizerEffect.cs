using NAudio.Dsp;
using Rise.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;

namespace Rise.Effects
{
    /// <summary>
    /// A 10-band parametric equalizer implemented as an <see cref="IBasicAudioEffect"/>.
    /// In WinUI 3 (unpackaged desktop) the class no longer needs to be a WinRT component
    /// (winmdobj): MediaPlayer accepts any in-process IBasicAudioEffect implementation.
    /// The singleton <see cref="Current"/> is registered once via
    /// <c>MediaPlayer.AddVideoEffect / AddAudioEffect</c> from App.xaml.cs.
    /// </summary>
    public sealed partial class EqualizerEffect : IBasicAudioEffect, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // ── Singleton ──────────────────────────────────────────────────────────

        private static readonly Lazy<EqualizerEffect> _current
            = new(() => new EqualizerEffect());

        /// <summary>Gets the process-wide equalizer instance.</summary>
        public static EqualizerEffect Current => _current.Value;

        /// <summary>True once <see cref="Current"/> has been accessed.</summary>
        public static bool Initialized => _current.IsValueCreated;

        // ── Bands ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Observable collection of EQ bands.
        /// Replaces <c>IObservableVector&lt;EqualizerBand&gt;</c> (WinRT-only type)
        /// with <see cref="ObservableCollection{T}"/> which is available in all
        /// .NET targets and still raises CollectionChanged for XAML binding.
        /// </summary>
        public ObservableCollection<EqualizerBand> Bands { get; private set; } = new();

        // ── IsEnabled ──────────────────────────────────────────────────────────

        private bool _isEnabled;

        /// <summary>
        /// Bypasses DSP processing when false without removing the effect from
        /// the MediaPlayer chain (MediaPlayer has no remove-effect API).
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        // ── Internal DSP state ─────────────────────────────────────────────────

        private static List<AudioEncodingProperties> _supportedEncodingProperties;
        private AudioEncodingProperties _currentEncodingProperties;

        private BiQuadFilter[,] _filters;
        private int _channels;
        private int _bandCount;
        private IPropertySet _configuration;

        // ── Band initialisation ────────────────────────────────────────────────

        /// <summary>
        /// Populates <see cref="Bands"/> with default 10-band configuration
        /// using the supplied gain values.  Gains are normalised so the
        /// loudest band sits at 0 dB.
        /// </summary>
        public void InitializeBands(float[] gains)
        {
            if (Bands.Count > 0)
                return;

            float max = float.MinValue;
            foreach (float g in gains)
                if (g > max) max = g;

            var defs = new (float freq, float bw)[]
            {
                (30,    0.8f),
                (75,    0.8f),
                (150,   0.8f),
                (300,   0.8f),
                (600,   0.8f),
                (1250,  0.8f),
                (2500,  0.8f),
                (5000,  0.8f),
                (10000, 0.8f),
                (20000, 0.8f),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                Bands.Add(new EqualizerBand
                {
                    Index = i,
                    Frequency = defs[i].freq,
                    Bandwidth = defs[i].bw,
                    Gain = gains[i] - max,
                });
            }
        }

        // ── Filter management ──────────────────────────────────────────────────

        /// <summary>Rebuilds the BiQuad filters for a single band.</summary>
        public void UpdateBand(int index)
        {
            if (_filters == null) return;
            var band = Bands[index];
            for (int ch = 0; ch < _channels; ch++)
            {
                if (_filters[ch, index] == null)
                    _filters[ch, index] = BiQuadFilter.PeakingEQ(
                        _currentEncodingProperties.SampleRate,
                        band.Frequency, band.Bandwidth, band.Gain);
                else
                    _filters[ch, index].SetPeakingEq(
                        _currentEncodingProperties.SampleRate,
                        band.Frequency, band.Bandwidth, band.Gain);
            }
        }

        /// <summary>Rebuilds the BiQuad filters for every band.</summary>
        public void UpdateAllBands()
        {
            for (int i = 0; i < _bandCount; i++)
                UpdateBand(i);
        }

        // ── IBasicAudioEffect implementation ───────────────────────────────────

        public bool UseInputFrameForOutput => true;

        public IReadOnlyList<AudioEncodingProperties> SupportedEncodingProperties
        {
            get
            {
                if (_supportedEncodingProperties != null)
                    return _supportedEncodingProperties;

                _supportedEncodingProperties = new List<AudioEncodingProperties>();

                void Add(uint sampleRate, uint channels)
                {
                    var p = AudioEncodingProperties.CreatePcm(sampleRate, channels, 32);
                    p.Subtype = MediaEncodingSubtypes.Float;
                    _supportedEncodingProperties.Add(p);
                }

                Add(44100, 1); Add(48000, 1);
                Add(44100, 2); Add(48000, 2);
                Add(96000, 2); Add(192000, 2);

                return _supportedEncodingProperties;
            }
        }

        public void SetEncodingProperties(AudioEncodingProperties encodingProperties)
        {
            _currentEncodingProperties = encodingProperties;

            if (_channels != (int)encodingProperties.ChannelCount || _bandCount != Bands.Count)
            {
                _channels = (int)encodingProperties.ChannelCount;
                _bandCount = Bands.Count;
                _filters = new BiQuadFilter[_channels, _bandCount];
            }

            UpdateAllBands();
        }

        public void ProcessFrame(ProcessAudioFrameContext context)
        {
            if (!IsEnabled) return;

            unsafe
            {
                using AudioBuffer inputBuffer = context.InputFrame.LockBuffer(AudioBufferAccessMode.ReadWrite);
                using IMemoryBufferReference inputReference = inputBuffer.CreateReference();

                ((IMemoryBufferByteAccess)inputReference).GetBuffer(
                    out byte* inputDataInBytes, out _);

                float* inputDataInFloat = (float*)inputDataInBytes;
                int dataInFloatLength = (int)inputBuffer.Length / sizeof(float);

                for (int n = 0; n < dataInFloatLength; n++)
                {
                    int ch = n % _channels;
                    for (int band = 0; band < _bandCount; band++)
                        inputDataInFloat[n] = _filters[ch, band].Transform(inputDataInFloat[n]);
                }
            }
        }

        public void DiscardQueuedFrames() { }

        public void Close(MediaEffectClosedReason reason)
        {
            if (reason != MediaEffectClosedReason.EffectCurrentlyUnloaded)
                return;

            if (_filters != null)
            {
                for (int i = 0; i < _channels; i++)
                    for (int j = 0; j < _bandCount; j++)
                        _filters[i, j] = null;
            }

            _channels = 0;
            _bandCount = 0;
            _filters = null;
        }

        public void SetProperties(IPropertySet configuration)
            => _configuration = configuration;
    }
}
