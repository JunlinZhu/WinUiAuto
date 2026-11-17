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
            s_playbackCancellation?.Dispose();
            s_playbackCancellation = new CancellationTokenSource();
            s_threadVerify = new Thread(PlayRecordedUiTasks);
            s_threadVerify.IsBackground = true;
            s_threadVerify.SetApartmentState(ApartmentState.STA);
            s_threadVerify.Start();
        }

        public static void StopVerifyThread()
        {
            s_playbackCancellation?.Cancel();
            if (s_threadVerify == null)
            {
                s_playbackCancellation?.Dispose();
                s_playbackCancellation = null;
                MainWindow.FinishedPlaying();
                return;
            }

            s_threadVerify.Join(1000);
            if (s_threadVerify != null && s_threadVerify.IsAlive)
            {
                s_threadVerify.Interrupt();
                s_threadVerify.Join(500);
            }

            s_threadVerify = null;
            s_playbackCancellation?.Dispose();
            s_playbackCancellation = null;
            MainWindow.FinishedPlaying();
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

        public static void PlayRecordedUiTasks()
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
                playback.Play(tasks, progress: progress, cancellationToken: s_playbackCancellation?.Token ?? CancellationToken.None);
            }
            catch (ThreadInterruptedException)
            {
                // StopVerifyThread uses Interrupt only as a last-resort cancellation signal.
            }
            finally
            {
                MainWindow.FinishedPlaying();
                s_threadVerify = null;
                s_playbackCancellation?.Dispose();
                s_playbackCancellation = null;
            }
        }
    }
}