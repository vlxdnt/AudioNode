using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioNode.ViewModels
{
    /// <summary>
    /// Installs a low-level keyboard hook to intercept global media keys.
    /// By returning a non-zero value from the hook, we "swallow" the keypress
    /// so Windows and other apps never see it — giving us full priority.
    /// </summary>
    public class MediaKeyHook : IDisposable
    {
        // ---------------------------------------------------------------
        // user32.dll imports
        // ---------------------------------------------------------------

        /// <summary>Installs a hook procedure that monitors low-level keyboard input events.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        /// <summary>Removes the hook installed by SetWindowsHookEx.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>Passes the hook information to the next hook in the chain.</summary>
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        // ---------------------------------------------------------------
        // kernel32.dll import
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a module handle. Required by SetWindowsHookEx to scope
        /// the hook to the current process's module.
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // ---------------------------------------------------------------
        // Win32 constants
        // ---------------------------------------------------------------

        private const int WH_KEYBOARD_LL = 13;   // Low-level keyboard hook type
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;

        // Virtual-key codes for media buttons
        private const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const int VK_MEDIA_STOP       = 0xB2;
        private const int VK_MEDIA_NEXT_TRACK = 0xB0;
        private const int VK_MEDIA_PREV_TRACK = 0xB1;
        private const int VK_VOLUME_MUTE      = 0xAD;
        private const int VK_VOLUME_DOWN      = 0xAE;
        private const int VK_VOLUME_UP        = 0xAF;

        // ---------------------------------------------------------------
        // Public events, subscribe to these in MainWindow
        // ---------------------------------------------------------------
        public event EventHandler? PlayPausePressed;
        public event EventHandler? StopPressed;
        public event EventHandler? NextTrackPressed;
        public event EventHandler? PreviousTrackPressed;
        public event EventHandler? MutePressed;
        public event EventHandler? VolumeUpPressed;
        public event EventHandler? VolumeDownPressed;

        // ---------------------------------------------------------------
        // Internal state
        // ---------------------------------------------------------------

        /// <summary>
        /// When true, media key events are consumed here and NOT forwarded
        /// to Windows or any other application. Set false to allow pass-through.
        /// </summary>
        public bool TakePriority { get; set; } = true;

        private readonly LowLevelKeyboardProc _hookCallback;  // Keep alive — GC must not collect this
        private IntPtr _hookHandle = IntPtr.Zero;

        // Delegate signature required by SetWindowsHookEx for WH_KEYBOARD_LL
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Mirrors the KBDLLHOOKSTRUCT Win32 structure
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint   vkCode;      // Virtual-key code
            public uint   scanCode;
            public uint   flags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        // ---------------------------------------------------------------
        // Constructor / Install
        // ---------------------------------------------------------------

        public MediaKeyHook()
        {
            _hookCallback = HookCallback;  // Store so GC doesn't collect it
        }

        /// <summary>
        /// Installs the global keyboard hook. Call once (e.g. in MainWindow constructor).
        /// </summary>
        public void Install()
        {
            if (_hookHandle != IntPtr.Zero) return; // Already installed

            using var curProcess = Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule
                ?? throw new InvalidOperationException("Cannot get main module.");

            // GetModuleHandle (kernel32.dll) gives SetWindowsHookEx (user32.dll)
            // the correct module scope so the hook fires for all threads/processes.
            IntPtr hMod = GetModuleHandle(curModule.ModuleName);

            _hookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _hookCallback,
                hMod,
                0); // 0 = monitor all threads on the desktop

            if (_hookHandle == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>Removes the hook. Call this before the app exits.</summary>
        public void Uninstall()
        {
            if (_hookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        // ---------------------------------------------------------------
        // Hook callback — runs on every keypress system-wide
        // ---------------------------------------------------------------

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // nCode < 0 means we must pass the event on without processing
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool handled = HandleMediaKey((int)kbd.vkCode);

                // If we handled the key AND TakePriority is on,
                // return a non-zero value to SWALLOW the event.
                // Windows and other apps will never see it.
                if (handled && TakePriority)
                    return (IntPtr)1;
            }

            // Pass the event along to the next hook in the chain
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Fires the appropriate event for each media key.
        /// Returns true if the key was a media key we care about.
        /// </summary>
        private bool HandleMediaKey(int vkCode)
        {
            switch (vkCode)
            {
                case VK_MEDIA_PLAY_PAUSE:
                    PlayPausePressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_MEDIA_STOP:
                    StopPressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_MEDIA_NEXT_TRACK:
                    NextTrackPressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_MEDIA_PREV_TRACK:
                    PreviousTrackPressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_VOLUME_MUTE:
                    MutePressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_VOLUME_UP:
                    VolumeUpPressed?.Invoke(this, EventArgs.Empty);
                    return true;

                case VK_VOLUME_DOWN:
                    VolumeDownPressed?.Invoke(this, EventArgs.Empty);
                    return true;

                default:
                    return false; // Not a media key — let it pass through
            }
        }

        // ---------------------------------------------------------------
        // IDisposable
        // ---------------------------------------------------------------

        public void Dispose() => Uninstall();
    }
}
