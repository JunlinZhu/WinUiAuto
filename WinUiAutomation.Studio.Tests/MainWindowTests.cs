using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
public sealed class MainWindowTests
{
    [TestMethod]
    public void BuildNodePathSerializesTheNodeAndItsAncestorsInRootToLeafOrder()
    {
        var root = new UiTreeNode(null)
        {
            Tag = "Window",
            Name = "Desktop"
        };
        var child = new UiTreeNode(root)
        {
            Tag = "Button",
            Position = 2,
            Name = "Save"
        };

        string path = MainWindow.BuildNodePath(child);

        using JsonDocument document = JsonDocument.Parse(path);
        Assert.AreEqual(2, document.RootElement.GetArrayLength());
        Assert.AreEqual("Window", document.RootElement[0].GetProperty("controlType").GetString());
        Assert.AreEqual("Button", document.RootElement[1].GetProperty("controlType").GetString());
        Assert.AreEqual(2, document.RootElement[1].GetProperty("position").GetInt32());
        Assert.AreEqual("Save", document.RootElement[1].GetProperty("name").GetString());
    }

    [TestMethod]
    public void IndentCodeBlockIndentsNonemptyLinesAndNormalizesLineEndings()
    {
        string result = MainWindow.IndentCodeBlock("if (true)\r\n\r\nreturn;", "    ");

        Assert.AreEqual(string.Join(Environment.NewLine, "    if (true)", "", "    return;"), result);
        Assert.AreEqual(string.Empty, MainWindow.IndentCodeBlock(string.Empty, "    "));
    }
}
