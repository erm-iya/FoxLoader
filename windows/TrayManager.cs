using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ErmiyaDesktop
{
    public sealed class TrayManager : IDisposable
    {
        private const int WM_USER = 0x0400;
        public const int WM_TRAYICON = WM_USER + 1024;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_SETVERSION = 0x00000004;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        private const int NOTIFYICON_VERSION_4 = 4;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;
        private const int NIN_SELECT = 0x0400;
        private const int NIN_KEYSELECT = 0x0401;
        private const int WM_NULL = 0x0000;

        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_NONOTIFY = 0x0080;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint MF_CHECKED = 0x00000008;
        private const uint MF_UNCHECKED = 0x00000000;

        private const int CMD_OPEN = 1001;
        private const int CMD_ADD = 1002;
        private const int CMD_SETTINGS = 1003;
        private const int CMD_SPEED_LIMITER = 1004;
        private const int CMD_EXIT = 1005;

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll")]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

        private readonly IntPtr _hwnd;
        private readonly Action _onOpen;
        private readonly Action _onAdd;
        private readonly Action _onSettings;
        private readonly Action _onToggleSpeedLimiter;
        private readonly Action _onExit;

        private NOTIFYICONDATA _nid;
        private IntPtr _hIcon = IntPtr.Zero;
        private SubclassProc? _subclassDelegate;
        private bool _isCreated = false;

        public TrayManager(
            IntPtr hwnd,
            Action onOpen,
            Action onAdd,
            Action onSettings,
            Action onToggleSpeedLimiter,
            Action onExit)
        {
            _hwnd = hwnd;
            _onOpen = onOpen;
            _onAdd = onAdd;
            _onSettings = onSettings;
            _onToggleSpeedLimiter = onToggleSpeedLimiter;
            _onExit = onExit;

            Initialize();
        }

        private void Initialize()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
            }

            if (File.Exists(iconPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            }

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "FoxLoader"
            };

            _isCreated = Shell_NotifyIcon(NIM_ADD, ref _nid);
            if (_isCreated)
            {
                _nid.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
                Shell_NotifyIcon(NIM_SETVERSION, ref _nid);
            }

            _subclassDelegate = WndProc;
            SetWindowSubclass(_hwnd, _subclassDelegate, (UIntPtr)101, UIntPtr.Zero);
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
        {
            if (uMsg == WM_TRAYICON)
            {
                // In NOTIFYICON_VERSION_4, event code is in the low 16 bits of lParam
                int msg = (int)((long)lParam & 0xFFFF);
                if (msg == WM_LBUTTONUP || msg == WM_LBUTTONDBLCLK || msg == NIN_SELECT)
                {
                    _onOpen?.Invoke();
                    return IntPtr.Zero;
                }
                else if (msg == WM_RBUTTONUP || msg == WM_CONTEXTMENU)
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            try
            {
                AppendMenu(hMenu, MF_STRING, CMD_OPEN, "Show Downloads");
                AppendMenu(hMenu, MF_STRING, CMD_ADD, "Import From Clipboard");
                AppendMenu(hMenu, MF_STRING, CMD_SETTINGS, "Settings");

                bool speedLimiterEnabled = false;
                try
                {
                    var settings = SettingsStorage.LoadFromDisk();
                    speedLimiterEnabled = settings?.SpeedLimiter?.Enabled ?? false;
                }
                catch { }

                uint speedFlag = MF_STRING | (speedLimiterEnabled ? MF_CHECKED : MF_UNCHECKED);
                AppendMenu(hMenu, speedFlag, CMD_SPEED_LIMITER, "Speed Limiter");

                AppendMenu(hMenu, MF_SEPARATOR, 0, null);
                AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Exit FoxLoader");

                GetCursorPos(out var pt);
                SetForegroundWindow(_hwnd);

                uint cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
                PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (cmd == CMD_OPEN)
                {
                    _onOpen?.Invoke();
                }
                else if (cmd == CMD_ADD)
                {
                    _onAdd?.Invoke();
                }
                else if (cmd == CMD_SETTINGS)
                {
                    _onSettings?.Invoke();
                }
                else if (cmd == CMD_SPEED_LIMITER)
                {
                    _onToggleSpeedLimiter?.Invoke();
                }
                else if (cmd == CMD_EXIT)
                {
                    _onExit?.Invoke();
                }
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }

        public void Dispose()
        {
            if (_isCreated)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _isCreated = false;
            }

            if (_subclassDelegate != null)
            {
                RemoveWindowSubclass(_hwnd, _subclassDelegate, (UIntPtr)101);
                _subclassDelegate = null;
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }
    }
}
