namespace SleepTimer
{
    readonly struct Shortcut
    {
        public readonly int Modifiers, Key;
        public bool IsBound => Key != 0;
        Shortcut(int modifiers, int key) { Modifiers = modifiers; Key = key; }

        // Win32 modifiers: Alt=1, Ctrl=2, Shift=4, Win=8. Require Alt/Ctrl/Win
        // so ordinary typing cannot start a power timer.
        public static bool TryCreate(int modifiers, int key, out Shortcut shortcut)
        {
            shortcut = default;
            bool supported = key >= 0x30 && key <= 0x39 || key >= 0x41 && key <= 0x5A
                || key >= 0x70 && key <= 0x87 || key >= 0x60 && key <= 0x6F
                || key >= 0x20 && key <= 0x28 || key == 0x2D || key == 0x2E
                || key >= 0xBA && key <= 0xE2;
            if ((modifiers & ~15) != 0 || (modifiers & 11) == 0 || !supported) return false;
            shortcut = new Shortcut(modifiers, key);
            return true;
        }

        public bool Matches(Shortcut other) => IsBound && other.IsBound && Modifiers == other.Modifiers && Key == other.Key;
        public bool MatchesMessage(long message) => IsBound && (message & 0xBFFF) == Modifiers
            && ((message >> 16) & 0xFFFF) == Key;
    }
}
