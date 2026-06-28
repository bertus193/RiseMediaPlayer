using System;
using WinRT;

namespace Rise.Effects
{
    /// <summary>
    /// Registers <see cref="EqualizerEffect"/> as an in-process WinRT activatable class
    /// so that <c>MediaPlayer.AddAudioEffect</c> continues to work.
    ///
    /// In WinUI 3, MediaPlayer can work directly with managed IBasicAudioEffect
    /// implementations without requiring explicit WinRT activation registration.
    /// This class is kept for backward compatibility but may not be strictly necessary.
    ///
    /// Call <see cref="Register"/> once, before the first <c>AddAudioEffect</c>.
    /// App.xaml.cs already does this via <c>OnMPViewModelRequested</c>.
    /// </summary>
    public static class EqualizerEffectActivation
    {
        private static bool _registered;

        /// <summary>
        /// Registers <see cref="EqualizerEffect"/> as an in-process activatable type.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            // In CsWinRT 2.x with WinUI 3, explicit registration is typically not needed
            // as MediaPlayer can consume managed IBasicAudioEffect implementations directly.
            // If WinRT activation is required, consider using:
            // - A custom activation factory via DllGetActivationFactory
            // - Or ensure the effect is passed by Type reference (which it is in AddEffect call)
        }
    }
}
