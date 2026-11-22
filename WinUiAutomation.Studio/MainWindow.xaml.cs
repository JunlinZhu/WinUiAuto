using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace WinUiAutomation.Studio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow s_mainWin = null;
        public static IntPtr s_windowHandle;
        public static bool s_removeName = false;
        public static bool s_removeClassName = false;
        public static bool s_removeAutoId = false;
        RecordedUiTask selectedTreeViewItem;

        System.Timers.Timer timer = null;
        private bool _isUpdatingSelection = false;
        public static string s_walkDirection = "Up";

        enum UiThreadTask { UpdateTreeView = 3001 };
        public enum UiSelectChangedSource { ComboBoxSelected, TreeViewItemSelected, SelectChangeFinished };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UiAutoLib.Init();

            s_windowHandle = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(s_windowHandle).AddHook(RunOnUiThread);

            s_mainWin = this;
            this.comboBoxRecordedUi.ItemsSource = RecordedUiTask.s_listRecordedUi;
            this.treeUiPath.ItemsSource = UiTreeNode.s_uiTreeNodes;

            KeyboardEventHandler.InitVk2StringMap();
        }

        public static IntPtr RunOnUiThread(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;

            if (msg == (int)UiThreadTask.UpdateTreeView)
            {
                if (s_mainWin.SelectChangedSource == UiSelectChangedSource.SelectChangeFinished)
                {
                    bool shouldRefresh = false;
                    lock (RecordedUiTask.s_lockRecordedUi)
                    {
                        if (RecordedUiTask.s_listRecordedUi.Count > 0)
                        {
                            shouldRefresh = true;
                        }
                    }

                    if (shouldRefresh)
                    {
                        // Perform UI refresh operations safely outside of the domain lock scope
                        s_mainWin.comboBoxRecordedUi.ItemsSource = null;
                        s_mainWin.comboBoxRecordedUi.ItemsSource = RecordedUiTask.s_listRecordedUi;
                        s_mainWin.comboBoxRecordedUi.SelectedIndex = RecordedUiTask.s_listRecordedUi.Count - 1;
                        s_mainWin.comboBoxRecordedUi.Items.Refresh();
                        s_mainWin.treeUiPath.Items.Refresh();
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                VerifyNodePath.StopVerifyThread();
                MouseEventHandler.UnInit();
                UiAutoLib.UnInit();
                s_mainWin = null;

                if (s_windowHandle != IntPtr.Zero)
                {
                    System.Windows.Interop.HwndSource.FromHwnd(s_windowHandle)?.RemoveHook(RunOnUiThread);
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        public bool IsRecording { get; set; }

        public UiSelectChangedSource SelectChangedSource { get; set; }

        private void SetUiInteractionEnabled(bool enabled)
        {
            btnWalkUp.IsEnabled = enabled;
            btnWalkDown.IsEnabled = enabled;
            btnClear.IsEnabled = enabled;
            checkboxRemoveName.IsEnabled = enabled;
            checkboxRemoveClassName.IsEnabled = enabled;
            checkboxRemoveAutoId.IsEnabled = enabled;
            btnSkillJson.IsEnabled = enabled;
            btnDelete.IsEnabled = enabled;
            btnGenerateCode.IsEnabled = enabled;
            treeUiPath.IsEnabled = enabled;
            comboBoxRecordedUi.IsEnabled = enabled;
            rtbNodePath.IsEnabled = enabled;
            textBoxCode.IsEnabled = enabled;
            btnVerify.IsEnabled = enabled;
            btnRecord.IsEnabled = enabled;
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            SelectChangedSource = UiSelectChangedSource.SelectChangeFinished;

            if (btnRecord.IsChecked == true)
            {
                BeginRecordingTiming();
                MouseEventHandler.Init();
                btnRecord.Content = "Stop Recording";
                IsRecording = true;
                SetUiInteractionEnabled(false);
                btnRecord.IsEnabled = true;
            }
            else if (btnRecord.IsChecked == false)
            {
                MainWindow.s_mainWin.IsRecording = false;
                MouseEventHandler.UnInit();
                btnRecord.Content = "Record";
                SetUiInteractionEnabled(true);
            }
        }

        private static DateTime s_lastRecordedTaskTimeUtc = DateTime.MinValue;

        private static void BeginRecordingTiming()
        {
            lock (RecordedUiTask.s_lockRecordedUi)
            {
                s_lastRecordedTaskTimeUtc = DateTime.UtcNow;
            }
        }

        public static void AddRecordedUi(RecordedUiTask uiTask)
        {
            lock (RecordedUiTask.s_lockRecordedUi)
            {
                DateTime now = DateTime.UtcNow;
                uiTask.RecordedGapMs = s_lastRecordedTaskTimeUtc == DateTime.MinValue
                    ? 0
                    : (int)Math.Min(int.MaxValue, Math.Max(0, (now - s_lastRecordedTaskTimeUtc).TotalMilliseconds));
                s_lastRecordedTaskTimeUtc = now;

                if (RecordedUiTask.s_listRecordedUi.Count == 0)
                {
                    uiTask.RecordedGapMs = 0;
                }

                RecordedUiTask.s_listRecordedUi.Add(uiTask);

                if (uiTask.UiTaskName != EnumUiTaskName.KeyboardInput)
                {
                    UiTreeNode.AddToUiTree(uiTask);
                }
            }

            NativeMethods.PostMessage(MainWindow.s_windowHandle, (int)UiThreadTask.UpdateTreeView, 0, 0);
        }

        private void comboBoxRecordedUi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            var selectedItem = (comboBoxRecordedUi.SelectedItem as RecordedUiTask);
            if (selectedItem == null)
            {
                return;
            }

            try
            {
                _isUpdatingSelection = true;
                SelectChangedSource = UiSelectChangedSource.ComboBoxSelected;

                RefreshSkillJsonPreview();

                if (selectedItem.UiTaskName != EnumUiTaskName.KeyboardInput)
                {
                    rtbNodePath.Text = selectedItem.GetJson();
                    SelectTaskInTree(selectedItem);
                }
            }
            finally
            {
                _isUpdatingSelection = false;
                SelectChangedSource = UiSelectChangedSource.SelectChangeFinished;
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            lock (RecordedUiTask.s_lockRecordedUi)
            {
                RecordedUiTask.s_listRecordedUi.Clear();
                UiTreeNode.s_uiTreeNodes.Clear();
            }

            this.comboBoxRecordedUi.Items.Refresh();
            treeUiPath.Items.Refresh();

            rtbNodePath.Text = "";
            textBoxCode.Text = "";
        }

        private void GenerateSkillJson(object sender, RoutedEventArgs e)
        {
            lock (RecordedUiTask.s_lockRecordedUi)
            {
                if (RecordedUiTask.s_listRecordedUi.Count == 0)
                {
                    return;
                }

                if (btnRecord.IsChecked == true)
                {
                    btnRecord_Click(null, null);
                }

            }

            string skillJson = RecordedUiTask.GetSkillJson(removeName: s_removeName, removeClassName: s_removeClassName, removeAutoId: s_removeAutoId);
            Clipboard.SetText(skillJson);
            textBoxCode.Text = skillJson;

            this.Dispatcher.Invoke(new Action(() =>
            {
                SetButtonToolTipText(btnSkillJson, "Versioned skill JSON copied to clipboard");
                ((ToolTip)btnSkillJson.ToolTip).IsOpen = true;

                timer = new System.Timers.Timer(2000) { Enabled = true, AutoReset = false };
                timer.Elapsed += ResetToolTip;
                timer.Start();
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void ResetToolTip(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= ResetToolTip;
                timer = null;
            }

            this.Dispatcher.Invoke(new Action(() =>
            {
                ((ToolTip)btnSkillJson.ToolTip).IsOpen = false;
                SetButtonToolTipText(btnSkillJson, "Copy versioned skill JSON to Clipboard");
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private static void SetButtonToolTipText(System.Windows.Controls.Button button, string text)
        {
            if (button?.ToolTip is ToolTip toolTip && toolTip.Content is TextBlock textBlock)
            {
                textBlock.Text = text;
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (comboBoxRecordedUi.Items == null
                || comboBoxRecordedUi.Items.Count == 0
                || comboBoxRecordedUi.SelectedItem == null)
            {
                return;
            }

            lock (RecordedUiTask.s_lockRecordedUi)
            {
                var selectedIndex = comboBoxRecordedUi.SelectedIndex;
                var uiTask = (RecordedUiTask)comboBoxRecordedUi.SelectedItem;
                if (uiTask == null)
                {
                    return;
                }

                // Remove tree node mapping (applies to non-keyboard tasks)
                if (uiTask.UiTaskName != EnumUiTaskName.KeyboardInput)
                {
                    UiTreeNode.RemoveUiTreeNode(uiTask);
                }

                // Remove task from list
                RecordedUiTask.s_listRecordedUi.Remove(uiTask);

                if (RecordedUiTask.s_listRecordedUi.Count > 0)
                {
                    RecordedUiTask.s_listRecordedUi[0].RecordedGapMs = 0;
                    comboBoxRecordedUi.SelectedIndex = selectedIndex < RecordedUiTask.s_listRecordedUi.Count ? selectedIndex : RecordedUiTask.s_listRecordedUi.Count - 1;
                    comboBoxRecordedUi.SelectedValue = RecordedUiTask.s_listRecordedUi[comboBoxRecordedUi.SelectedIndex];
                }
                else
                {
                    rtbNodePath.Text = "";
                    textBoxCode.Text = "";
                    comboBoxRecordedUi.SelectedItem = null;
                }

                comboBoxRecordedUi.Items.Refresh();
                treeUiPath.Items.Refresh();
            }
        }

        private void HighlightPath()
        {
            var selectedItem = treeUiPath.SelectedItem as UiTreeNode;
            if (selectedItem == null)
            {
                return;
            }

            var path = BuildNodePath(selectedItem);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            RECT rect = new RECT();
            var elementState = new StringBuilder(NativeMethods.BUFFERSIZE);
            UiAutoLib.LocateUiByPath(path, true, ref rect, elementState, elementState.Capacity);
        }

        private RecordedUiTask GetExpandedLeafNode(TreeViewItem tvi, UiTreeNode node, RecordedUiTask recordedUiTask)
        {
            if (tvi == null || node == null)
            {
                return null;
            }

            if (recordedUiTask != null)
            {
                if (node.UiTask == recordedUiTask)
                {
                    return node.UiTask;
                }
            }
            else if (node.Items.Count == 0)
            {
                return node.UiTask;
            }

            foreach (var c in node.Items)
            {
                var subContainer = (TreeViewItem)tvi.ItemContainerGenerator.ContainerFromItem(c);
                if (subContainer != null)
                {
                    if (subContainer.IsExpanded)
                    {
                        var retTemp = GetExpandedLeafNode(subContainer, c, recordedUiTask);
                        if (retTemp != null)
                        {
                            return retTemp;
                        }
                    }
                }
            }

            return null;
        }

        private void SelectTaskInTree(RecordedUiTask task)
        {
            if (task == null)
            {
                return;
            }

            treeUiPath.UpdateLayout();
            foreach (var root in UiTreeNode.s_uiTreeNodes)
            {
                var rootContainer = treeUiPath.ItemContainerGenerator.ContainerFromItem(root) as TreeViewItem;
                var selectedContainer = ExpandSelectedTreeNode(root, rootContainer, task);
                if (selectedContainer != null)
                {
                    selectedContainer.IsSelected = true;
                    selectedContainer.BringIntoView();
                    selectedTreeViewItem = task;
                    return;
                }
            }
        }

        private TreeViewItem UiTreeNode2TreeViewItem(TreeViewItem tvi, UiTreeNode node, UiTreeNode nodeTarget)
        {
            if (tvi == null || node == null)
            {
                return null;
            }

            if (node == nodeTarget)
            {
                return tvi;
            }

            foreach (var c in node.Items)
            {
                var subContainer = (TreeViewItem)tvi.ItemContainerGenerator.ContainerFromItem(c);
                if (subContainer != null)
                {
                    var ret = UiTreeNode2TreeViewItem(subContainer, c, nodeTarget);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }

            return null;
        }

        internal static string BuildNodePath(UiTreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            var nodes = new System.Collections.Generic.List<UiTreeNode>();
            while (node != null)
            {
                nodes.Insert(0, node);
                node = node.Parent;
            }

            if (nodes.Count == 0)
            {
                return null;
            }

            return System.Text.Json.JsonSerializer.Serialize(nodes);
        }

        private void treeUiPath_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isUpdatingSelection) return;

            var selectedItem = treeUiPath.SelectedItem as UiTreeNode;
            if (selectedItem == null)
            {
                return;
            }

            try
            {
                _isUpdatingSelection = true;
                SelectChangedSource = UiSelectChangedSource.TreeViewItemSelected;

                RecordedUiTask leafUiTask = selectedItem.UiTask;

                if (leafUiTask == null && treeUiPath.Items.Count > 0 && UiTreeNode.s_uiTreeNodes.Count > 0)
                {
                    foreach (var root in UiTreeNode.s_uiTreeNodes)
                    {
                        var rootContainer = treeUiPath.ItemContainerGenerator.ContainerFromItem(root) as TreeViewItem;
                        TreeViewItem tvi = UiTreeNode2TreeViewItem(rootContainer, root, selectedItem);
                        if (tvi != null)
                        {
                            leafUiTask = GetExpandedLeafNode(tvi, selectedItem, selectedTreeViewItem);
                            if (leafUiTask == null)
                            {
                                leafUiTask = GetExpandedLeafNode(tvi, selectedItem, null);
                                selectedTreeViewItem = leafUiTask;
                            }

                            break;
                        }
                    }
                }

                if (leafUiTask == null)
                {
                    return;
                }
                else
                {
                    selectedTreeViewItem = leafUiTask;
                }

                if (leafUiTask != comboBoxRecordedUi.SelectedItem)
                {
                    comboBoxRecordedUi.SelectedItem = leafUiTask;
                }

                if (selectedItem.Items.Count > 0)
                {
                    treeUiPath.ContextMenu = treeUiPath.Resources["UiNode"] as System.Windows.Controls.ContextMenu;
                }
                else
                {
                    treeUiPath.ContextMenu = treeUiPath.Resources["LeafNode"] as System.Windows.Controls.ContextMenu;
                }

                HighlightPath();
            }
            finally
            {
                _isUpdatingSelection = false;
                SelectChangedSource = UiSelectChangedSource.SelectChangeFinished;
            }
        }

        private TreeViewItem ExpandSelectedTreeNode(UiTreeNode uiTreeNode, TreeViewItem tvi, RecordedUiTask recordedUiTask)
        {
            if (tvi == null || uiTreeNode == null)
            {
                return null;
            }

            if (uiTreeNode.Items.Count == 0)
            {
                if (uiTreeNode.UiTask == recordedUiTask)
                {
                    tvi.IsSelected = true;
                    return tvi;
                }
                else
                {
                    return null;
                }
            }

            foreach (var c in uiTreeNode.Items)
            {
                var subContainer = (TreeViewItem)tvi.ItemContainerGenerator.ContainerFromItem(c);
                if (subContainer != null)
                {
                    subContainer.IsExpanded = false;
                }
            }

            foreach (var c in uiTreeNode.Items)
            {
                var subContainer = (TreeViewItem)tvi.ItemContainerGenerator.ContainerFromItem(c);
                if (subContainer != null)
                {
                    var ret = ExpandSelectedTreeNode(c, subContainer, recordedUiTask);
                    if (ret != null)
                    {
                        subContainer.IsExpanded = true;
                        return ret;
                    }
                }
            }

            return null;
        }

        private void Highlight_Click(object sender, RoutedEventArgs e)
        {
            HighlightPath();
        }

        private void MenuRemoveNode_Click(object sender, RoutedEventArgs e)
        {
            var uiTreeNode = treeUiPath.SelectedItem as UiTreeNode;
            if (uiTreeNode == null || uiTreeNode.UiTask == null)
            {
                // Optional: if you want to support removing non-leaf nodes,
                // handle that case here. Otherwise, ensure the menu item is
                // disabled/hidden for non-leaf nodes.
                return;
            }

            var leafUiTask = uiTreeNode.UiTask;

            lock (RecordedUiTask.s_lockRecordedUi)
            {
                var currentJson = leafUiTask.GetJson();
                if (string.IsNullOrWhiteSpace(currentJson))
                    return;

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(currentJson);
                    if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array ||
                        doc.RootElement.GetArrayLength() <= 1)
                        return;

                    // Build a new path without the last node (the leaf/target)
                    var trimmedNodes = new System.Collections.Generic.List<UiTreeNode>();
                    int index = 0;
                    int lastIndex = doc.RootElement.GetArrayLength() - 1;

                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        if (index++ == lastIndex)
                            break;

                        trimmedNodes.Add(ParseUiTreeNode(element));
                    }

                    string newJson = System.Text.Json.JsonSerializer.Serialize(trimmedNodes);

                    // Update the task model and force a rebuild of its internal tree
                    leafUiTask.UpdateJson(newJson);
                    rtbNodePath.Text = leafUiTask.GetJson();   // rebuilds _rootNode & returns canonical JSON

                    // Reset every cached task chain before merging. AddToUiTree links
                    // cached chains together, so clearing only the root list can retain
                    // nodes from the previous merged tree.
                    UiTreeNode.s_uiTreeNodes.Clear();
                    foreach (var item in RecordedUiTask.s_listRecordedUi)
                    {
                        if (item.UiTaskName != EnumUiTaskName.KeyboardInput)
                        {
                            item.RebuildUiTreeNode();
                            UiTreeNode.AddToUiTree(item);
                        }
                    }

                    // Refresh UI
                    comboBoxRecordedUi.Items.Refresh();
                    treeUiPath.Items.Refresh();

                    // Refresh code pane if this task is currently selected
                    if (comboBoxRecordedUi.SelectedItem == leafUiTask)
                    {
                        RefreshSkillJsonPreview();
                        SelectTaskInTree(leafUiTask);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to trim JSON node path: {ex.Message}");
                }
            }
        }

        private static UiTreeNode ParseUiTreeNode(System.Text.Json.JsonElement el)
        {
            var node = new UiTreeNode(null);
            if (el.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return node;
            }

            System.Text.Json.JsonElement value;
            if (el.TryGetProperty("controlType", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.Tag = value.GetString();

            if (el.TryGetProperty("position", out value) && value.ValueKind == System.Text.Json.JsonValueKind.Number)
                node.Position = value.GetInt32();

            if (el.TryGetProperty("automationId", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.AutomationId = value.GetString();
            if (el.TryGetProperty("className", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.ClassName = value.GetString();
            if (el.TryGetProperty("name", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.Name = value.GetString();
            if (el.TryGetProperty("fullDescription", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.FullDescription = value.GetString();
            if (el.TryGetProperty("expandCollapseState", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.ExpandCollapseState = value.GetString();
            if (el.TryGetProperty("itemType", out value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                node.ItemType = value.GetString();

            if (el.TryGetProperty("x", out value)) node.Left = value.ToString();
            if (el.TryGetProperty("y", out value)) node.Top = value.ToString();
            if (el.TryGetProperty("w", out value)) node.Width = value.ToString();
            if (el.TryGetProperty("h", out value)) node.Height = value.ToString();
            if (el.TryGetProperty("lx", out value)) node.Lx = value.ToString();
            if (el.TryGetProperty("ly", out value)) node.Ly = value.ToString();

            return node;
        }
        private void comboBoxRecordedUi_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(comboBoxRecordedUi.Text))
                comboBoxRecordedUi_ToolTip.Text = comboBoxRecordedUi.Text;
        }

        private void textBoxCode_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBoxCode.Text))
                textBoxCode_ToolTip.Text = textBoxCode.Text;
        }

        public static void FinishedPlaying()
        {
            if (s_mainWin == null)
            {
                return;
            }

            s_mainWin.Dispatcher.BeginInvoke(new Action(() =>
            {
                s_mainWin.btnVerify.Content = "Play";
                s_mainWin.SetUiInteractionEnabled(true);
                s_mainWin.btnVerify.IsEnabled = true;

            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            if (this.btnVerify.Content.ToString() == "Stop Playing")
            {
                VerifyNodePath.StopVerifyThread();
            }
            else
            {
                VerifyNodePath.StartVerifyThread();
                this.btnVerify.Content = "Stop Playing";
                SetUiInteractionEnabled(false);
                btnVerify.IsEnabled = true;
            }
        }

        private void MenuVerify(object sender, RoutedEventArgs e)
        {
            VerifyNodePath.DoVerify(rtbNodePath.Text.Trim());
        }

        private void MenuVerify5Sec(object sender, RoutedEventArgs e)
        {
            System.Threading.Thread.Sleep(5000);
            MenuVerify(null, null);
        }

        public static void ScrollToTask(RecordedUiTask uiTask)
        {
            s_mainWin.Dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0; i < s_mainWin.comboBoxRecordedUi.Items.Count; i++)
                {
                    if (s_mainWin.comboBoxRecordedUi.Items[i] == uiTask)
                    {
                        s_mainWin.comboBoxRecordedUi.SelectedIndex = i;
                        break;
                    }
                }

            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void btnSkillJson_Click(object sender, RoutedEventArgs e)
        {
            GenerateSkillJson(sender, e);
        }

        private void btnGenerateCsCode_Click(object sender, RoutedEventArgs e)
        {
            GenerateCSCode(sender, e);
        }

        internal static string IndentCodeBlock(string code, string indent)
        {
            if (string.IsNullOrEmpty(code)) return code;

            var lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    lines[i] = indent + lines[i];
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        private void GenerateCSCode(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            lock (RecordedUiTask.s_lockRecordedUi)
            {
                if (RecordedUiTask.s_listRecordedUi.Count == 0)
                {
                    return;
                }

                if (btnRecord.IsChecked == true)
                {
                    btnRecord_Click(null, null);
                }

                int nOrder = 0;
                int nOrderFocused = 0;
                foreach (var uiTask in RecordedUiTask.s_listRecordedUi)
                {
                    if (uiTask.UiTaskName == EnumUiTaskName.LeftClick)
                    {
                        nOrderFocused = nOrder;
                    }

                    if (uiTask == RecordedUiTask.s_listRecordedUi.First())
                    {
                        sb.AppendLine("internal static void RunGeneratedAutomation(IGeneratedAutomation automation, Action<int>? sleep = null)");
                        sb.AppendLine("{");
                        sb.AppendLine("\tsleep ??= Thread.Sleep;");
                        sb.AppendLine();
                    }

                    sb.AppendLine(IndentCodeBlock(uiTask.CreateCSCode(nOrder++, nOrderFocused), "\t"));

                    if (uiTask == RecordedUiTask.s_listRecordedUi.Last())
                    {
                        sb.AppendLine("}");
                    }
                }
            }

            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());

                this.Dispatcher.Invoke(new Action(() =>
                {
                    SetButtonToolTipText(btnGenerateCode, "Generated WinUiAutomation code copied to clipboard");
                    ((ToolTip)btnGenerateCode.ToolTip).IsOpen = true;

                    timer = new System.Timers.Timer(2000) { Enabled = true, AutoReset = false };
                    timer.Elapsed += ResetToolTip;
                    timer.Start();
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private void rtbNodePath_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var tr = rtbNodePath.Text;
            if (tr.Trim().Length > 0)
            {
                var sfd = new SaveFileDialog()
                {
                    Filter = "Windows UI Skill (*.winui-skill.json)|*.winui-skill.json|JSON files (*.json)|*.json|Legacy Recorded UI (*.uia)|*.uia|All files (*.*)|*.*",
                    DefaultExt = "winui-skill.json",
                    AddExtension = true,
                    FileName = "recording.winui-skill.json"
                };

                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        RecordedUiTask.SaveRecordedUiTask(sfd.FileName);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(this, exception.Message, "Unable to save skill", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                var ofd = new OpenFileDialog()
                {
                    Filter = "Windows UI Skill (*.winui-skill.json;*.json)|*.winui-skill.json;*.json|Legacy Recorded UI (*.uia)|*.uia|All files (*.*)|*.*",
                    DefaultExt = "winui-skill.json",
                    Multiselect = false
                };

                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        RecordedUiTask.ReadRecordedUiTask(ofd.FileName);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(this, exception.Message, "Unable to load skill", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    this.comboBoxRecordedUi.Items.Refresh();
                    this.treeUiPath.Items.Refresh();

                    if (RecordedUiTask.s_listRecordedUi.Count > 0)
                    {
                        this.comboBoxRecordedUi.SelectedIndex = RecordedUiTask.s_listRecordedUi.Count - 1;
                        var last = RecordedUiTask.s_listRecordedUi[this.comboBoxRecordedUi.SelectedIndex];

                        rtbNodePath.Text = last.GetJson(MainWindow.s_removeName, MainWindow.s_removeClassName, MainWindow.s_removeAutoId);
                        RefreshSkillJsonPreview();
                    }
                    else
                    {
                        rtbNodePath.Text = string.Empty;
                        textBoxCode.Text = string.Empty;
                    }
                }
            }
        }

        private void checkboxRemoveName_Click(object sender, RoutedEventArgs e)
        {
            s_removeName = checkboxRemoveName.IsChecked == true;
            RefreshSkillJsonPreview();
        }

        private void checkboxRemoveClassName_Click(object sender, RoutedEventArgs e)
        {
            s_removeClassName = checkboxRemoveClassName.IsChecked == true;
            RefreshSkillJsonPreview();
        }

        private void checkboxRemoveAutoId_Click(object sender, RoutedEventArgs e)
        {
            s_removeAutoId = checkboxRemoveAutoId.IsChecked == true;
            RefreshSkillJsonPreview();
        }

        private void RefreshSkillJsonPreview()
        {
            textBoxCode.Text = RecordedUiTask.s_listRecordedUi.Count == 0
                ? string.Empty
                : RecordedUiTask.GetSkillJson(removeName: s_removeName, removeClassName: s_removeClassName, removeAutoId: s_removeAutoId);
        }

        private void rbtnUpDown_Click(object sender, RoutedEventArgs e)
        {
            s_walkDirection = btnWalkDown.IsChecked == true ? "Down" : "Up";
        }
    }
}