using System.Runtime.InteropServices;
using System.Windows.Input;

namespace LiveCaptionsTranslator.services.Ocr
{
    internal enum ScreenOcrHotkeyStatus
    {
        Disabled,
        Registered,
        Invalid,
        Conflict
    }

    internal sealed class ScreenOcrHotkeyRegistration
    {
        public ScreenOcrHotkeyRegistration(ScreenOcrHotkeyStatus status, string normalizedHotkey = "")
        {
            Status = status;
            NormalizedHotkey = normalizedHotkey;
        }

        public ScreenOcrHotkeyStatus Status { get; }

        public string NormalizedHotkey { get; }
    }

    internal static class ScreenOcrHotkeyService
    {
        public const int HotkeyId = 0x4342;
        public const int WmHotkey = 0x0312;
        public const string DefaultHotkey = "Ctrl + Alt + O";

        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static ScreenOcrHotkeyRegistration Register(IntPtr hwnd, string? hotkey)
        {
            if (!TryParse(hotkey, out var modifiers, out var virtualKey, out var normalized))
                return new ScreenOcrHotkeyRegistration(ScreenOcrHotkeyStatus.Invalid);

            return RegisterHotKey(hwnd, HotkeyId, modifiers, virtualKey)
                ? new ScreenOcrHotkeyRegistration(ScreenOcrHotkeyStatus.Registered, normalized)
                : new ScreenOcrHotkeyRegistration(ScreenOcrHotkeyStatus.Conflict, normalized);
        }

        public static void Unregister(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
                UnregisterHotKey(hwnd, HotkeyId);
        }

        public static bool TryNormalize(string? hotkey, out string normalized)
        {
            return TryParse(hotkey, out _, out _, out normalized);
        }

        private static bool TryParse(
            string? hotkey,
            out uint modifiers,
            out uint virtualKey,
            out string normalized)
        {
            modifiers = 0;
            virtualKey = 0;
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(hotkey))
                return false;

            var parts = hotkey
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (parts.Count < 2)
                return false;

            var modifierNames = new List<string>();
            Key? key = null;

            foreach (var part in parts)
            {
                var token = part.Replace(" ", string.Empty);
                switch (token.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= ModControl;
                        AddModifierName(modifierNames, "Ctrl");
                        break;
                    case "alt":
                        modifiers |= ModAlt;
                        AddModifierName(modifierNames, "Alt");
                        break;
                    case "shift":
                        modifiers |= ModShift;
                        AddModifierName(modifierNames, "Shift");
                        break;
                    case "win":
                    case "windows":
                        modifiers |= ModWin;
                        AddModifierName(modifierNames, "Win");
                        break;
                    default:
                        if (key != null)
                            return false;

                        key = ParseKey(token);
                        if (key == null)
                            return false;
                        break;
                }
            }

            if (modifiers == 0 || key == null)
                return false;

            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
            if (virtualKey == 0)
                return false;

            normalized = string.Join(" + ", modifierNames.Concat([FormatKey(key.Value)]));
            return true;
        }

        private static Key? ParseKey(string token)
        {
            try
            {
                var converter = new KeyConverter();
                return converter.ConvertFromString(token) is Key key && key != Key.None
                    ? key
                    : null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static void AddModifierName(List<string> modifierNames, string name)
        {
            if (!modifierNames.Contains(name, StringComparer.Ordinal))
                modifierNames.Add(name);
        }

        private static string FormatKey(Key key)
        {
            return key.ToString().Length == 1
                ? key.ToString().ToUpperInvariant()
                : key.ToString();
        }
    }
}
