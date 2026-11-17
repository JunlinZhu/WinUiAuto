using UiAutomation.Windows;
using System.Diagnostics;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.Runtime;

public enum CompletionAssertionStatus
{
    Succeeded,
    TimedOut,
    Canceled
}

public sealed record CompletionAssertionResult(
    int Index,
    AutomationCompletionAssertion Assertion,
    CompletionAssertionStatus Status,
    int Attempts,
    TimeSpan Elapsed,
    UiLocateResult LastLocateResult);

public sealed record SkillRunResult(
    string? SkillName,
    PlaybackResult Playback,
    IReadOnlyList<CompletionAssertionResult> CompletionAssertions,
    bool WasCanceled)
{
    public bool PlaybackSucceeded =>
        !Playback.WasCanceled && Playback.Tasks.All(task => task.Status == PlaybackTaskStatus.Succeeded);

    public bool AssertionsSucceeded =>
        CompletionAssertions.All(assertion => assertion.Status == CompletionAssertionStatus.Succeeded);

    public bool Succeeded => PlaybackSucceeded && AssertionsSucceeded && !WasCanceled;
}

public sealed class AutomationSkillRunner
{
    private readonly IAutomationDriver automation;
    private readonly AutomationPlayback playback;

    public AutomationSkillRunner(WindowsAutomation automation)
        : this(new WindowsAutomationDriver(automation))
    {
    }

    public AutomationSkillRunner(IAutomationDriver automation)
    {
        this.automation = automation ?? throw new ArgumentNullException(nameof(automation));
        playback = new AutomationPlayback(automation);
    }

    public SkillRunResult RunJson(
        string skillJson,
        PlaybackOptions? playbackOptions = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Run(AutomationSkillJson.Deserialize(skillJson), playbackOptions, progress, cancellationToken);

    public SkillRunResult RunFile(
        string filePath,
        PlaybackOptions? playbackOptions = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return RunJson(File.ReadAllText(filePath), playbackOptions, progress, cancellationToken);
    }

    public SkillRunResult Run(
        AutomationSkill skill,
        PlaybackOptions? playbackOptions = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        AutomationSkill validatedSkill = AutomationSkillJson.Deserialize(AutomationSkillJson.Serialize(skill));
        PlaybackResult playbackResult = playback.Play(validatedSkill.ToSequence(), playbackOptions, progress, cancellationToken);
        var assertionResults = new List<CompletionAssertionResult>(validatedSkill.CompletionAssertions.Count);

        if (!playbackResult.WasCanceled)
        {
            for (int index = 0; index < validatedSkill.CompletionAssertions.Count; index++)
            {
                CompletionAssertionResult result = Evaluate(index, validatedSkill.CompletionAssertions[index], cancellationToken);
                assertionResults.Add(result);
                if (result.Status == CompletionAssertionStatus.Canceled)
                {
                    break;
                }
            }
        }

        bool wasCanceled = playbackResult.WasCanceled
            || assertionResults.Any(result => result.Status == CompletionAssertionStatus.Canceled);
        return new SkillRunResult(skill.Name, playbackResult, assertionResults.AsReadOnly(), wasCanceled);
    }

    private CompletionAssertionResult Evaluate(
        int index,
        AutomationCompletionAssertion assertion,
        CancellationToken cancellationToken)
    {
        string nodePath = assertion.GetNodePath();
        UiLocateResult lastResult = NotFound();
        int attempts = 0;
        TimeSpan timeout = TimeSpan.FromMilliseconds(assertion.TimeoutMilliseconds);
        Stopwatch stopwatch = Stopwatch.StartNew();

        using CancellationTokenRegistration registration = cancellationToken.Register(automation.AbortLocate);
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new CompletionAssertionResult(index, assertion, CompletionAssertionStatus.Canceled, attempts, stopwatch.Elapsed, lastResult);
            }

            attempts++;
            lastResult = automation.Locate(nodePath);
            bool succeeded = assertion.Kind switch
            {
                AutomationCompletionAssertionKind.ElementExists => lastResult.Found,
                AutomationCompletionAssertionKind.ElementNotExists => !lastResult.Found,
                _ => throw new InvalidOperationException($"Unsupported completion assertion kind: {assertion.Kind}.")
            };
            if (succeeded)
            {
                return new CompletionAssertionResult(index, assertion, CompletionAssertionStatus.Succeeded, attempts, stopwatch.Elapsed, lastResult);
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return new CompletionAssertionResult(index, assertion, CompletionAssertionStatus.TimedOut, attempts, stopwatch.Elapsed, lastResult);
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            TimeSpan pollInterval = TimeSpan.FromMilliseconds(assertion.PollIntervalMilliseconds);
            TimeSpan delay = remaining < pollInterval ? remaining : pollInterval;
            if (cancellationToken.WaitHandle.WaitOne(delay))
            {
                return new CompletionAssertionResult(index, assertion, CompletionAssertionStatus.Canceled, attempts, stopwatch.Elapsed, lastResult);
            }
        }
    }

    private static UiLocateResult NotFound() =>
        new(false, false, default, string.Empty, Array.Empty<UiElementState>(), 0);
}
