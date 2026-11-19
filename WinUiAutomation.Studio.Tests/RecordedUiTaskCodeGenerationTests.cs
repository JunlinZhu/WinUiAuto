using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
public sealed class RecordedUiTaskCodeGenerationTests
{
    [TestMethod]
    public void MouseTaskEmitsLocateActionFailureAndRecordedDelay()
    {
        RecordedUiTask task = CreateMouseTask(EnumUiTaskName.LeftClick);
        task.RecordedGapMs = 250;
        task.LeftLocal = 4;
        task.TopLocal = 5;
        task.DeltaX = 6;
        task.DeltaY = 7;

        string code = task.CreateCSCode(1, 0);

        StringAssert.Contains(code, "sleep(250);");
        StringAssert.Contains(code, "automation.Locate(json1, new UiLocateOptions { Flash = true });");
        StringAssert.Contains(code, "automation.PerformMouseAction(UiAction.LeftClick, located1.Bounds, 4, 5, 6, 7);");
        StringAssert.Contains(code, "Console.WriteLine($\"Failed to find element {json1}\");");
        StringAssert.Contains(code, "return;");
    }

    [TestMethod]
    public void KeyboardTaskEmitsInputWithoutLocateOrMouseAction()
    {
        var task = new RecordedUiTask(
            EnumUiTaskName.KeyboardInput,
            "AQID",
            bCapLock: true,
            scrollLock: false,
            numLock: true)
        {
            RecordedGapMs = 100
        };

        string code = task.CreateCSCode(1, 0);

        StringAssert.Contains(code, "sleep(100);");
        StringAssert.Contains(code, "automation.SendKeyboardInput(\"AQID\", true, true, false);");
        Assert.IsFalse(code.Contains("automation.Locate", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("PerformMouseAction", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FirstTaskDoesNotEmitARecordedDelay()
    {
        RecordedUiTask task = CreateMouseTask(EnumUiTaskName.RightClick);
        task.RecordedGapMs = 500;

        string code = task.CreateCSCode(0, 0);

        Assert.IsFalse(code.Contains("sleep(500);", StringComparison.Ordinal));
        StringAssert.Contains(code, "automation.PerformMouseAction(UiAction.RightClick");
    }

    private static RecordedUiTask CreateMouseTask(EnumUiTaskName action)
    {
        var root = new UiTreeNode(null)
        {
            Tag = "Pane",
            Position = 0,
            Name = "Root"
        };
        var leaf = new UiTreeNode(root)
        {
            Tag = "Button",
            Position = 1,
            Name = "Submit",
            ClassName = "ButtonClass",
            AutomationId = "submitButton",
            Left = "10",
            Top = "20",
            Width = "100",
            Height = "30"
        };

        return new RecordedUiTask(new List<UiTreeNode> { root, leaf }, action);
    }
}
