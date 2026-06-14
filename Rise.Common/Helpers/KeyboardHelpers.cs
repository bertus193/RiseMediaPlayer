using Microsoft.UI.Input;
using Windows.System;

namespace Rise.Common.Helpers
{
    public class KeyboardHelpers
    {
        public static bool IsCtrlPressed()
        {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }
    }
}
