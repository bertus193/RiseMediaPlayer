using System.Runtime.InteropServices;

namespace Rise.Effects
{
    /// <summary>
    /// COM interface for direct byte-level access to a <see cref="Windows.Foundation.IMemoryBufferReference"/>.
    /// Required for zero-copy audio frame processing in <see cref="EqualizerEffect.ProcessFrame"/>.
    /// This interface definition is unchanged from the UWP version - the COM contract is identical
    /// in WinUI 3 / Windows App SDK.
    /// </summary>
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
