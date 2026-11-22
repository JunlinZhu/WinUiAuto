using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WinUiAutomation.Runtime;

namespace WinUiAutomation.Studio
{
    public class VerifyNodePath
    {
        static Thread s_threadVerify = null;
        static CancellationTokenSource s_playbackCancellation;

        static string s_strPathToVerify;

        public static void StartVerifyThread()
        {
            if (s_threadVerify?.IsAlive == true)
            {
                return;
            }

            s_playbackCancellation = new CancellationTokenSource();
            CancellationTokenSource playbackCancellation = s_playbackCancellation;
            s_threadVerify = new Thread(() => PlayRecordedUiTasks(playbackCancellation));
            s_threadVerify.IsBackground = true;
            s_threadVerify.SetApartmentState(ApartmentState.STA);
            s_threadVerify.Start();
        }

        public static void StopVerifyThread()
        {
            s_playbackCancellation?.Cancel();
        }

        public static void DoVerify(string nodePath)
        {
            s_strPathToVerify = NormalizeNodePathInput(nodePath);
            if (string.IsNullOrEmpty(s_strPathToVerify))
            {
                return;
            }

            var threadVerify = new Thread(DoVerifyThread);
            threadVerify.IsBackground = true;
            threadVerify.SetApartmentState(ApartmentState.STA);
            threadVerify.Start();
        }

        private static string NormalizeNodePathInput(string nodePath)
        {
            return string.IsNullOrWhiteSpace(nodePath) ? null : nodePath.Trim();
        }

        private static void DoVerifyThread()
        {
            if (string.IsNullOrEmpty(s_strPathToVerify))
            {
                return;
            }

            RECT rect = new RECT();
            var elementState = new StringBuilder(NativeMethods.BUFFERSIZE);
            UiAutoLib.LocateUiByPath(s_strPathToVerify, true, ref rect, elementState, elementState.Capacity);
            if (rect.Right > rect.Left)
            {
                UiAutoLib.FlashRect(ref rect, 0, 255, 0, 5000, 5);
            }
            else
            {
            }
        }

        private static void PlayRecordedUiTasks(CancellationTokenSource playbackCancellation)
        {
            try
            {
                List<RecordedUiTask> recordedTasks;
                lock (RecordedUiTask.s_lockRecordedUi)
                {
                    recordedTasks = new List<RecordedUiTask>(RecordedUiTask.s_listRecordedUi);
                }

                var tasks = new List<AutomationTask>(recordedTasks.Count);
                foreach (RecordedUiTask task in recordedTasks)
                {
                    tasks.Add(task.ToAutomationTask(MainWindow.s_removeName, MainWindow.s_removeClassName, MainWindow.s_removeAutoId));
                }

                var progress = new Progress<PlaybackProgress>(update => MainWindow.ScrollToTask(recordedTasks[update.Index]));
                var playback = new AutomationPlayback(UiAutoLib.Automation);
                playback.Play(tasks, progress: progress, cancellationToken: playbackCancellation.Token);
            }
            finally
            {
                MainWindow.FinishedPlaying();
                if (ReferenceEquals(s_playbackCancellation, playbackCancellation))
                {
                    s_threadVerify = null;
                    s_playbackCancellation = null;
                }
                playbackCancellation.Dispose();
            }
        }
    }
}