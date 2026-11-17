using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace WinUiAutomation.Studio
{
    // Serializes UiTreeNode to the JSON descriptor shape the C++ side expects.
    // Emits only fields that carry a value; omits null and empty strings.
    // controlType and position are always emitted (position is 0 for the root
    // and for any node that had no sibling index), matching what
    // RecordedUiTask.WriteNode produces -- so a path built here is
    // interchangeable with one built from _pathNodes.
    public sealed class UiTreeNodeJsonConverter : JsonConverter<UiTreeNode>
    {
        public override UiTreeNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Not needed for CreateNodePath. If you ever want to deserialize
            // back into UiTreeNode, mirror ParseNodeObject from RecordedUiTask.
            throw new NotSupportedException("UiTreeNode is read via RecordedUiTask.ParseNodeObject.");
        }

        // System.Text.Json.JsonSerializer.Serialize(nodes) calls this for each node in the array,
        // so we don't need to write the array start/end here.
        public override void Write(Utf8JsonWriter writer, UiTreeNode node, JsonSerializerOptions options)
        {
            if (node == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            // Always present (matches WriteNode)
            writer.WriteString("controlType", node.Tag ?? string.Empty);
            writer.WriteNumber("position", node.Position);

            // Omitted when null or empty
            if (!string.IsNullOrEmpty(node.AutomationId)) writer.WriteString("automationId", node.AutomationId);
            if (!string.IsNullOrEmpty(node.ClassName)) writer.WriteString("className", node.ClassName);
            if (!string.IsNullOrEmpty(node.Name)) writer.WriteString("name", node.Name);
            if (!string.IsNullOrEmpty(node.FullDescription)) writer.WriteString("fullDescription", node.FullDescription);
            if (!string.IsNullOrEmpty(node.ExpandCollapseState)) writer.WriteString("expandCollapseState", node.ExpandCollapseState);
            if (!string.IsNullOrEmpty(node.ItemType)) writer.WriteString("itemType", node.ItemType);

            // Leaf-only geometry. Emitted as numbers, matching WriteNode.
            WriteNumberIfPresent(writer, "x", node.Left);
            WriteNumberIfPresent(writer, "y", node.Top);
            WriteNumberIfPresent(writer, "w", node.Width);
            WriteNumberIfPresent(writer, "h", node.Height);
            WriteNumberIfPresent(writer, "lx", node.Lx);
            WriteNumberIfPresent(writer, "ly", node.Ly);

            writer.WriteEndObject();
        }

        private static void WriteNumberIfPresent(Utf8JsonWriter writer, string name, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (int.TryParse(raw, out var v)) writer.WriteNumber(name, v);
            // Non-numeric values are dropped: the C++ side parses these as
            // numbers, so writing a quoted string would fail there anyway.
        }
    }

    [JsonConverter(typeof(UiTreeNodeJsonConverter))]
    public class UiTreeNode
    {
        public static List<UiTreeNode> s_uiTreeNodes = new List<UiTreeNode>();

        public enum CompareMethod { Equal, StartsWith, Contains };

        public UiTreeNode(UiTreeNode pNode)
        {
            Parent = pNode;
            this.Items = new List<UiTreeNode>();
        }

        [JsonIgnore]
        public UiTreeNode Parent { get; set; }
        public string Tag { get; set; }
        [JsonIgnore]
        public string Title { get; set; }
        public string ClassName { get; set; }
        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string Left { get; set; }
        public string Top { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        [JsonIgnore]
        public string RuntimeId { get; set; }
        [JsonIgnore]
        public string FullDescription { get; set; }
        public string ExpandCollapseState { get; set; }
        [JsonIgnore]
        public RecordedUiTask UiTask { get; set; }
        [JsonIgnore]
        public List<UiTreeNode> Items { get; set; }
        [JsonIgnore]
        public CompareMethod NameCompareMethod { get; set; }
        [JsonIgnore]
        public CompareMethod ClassNameCompareMethod { get; set; }
        [JsonIgnore]
        public CompareMethod AutomationIdCompareMethod { get; set; }

        public int Position { get; set; }   // sibling index; 0 = not applicable
        public string ItemType { get; set; }   // rarely present, matches C++ ParseJson key
        public string Lx { get; set; }   // leaf-only click offset X
        public string Ly { get; set; }   // leaf-only click offset Y

        [JsonIgnore]
        public string NodePath { get; set; }


        public static string EncodeAttributeValue(string strData)
        {
            if (strData == null)
                return null;

            strData = strData.Replace("&", "&amp;");
            strData = strData.Replace("'", "''");
            strData = strData.Replace("<", "&lt;");
            strData = strData.Replace(">", "&gt;");

            return strData;
        }

        static UiTreeNode FindInCachedUiTree(UiTreeNode node, List<UiTreeNode> cachedNode)
        {
            if (cachedNode == null || node == null) return null;

            foreach (var snode in cachedNode)
            {
                if (snode.NodePath == node.NodePath)
                {
                    return snode;
                }
            }

            return null;
        }

        public static void AddToUiTree(RecordedUiTask recordedUi)
        {
            if (recordedUi == null) return;

            if (recordedUi.GetUiTreeNode() == null)
            {
                recordedUi.GetJson();
            }

            var uiNode = recordedUi.GetUiTreeNode();

            if (uiNode == null || uiNode.Items == null || uiNode.Items.Count == 0)
            {
                return;
            }

            if (s_uiTreeNodes.Count == 0)
            {
                s_uiTreeNodes.Add(uiNode);
                return;
            }

            UiTreeNode node = uiNode;
            UiTreeNode nodeCached = null;
            List<UiTreeNode> listCached = s_uiTreeNodes;

            while (node != null)
            {
                var nodeAddTo = FindInCachedUiTree(node, listCached);

                if (node.Items.Count > 0 && nodeAddTo != null)
                {
                    node = node.Items.First();
                    listCached = nodeAddTo.Items;
                    nodeCached = nodeAddTo;
                }
                else
                {
                    if (nodeCached != null)
                    {
                        node.Parent = nodeCached;
                        nodeCached.Items.Add(node);
                    }
                    break;
                }
            }
        }

        static UiTreeNode FindUiTaskNode(UiTreeNode node, RecordedUiTask uiTaskNode)
        {
            if (node == null) return null;
            if (node.UiTask?._strPath == uiTaskNode._strPath)
            {
                return node;
            }

            if (node.Items != null)
            {
                for (int i = 0; i < node.Items.Count; i++)
                {
                    var ret = FindUiTaskNode(node.Items[i], uiTaskNode);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            }

            return null;
        }

        public static void RemoveUiTreeNode(RecordedUiTask uiTaskNode)
        {
            if (uiTaskNode == null || s_uiTreeNodes.Count == 0)
            {
                return;
            }

            // Search across ALL root nodes in s_uiTreeNodes, not just the first one
            UiTreeNode uiNode = null;
            foreach (var root in s_uiTreeNodes)
            {
                uiNode = FindUiTaskNode(root, uiTaskNode);
                if (uiNode != null)
                {
                    break;
                }
            }

            if (uiNode == null)
            {
                return;
            }

            UiTreeNode uiParent = uiNode.Parent;

            // Node is a root node
            if (uiParent == null)
            {
                uiNode.UiTask = null;
                if (uiNode.Items.Count == 0)
                {
                    s_uiTreeNodes.Remove(uiNode);
                }
                return;
            }

            // Node is a child node: Prune upward if parents become empty
            while (uiParent != null)
            {
                uiNode.UiTask = null;

                if (uiNode.Items.Count == 0)
                {
                    uiParent.Items.Remove(uiNode);
                }

                // Stop pruning if the parent still contains other child nodes
                if (uiParent.Items.Count > 0)
                {
                    break;
                }
                else
                {
                    uiNode = uiParent;
                    uiParent = uiNode.Parent;

                    // If we pruned all the way up to a root node with no remaining children
                    if (uiParent == null)
                    {
                        s_uiTreeNodes.Remove(uiNode);
                    }
                }
            }
        }

        public static List<UiTreeNode> UiTreeNodesFromString(string nodeJson)
        {
            string lx = string.Empty;
            string ly = string.Empty;
            string controlType = string.Empty;
            string name = string.Empty;

            var nodesRootToLeaf = new List<UiTreeNode>();
            // Accept either legacy newline-delimited node lines or new JSON array descriptors.
            string trimmed = nodeJson.TrimStart();
            if (trimmed.StartsWith("["))
            {
                try
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(trimmed))
                    {
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                // Re-parse the JSON object into a typed node. The
                                // per-field mapping (controlType -> Tag, x -> Left,
                                // lx -> Lx, etc.) lives in RecordedUiTask so this
                                // file does not need to know the wire format.
                                UiTreeNode node = RecordedUiTask.ParseNodeString(el.GetRawText());

                                // The outer variables are used only for the
                                // legacy explicit-assignment path further down
                                // (which is now a no-op for freshly recorded
                                // tasks, since the RecordedUiTask constructor
                                // already populates these from the leaf node).
                                // Kept for the fallback path and for any
                                // external diagnostic use.
                                controlType = node.Tag ?? "";
                                name = node.Name ?? "";
                                lx = node.Lx ?? "";
                                ly = node.Ly ?? "";

                                nodesRootToLeaf.Add(node);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            else
            {
            }

            return nodesRootToLeaf;
        }

        public static void GenerateUiTreeNode(RecordedUiTask task, List<UiTreeNode> pathNodes, ref UiTreeNode root)
        {
            root = null;
            if (pathNodes == null || pathNodes.Count == 0)
                return;

            UiTreeNode prev = null;
            foreach (var node in pathNodes)
            {
                if (node == null)
                    continue;

                node.Items.Clear();
                node.UiTask = null;
                node.Parent = prev;

                if (prev != null)
                    prev.Items.Add(node);
                else
                    root = node;

                AssignNodePath(node, prev);
                node.Title = BuildTitle(node);
                prev = node;
            }

            // Attach the task to the leaf (last non-null node processed).
            if (prev != null)
                prev.UiTask = task;
        }

        // Builds the display string shown in the Recorded UI tree.
        private const int kTitleNameMaxLen = 60;

        private static string BuildTitle(UiTreeNode node)
        {
            string tag = string.IsNullOrEmpty(node.Tag) ? "Node" : node.Tag;

            if (node.Position > 0)
                tag += "[" + node.Position + "]";

            string action = node.UiTask == null ? null : node.UiTask.UiTaskName.ToString();

            string nodeName = node.Name != null && node.Name.Length > 0 ? node.Name : node.ClassName;
            nodeName = nodeName != null && nodeName.Length > 0 ? nodeName : node.AutomationId;
            if (nodeName != null && nodeName.Length > kTitleNameMaxLen)
                nodeName = nodeName.Substring(0, kTitleNameMaxLen) + "\u2026"; // ellipsis

            return tag + " " + action + " \"" + nodeName + "\"";
        }

        private static void AssignNodePath(UiTreeNode node, UiTreeNode parent)
        {
            var sb = new StringBuilder();
            if (parent != null && !string.IsNullOrEmpty(parent.NodePath))
                sb.Append(parent.NodePath);

            sb.Append('/');
            sb.Append(string.IsNullOrEmpty(node.Tag) ? "Node" : node.Tag);

            if (node.Position > 0)
                sb.Append("[@Position='").Append(node.Position).Append("']");
            if (!string.IsNullOrEmpty(node.AutomationId))
                sb.Append("[@AutomationId='").Append(node.AutomationId).Append("']");
            if (!string.IsNullOrEmpty(node.ClassName))
                sb.Append("[@ClassName='").Append(node.ClassName).Append("']");
            if (!string.IsNullOrEmpty(node.Name))
                sb.Append("[@Name='").Append(node.Name).Append("']");

            node.NodePath = sb.ToString();
        }
    }
}