using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Timers;

namespace WinUiAutomation.Studio
{
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    class MouseKeyboardHook
    {
        //Declare MouseHookProcedure as a HookProc type.
        static HookProc MouseHookProcedure;
        static HookProc KeyboardHookProcedure;

        //Declare the hook handle as an int.
        static uint s_hHookMouse = 0;
        static uint s_hHookKeyboard = 0;
        static uint s_curProcessId;
        static POINT s_pt;

        // WinUser.h
        public const int WM_MOUSEFIRST = 0x0200;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_RBUTTONDBLCLK = 0x0206;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_MBUTTONDBLCLK = 0x0209;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_XBUTTONUP = 0x020C;
        public const int WM_XBUTTONDBLCLK = 0x020D;

        //Declare the mouse hook constant.
        public const uint WH_JOURNALRECORD = 0;
        public const uint WH_JOURNALPLAYBACK = 1;
        public const uint WH_KEYBOARD = 2;
        public const uint WH_GETMESSAGE = 3;
        public const uint WH_CALLWNDPROC = 4;
        public const uint WH_CBT = 5;
        public const uint WH_SYSMSGFILTER = 6;
        public const uint WH_MOUSE = 7;
        public const uint WH_HARDWARE = 8;
        public const uint WH_DEBUG = 9;
        public const uint WH_SHELL = 10;
        public const uint WH_FOREGROUNDIDLE = 11;
        public const uint WH_CALLWNDPROCRET = 12;
        public const uint WH_KEYBOARD_LL = 13;
        public const uint WH_MOUSE_LL = 14;

        [StructLayout(LayoutKind.Sequential)]
        public class MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public uint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public uint dwExtraInfo;
        }

        private static void StartMouseHook()
        {
            StopMouseHook();

            MouseHookProcedure = new HookProc(MouseHookProc);
            s_hHookMouse = (uint)NativeMethods.SetWindowsHookExNative(MouseHookProcedure, (uint)WH_MOUSE_LL, NativeMethods.GetCurrentThreadId());
        }

        private static void StopMouseHook()
        {
            if (s_hHookMouse != 0)
            {
                if (!NativeMethods.UnhookWindowsHookEx((IntPtr)s_hHookMouse))
                {
                    // Log or ignore; fallback to forced exit later
                }
                s_hHookMouse = 0;
            }
            MouseHookProcedure = null;
        }

        private static void StartKeyboardHook()
        {
            StopKeyboardHook();

            KeyboardHookProcedure = new HookProc(KeyboardHookProc);
            s_hHookKeyboard = (uint)NativeMethods.SetWindowsHookExNative(KeyboardHookProcedure, (uint)WH_KEYBOARD_LL, 0);
        }

        private static void StopKeyboardHook()
        {
            if (s_hHookKeyboard != 0)
            {
                NativeMethods.UninitializeHook(s_hHookKeyboard);
                s_hHookKeyboard = 0;
            }

            KeyboardHookProcedure = null;
        }

        public static bool StartHook()
        {
            s_curProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

            MouseKeyboardHook.StartMouseHook();
            MouseKeyboardHook.StartKeyboardHook();

            return (s_hHookKeyboard != 0 && s_hHookMouse != 0);
        }

        public static void StopHook()
        {
            MouseKeyboardHook.StopMouseHook();
            MouseKeyboardHook.StopKeyboardHook();
        }

        public static IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0
                || MouseEventHandler.s_threadRecorder == null
                || MainWindow.s_mainWin.IsRecording == false)
            {
                return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            NativeMethods.GetPhysicalCursorPos(out s_pt);
            IntPtr hwndAtCursor = NativeMethods.WindowFromPoint(s_pt);
            NativeMethods.GetWindowThreadProcessId(hwndAtCursor, out uint pid);
            if (pid == s_curProcessId)
            {
                return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            KeyboardEvents kEvent = (KeyboardEvents)wParam.ToInt32();
            KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

            int scanCode = (int)kbd.scanCode;
            VirtualKeys vk = (VirtualKeys)kbd.vkCode;

            // Record encoded key virtual key
            KeyboardEventHandler.RecordKey(kEvent, vk, scanCode);

            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || MouseEventHandler.s_threadRecorder == null)
            {
                return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            NativeMethods.GetPhysicalCursorPos(out s_pt);
            IntPtr hwndAtCursor = NativeMethods.WindowFromPoint(s_pt);
            NativeMethods.GetWindowThreadProcessId(hwndAtCursor, out uint pid);
            if (pid == s_curProcessId)
            {
                return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            MSLLHOOKSTRUCT mhs = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            int left = mhs.pt.X;
            int top = mhs.pt.Y;

            switch (wParam.ToInt32())
            {
                case WM_MOUSEMOVE:
                    MouseEventHandler.MouseMove(left, top);
                    break;

                case WM_MOUSEWHEEL:
                    short zDelta = (short)(mhs.mouseData >> 16);
                    MouseEventHandler.MouseWheel(left, top, zDelta);
                    break;

                case WM_LBUTTONDOWN:
                    MouseEventHandler.LeftMouseDown(left, top);
                    break;

                case WM_LBUTTONUP:
                    MouseEventHandler.LeftMouseUp(left, top);
                    break;

                case WM_RBUTTONDOWN:
                    MouseEventHandler.RightMouseDown(left, top);
                    break;

                case WM_RBUTTONUP:
                    MouseEventHandler.RightMouseUp(left, top);
                    break;
            }

            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
    }
}
