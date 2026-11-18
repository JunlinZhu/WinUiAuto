using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UiAutomation.Windows;
using WinUiAutomation.Runtime;

namespace WinUiAutomation.Runtime.Tests;

[TestClass]
public sealed class RuntimeTests
{
    [TestMethod]
    public void PlaybackRetriesLocateAndExecutesOnlyAfterResolution()
    {
        var driver = new FakeDriver(
            NotFound(),
            Found(),
            Found());
        var task = new AutomationTask { Action = AutomationTaskAction.LeftClick, NodePath = "[]" };

        PlaybackResult result = new AutomationPlayback(driver).Play(
            new[] { task },
            new PlaybackOptions
            {
                InitialDelay = TimeSpan.Zero,
                PostActionDelay = TimeSpan.Zero,
                LocateRetry = new RetryPolicy { MaxAttempts = 3, Delay = TimeSpan.Zero },
                Stability = new StabilityPolicy { MaxChecks = 1, Delay = TimeSpan.Zero }
            });

        Assert.AreEqual(PlaybackTaskStatus.Succeeded, result.Tasks[0].Status);
        Assert.AreEqual(2, result.Tasks[0].LocateAttempts);
        Assert.AreEqual(1, driver.MouseActions);
        Assert.IsTrue(result.Tasks[0].InputSent);
    }

    [TestMethod]
    public void DryRunResolvesTargetWithoutSendingInput()
    {
        var driver = new FakeDriver(Found(), Found());
        PlaybackResult result = new AutomationPlayback(driver).Play(
            new[] { new AutomationTask { Action = AutomationTaskAction.LeftClick, NodePath = "[]" } },
            new PlaybackOptions
            {
                InitialDelay = TimeSpan.Zero,
                PostActionDelay = TimeSpan.Zero,
                DryRun = true,
                Stability = new StabilityPolicy { MaxChecks = 1, Delay = TimeSpan.Zero }
            });

        Assert.AreEqual(PlaybackTaskStatus.Succeeded, result.Tasks[0].Status);
        Assert.IsFalse(result.Tasks[0].InputSent);
        Assert.AreEqual(0, driver.MouseActions);
    }

    [TestMethod]
    public void SkillJsonRejectsUnknownProperties()
    {
        SkillRunResult result = new AutomationSkillRunner(new FakeDriver()).RunJson("{\"format\":\"win-ui-automation-skill\",\"schemaVersion\":2,\"tasks\":[],\"unexpected\":true}");

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("JsonException", result.Error?.Type);
    }

    [TestMethod]
    public void CompletionAssertionsAreReported()
    {
        var driver = new FakeDriver(Found(), Found());
        var skill = new AutomationSkill
        {
            Name = "completion",
            Tasks = new[] { new AutomationSkillTask { Action = AutomationTaskAction.KeyboardInput, KeyboardInputBase64 = Convert.ToBase64String(new byte[] { 1 }) } },
            CompletionAssertions = new[]
            {
                new AutomationCompletionAssertion
                {
                    Kind = AutomationCompletionAssertionKind.ElementExists,
                    Target = JsonDocument.Parse("[]").RootElement.Clone(),
                    TimeoutMilliseconds = 1,
                    PollIntervalMilliseconds = 1
                }
            }
        };

        SkillRunResult result = new AutomationSkillRunner(driver).Run(skill, new PlaybackOptions { InitialDelay = TimeSpan.Zero, PostActionDelay = TimeSpan.Zero });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(CompletionAssertionStatus.Succeeded, result.CompletionAssertions[0].Status);
        Assert.IsTrue(result.AssertionsSucceeded);
    }

    private static UiLocateResult Found() => new(true, false, new UiRectangle(1, 2, 20, 30), "Target", new[] { new UiElementState("Target", 0, new Dictionary<string, string> { ["name"] = "Ready", ["value"] = "Ready" }) }, 0);
    private static UiLocateResult NotFound() => new(false, false, default, string.Empty, Array.Empty<UiElementState>(), 0);

    private sealed class FakeDriver : IAutomationDriver
    {
        private readonly Queue<UiLocateResult> locateResults;
        public int MouseActions { get; private set; }

        public FakeDriver(params UiLocateResult[] locateResults) => this.locateResults = new(locateResults);
        public UiLocateResult Locate(string nodePath, UiLocateOptions? options = null) => locateResults.Count > 0 ? locateResults.Dequeue() : Found();
        public void AbortLocate() { }
        public void Flash(UiRectangle bounds, int red, int green, int blue, int durationMilliseconds, int inflate) { }
        public void PerformMouseAction(UiAction action, UiRectangle bounds, int offsetX, int offsetY, int deltaX, int deltaY) => MouseActions++;
        public void SendKeyboardInput(string text, bool capsLock, bool numLock, bool scrollLock) { }
    }
}
