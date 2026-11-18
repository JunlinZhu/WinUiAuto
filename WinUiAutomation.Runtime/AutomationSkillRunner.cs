using UiAutomation.Windows;
using System.Diagnostics;
using System.Text.Json;
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
    public SkillRunError? Error { get; init; }
    public bool PlaybackSucceeded =>
        !Playback.WasCanceled && Playback.Tasks.All(task => task.Status == PlaybackTaskStatus.Succeeded);

    public bool AssertionsSucceeded =>
        CompletionAssertions.All(assertion => assertion.Status == CompletionAssertionStatus.Succeeded);

    public bool Succeeded => Error is null && PlaybackSucceeded && AssertionsSucceeded && !WasCanceled;
}

public sealed record SkillRunError(string Type, string Message, int? HResult = null);

public sealed record AutomationAuditEvent(
    DateTimeOffset Timestamp,
    string? SkillName,
    int TaskIndex,
    AutomationTaskAction Action,
    string? Target,
    string Result);

public interface IAutomationAuditSink
{
    void Record(AutomationAuditEvent auditEvent);
}

public sealed record AutomationSkillRunnerOptions
{
    public nint RootWindow { get; init; }
    public IAutomationAuditSink? AuditSink { get; init; }
}

public sealed class AutomationSkillRunner
{
    private readonly IAutomationDriver automation;
    private readonly AutomationPlayback playback;
    private readonly AutomationSkillRunnerOptions runnerOptions;

    public AutomationSkillRunner(WindowsAutomation automation, AutomationSkillRunnerOptions? options = null)
        : this(new WindowsAutomationDriver(automation), options)
    {
    }

    public AutomationSkillRunner(IAutomationDriver automation, AutomationSkillRunnerOptions? options = null)
    {
        this.automation = automation ?? throw new ArgumentNullException(nameof(automation));
        playback = new AutomationPlayback(automation);
        runnerOptions = options ?? new AutomationSkillRunnerOptions();
    }

    public SkillRunResult RunJson(
        string skillJson,
        PlaybackOptions? playbackOptions = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Run(AutomationSkillJson.Deserialize(skillJson), playbackOptions, progress, cancellationToken);
        }
        catch (JsonException exception)
        {
            return Failed(null, "JsonException", exception.Message);
        }
        catch (UiAutomationException exception)
        {
            return Failed(null, nameof(UiAutomationException), exception.Message, exception.HResult);
        }
    }

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
        AutomationSkill validatedSkill;
        try
        {
            validatedSkill = AutomationSkillJson.Deserialize(AutomationSkillJson.Serialize(skill));
            automation.ValidateRootWindow(runnerOptions.RootWindow);
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
            for (int index = 0; index < playbackResult.Tasks.Count; index++)
            {
                PlaybackTaskResult task = playbackResult.Tasks[index];
                runnerOptions.AuditSink?.Record(new AutomationAuditEvent(DateTimeOffset.UtcNow, skill.Name, task.Index, task.Task.Action, task.Task.NodePath, task.Status.ToString()));
            }

            return new SkillRunResult(skill.Name, playbackResult, assertionResults.AsReadOnly(), wasCanceled);
        }
        catch (JsonException exception)
        {
            return Failed(skill.Name, "JsonException", exception.Message);
        }
        catch (UiAutomationException exception)
        {
            return Failed(skill.Name, nameof(UiAutomationException), exception.Message, exception.HResult);
        }
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
            bool succeeded;
            if (assertion.Kind == AutomationCompletionAssertionKind.ScreenshotChanged)
            {
                byte[] actual = automation.GetScreenshot(runnerOptions.RootWindow);
                byte[] expected = Convert.FromBase64String(assertion.ExpectedScreenshotBase64!);
                succeeded = !actual.AsSpan().SequenceEqual(expected);
            }
            else
            {
                lastResult = automation.Locate(nodePath);
                succeeded = assertion.Kind switch
                {
                    AutomationCompletionAssertionKind.ElementExists => lastResult.Found,
                    AutomationCompletionAssertionKind.ElementNotExists => !lastResult.Found,
                    AutomationCompletionAssertionKind.ValueEquals => lastResult.Found && string.Equals(lastResult.TargetState?.Value, assertion.ExpectedValue, StringComparison.Ordinal),
                    AutomationCompletionAssertionKind.TextEquals => lastResult.Found && string.Equals(lastResult.TargetState?.Name ?? lastResult.TargetState?.Value, assertion.ExpectedValue, StringComparison.Ordinal),
                    _ => throw new InvalidOperationException($"Unsupported completion assertion kind: {assertion.Kind}.")
                };
            }
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

    private static SkillRunResult Failed(string? skillName, string type, string message, int? hResult = null) =>
        new(skillName, new PlaybackResult(Array.Empty<PlaybackTaskResult>(), false), Array.Empty<CompletionAssertionResult>(), false)
        {
            Error = new SkillRunError(type, message, hResult)
        };
}
