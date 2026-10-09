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

        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_NONOTIFY = 0x0080;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;

        private const int CMD_OPEN = 1001;
        private const int CMD_EXIT = 1002;

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

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

        [DllImport("comctl32.dll")]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

        private readonly IntPtr _hwnd;
        private readonly Action _onOpen;
        private readonly Action _onExit;
        private NOTIFYICONDATA _nid;
        private IntPtr _hIcon = IntPtr.Zero;
        private SubclassProc? _subclassDelegate;
        private bool _isCreated = false;

        public TrayManager(IntPtr hwnd, Action onOpen, Action onExit)
        {
            _hwnd = hwnd;
            _onOpen = onOpen;
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
                int msg = (int)lParam;
                if (msg == WM_LBUTTONUP || msg == WM_LBUTTONDBLCLK)
                {
                    _onOpen.Invoke();
                    return IntPtr.Zero;
                }
                else if (msg == WM_RBUTTONUP)
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
                AppendMenu(hMenu, MF_STRING, CMD_OPEN, "Open FoxLoader");
                AppendMenu(hMenu, MF_SEPARATOR, 0, null);
                AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Exit FoxLoader");

                GetCursorPos(out var pt);
                SetForegroundWindow(_hwnd);

                uint cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

                if (cmd == CMD_OPEN)
                {
                    _onOpen.Invoke();
                }
                else if (cmd == CMD_EXIT)
                {
                    _onExit.Invoke();
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
