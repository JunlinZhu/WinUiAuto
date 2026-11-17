namespace UiAutomation.Windows;

[Flags]
public enum UiSnapshotFlags
{
    None = 0,
    IncludeOffscreen = 1 << 0,
    InteractiveOnly = 1 << 1,
    IncludeValues = 1 << 2
}

[Flags]
public enum UiSnapshotQueryFlags
{
    None = 0,
    NameContains = 1 << 0,
    AutomationIdContains = 1 << 1,
    ClassNameContains = 1 << 2
}

public sealed class UiSnapshotOptions
{
    public const int DefaultMaxDepth = 12;
    public const int DefaultMaxElements = 500;
    public const int DefaultMaxVisitedElements = 5000;

    public int MaxDepth { get; init; } = DefaultMaxDepth;
    public int MaxElements { get; init; } = DefaultMaxElements;
    public int MaxVisitedElements { get; init; } = DefaultMaxVisitedElements;
    public UiSnapshotFlags Flags { get; init; }
}

public sealed class UiSnapshotQuery
{
    public string? ControlType { get; init; }
    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? ClassName { get; init; }
    public int? ProcessId { get; init; }
    public UiSnapshotQueryFlags Flags { get; init; }
}

public sealed record UiSnapshot(
    int SchemaVersion,
    ulong RootWindowHandle,
    int VisitedCount,
    int ElementCount,
    bool Truncated,
    IReadOnlyList<UiSnapshotElement> Elements);

public sealed record UiSnapshotElement(
    string Id,
    string? ParentId,
    int Depth,
    string ControlType,
    string? Name,
    string? AutomationId,
    string? ClassName,
    string? Value,
    bool ValueRedacted,
    int ProcessId,
    ulong NativeWindowHandle,
    UiRectangle Bounds,
    bool Enabled,
    bool Offscreen,
    bool Focused,
    IReadOnlyList<string> Actions,
    string NodePath);