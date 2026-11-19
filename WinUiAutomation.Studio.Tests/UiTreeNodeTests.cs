using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
[DoNotParallelize]
public sealed class UiTreeNodeTests
{
    [TestInitialize]
    public void Initialize()
    {
        UiTreeNode.s_uiTreeNodes.Clear();
    }

    [TestCleanup]
    public void Cleanup()
    {
        UiTreeNode.s_uiTreeNodes.Clear();
    }

    [TestMethod]
    public void EncodeAttributeValueEscapesXmlCharactersAndSingleQuotes()
    {
        Assert.AreEqual("A&amp;B'' &lt;tag&gt;", UiTreeNode.EncodeAttributeValue("A&B' <tag>"));
        Assert.IsNull(UiTreeNode.EncodeAttributeValue(null));
    }

    [TestMethod]
    public void JsonSerializationUsesPathKeysAndOmitsInvalidGeometry()
    {
        var node = new UiTreeNode(null)
        {
            Tag = "Button",
            Position = 2,
            AutomationId = "save",
            Name = "Save",
            Left = "12",
            Top = "not-a-number",
            Width = "20",
            Height = ""
        };

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(node));
        JsonElement json = document.RootElement;

        Assert.AreEqual("Button", json.GetProperty("controlType").GetString());
        Assert.AreEqual(2, json.GetProperty("position").GetInt32());
        Assert.AreEqual("save", json.GetProperty("automationId").GetString());
        Assert.AreEqual(12, json.GetProperty("x").GetInt32());
        Assert.AreEqual(20, json.GetProperty("w").GetInt32());
        Assert.IsFalse(json.TryGetProperty("y", out _));
        Assert.IsFalse(json.TryGetProperty("h", out _));
    }

    [TestMethod]
    public void UiTreeNodesFromStringMapsPathFields()
    {
        const string path = "[{\"controlType\":\"Pane\",\"position\":0},{\"controlType\":\"Button\",\"position\":3,\"name\":\"Save\",\"lx\":6,\"ly\":7}]";

        List<UiTreeNode> nodes = UiTreeNode.UiTreeNodesFromString(path);

        Assert.AreEqual(2, nodes.Count);
        Assert.AreEqual("Pane", nodes[0].Tag);
        Assert.AreEqual(0, nodes[0].Position);
        Assert.AreEqual("Button", nodes[1].Tag);
        Assert.AreEqual(3, nodes[1].Position);
        Assert.AreEqual("Save", nodes[1].Name);
        Assert.AreEqual("6", nodes[1].Lx);
        Assert.AreEqual("7", nodes[1].Ly);
    }

    [TestMethod]
    public void AddToUiTreeMergesSharedAncestorsAndRemovePrunesEmptyBranches()
    {
        RecordedUiTask first = CreateTreeTask("First");
        RecordedUiTask second = CreateTreeTask("Second");

        UiTreeNode.AddToUiTree(first);
        UiTreeNode.AddToUiTree(second);

        Assert.AreEqual(1, UiTreeNode.s_uiTreeNodes.Count);
        UiTreeNode root = UiTreeNode.s_uiTreeNodes[0];
        Assert.AreEqual(1, root.Items.Count);
        UiTreeNode branch = root.Items[0];
        Assert.AreEqual(2, branch.Items.Count);
        Assert.AreSame(first, branch.Items[0].UiTask);
        Assert.AreSame(second, branch.Items[1].UiTask);

        UiTreeNode.RemoveUiTreeNode(second);

        Assert.AreEqual(1, branch.Items.Count);
        Assert.AreSame(first, branch.Items[0].UiTask);

        UiTreeNode.RemoveUiTreeNode(first);

        Assert.AreEqual(0, UiTreeNode.s_uiTreeNodes.Count);
    }

    private static RecordedUiTask CreateTreeTask(string leafName)
    {
        var root = new UiTreeNode(null)
        {
            Tag = "Window",
            Name = "Desktop"
        };
        var branch = new UiTreeNode(root)
        {
            Tag = "Pane",
            Position = 1,
            Name = "Content"
        };
        var leaf = new UiTreeNode(branch)
        {
            Tag = "Button",
            Position = 1,
            Name = leafName
        };

        return new RecordedUiTask(new List<UiTreeNode> { root, branch, leaf }, EnumUiTaskName.LeftClick);
    }
}