using System;
using WinRT;

namespace Rise.Effects
{
    /// <summary>
    /// Registers <see cref="EqualizerEffect"/> as an in-process WinRT activatable class
    /// so that <c>MediaPlayer.AddAudioEffect("Rise.Effects.EqualizerEffect", …)</c>
    /// continues to work after the migration away from <c>winmdobj</c>.
    ///
    /// In UWP the runtime discovered activatable classes from the .winmd metadata.
    /// In WinUI 3 (unpackaged desktop) we register them explicitly at startup using
    /// the CsWinRT <see cref="Module.RegisterActivatableObject"/> API, which hooks
    /// the in-process COM activation path that <c>RoGetActivationFactory</c> calls.
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

            // CsWinRT in-process activation registration.
            // The string must match the FullName used in AddAudioEffect.
            Module.RegisterActivatableObject(
                "Rise.Effects.EqualizerEffect",
                () => MarshalInspectable<EqualizerEffect>.FromManaged(EqualizerEffect.Current));
        }
    }
}
