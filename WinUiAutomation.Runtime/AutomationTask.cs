using UiAutomation.Windows;

namespace WinUiAutomation.Runtime;

public enum AutomationTaskAction
{
    KeyboardInput,
    LeftClick,
    RightClick,
    LeftDoubleClick,
    Drag,
    MouseWheel,
    MouseHover,
    DefaultInvoke
}

public sealed record AutomationSequence
{
    public string? Name { get; init; }
    public required IReadOnlyList<AutomationTask> Tasks { get; init; }
}

public sealed record AutomationTask
{
    public required AutomationTaskAction Action { get; init; }
    public string? NodePath { get; init; }
    public string? KeyboardInput { get; init; }
    public bool CapsLock { get; init; }
    public bool NumLock { get; init; }
    public bool ScrollLock { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int DeltaX { get; init; }
    public int DeltaY { get; init; }
    public TimeSpan DelayBefore { get; init; }

    internal UiAction ToUiAction() => Action switch
    {
        AutomationTaskAction.DefaultInvoke => UiAction.DefaultInvoke,
        AutomationTaskAction.LeftClick => UiAction.LeftClick,
        AutomationTaskAction.RightClick => UiAction.RightClick,
        AutomationTaskAction.LeftDoubleClick => UiAction.LeftDoubleClick,
        AutomationTaskAction.Drag => UiAction.Drag,
        AutomationTaskAction.MouseWheel => UiAction.MouseWheel,
        AutomationTaskAction.MouseHover => UiAction.MouseHover,
        _ => throw new InvalidOperationException($"{Action} is not a mouse action.")
    };
}

public sealed record RetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed record StabilityPolicy
{
    public int MaxChecks { get; init; } = 2;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed record PlaybackOptions
{
    public RetryPolicy LocateRetry { get; init; } = new();
    public StabilityPolicy Stability { get; init; } = new();
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan PostActionDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumTaskDelay { get; init; } = TimeSpan.FromSeconds(30);
    public bool FlashDuringStabilityChecks { get; init; } = true;
    public bool UseSameBoundsParentFallback { get; init; } = true;
}

public enum PlaybackTaskStatus
{
    Succeeded,
    ElementNotFound,
    ElementNotStable,
    Canceled
}

public sealed record PlaybackTaskResult(
    int Index,
    AutomationTask Task,
    PlaybackTaskStatus Status,
    UiRectangle Bounds,
    bool UsedParentFallback,
    int LocateAttempts);

public sealed record PlaybackResult(IReadOnlyList<PlaybackTaskResult> Tasks, bool WasCanceled)
{
    public int SucceededCount => Tasks.Count(task => task.Status == PlaybackTaskStatus.Succeeded);
}

public sealed record PlaybackProgress(int Index, AutomationTask Task);