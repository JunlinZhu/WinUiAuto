using System;
using System.Runtime.InteropServices;
using System.Text;
using UiAutomation.Windows;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.Studio
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    internal class NativeMethods
    {
        internal const int BUFFERSIZE = WindowsAutomation.DefaultBufferSize;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("User32.Dll")]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, uint wParam, uint lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        internal static extern bool GetPhysicalCursorPos(out POINT point);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("USER32.dll")]
        internal static extern short GetKeyState(int nVirtKey);

        [DllImport("UiAutoLib.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint SetWindowsHookExNative(HookProc hProc, uint nHookId, uint nThreadId);

        [DllImport("UiAutoLib.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern IntPtr UninitializeHook(uint hId);

        internal static void MouseTask(string taskName, RECT rect, int offsetX, int offsetY, int deltaX, int deltaY)
        {
            string actionName = taskName == "LeftDblClick" ? nameof(UiAction.LeftDoubleClick) : taskName;
            UiAutoLib.Automation.PerformMouseAction(
                Enum.Parse<UiAction>(actionName),
                UiAutoLib.ToManaged(rect),
                offsetX,
                offsetY,
                deltaX,
                deltaY);
        }

        internal static void InvokeElementAction(int left, int top)
        {
            UiAutoLib.Automation.InvokeElementAction(left, top);
        }

        internal static void KeyboardTask(string text, bool capsLock, bool numLock, bool scrollLock)
        {
            UiAutoLib.Automation.SendKeyboardInput(text, capsLock, numLock, scrollLock);
        }
    }

    internal static class UiAutoLib
    {
        private static WindowsAutomation automation;

        internal static WindowsAutomation Automation => automation ?? throw new InvalidOperationException("UI automation is not initialized.");

        internal static void Init() => automation ??= new WindowsAutomation();

        internal static void UnInit()
        {
            automation?.Dispose();
            automation = null;
        }

        internal static uint CaptureUiPath(int left, int top, bool leafToRoot, StringBuilder path, int maxCount, ref RECT rect)
        {
            UiCaptureResult result = Automation.CapturePath(
                left,
                top,
                leafToRoot ? PathDirection.LeafToRoot : PathDirection.RootToLeaf,
                maxCount);
            path.Clear().Append(result.NodePath);
            rect = ToNative(result.Bounds);
            return 0;
        }

        internal static uint LocateUiByPath(string nodePath, bool flash, ref RECT rect, StringBuilder elementState, int maxCount)
        {
            UiLocateResult result = Automation.Locate(
                nodePath,
                new UiLocateOptions { Flash = flash, StateBufferSize = maxCount });
            rect = ToNative(result.Bounds);
            elementState.Clear().Append(result.RawState);
            return unchecked((uint)result.NativeHResult);
        }

        internal static uint AbortLocateUiByPath()
        {
            Automation.AbortLocate();
            return 0x80004004;
        }

        internal static uint FlashRect(ref RECT rect, int red, int green, int blue, int duration, int inflate)
        {
            Automation.Flash(ToManaged(rect), red, green, blue, duration, inflate);
            return 0;
        }

        internal static UiRectangle ToManaged(RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

        private static RECT ToNative(UiRectangle rect) => new()
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom
        };
    }
}
