using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WinUiAutomation.Runtime;

namespace WinUiAutomation.Studio
{
    public enum EnumUiTaskName : UInt32
    {
        UnknownUiTask = 3000,
        KeyboardInput,
        LeftClick,
        RightClick,
        LeftDblClick,
        Drag,
        MouseWheel,
        MouseHover,
        DefaultInvoke
    }

    public class RecordedUiTask
    {
        public static List<RecordedUiTask> s_listRecordedUi = new List<RecordedUiTask>();
        public static object s_lockRecordedUi = new object();

        private UiTreeNode _rootNode;
        public string _strPath = null;
        private string _strDescription = null;

        private List<UiTreeNode> _pathNodes;

        public int RecordedGapMs { get; set; }

        internal AutomationTask ToAutomationTask(bool removeName, bool removeClassName = false, bool removeAutoId = false)
        {
            AutomationTaskAction action = UiTaskName switch
            {
                EnumUiTaskName.KeyboardInput => AutomationTaskAction.KeyboardInput,
                EnumUiTaskName.LeftClick => AutomationTaskAction.LeftClick,
                EnumUiTaskName.RightClick => AutomationTaskAction.RightClick,
                EnumUiTaskName.LeftDblClick => AutomationTaskAction.LeftDoubleClick,
                EnumUiTaskName.Drag => AutomationTaskAction.Drag,
                EnumUiTaskName.MouseWheel => AutomationTaskAction.MouseWheel,
                EnumUiTaskName.MouseHover => AutomationTaskAction.MouseHover,
                EnumUiTaskName.DefaultInvoke => AutomationTaskAction.DefaultInvoke,
                _ => throw new InvalidOperationException($"Unsupported UI task: {UiTaskName}.")
            };

            return new AutomationTask
            {
                Action = action,
                NodePath = UiTaskName is EnumUiTaskName.KeyboardInput or EnumUiTaskName.MouseWheel
                    ? null
                    : GetJson(removeName, removeClassName, removeAutoId),
                KeyboardInput = Base64Text,
                CapsLock = CapsLock,
                NumLock = NumLock,
                ScrollLock = ScrollLock,
                OffsetX = LeftLocal,
                OffsetY = TopLocal,
                DeltaX = DeltaX,
                DeltaY = DeltaY,
                DelayBefore = TimeSpan.FromMilliseconds(RecordedGapMs)
            };
        }

        public RecordedUiTask(List<UiTreeNode> pathNodes, EnumUiTaskName taskName)
        {
            UiTaskName = taskName;
            _pathNodes = pathNodes;

            // Handle dynamic content like ' - selected' in Names. 
            OutlookName();

            RefreshTargetFieldsFromLastNode(true);
        }

        public RecordedUiTask(EnumUiTaskName taskName, string base64Text, bool bCapLock, bool scrollLock, bool numLock)
        {
            UiTaskName = taskName;
            Base64Text = base64Text;
            CapsLock = bCapLock;
            NumLock = numLock;
            ScrollLock = scrollLock;
        }

        private void OutlookName()
        {
            if (_pathNodes == null || _pathNodes.Count <= 1)
            {
                return;
            }

            var root = _pathNodes[1];
            var isOutlook = root != null
                && root.Tag == "Window"
                && root.ClassName != null
                && root.ClassName.Contains("Outlook");
            if (!isOutlook)
            {
                return;
            }

            foreach (var node in _pathNodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.Name))
                {
                    var nameValue = node.Name;
                    var indexUnRead = nameValue.IndexOf("Unread ");
                    if (indexUnRead >= 0)
                    {
                        nameValue = nameValue.Substring(indexUnRead + "Unread ".Length);
                      }

                    if (nameValue.Contains("Message list No conversations selected"))
                    {
                        node.Name = "";
                    }
                    else if (nameValue.Contains("Message body"))
                    { 
                        node.Name = "";
                    }
                    else if (nameValue.Length > 0 && !nameValue.StartsWith("contains:"))
                    {
                        node.Name = "contains:" + nameValue;
                    }
                }

                if (!string.IsNullOrEmpty(node.ClassName) && !node.ClassName.StartsWith("contains:"))
                {
                    var className = node.ClassName;
                    var indexFirstSpace = className.IndexOf(" ");
                    if (indexFirstSpace > 0)
                    {
                        node.ClassName = "contains:" + className.Substring(0, indexFirstSpace);
                    }
                }
            }
        }

        public static bool LeftClickToDoubleClick()
        {
            var lastClick = s_listRecordedUi?.Last();
            if (lastClick == null || lastClick.UiTaskName != EnumUiTaskName.LeftClick)
            {
                return false;
            }

            lastClick.UiTaskName = EnumUiTaskName.LeftDblClick;
            lastClick._strDescription = null;

            if (lastClick._pathNodes != null && lastClick._pathNodes.Count > 0)
            {
                var leaf = lastClick._pathNodes[lastClick._pathNodes.Count - 1];
                if (leaf != null)
                {
                    var label = string.IsNullOrEmpty(leaf.Tag) ? "Node" : leaf.Tag;
                    if (leaf.Position > 0)
                        label += "[" + leaf.Position + "]";

                    label += " " + lastClick.UiTaskName.ToString();

                    var name = !string.IsNullOrEmpty(leaf.Name) ? leaf.Name : (!string.IsNullOrEmpty(leaf.ClassName) ? leaf.ClassName : leaf.AutomationId);
                    if (!string.IsNullOrEmpty(name))
                        label += " \"" + name + "\"";

                    leaf.Title = label;
                }
            }

            MainWindow.s_mainWin?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var mainWin = MainWindow.s_mainWin;
                if (mainWin == null || mainWin.comboBoxRecordedUi.SelectedIndex < 0)
                {
                    return;
                }

                var selectedIndex = mainWin.comboBoxRecordedUi.SelectedIndex;
                mainWin.comboBoxRecordedUi.SelectedIndex = -1;
                mainWin.comboBoxRecordedUi.SelectedIndex = selectedIndex;
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);

            return true;
        }

        public static void SaveRecordedUiTask(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            System.IO.File.WriteAllText(filePath, GetSkillJson(), new UTF8Encoding(false));
        }

        public static string GetSkillJson(string name = null, bool removeName = false, bool removeClassName = false, bool removeAutoId = false)
        {
            List<AutomationTask> tasks;
            lock (s_lockRecordedUi)
            {
                tasks = s_listRecordedUi.Select(task => task.ToAutomationTask(removeName, removeClassName, removeAutoId)).ToList();
            }

            var sequence = new AutomationSequence
            {
                Name = name,
                Tasks = tasks
            };
            return AutomationSkillJson.Serialize(AutomationSkill.FromSequence(sequence));
        }

        private static void WriteNode(Utf8JsonWriter writer, UiTreeNode node, bool removeName = false, bool removeClassName = false, bool removeAutoId = false)
        {
            writer.WriteStartObject();
            writer.WriteString("controlType", node.Tag ?? string.Empty);
            writer.WriteNumber("position", node.Position);

            if (!removeAutoId && !string.IsNullOrEmpty(node.AutomationId)) writer.WriteString("automationId", node.AutomationId);
            if (!removeClassName && !string.IsNullOrEmpty(node.ClassName)) writer.WriteString("className", node.ClassName);
            if (!removeName && !string.IsNullOrEmpty(node.Name)) writer.WriteString("name", node.Name);
            if (!string.IsNullOrEmpty(node.FullDescription)) writer.WriteString("fullDescription", node.FullDescription);
            if (!string.IsNullOrEmpty(node.ExpandCollapseState)) writer.WriteString("expandCollapseState", node.ExpandCollapseState);
            if (!string.IsNullOrEmpty(node.ItemType)) writer.WriteString("itemType", node.ItemType);

            if (!string.IsNullOrEmpty(node.Left)) writer.WriteNumber("x", ParseNumOrZero(node.Left));
            if (!string.IsNullOrEmpty(node.Top)) writer.WriteNumber("y", ParseNumOrZero(node.Top));
            if (!string.IsNullOrEmpty(node.Width)) writer.WriteNumber("w", ParseNumOrZero(node.Width));
            if (!string.IsNullOrEmpty(node.Height)) writer.WriteNumber("h", ParseNumOrZero(node.Height));
            if (!string.IsNullOrEmpty(node.Lx)) writer.WriteNumber("lx", ParseNumOrZero(node.Lx));
            if (!string.IsNullOrEmpty(node.Ly)) writer.WriteNumber("ly", ParseNumOrZero(node.Ly));

            writer.WriteEndObject();
        }

        private static int ParseNumOrZero(string s)
        {
            return int.TryParse(s, out var v) ? v : 0;
        }

        public static void ReadRecordedUiTask(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            var fileText = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            var trimmed = fileText.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                LoadSkill(AutomationSkillJson.Deserialize(fileText));
                return;
            }

            lock (s_lockRecordedUi)
            {
                s_listRecordedUi.Clear();
                UiTreeNode.s_uiTreeNodes.Clear();

                // Import the unversioned JSON array and line formats written by earlier Studio versions.
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    using (var doc = JsonDocument.Parse(fileText))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                AddLoadedTask(ParseTaskElement(el));
                            }
                        }
                    }
                    return;
                }

                var lines = fileText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                        var trimmedLine = line.TrimEnd('\r', '\n');
                        if (string.IsNullOrWhiteSpace(trimmedLine))
                            continue;

                        var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
                        if (parts.Length < 1)
                            continue;

                        int uiTaskInt = 0;
                        int.TryParse(parts[0], out uiTaskInt);
                        var taskName = (EnumUiTaskName)uiTaskInt;

                        string pathNodesDecoded = parts.Length > 1 ? XmlDecode(parts[1]) : string.Empty;

                        List<UiTreeNode> pathNodes = null;
                        if (!string.IsNullOrEmpty(pathNodesDecoded))
                        {
                            var t = pathNodesDecoded.TrimStart();
                            if (t.StartsWith("["))
                            {
                                try
                                {
                                    var list = new List<UiTreeNode>();
                                    using (var doc = JsonDocument.Parse(pathNodesDecoded))
                                    {
                                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var el in doc.RootElement.EnumerateArray())
                                            {
                                                list.Add(ParseNodeObject(el));
                                            }
                                        }
                                    }
                                    pathNodes = list.Count == 0 ? null : list;
                                }
                                catch (Exception)
                                {
                                    pathNodes = SplitLegacyUnitSeparated(pathNodesDecoded);
                                }
                            }
                            else
                            {
                                pathNodes = SplitLegacyUnitSeparated(pathNodesDecoded);
                            }
                        }

                        string base64 = parts.Length > 2 ? parts[2] : null;
                        bool caps = parts.Length > 3 && bool.TryParse(parts[3], out var b1) ? b1 : false;
                        bool num = parts.Length > 4 && bool.TryParse(parts[4], out var b2) ? b2 : false;
                        bool scroll = parts.Length > 5 && bool.TryParse(parts[5], out var b3) ? b3 : false;

                        var task = new RecordedUiTask(pathNodes, taskName)
                        {
                            Base64Text = base64,
                            CapsLock = caps,
                            NumLock = num,
                            ScrollLock = scroll,
                            Tag = parts.Length > 6 ? XmlDecode(parts[6]) : null,
                            Name = parts.Length > 7 ? XmlDecode(parts[7]) : null,
                            Left = parts.Length > 8 && int.TryParse(parts[8], out var left) ? left : 0,
                            Top = parts.Length > 9 && int.TryParse(parts[9], out var top) ? top : 0,
                            LeftLocal = parts.Length > 10 && int.TryParse(parts[10], out var leftLocal) ? leftLocal : 0,
                            TopLocal = parts.Length > 11 && int.TryParse(parts[11], out var topLocal) ? topLocal : 0,
                            DeltaX = parts.Length > 12 && int.TryParse(parts[12], out var dx) ? dx : 0,
                            DeltaY = parts.Length > 13 && int.TryParse(parts[13], out var dy) ? dy : 0,
                            RecordedGapMs = parts.Length > 14 && int.TryParse(parts[14], out var gap) ? gap : 0
                        };

                    AddLoadedTask(task);
                }
            }
        }

        private static void LoadSkill(AutomationSkill skill)
        {
            AutomationSequence sequence = skill.ToSequence();
            var loadedTasks = sequence.Tasks.Select(FromAutomationTask).ToList();
            lock (s_lockRecordedUi)
            {
                s_listRecordedUi.Clear();
                UiTreeNode.s_uiTreeNodes.Clear();
                foreach (RecordedUiTask task in loadedTasks)
                {
                    AddLoadedTask(task);
                }
            }
        }

        private static RecordedUiTask FromAutomationTask(AutomationTask task)
        {
            EnumUiTaskName taskName = task.Action switch
            {
                AutomationTaskAction.KeyboardInput => EnumUiTaskName.KeyboardInput,
                AutomationTaskAction.LeftClick => EnumUiTaskName.LeftClick,
                AutomationTaskAction.RightClick => EnumUiTaskName.RightClick,
                AutomationTaskAction.LeftDoubleClick => EnumUiTaskName.LeftDblClick,
                AutomationTaskAction.Drag => EnumUiTaskName.Drag,
                AutomationTaskAction.MouseWheel => EnumUiTaskName.MouseWheel,
                AutomationTaskAction.MouseHover => EnumUiTaskName.MouseHover,
                AutomationTaskAction.DefaultInvoke => EnumUiTaskName.DefaultInvoke,
                _ => throw new InvalidOperationException($"Unsupported UI task: {task.Action}.")
            };
            List<UiTreeNode> pathNodes = null;
            if (!string.IsNullOrWhiteSpace(task.NodePath))
            {
                using var document = JsonDocument.Parse(task.NodePath);
                pathNodes = document.RootElement.EnumerateArray().Select(ParseNodeObject).ToList();
            }

            return new RecordedUiTask(pathNodes, taskName)
            {
                Base64Text = task.KeyboardInput,
                CapsLock = task.CapsLock,
                NumLock = task.NumLock,
                ScrollLock = task.ScrollLock,
                LeftLocal = task.OffsetX,
                TopLocal = task.OffsetY,
                DeltaX = task.DeltaX,
                DeltaY = task.DeltaY,
                RecordedGapMs = checked((int)task.DelayBefore.TotalMilliseconds)
            };
        }

        private static void AddLoadedTask(RecordedUiTask task)
        {
            if (task == null)
                return;

            if (s_listRecordedUi.Count == 0)
                task.RecordedGapMs = 0;

            s_listRecordedUi.Add(task);
            if (task.UiTaskName != EnumUiTaskName.KeyboardInput)
                UiTreeNode.AddToUiTree(task);
        }

        private static List<UiTreeNode> SplitLegacyUnitSeparated(string encoded)
        {
            var parts = encoded.Split(new[] { '\u001F' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var list = new List<UiTreeNode>(parts.Length);
            foreach (var s in parts)
                list.Add(ParseNodeString(s));
            return list;
        }

        private static RecordedUiTask ParseTaskElement(JsonElement el)
        {
            if (el.TryGetProperty("UiAction", out var actionEl) && actionEl.ValueKind == JsonValueKind.Object)
            {
                List<UiTreeNode> pathNodes = ExtractPathNodes(el, "TargetElement");

                EnumUiTaskName taskName = EnumUiTaskName.UnknownUiTask;
                if (actionEl.TryGetProperty("TaskName", out var pTaskName))
                {
                    if (pTaskName.ValueKind == JsonValueKind.String)
                        taskName = KeyboardEventHandler.FromStringTaskName(pTaskName.GetString());
                    else if (pTaskName.ValueKind == JsonValueKind.Number)
                        taskName = (EnumUiTaskName)pTaskName.GetInt32();
                }

                string base64 = actionEl.TryGetProperty("Base64Text", out var pBase) && pBase.ValueKind == JsonValueKind.String ? pBase.GetString() : null;
                bool caps = actionEl.TryGetProperty("CapsLock", out var pCaps) && pCaps.ValueKind == JsonValueKind.True;
                bool num = actionEl.TryGetProperty("NumLock", out var pNum) && pNum.ValueKind == JsonValueKind.True;
                bool scroll = actionEl.TryGetProperty("ScrollLock", out var pScroll) && pScroll.ValueKind == JsonValueKind.True;
                int dx = actionEl.TryGetProperty("DeltaX", out var pDx) && pDx.ValueKind == JsonValueKind.Number ? pDx.GetInt32() : 0;
                int dy = actionEl.TryGetProperty("DeltaY", out var pDy) && pDy.ValueKind == JsonValueKind.Number ? pDy.GetInt32() : 0;
                int gapMs = actionEl.TryGetProperty("RecordedGapMs", out var pGap) && pGap.ValueKind == JsonValueKind.Number ? pGap.GetInt32() : 0;

                return new RecordedUiTask(pathNodes, taskName)
                {
                    Base64Text = base64,
                    CapsLock = caps,
                    NumLock = num,
                    ScrollLock = scroll,
                    DeltaX = dx,
                    DeltaY = dy,
                    RecordedGapMs = gapMs
                };
            }

            // Older flat format: UiTaskName/pathNodes/Tag/Name/... as sibling properties
            int uiTaskInt = el.TryGetProperty("UiTaskName", out var pUi) && pUi.ValueKind == JsonValueKind.Number ? pUi.GetInt32() : (int)EnumUiTaskName.UnknownUiTask;
            var legacyTaskName = (EnumUiTaskName)uiTaskInt;

            List<UiTreeNode> legacyPathNodes = ExtractPathNodes(el, "pathNodes");

            string legacyBase64 = el.TryGetProperty("Base64Text", out var lpBase) && lpBase.ValueKind == JsonValueKind.String ? lpBase.GetString() : null;
            bool legacyCaps = el.TryGetProperty("CapsLock", out var lpCaps) && lpCaps.ValueKind == JsonValueKind.True;
            bool legacyNum = el.TryGetProperty("NumLock", out var lpNum) && lpNum.ValueKind == JsonValueKind.True;
            bool legacyScroll = el.TryGetProperty("ScrollLock", out var lpScroll) && lpScroll.ValueKind == JsonValueKind.True;
            int legacyDx = el.TryGetProperty("DeltaX", out var lpDx) && lpDx.ValueKind == JsonValueKind.Number ? lpDx.GetInt32() : 0;
            int legacyDy = el.TryGetProperty("DeltaY", out var lpDy) && lpDy.ValueKind == JsonValueKind.Number ? lpDy.GetInt32() : 0;

            return new RecordedUiTask(legacyPathNodes, legacyTaskName)
            {
                Base64Text = legacyBase64,
                CapsLock = legacyCaps,
                NumLock = legacyNum,
                ScrollLock = legacyScroll,
                DeltaX = legacyDx,
                DeltaY = legacyDy,
                RecordedGapMs = el.TryGetProperty("RecordedGapMs", out var lpGap) && lpGap.ValueKind == JsonValueKind.Number ? lpGap.GetInt32() : 0
            };
        }

        private static List<UiTreeNode> ExtractPathNodes(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var pPath) || pPath.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<UiTreeNode>();
            foreach (var pn in pPath.EnumerateArray())
            {
                if (pn.ValueKind == JsonValueKind.Object || pn.ValueKind == JsonValueKind.Array)
                    list.Add(ParseNodeObject(pn));
                else if (pn.ValueKind == JsonValueKind.String)
                    list.Add(ParseNodeString(pn.GetString()));
                else
                    list.Add(new UiTreeNode(null));
            }
            return list.Count == 0 ? null : list;
        }

        private static UiTreeNode ParseNodeObject(JsonElement el)
        {
            var node = new UiTreeNode(null);
            if (el.ValueKind != JsonValueKind.Object) return node;

            if (el.TryGetProperty("controlType", out var v) && v.ValueKind == JsonValueKind.String)
                node.Tag = v.GetString();

            if (el.TryGetProperty("position", out var p) && p.ValueKind == JsonValueKind.Number)
                node.Position = p.GetInt32();

            if (el.TryGetProperty("automationId", out v) && v.ValueKind == JsonValueKind.String)
                node.AutomationId = v.GetString();
            if (el.TryGetProperty("className", out v) && v.ValueKind == JsonValueKind.String)
                node.ClassName = v.GetString();
            if (el.TryGetProperty("name", out v) && v.ValueKind == JsonValueKind.String)
                node.Name = v.GetString();
            if (el.TryGetProperty("fullDescription", out v) && v.ValueKind == JsonValueKind.String)
                node.FullDescription = v.GetString();
            if (el.TryGetProperty("expandCollapseState", out v) && v.ValueKind == JsonValueKind.String)
                node.ExpandCollapseState = v.GetString();
            if (el.TryGetProperty("itemType", out v) && v.ValueKind == JsonValueKind.String)
                node.ItemType = v.GetString();

            if (el.TryGetProperty("x", out v)) node.Left = v.ToString();
            if (el.TryGetProperty("y", out v)) node.Top = v.ToString();
            if (el.TryGetProperty("w", out v)) node.Width = v.ToString();
            if (el.TryGetProperty("h", out v)) node.Height = v.ToString();
            if (el.TryGetProperty("lx", out v)) node.Lx = v.ToString();
            if (el.TryGetProperty("ly", out v)) node.Ly = v.ToString();

            return node;
        }

        internal static UiTreeNode ParseNodeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return new UiTreeNode(null);
            var trimmed = s.TrimStart();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    return ParseNodeObject(doc.RootElement);
                }
                catch (Exception)
                {
                    // fall through to empty node
                }
            }
            return new UiTreeNode(null);
        }

        private static string SerializePathNodes(List<UiTreeNode> nodes, bool removeName = false, bool removeClassName = false, bool removeAutoId = false)
        {
            if (nodes == null || nodes.Count == 0) return "[]";

            using (var ms = new System.IO.MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(ms))
                {
                    writer.WriteStartArray();
                    foreach (var n in nodes)
                    {
                        if (n == null) continue;
                        WriteNode(writer, n, removeName, removeClassName, removeAutoId);
                    }
                    writer.WriteEndArray();
                    writer.Flush();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private void RefreshTargetFieldsFromLastNode(bool initializeOffsets)
        {
            if (_pathNodes == null || _pathNodes.Count == 0) return;
            var leaf = _pathNodes[_pathNodes.Count - 1];
            if (leaf == null) return;

            Tag = leaf.Tag ?? Tag;
            Name = leaf.Name ?? Name;

            Left = int.TryParse(leaf.Left, out var left) ? left : 0;
            Top = int.TryParse(leaf.Top, out var top) ? top : 0;
            if (initializeOffsets)
            {
                LeftLocal = int.TryParse(leaf.Lx, out var lx) ? lx : 0;
                TopLocal = int.TryParse(leaf.Ly, out var ly) ? ly : 0;
            }
        }

        public string GetJson(bool removeName = false, bool removeClassName = false, bool removeAutoId = false)
        {
            if (this._strPath == null)
            {
                this._strPath = SerializePathNodes(_pathNodes);

                RefreshTargetFieldsFromLastNode(false);
                UiTreeNode.GenerateUiTreeNode(this, _pathNodes, ref _rootNode);
            }

            return removeName || removeClassName || removeAutoId
                ? SerializePathNodes(_pathNodes, removeName, removeClassName, removeAutoId)
                : this._strPath;
        }

        internal void RebuildUiTreeNode()
        {
            UiTreeNode.GenerateUiTreeNode(this, _pathNodes, ref _rootNode);
        }

        public string UpdateJson(string json)
        {
            try
            {
                if (!string.IsNullOrEmpty(json) && json.TrimStart().StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<UiTreeNode>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                            list.Add(ParseNodeObject(el));
                        _pathNodes = list.Count == 0 ? null : list;
                    }
                }
            }
            catch (Exception) { }

            _rootNode = null;
            _strPath = null;
            _strDescription = null;          // <-- invalidate cached description
            RefreshTargetFieldsFromLastNode(false);
            return json;
        }

        private static string XmlDecode(string s)
        {
            if (s == null) return null;
            return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("''", "'");
        }

        public UiTreeNode GetUiTreeNode()
        {
            return _rootNode;
        }

        public string Base64Text { get; set; }

        public EnumUiTaskName UiTaskName { get; set; }

        public string Tag { get; set; }

        public string Name { get; set; }

        public int Left { get; set; }

        public int Top { get; set; }

        public int LeftLocal { get; set; }

        public int TopLocal { get; set; }

        public int DeltaX { get; set; }

        public int DeltaY { get; set; }

        public bool CapsLock { get; set; }

        public bool NumLock { get; set; }

        public bool ScrollLock { get; set; }

        public string Description
        {
            get
            {
                try
                {
                    if (this._strDescription == null)
                    {
                        if (this.UiTaskName == EnumUiTaskName.KeyboardInput)
                        {
                            var keyboardTaskDescription = GenerateUiAutomationCode.GetDecodedKeyboardInput(this.Base64Text, this.CapsLock, this.NumLock, this.ScrollLock);
                            StringBuilder sb = new StringBuilder();
                            foreach (var strLine in keyboardTaskDescription)
                            {
                                sb.Append(strLine);
                            }

                            this._strDescription = $"{this.UiTaskName} VirtualKeys=\"{sb.ToString()}\" CapsLock={this.CapsLock} NumLock={this.NumLock} ScrollLock={this.ScrollLock}";
                        }
                        else
                        {
                            var leafNode = this._pathNodes.Last();
                            var leafParentNode = leafNode.Parent;

                            var nodeTitle = leafNode.Name;

                            if (nodeTitle == null || nodeTitle.Length == 0)
                                nodeTitle = leafParentNode.Name;

                            if (nodeTitle == null || nodeTitle.Length == 0)
                                nodeTitle = leafNode.ClassName;

                            if (nodeTitle.Length > 30)
                                nodeTitle = nodeTitle.Substring(0, 30);

                            // set LeftLocal and TopLocal from _pathNodes.Last()
                            if (this.UiTaskName == EnumUiTaskName.Drag || this.UiTaskName == EnumUiTaskName.MouseWheel)
                            {
                                this._strDescription = $"{this.UiTaskName} on {Tag} \"{nodeTitle}\" at ({this.LeftLocal},{this.TopLocal}), ({this.DeltaX},{this.DeltaY})";
                            }
                            else
                            {
                                this._strDescription = $"{this.UiTaskName} on {Tag} \"{nodeTitle}\" at ({this.LeftLocal},{this.TopLocal})";
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }

                if (string.IsNullOrEmpty(this._strDescription))
                {
                    this._strDescription = string.Empty;
                }
                return this._strDescription;
            }
            //set is not defined
        }

        public override string ToString()
        {
            return Description;
        }

        public void AppendKeyboardInput(string textToAppend)
        {
            byte[] data1 = Convert.FromBase64String(this.Base64Text);
            byte[] data2 = Convert.FromBase64String(textToAppend);
            byte[] data = new byte[data1.Length + data2.Length];
            data1.CopyTo(data, 0);
            data2.CopyTo(data, data1.Length);
            this.Base64Text = Convert.ToBase64String(data);

            var keyboardTaskDescription = GenerateUiAutomationCode.GetDecodedKeyboardInput(this.Base64Text, this.CapsLock, this.NumLock, this.ScrollLock);
            StringBuilder sb = new StringBuilder();
            foreach (var strLine in keyboardTaskDescription)
            {
                sb.Append(strLine);
            }

            this._strDescription = $"{this.UiTaskName} VirtualKeys=\"{sb.ToString()}\" CapsLock={this.CapsLock} NumLock={this.NumLock} ScrollLock={this.ScrollLock}";
        }

    }

    class GenerateUiAutomationCode
    {
        public static List<string> GetDecodedKeyboardInput(string strBase64, bool bCapsLock, bool bNumLock, bool bScrollLock)
        {
            byte[] data = Convert.FromBase64String(strBase64);

            int i = 0;
            bool shift = false;

            StringBuilder sb = new StringBuilder();
            List<string> lines = new List<string>();

            int nCtrlKeyDown = 0;

            while (i < data.Length / 2)
            {
                int n1 = i * 2;
                int n2 = i * 2 + 1;
                i++;

                bool bIsKeyDown = data[n1] == 0;
                VirtualKeys vk = (VirtualKeys)data[n2];

                char ch = KeyboardEventHandler.Vk2char((int)vk, shift || bCapsLock);

                if (bIsKeyDown) //Keydown
                {
                    if (char.IsControl(ch))
                    {
                        nCtrlKeyDown++;

                        if (nCtrlKeyDown == 1 && sb.Length > 0)
                        {
                            lines.Add("\"" + sb.ToString() + "\"");
                            sb.Clear();
                        }

                        string vkStr = vk.ToString();
                        string vkSendKey = KeyboardEventHandler.Vk2string(vkStr);
                        if (nCtrlKeyDown == 1)
                            sb.Append("Keys." + vkSendKey);
                        else
                            sb.Append(" + Keys." + vkSendKey);
                    }
                    else if (ch != 0)
                    {
                        string strToAppend = ch.ToString();
                        if (ch == '\\')
                        {
                            strToAppend += "\\";
                        }

                        if (nCtrlKeyDown > 0)
                        {
                            sb.Append(" + \"" + strToAppend + "\"");
                        }
                        else
                        {
                            sb.Append(strToAppend);
                        }
                    }
                }
                else //Keyup
                {
                    if (char.IsControl(ch))
                    {
                        nCtrlKeyDown--;

                        string vkStr = vk.ToString();
                        string vkSendKey = KeyboardEventHandler.Vk2string(vkStr);

                        if (nCtrlKeyDown == 0)
                        {
                            lines.Add(sb.ToString() + " + Keys." + vkSendKey);
                            sb.Clear();
                        }
                        else
                        {
                            sb.Append(" + Keys." + vkSendKey);
                        }
                    }
                }
            }

            if (sb.Length > 0)
            {
                lines.Add("\"" + sb.ToString() + "\"");
            }

            return lines;
        }
    }
}