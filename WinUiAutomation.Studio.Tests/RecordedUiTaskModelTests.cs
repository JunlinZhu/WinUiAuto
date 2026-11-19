using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
public sealed class RecordedUiTaskModelTests
{
    [TestMethod]
    public void GetJsonSerializesPathAttributesAndNumericGeometry()
    {
        RecordedUiTask task = CreateTask();

        using JsonDocument document = JsonDocument.Parse(task.GetJson());
        JsonElement nodes = document.RootElement;
        JsonElement leaf = nodes[1];

        Assert.AreEqual(2, nodes.GetArrayLength());
        Assert.AreEqual("Button", leaf.GetProperty("controlType").GetString());
        Assert.AreEqual(1, leaf.GetProperty("position").GetInt32());
        Assert.AreEqual("saveButton", leaf.GetProperty("automationId").GetString());
        Assert.AreEqual("ButtonClass", leaf.GetProperty("className").GetString());
        Assert.AreEqual("Save", leaf.GetProperty("name").GetString());
        Assert.AreEqual(10, leaf.GetProperty("x").GetInt32());
        Assert.AreEqual(20, leaf.GetProperty("y").GetInt32());
        Assert.AreEqual(100, leaf.GetProperty("w").GetInt32());
        Assert.AreEqual(30, leaf.GetProperty("h").GetInt32());
        Assert.AreEqual(4, leaf.GetProperty("lx").GetInt32());
        Assert.AreEqual(5, leaf.GetProperty("ly").GetInt32());
    }

    [TestMethod]
    public void GetJsonCanRemoveEachIdentityAttribute()
    {
        RecordedUiTask task = CreateTask();

        using JsonDocument document = JsonDocument.Parse(task.GetJson(
            removeName: true, removeClassName: true, removeAutoId: true));
        JsonElement leaf = document.RootElement[1];

        Assert.IsFalse(leaf.TryGetProperty("name", out _));
        Assert.IsFalse(leaf.TryGetProperty("className", out _));
        Assert.IsFalse(leaf.TryGetProperty("automationId", out _));
        Assert.AreEqual(10, leaf.GetProperty("x").GetInt32());
    }

    [TestMethod]
    public void UpdateJsonReplacesPathAndRefreshesTaskTargetFields()
    {
        RecordedUiTask task = CreateTask();
        const string updatedPath = "[{\"controlType\":\"Window\",\"position\":0},{\"controlType\":\"Text\",\"position\":2,\"name\":\"Updated\",\"x\":42,\"y\":7}]";

        string result = task.UpdateJson(updatedPath);

        Assert.AreEqual(updatedPath, result);
        Assert.AreEqual("Text", task.Tag);
        Assert.AreEqual("Updated", task.Name);
        Assert.AreEqual(42, task.Left);
        Assert.AreEqual(7, task.Top);

        using JsonDocument document = JsonDocument.Parse(task.GetJson());
        Assert.AreEqual("Text", document.RootElement[1].GetProperty("controlType").GetString());
        Assert.AreEqual("Updated", document.RootElement[1].GetProperty("name").GetString());
    }

    [TestMethod]
    public void KeyboardTaskWithoutPathSerializesAsAnEmptyNodeArray()
    {
        var task = new RecordedUiTask(EnumUiTaskName.KeyboardInput, "AQID", false, false, false);

        Assert.AreEqual("[]", task.GetJson());
    }

    private static RecordedUiTask CreateTask()
    {
        var root = new UiTreeNode(null)
        {
            Tag = "Pane",
            Name = "Root"
        };
        var leaf = new UiTreeNode(root)
        {
            Tag = "Button",
            Position = 1,
            Name = "Save",
            ClassName = "ButtonClass",
            AutomationId = "saveButton",
            Left = "10",
            Top = "20",
            Width = "100",
            Height = "30",
            Lx = "4",
            Ly = "5"
        };

        return new RecordedUiTask(new List<UiTreeNode> { root, leaf }, EnumUiTaskName.LeftClick);
    }
}