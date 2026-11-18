using System.Diagnostics;
using UiAutomation.Windows;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.Runtime;

public sealed class AutomationPlayback
{
    private readonly IAutomationDriver automation;

    public AutomationPlayback(WindowsAutomation automation)
        : this(new WindowsAutomationDriver(automation))
    {
    }

    public AutomationPlayback(IAutomationDriver automation)
    {
        this.automation = automation ?? throw new ArgumentNullException(nameof(automation));
    }

    public PlaybackResult Play(
        AutomationSequence sequence,
        PlaybackOptions? options = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return Play(sequence.Tasks, options, progress, cancellationToken);
    }

    public PlaybackResult Play(
        IReadOnlyList<AutomationTask> tasks,
        PlaybackOptions? options = null,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        options ??= new PlaybackOptions();
        Validate(options);

        var results = new List<PlaybackTaskResult>(tasks.Count);
        using CancellationTokenRegistration registration = cancellationToken.Register(automation.AbortLocate);
        if (!Wait(options.InitialDelay, cancellationToken))
        {
            return new PlaybackResult(results.AsReadOnly(), true);
        }

        UiRectangle lastBounds = default;
        var previousTaskTimer = new Stopwatch();
        for (int index = 0; index < tasks.Count; index++)
        {
            AutomationTask task = tasks[index] ?? throw new ArgumentException("Playback tasks cannot contain null entries.", nameof(tasks));
            progress?.Report(new PlaybackProgress(index, task));
            TimeSpan taskDelay = task.DelayBefore < options.MaximumTaskDelay
                ? task.DelayBefore
                : options.MaximumTaskDelay;
            if (previousTaskTimer.IsRunning)
            {
                taskDelay -= previousTaskTimer.Elapsed;
            }
            if (!Wait(taskDelay, cancellationToken))
            {
                results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.Canceled, lastBounds, false, 0));
                return new PlaybackResult(results.AsReadOnly(), true);
            }
            previousTaskTimer.Restart();

            if (task.Action == AutomationTaskAction.KeyboardInput)
            {
                if (!options.DryRun)
                {
                    automation.SendKeyboardInput(task.KeyboardInput ?? string.Empty, task.CapsLock, task.NumLock, task.ScrollLock);
                }
                results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.Succeeded, lastBounds, false, 0) { InputSent = !options.DryRun });
                continue;
            }

            if (task.Action == AutomationTaskAction.MouseWheel && string.IsNullOrWhiteSpace(task.NodePath))
            {
                if (!options.DryRun)
                {
                    automation.PerformMouseAction(UiAction.MouseWheel, lastBounds, task.OffsetX, task.OffsetY, task.DeltaX, task.DeltaY);
                }
                results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.Succeeded, lastBounds, false, 0) { InputSent = !options.DryRun });
                continue;
            }

            LocateOutcome located = Locate(task.NodePath, options, cancellationToken);
            if (located.Canceled)
            {
                results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.Canceled, lastBounds, located.UsedParentFallback, located.Attempts));
                return new PlaybackResult(results.AsReadOnly(), true);
            }

            if (!located.Result.Found)
            {
                results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.ElementNotFound, located.Result.Bounds, located.UsedParentFallback, located.Attempts));
                continue;
            }

            lastBounds = located.Result.Bounds;
            if (!located.UsedParentFallback && !WaitForStableBounds(task.NodePath!, ref lastBounds, options, cancellationToken))
            {
                PlaybackTaskStatus status = cancellationToken.IsCancellationRequested
                    ? PlaybackTaskStatus.Canceled
                    : PlaybackTaskStatus.ElementNotStable;
                results.Add(new PlaybackTaskResult(index, task, status, lastBounds, false, located.Attempts));
                if (status == PlaybackTaskStatus.Canceled)
                {
                    return new PlaybackResult(results.AsReadOnly(), true);
                }
                continue;
            }

            if (!options.DryRun)
            {
                automation.PerformMouseAction(task.ToUiAction(), lastBounds, task.OffsetX, task.OffsetY, task.DeltaX, task.DeltaY);
            }
            results.Add(new PlaybackTaskResult(index, task, PlaybackTaskStatus.Succeeded, lastBounds, located.UsedParentFallback, located.Attempts) { InputSent = !options.DryRun });
            if (!Wait(options.PostActionDelay, cancellationToken))
            {
                return new PlaybackResult(results.AsReadOnly(), true);
            }
        }

        return new PlaybackResult(results.AsReadOnly(), false);
    }

    private LocateOutcome Locate(string? nodePath, PlaybackOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            return new LocateOutcome(NotFound(), false, 0, false);
        }

        UiLocateResult result = NotFound();
        for (int attempt = 1; attempt <= options.LocateRetry.MaxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new LocateOutcome(result, false, attempt - 1, true);
            }

            result = automation.Locate(nodePath);
            if (result.Found)
            {
                return new LocateOutcome(result, false, attempt, false);
            }

            if (attempt < options.LocateRetry.MaxAttempts && !Wait(options.LocateRetry.Delay, cancellationToken))
            {
                return new LocateOutcome(result, false, attempt, true);
            }
        }

        return new LocateOutcome(result, false, options.LocateRetry.MaxAttempts, false);
    }

    private bool WaitForStableBounds(string nodePath, ref UiRectangle bounds, PlaybackOptions options, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < options.Stability.MaxChecks; attempt++)
        {
            UiRectangle previous = bounds;
            if (!Wait(options.Stability.Delay, cancellationToken))
            {
                return false;
            }

            UiLocateResult result = automation.Locate(nodePath, new UiLocateOptions { Flash = options.FlashDuringStabilityChecks });
            bounds = result.Bounds;
            if (result.Found && bounds == previous)
            {
                return true;
            }
        }

        return false;
    }

    private static UiLocateResult NotFound() => new(false, false, default, string.Empty, Array.Empty<UiElementState>(), 0);

    private static bool Wait(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        return !cancellationToken.WaitHandle.WaitOne(delay);
    }

    private static void Validate(PlaybackOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LocateRetry.MaxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Stability.MaxChecks);
        if (options.LocateRetry.Delay < TimeSpan.Zero ||
            options.Stability.Delay < TimeSpan.Zero ||
            options.InitialDelay < TimeSpan.Zero ||
            options.PostActionDelay < TimeSpan.Zero ||
            options.MaximumTaskDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Playback delays cannot be negative.");
        }
    }

    private sealed record LocateOutcome(UiLocateResult Result, bool UsedParentFallback, int Attempts, bool Canceled);
}