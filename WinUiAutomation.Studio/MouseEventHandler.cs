using System;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace WinUiAutomation.Studio
{
    public enum MouseState
    {
        Idle,
        LeftDown,
        RightDown,
        Dragging,
        Wheel
    }

    class MouseEventHandler
    {
        // mosue events
        static int wheelAmount = 0;
        static MouseState s_mouseState = MouseState.Idle;
        static int leftDown, topDown, leftMove, topMove;
        static DateTime lastRecordedDateTime = DateTime.MinValue;

        // recording thread
        public static Thread s_threadRecorder = null;
        static ManualResetEvent s_eventQuitRecording = new ManualResetEvent(false);
        static ManualResetEvent s_eventRecordNow = new ManualResetEvent(false);

        static POINT ptUiWalking = new POINT { X = 0, Y = 0 };
        const int nMinDist = 15;
        static int moveDist = 0;

        static string s_strUiNodePath = null;
        public static RECT s_rectCursor = new RECT();
        public static object s_lockUiPath = new object();

        public static RECT s_rectLast = new RECT();
        public static RECT s_hoverRect = new RECT();

        static readonly object s_lockHoverTimer = new object();
        static Timer hoverTimer = null;
        static int hoverTimerGeneration = 0;

        static readonly TimerCallback hoverTimerCallback = new TimerCallback((state) =>
        {
            lock (s_lockHoverTimer)
            {
                if (hoverTimer == null || state is not int generation || generation != hoverTimerGeneration)
                {
                    return;
                }

                if (AreSameRect(s_hoverRect, s_rectCursor))
                {
                    return;
                }

                if (Volatile.Read(ref moveDist) > 2 * nMinDist)
                {
                    CreateUiTask(
                        EnumUiTaskName.MouseHover,
                        ptUiWalking.X - s_rectCursor.Left,
                        ptUiWalking.Y - s_rectCursor.Top,
                        0,
                        0);

                    s_hoverRect = s_rectCursor;
                }
                else
                {
                    hoverTimer.Change(500, Timeout.Infinite);
                }
            }
        });

        private static void StartHoverTimer()
        {
            lock (s_lockHoverTimer)
            {
                Interlocked.Exchange(ref moveDist, 0);
                hoverTimer?.Dispose();
                hoverTimerGeneration++;
                hoverTimer = new Timer(hoverTimerCallback, hoverTimerGeneration, 2000, Timeout.Infinite);
            }
        }

        private static void CancelHoverTimer()
        {
            lock (s_lockHoverTimer)
            {
                Interlocked.Exchange(ref moveDist, 0);
                hoverTimer?.Dispose();
                hoverTimer = null;
                hoverTimerGeneration++;
            }
        }

        public static void Init()
        {
            s_strUiNodePath = null;
            lastRecordedDateTime = DateTime.MinValue;
            KeyboardEventHandler.s_listRecordedKeycode.Clear();

            s_eventQuitRecording.Reset();
            s_eventRecordNow.Reset();

            s_threadRecorder = new Thread(RecorderThread);
            s_threadRecorder.IsBackground = true;
            s_threadRecorder.SetApartmentState(ApartmentState.STA);
            s_threadRecorder.Start();

            MouseKeyboardHook.StartHook();
        }

        public static void UnInit()
        {
            MouseKeyboardHook.StopHook();
            CancelHoverTimer();

            s_eventRecordNow.Reset();
            s_eventQuitRecording.Set();

            if (s_threadRecorder != null)
            {
                if (!s_threadRecorder.Join(1000) && s_threadRecorder.IsAlive)
                {
                    s_threadRecorder.Interrupt();
                    s_threadRecorder.Join(500);
                }

                s_threadRecorder = null;
            }
        }

        private static bool AreSameRect(RECT rect1, RECT rect2)
        {
            return (rect1.Left == rect2.Left &&
                    rect1.Top == rect2.Top &&
                    rect1.Right == rect2.Right &&
                    rect1.Bottom == rect2.Bottom);
        }

        private static void RecorderThread()
        {
            StringBuilder sb = new StringBuilder(NativeMethods.BUFFERSIZE);
            POINT pt = new POINT { X = 0, Y = 0 };
            var curProcessId = (uint)Process.GetCurrentProcess().Id;
            bool leftToRoot = MainWindow.s_walkDirection == "Up";

            try
            {
                while (true)
                {
                    bool bStartWalk = s_eventRecordNow.WaitOne();

                    if (KeyboardEventHandler.s_listRecordedKeycode.Count > 0)
                    {
                        KeyboardEventHandler.PublishKeyboardInput();
                    }

                    if (s_eventQuitRecording.WaitOne(0))
                    {
                        break;
                    }

                    if (bStartWalk)
                    {
                        NativeMethods.GetPhysicalCursorPos(out pt);
                        int dist = Math.Abs(pt.X - ptUiWalking.X) + Math.Abs(pt.Y - ptUiWalking.Y);

                        if (dist > nMinDist)
                        {
                            IntPtr hwndAtCursor = NativeMethods.WindowFromPoint(pt);
                            NativeMethods.GetWindowThreadProcessId(hwndAtCursor, out uint pid);
                            bool isOwnApp = (pid == curProcessId);
                            if (!isOwnApp)
                            {
                                ptUiWalking.X = pt.X;
                                ptUiWalking.Y = pt.Y;

                                try
                                {
                                    sb.Clear();
                                    s_rectLast = s_rectCursor;
                                    UiAutoLib.CaptureUiPath(ptUiWalking.X, ptUiWalking.Y, leftToRoot, sb, sb.Capacity, ref s_rectCursor);

                                    if (!AreSameRect(s_rectCursor, s_rectLast))
                                    {
                                        StartHoverTimer();
                                    }

                                    lock (s_lockUiPath)
                                    {
                                        s_strUiNodePath = sb.ToString();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // UI Automation calls against a foreign (possibly hung/elevated)
                                    // window can throw (e.g. COMException). Don't let a single bad
                                    // sample kill the recorder thread -- log and keep walking.
                                    Debug.WriteLine($"GetUiNodePath failed: {ex.Message}");
                                }
                            }
                        }

                        if (s_eventQuitRecording.WaitOne(0))
                        {
                            break;
                        }
                        s_eventRecordNow.Reset();
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
            }
        }

        public static void MouseMove(int left, int top)
        {
            if (s_mouseState == MouseState.LeftDown)
            {
                CancelHoverTimer();
                s_mouseState = MouseState.Dragging;
                s_eventRecordNow.Reset();
                return;
            }

            if (s_mouseState == MouseState.Dragging)
            {
                CancelHoverTimer();
                return;
            }

            int dist = Math.Abs(left - leftMove) + Math.Abs(top - topMove);
            if (dist < nMinDist)
            {
                s_eventRecordNow.Reset();
                return;
            }

            Interlocked.Add(ref moveDist, dist);
            leftMove = left;
            topMove = top;

            if (wheelAmount != 0)
            {
                CancelHoverTimer();
                ResolvePendingWheelTask(left, top);
            }

            s_eventRecordNow.Set();
        }

        static void CreateUiTask(EnumUiTaskName taskName, int leftLocal, int topLocal, int delaX, int deltaY)
        {
            CancelHoverTimer();
            string uiPath;
            lock (MouseEventHandler.s_lockUiPath)
            {
                uiPath = MouseEventHandler.s_strUiNodePath;
            }

            if (string.IsNullOrEmpty(uiPath))
            {
                // No UI path has been captured yet (e.g. the very first click right
                // after Init(), before the recorder thread has walked anything).
                Debug.WriteLine("CreateUiTask skipped: no UI path captured yet.");
                return;
            }

            int RecordedGapMs = 0;
            if (lastRecordedDateTime != DateTime.MinValue)
            {
                RecordedGapMs = (int)(System.DateTime.Now - lastRecordedDateTime).TotalMilliseconds;
            }
            lastRecordedDateTime = System.DateTime.Now;

            var nodesRootToLeaf = UiTreeNode.UiTreeNodesFromString(uiPath);
            var lastRecordedUi = new RecordedUiTask(nodesRootToLeaf, taskName);
            lastRecordedUi.LeftLocal = leftLocal;
            lastRecordedUi.TopLocal = topLocal;
            lastRecordedUi.DeltaX = delaX;
            lastRecordedUi.DeltaY = deltaY;
            lastRecordedUi.RecordedGapMs = RecordedGapMs;
            MainWindow.AddRecordedUi(lastRecordedUi);
        }

        public static void ResolvePendingWheelTask(int left, int top)
        {
            int pendingWheelAmount;
            lock (MouseEventHandler.s_lockUiPath)
            {
                pendingWheelAmount = wheelAmount;
                wheelAmount = 0;
                if (s_mouseState == MouseState.Wheel)
                {
                    s_mouseState = MouseState.Idle;
                }
            }

            if (pendingWheelAmount != 0)
            {
                CreateUiTask(EnumUiTaskName.MouseWheel, left - s_rectCursor.Left, top - s_rectCursor.Top, 5, pendingWheelAmount);
            }
        }

        public static void MouseWheel(int left, int top, short delta)
        {
            lock (MouseEventHandler.s_lockUiPath)
            {
                if (delta > 0)
                {
                    if (wheelAmount < 0)
                    {
                        ResolvePendingWheelTask(left, top);
                    }
                    wheelAmount += delta;
                }
                else if (delta < 0)
                {
                    if (wheelAmount > 0)
                    {
                        ResolvePendingWheelTask(left, top);
                    }
                    wheelAmount += delta;
                }

                if (wheelAmount != 0)
                {
                    s_mouseState = MouseState.Wheel;
                }
            }
        }

        public static void LeftMouseDown(int left, int top)
        {
            ResolvePendingWheelTask(left, top);

            lock (MouseEventHandler.s_lockUiPath)
            {
                if (s_mouseState == MouseState.Idle)
                {
                    s_mouseState = MouseState.LeftDown;
                    leftDown = left;
                    topDown = top;
                }
            }
        }

        public static void LeftMouseUp(int left, int top)
        {
            s_eventRecordNow.Reset();

            ResolvePendingWheelTask(left, top);
            var leftLocal = leftDown - s_rectCursor.Left;
            var topLocal = topDown - s_rectCursor.Top;
            var deltaX = left - leftDown;
            var deltaY = top - topDown;

            if (s_mouseState == MouseState.Dragging && (Math.Abs(deltaX) + Math.Abs(deltaY)) > 1)
            {
                CreateUiTask(EnumUiTaskName.Drag, leftLocal, topLocal, deltaX, deltaY);
            }
            else
            {
                int RecordedGapMs = 0;
                if (lastRecordedDateTime != DateTime.MinValue)
                {
                    RecordedGapMs = (int)(System.DateTime.Now - lastRecordedDateTime).TotalMilliseconds;
                }

                if (lastRecordedDateTime != DateTime.MinValue && RecordedGapMs < NativeMethods.GetDoubleClickTime())
                {
                    if (!RecordedUiTask.LeftClickToDoubleClick())
                    {
                        CreateUiTask(EnumUiTaskName.LeftDblClick, leftLocal, topLocal, deltaX, deltaY);
                    }
                }
                else
                {
                    CreateUiTask(EnumUiTaskName.LeftClick, leftLocal, topLocal, deltaX, deltaY);
                }
            }

            s_mouseState = MouseState.Idle;
        }

        public static void RightMouseDown(int left, int top)
        {
            ResolvePendingWheelTask(left, top);

            if (s_mouseState == MouseState.Idle)
            {
                s_mouseState = MouseState.RightDown;
                leftDown = left;
                topDown = top;
            }
        }

        public static void RightMouseUp(int left, int top)
        {
            s_eventRecordNow.Reset();

            if (s_mouseState == MouseState.RightDown)
            {
                ResolvePendingWheelTask(left, top);

                CreateUiTask(EnumUiTaskName.RightClick,
                    leftDown - s_rectCursor.Left,
                    topDown - s_rectCursor.Top,
                    left - leftDown,
                    top - topDown);

                s_mouseState = MouseState.Idle;
            }
        }
    }
}