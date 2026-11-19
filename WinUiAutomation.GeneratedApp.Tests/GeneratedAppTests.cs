using Microsoft.VisualStudio.TestTools.UnitTesting;
using UiAutomation.Windows;

namespace WinUiAutomation.GeneratedApp.Tests;

[TestClass]
public sealed class GeneratedAppTests
{
    [TestMethod]
    public void GeneratedWorkflowExecutesAllLocatedSteps()
    {
        var automation = new FakeAutomation(Found(), Found(), Found());

        Program.RunGeneratedAutomation_SearchCopilot(automation, _ => { });

        Assert.AreEqual(3, automation.LocateCalls);
        Assert.AreEqual(3, automation.MouseActions.Count);
        Assert.AreEqual(UiAction.LeftClick, automation.MouseActions[0]);
        Assert.AreEqual(UiAction.RightClick, automation.MouseActions[1]);
        Assert.AreEqual(UiAction.LeftClick, automation.MouseActions[2]);
        Assert.AreEqual(1, automation.KeyboardInputs);
    }

    [TestMethod]
    public void GeneratedWorkflowStopsWhenSearchCannotBeLocated()
    {
        var automation = new FakeAutomation(NotFound());

        Program.RunGeneratedAutomation_SearchCopilot(automation, _ => { });

        Assert.AreEqual(1, automation.LocateCalls);
        Assert.AreEqual(0, automation.MouseActions.Count);
        Assert.AreEqual(0, automation.KeyboardInputs);
    }

    [TestMethod]
    public void GeneratedWorkflowStopsWhenApplicationCannotBeLocated()
    {
        var automation = new FakeAutomation(Found(), NotFound());

        Program.RunGeneratedAutomation_SearchCopilot(automation, _ => { });

        Assert.AreEqual(2, automation.LocateCalls);
        Assert.AreEqual(1, automation.MouseActions.Count);
        Assert.AreEqual(1, automation.KeyboardInputs);
    }

    [TestMethod]
    public void GeneratedWorkflowStopsWhenCloseCommandCannotBeLocated()
    {
        var automation = new FakeAutomation(Found(), Found(), NotFound());

        Program.RunGeneratedAutomation_SearchCopilot(automation, _ => { });

        Assert.AreEqual(3, automation.LocateCalls);
        Assert.AreEqual(2, automation.MouseActions.Count);
        Assert.AreEqual(1, automation.KeyboardInputs);
    }

    private static UiLocateResult Found() => new(
        true,
        false,
        new UiRectangle(1, 2, 20, 30),
        "Target",
        Array.Empty<UiElementState>(),
        0);

    private static UiLocateResult NotFound() => new(
        false,
        false,
        default,
        string.Empty,
        Array.Empty<UiElementState>(),
        0);

    private sealed class FakeAutomation : Program.IGeneratedAutomation
    {
        private readonly Queue<UiLocateResult> locateResults;

        public FakeAutomation(params UiLocateResult[] locateResults) => this.locateResults = new(locateResults);

        public int LocateCalls { get; private set; }
        public List<UiAction> MouseActions { get; } = new();
        public int KeyboardInputs { get; private set; }

        public UiLocateResult Locate(string nodePath, UiLocateOptions? options = null)
        {
            LocateCalls++;
            return locateResults.Dequeue();
        }

        public void PerformMouseAction(UiAction action, UiRectangle bounds, int offsetX, int offsetY, int deltaX, int deltaY) =>
            MouseActions.Add(action);

        public void SendKeyboardInput(string text, bool capsLock, bool numLock, bool scrollLock) => KeyboardInputs++;

    }
}