using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinUiAutomation.Studio
{
    public enum KeyboardEvents
    {
        KeyDown = 0x0100,
        KeyUp = 0x0101,
        SystemKeyDown = 0x0104,
        SystemKeyUp = 0x0105
    }

    // https://www.pinvoke.net/default.aspx/user32.GetKeyState + A-Z
    public enum VirtualKeys : int
    {
        VK_LBUTTON = 0x01,
        VK_RBUTTON = 0x02,
        VK_CANCEL = 0x03,
        VK_MBUTTON = 0x04,
        //
        VK_XBUTTON1 = 0x05,
        VK_XBUTTON2 = 0x06,
        //
        VK_BACK = 0x08,
        VK_TAB = 0x09,
        //
        VK_CLEAR = 0x0C,
        VK_RETURN = 0x0D,
        //
        VK_SHIFT = 0x10,
        VK_CONTROL = 0x11,
        VK_MENU = 0x12,
        VK_PAUSE = 0x13,
        VK_CAPITAL = 0x14,
        //
        VK_KANA = 0x15,
        VK_HANGEUL = 0x15,  /* old name - should be here for compatibility */
        VK_HANGUL = 0x15,
        VK_JUNJA = 0x17,
        VK_FINAL = 0x18,
        VK_HANJA = 0x19,
        VK_KANJI = 0x19,
        //
        VK_ESCAPE = 0x1B,
        //
        VK_CONVERT = 0x1C,
        VK_NONCONVERT = 0x1D,
        VK_ACCEPT = 0x1E,
        VK_MODECHANGE = 0x1F,
        //
        VK_SPACE = 0x20,
        VK_PRIOR = 0x21,
        VK_NEXT = 0x22,
        VK_END = 0x23,
        VK_HOME = 0x24,
        VK_LEFT = 0x25,
        VK_UP = 0x26,
        VK_RIGHT = 0x27,
        VK_DOWN = 0x28,
        VK_SELECT = 0x29,
        VK_PRINT = 0x2A,
        VK_EXECUTE = 0x2B,
        VK_SNAPSHOT = 0x2C,
        VK_INSERT = 0x2D,
        VK_DELETE = 0x2E,
        VK_HELP = 0x2F,

        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,

        //
        VK_LWIN = 0x5B,
        VK_RWIN = 0x5C,
        VK_APPS = 0x5D,
        //
        VK_SLEEP = 0x5F,
        //
        VK_NUMPAD0 = 0x60,
        VK_NUMPAD1 = 0x61,
        VK_NUMPAD2 = 0x62,
        VK_NUMPAD3 = 0x63,
        VK_NUMPAD4 = 0x64,
        VK_NUMPAD5 = 0x65,
        VK_NUMPAD6 = 0x66,
        VK_NUMPAD7 = 0x67,
        VK_NUMPAD8 = 0x68,
        VK_NUMPAD9 = 0x69,
        VK_MULTIPLY = 0x6A,
        VK_ADD = 0x6B,
        VK_SEPARATOR = 0x6C,
        VK_SUBTRACT = 0x6D,
        VK_DECIMAL = 0x6E,
        VK_DIVIDE = 0x6F,
        VK_F1 = 0x70,
        VK_F2 = 0x71,
        VK_F3 = 0x72,
        VK_F4 = 0x73,
        VK_F5 = 0x74,
        VK_F6 = 0x75,
        VK_F7 = 0x76,
        VK_F8 = 0x77,
        VK_F9 = 0x78,
        VK_F10 = 0x79,
        VK_F11 = 0x7A,
        VK_F12 = 0x7B,
        VK_F13 = 0x7C,
        VK_F14 = 0x7D,
        VK_F15 = 0x7E,
        VK_F16 = 0x7F,
        VK_F17 = 0x80,
        VK_F18 = 0x81,
        VK_F19 = 0x82,
        VK_F20 = 0x83,
        VK_F21 = 0x84,
        VK_F22 = 0x85,
        VK_F23 = 0x86,
        VK_F24 = 0x87,
        //
        VK_NUMLOCK = 0x90,
        VK_SCROLL = 0x91,
        //
        VK_OEM_NEC_EQUAL = 0x92,   // '=' key on numpad
                                   //
        VK_OEM_FJ_JISHO = 0x92,   // 'Dictionary' key
        VK_OEM_FJ_MASSHOU = 0x93,   // 'Unregister word' key
        VK_OEM_FJ_TOUROKU = 0x94,   // 'Register word' key
        VK_OEM_FJ_LOYA = 0x95,   // 'Left OYAYUBI' key
        VK_OEM_FJ_ROYA = 0x96,   // 'Right OYAYUBI' key
                                 //
        VK_LSHIFT = 0xA0,
        VK_RSHIFT = 0xA1,
        VK_LCONTROL = 0xA2,
        VK_RCONTROL = 0xA3,
        VK_LMENU = 0xA4,
        VK_RMENU = 0xA5,
        //
        VK_BROWSER_BACK = 0xA6,
        VK_BROWSER_FORWARD = 0xA7,
        VK_BROWSER_REFRESH = 0xA8,
        VK_BROWSER_STOP = 0xA9,
        VK_BROWSER_SEARCH = 0xAA,
        VK_BROWSER_FAVORITES = 0xAB,
        VK_BROWSER_HOME = 0xAC,
        //
        VK_VOLUME_MUTE = 0xAD,
        VK_VOLUME_DOWN = 0xAE,
        VK_VOLUME_UP = 0xAF,
        VK_MEDIA_NEXT_TRACK = 0xB0,
        VK_MEDIA_PREV_TRACK = 0xB1,
        VK_MEDIA_STOP = 0xB2,
        VK_MEDIA_PLAY_PAUSE = 0xB3,
        VK_LAUNCH_MAIL = 0xB4,
        VK_LAUNCH_MEDIA_SELECT = 0xB5,
        VK_LAUNCH_APP1 = 0xB6,
        VK_LAUNCH_APP2 = 0xB7,
        //
        VK_OEM_1 = 0xBA,   // ';:' for US
        VK_OEM_PLUS = 0xBB,   // '+' any country
        VK_OEM_COMMA = 0xBC,   // ',' any country
        VK_OEM_MINUS = 0xBD,   // '-' any country
        VK_OEM_PERIOD = 0xBE,   // '.' any country
        VK_OEM_2 = 0xBF,   // '/?' for US
        VK_OEM_3 = 0xC0,   // '`~' for US
                           //
        VK_OEM_4 = 0xDB,  //  '[{' for US
        VK_OEM_5 = 0xDC,  //  '\|' for US
        VK_OEM_6 = 0xDD,  //  ']}' for US
        VK_OEM_7 = 0xDE,  //  ''"' for US
        VK_OEM_8 = 0xDF,
        //
        VK_OEM_AX = 0xE1,  //  'AX' key on Japanese AX kbd
        VK_OEM_102 = 0xE2,  //  "<>" or "\|" on RT 102-key kbd.
        VK_ICO_HELP = 0xE3,  //  Help key on ICO
        VK_ICO_00 = 0xE4,  //  00 key on ICO
                           //
        VK_PROCESSKEY = 0xE5,
        //
        VK_ICO_CLEAR = 0xE6,
        //
        VK_PACKET = 0xE7,
        //
        VK_OEM_RESET = 0xE9,
        VK_OEM_JUMP = 0xEA,
        VK_OEM_PA1 = 0xEB,
        VK_OEM_PA2 = 0xEC,
        VK_OEM_PA3 = 0xED,
        VK_OEM_WSCTRL = 0xEE,
        VK_OEM_CUSEL = 0xEF,
        VK_OEM_ATTN = 0xF0,
        VK_OEM_FINISH = 0xF1,
        VK_OEM_COPY = 0xF2,
        VK_OEM_AUTO = 0xF3,
        VK_OEM_ENLW = 0xF4,
        VK_OEM_BACKTAB = 0xF5,
        //
        VK_ATTN = 0xF6,
        VK_CRSEL = 0xF7,
        VK_EXSEL = 0xF8,
        VK_EREOF = 0xF9,
        VK_PLAY = 0xFA,
        VK_ZOOM = 0xFB,
        VK_NONAME = 0xFC,
        VK_PA1 = 0xFD,
        VK_OEM_CLEAR = 0xFE
    }

    public class CharPair
    {
        public CharPair(char c1, char c2)
        {
            char1 = c1;
            char2 = c2;
        }

        public char char1;
        public char char2;
    }

    class KeyboardEventHandler
    {
        // keyboard state
        public static List<byte> s_listRecordedKeycode = new List<byte>();
        static int s_keyboardInputTick; // TODO: currently write-only, never read
        static VirtualKeys s_lastKeyCode = VirtualKeys.VK_RETURN;
        static bool s_lastKeyDown = false;
        static bool s_bCapsLock = false;
        static bool s_bNumLock = false;
        static bool s_bScrollLock = false;

        public static Dictionary<string, string> s_Vk2String = new Dictionary<string, string>();
        public static Dictionary<int, CharPair> s_Vk2CharPair = new Dictionary<int, CharPair>();

        public static EnumUiTaskName FromStringTaskName(string strTask)
        {
            foreach (EnumUiTaskName val in Enum.GetValues(typeof(EnumUiTaskName)))
            {
                if (strTask == val.ToString())
                {
                    return val;
                }
            }

            return EnumUiTaskName.UnknownUiTask;
        }

        public static void InitVk2StringMap()
        {
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD0.ToString(), "NumberPad0");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD1.ToString(), "NumberPad1");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD2.ToString(), "NumberPad2");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD3.ToString(), "NumberPad3");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD4.ToString(), "NumberPad4");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD5.ToString(), "NumberPad5");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD6.ToString(), "NumberPad6");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD7.ToString(), "NumberPad7");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD8.ToString(), "NumberPad8");
            s_Vk2String.Add(VirtualKeys.VK_NUMPAD9.ToString(), "NumberPad9");
            s_Vk2String.Add(VirtualKeys.VK_MULTIPLY.ToString(), "Multiply");
            s_Vk2String.Add(VirtualKeys.VK_ADD.ToString(), "Add");
            s_Vk2String.Add(VirtualKeys.VK_SEPARATOR.ToString(), "Separator");
            s_Vk2String.Add(VirtualKeys.VK_OEM_NEC_EQUAL.ToString(), "Equal");
            s_Vk2String.Add(VirtualKeys.VK_SUBTRACT.ToString(), "Subtract");
            s_Vk2String.Add(VirtualKeys.VK_DIVIDE.ToString(), "Divide");
            s_Vk2String.Add(VirtualKeys.VK_F1.ToString(), "F1");
            s_Vk2String.Add(VirtualKeys.VK_F2.ToString(), "F2");
            s_Vk2String.Add(VirtualKeys.VK_F3.ToString(), "F3");
            s_Vk2String.Add(VirtualKeys.VK_F4.ToString(), "F4");
            s_Vk2String.Add(VirtualKeys.VK_F5.ToString(), "F5");
            s_Vk2String.Add(VirtualKeys.VK_F6.ToString(), "F6");
            s_Vk2String.Add(VirtualKeys.VK_F7.ToString(), "F7");
            s_Vk2String.Add(VirtualKeys.VK_F8.ToString(), "F8");
            s_Vk2String.Add(VirtualKeys.VK_F9.ToString(), "F9");
            s_Vk2String.Add(VirtualKeys.VK_F10.ToString(), "F10");
            s_Vk2String.Add(VirtualKeys.VK_F11.ToString(), "F11");
            s_Vk2String.Add(VirtualKeys.VK_F12.ToString(), "F12");
            s_Vk2String.Add(VirtualKeys.VK_DECIMAL.ToString(), "Decimal");
            //s_Vk2String.Add(VirtualKeys., "Meta");
            s_Vk2String.Add(VirtualKeys.VK_OEM_1.ToString(), "Semicolon");
            s_Vk2String.Add(VirtualKeys.VK_INSERT.ToString(), "Insert");
            s_Vk2String.Add(VirtualKeys.VK_CANCEL.ToString(), "Cancel");
            s_Vk2String.Add(VirtualKeys.VK_HELP.ToString(), "Help");
            s_Vk2String.Add(VirtualKeys.VK_BACK.ToString(), "Backspace");
            s_Vk2String.Add(VirtualKeys.VK_TAB.ToString(), "Tab");
            s_Vk2String.Add(VirtualKeys.VK_CLEAR.ToString(), "Clear");
            s_Vk2String.Add(VirtualKeys.VK_RETURN.ToString(), "Return");
            //s_Vk2String.Add(VirtualKeys.VK_RETURN.ToString(), "Enter");
            s_Vk2String.Add(VirtualKeys.VK_LSHIFT.ToString(), "Shift");
            s_Vk2String.Add(VirtualKeys.VK_RSHIFT.ToString(), "RightShift");
            s_Vk2String.Add(VirtualKeys.VK_LCONTROL.ToString(), "Control");
            s_Vk2String.Add(VirtualKeys.VK_RCONTROL.ToString(), "RightControl");
            s_Vk2String.Add(VirtualKeys.VK_LMENU.ToString(), "Alt");
            s_Vk2String.Add(VirtualKeys.VK_RMENU.ToString(), "RightAlt");

            s_Vk2String.Add(VirtualKeys.VK_DELETE.ToString(), "Delete");
            s_Vk2String.Add(VirtualKeys.VK_PAUSE.ToString(), "Pause");
            s_Vk2String.Add(VirtualKeys.VK_SPACE.ToString(), "Space");
            s_Vk2String.Add(VirtualKeys.VK_PRIOR.ToString(), "PageUp");
            s_Vk2String.Add(VirtualKeys.VK_NEXT.ToString(), "PageDown");
            s_Vk2String.Add(VirtualKeys.VK_END.ToString(), "End");
            s_Vk2String.Add(VirtualKeys.VK_HOME.ToString(), "Home");
            s_Vk2String.Add(VirtualKeys.VK_LEFT.ToString(), "Left");
            //s_Vk2String.Add(VirtualKeys.VK_LEFT.ToString(), "ArrowLeft");
            s_Vk2String.Add(VirtualKeys.VK_UP.ToString(), "Up");
            //s_Vk2String.Add(VirtualKeys.VK_UP.ToString(), "ArrowUp");
            s_Vk2String.Add(VirtualKeys.VK_RIGHT.ToString(), "Right");
            //s_Vk2String.Add(VirtualKeys.VK_RIGHT.ToString(), "ArrowRight");
            s_Vk2String.Add(VirtualKeys.VK_DOWN.ToString(), "Down");
            //s_Vk2String.Add(VirtualKeys.VK_DOWN.ToString(), "ArrowDown");
            s_Vk2String.Add(VirtualKeys.VK_ESCAPE.ToString(), "Escape");
            s_Vk2String.Add(VirtualKeys.VK_LWIN.ToString(), "Command");
            s_Vk2String.Add(VirtualKeys.VK_RWIN.ToString(), "Command");

            s_Vk2CharPair.Add(0x30, new CharPair('0', ')'));
            s_Vk2CharPair.Add(0x31, new CharPair('1', '!'));
            s_Vk2CharPair.Add(0x32, new CharPair('2', '@'));
            s_Vk2CharPair.Add(0x33, new CharPair('3', '#'));
            s_Vk2CharPair.Add(0x34, new CharPair('4', '$'));
            s_Vk2CharPair.Add(0x35, new CharPair('5', '%'));
            s_Vk2CharPair.Add(0x36, new CharPair('6', '^'));
            s_Vk2CharPair.Add(0x37, new CharPair('7', '&'));
            s_Vk2CharPair.Add(0x38, new CharPair('8', '*'));
            s_Vk2CharPair.Add(0x39, new CharPair('9', '('));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_3, new CharPair('`', '~'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_MINUS, new CharPair('-', '_'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_PLUS, new CharPair('=', '+'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_4, new CharPair('[', '{'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_6, new CharPair(']', '}'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_5, new CharPair('\\', '|'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_1, new CharPair(';', ':'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_7, new CharPair(',', '<'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_PERIOD, new CharPair('.', '>'));
            s_Vk2CharPair.Add((int)VirtualKeys.VK_OEM_2, new CharPair('/', '?'));

            s_Vk2CharPair.Add(0x41, new CharPair('a', 'A'));
            s_Vk2CharPair.Add(0x42, new CharPair('b', 'B'));
            s_Vk2CharPair.Add(0x43, new CharPair('c', 'C'));
            s_Vk2CharPair.Add(0x44, new CharPair('d', 'D'));
            s_Vk2CharPair.Add(0x45, new CharPair('e', 'E'));
            s_Vk2CharPair.Add(0x46, new CharPair('f', 'F'));
            s_Vk2CharPair.Add(0x47, new CharPair('g', 'G'));
            s_Vk2CharPair.Add(0x48, new CharPair('h', 'H'));
            s_Vk2CharPair.Add(0x49, new CharPair('i', 'I'));
            s_Vk2CharPair.Add(0x4A, new CharPair('j', 'J'));
            s_Vk2CharPair.Add(0x4B, new CharPair('k', 'K'));
            s_Vk2CharPair.Add(0x4C, new CharPair('l', 'L'));
            s_Vk2CharPair.Add(0x4D, new CharPair('m', 'M'));
            s_Vk2CharPair.Add(0x4E, new CharPair('n', 'N'));
            s_Vk2CharPair.Add(0x4F, new CharPair('o', 'O'));
            s_Vk2CharPair.Add(0x50, new CharPair('p', 'P'));
            s_Vk2CharPair.Add(0x51, new CharPair('q', 'Q'));
            s_Vk2CharPair.Add(0x52, new CharPair('r', 'R'));
            s_Vk2CharPair.Add(0x53, new CharPair('s', 'S'));
            s_Vk2CharPair.Add(0x54, new CharPair('t', 'T'));
            s_Vk2CharPair.Add(0x55, new CharPair('u', 'U'));
            s_Vk2CharPair.Add(0x56, new CharPair('v', 'V'));
            s_Vk2CharPair.Add(0x57, new CharPair('w', 'W'));
            s_Vk2CharPair.Add(0x58, new CharPair('x', 'X'));
            s_Vk2CharPair.Add(0x59, new CharPair('y', 'Y'));
            s_Vk2CharPair.Add(0x5A, new CharPair('z', 'Z'));
        }

        public static string Vk2string(string vkString)
        {
            if (s_Vk2String.ContainsKey(vkString))
            {
                return s_Vk2String[vkString];
            }
            else
            {
                return vkString;
            }
        }

        public static char Vk2char(int vk, bool bShiftDown)
        {
            if (s_Vk2CharPair.ContainsKey(vk))
            {
                CharPair chaiPaire = s_Vk2CharPair[vk];
                return bShiftDown ? chaiPaire.char2 : chaiPaire.char1;
            }

            return (char)0;
        }

        public static void RecordKey(KeyboardEvents keyEvent, VirtualKeys vKey, int scanCode)
        {
            bool bIsKeydown = keyEvent == KeyboardEvents.SystemKeyDown || keyEvent == KeyboardEvents.KeyDown;

            if (s_lastKeyCode == vKey && s_lastKeyDown == bIsKeydown)
            {
                return;
            }

            s_lastKeyDown = bIsKeydown;
            s_lastKeyCode = vKey;

            lock (MouseEventHandler.s_lockUiPath)
            {
                if (s_listRecordedKeycode.Count == 0)
                {
                    s_keyboardInputTick = Environment.TickCount;
                    s_bCapsLock = (NativeMethods.GetKeyState((int)VirtualKeys.VK_CAPITAL) & 0x0001) != 0;
                    s_bNumLock = (NativeMethods.GetKeyState((int)VirtualKeys.VK_NUMLOCK) & 0x0001) != 0;
                    s_bScrollLock = (NativeMethods.GetKeyState((int)VirtualKeys.VK_SCROLL) & 0x0001) != 0;
                }

                s_listRecordedKeycode.Add((byte)(bIsKeydown ? 0 : 1));
                s_listRecordedKeycode.Add((byte)vKey);
            }
        }

        public static string XmlEncode(string strData)
        {
            if (strData == null)
                return null;

            // 1. Escape ampersands first to prevent double-escaping
            strData = strData.Replace("&", "&amp;");

            // 2. Escape single quotes for single-quoted NodePath strings
            // Note: In standard NodePath literals wrapped in single quotes, single quotes 
            // are escaped as '' or using concat(). If consumed by an XML DOM, &apos; is valid.
            strData = strData.Replace("\'", "''");

            // 3. Basic XML entities
            strData = strData.Replace("<", "&lt;");
            strData = strData.Replace(">", "&gt;");

            // Note: Double quotes (\") no longer need entity encoding inside single-quoted strings: ['...']

            return strData;
        }

        public static void AddKeyboardInputTask(ref string strBase64, bool bCapsLock, bool bNumLock, bool bScrollLock)
        {
            if (string.IsNullOrEmpty(strBase64))
            {
                return;
            }

            RecordedUiTask lastRecordedUi = null;
            lock (RecordedUiTask.s_lockRecordedUi)
            {
                if (RecordedUiTask.s_listRecordedUi.Count > 0)
                {
                    lastRecordedUi = RecordedUiTask.s_listRecordedUi.Last();
                }
            }

            if (lastRecordedUi != null && lastRecordedUi.UiTaskName == EnumUiTaskName.KeyboardInput)
            {
                lastRecordedUi.AppendKeyboardInput(strBase64);
            }
            else
            {
                var keyboarTask = new RecordedUiTask(EnumUiTaskName.KeyboardInput, strBase64, bCapsLock, bScrollLock, bNumLock);
                MainWindow.AddRecordedUi(keyboarTask);
            }

            strBase64 = null;
        }

        public static void PublishKeyboardInput()
        {
            string strBase64 = string.Empty;
            bool bCapsLock, bNumLock, bScrollLock;

            lock (MouseEventHandler.s_lockUiPath)
            {
                if (KeyboardEventHandler.s_listRecordedKeycode.Count == 0)
                {
                    return;
                }

                strBase64 = Convert.ToBase64String(KeyboardEventHandler.s_listRecordedKeycode.ToArray());
                KeyboardEventHandler.s_listRecordedKeycode.Clear();

                bCapsLock = s_bCapsLock;
                bNumLock = s_bNumLock;
                bScrollLock = s_bScrollLock;
            }

            if (string.IsNullOrEmpty(strBase64) == false)
            {
                KeyboardEventHandler.AddKeyboardInputTask(ref strBase64, bCapsLock, bNumLock, bScrollLock);
            }
        }
    }
}