# UiAutomation.Windows

`UiAutomation.Windows` provides a managed API for recording a reusable node path to a Windows UI element, locating the element later, inspecting its state, and replaying mouse or keyboard input.

```csharp
using UiAutomation.Windows;

using var automation = new UiAutomation.Windows.WinUiAutomation();
UiCaptureResult captured = automation.CapturePath(500, 300);
UiLocateResult located = automation.Locate(captured.NodePath, new UiLocateOptions { Flash = true });

if (located.Found)
{
	// Invoke the element's default UI Automation action without simulating a mouse click.
	automation.InvokeElementAction(500, 300);
	Console.WriteLine(located.TargetState?.Name);
}
```

Use `PerformMouseAction` instead when the automation requires physical mouse input.

Capture a bounded desktop snapshot, or query only matching elements, with typed results:

```csharp
UiSnapshot snapshot = automation.GetSnapshot(options: new UiSnapshotOptions
{
	MaxDepth = 8,
	MaxElements = 250,
	Flags = UiSnapshotFlags.InteractiveOnly | UiSnapshotFlags.IncludeValues
});

UiSnapshot buttons = automation.QuerySnapshot(new UiSnapshotQuery
{
	ControlType = "Button",
	Name = "save",
	Flags = UiSnapshotQueryFlags.NameContains
});
```

Pass a window handle as `rootWindow` to limit traversal to that window. Snapshot element values are omitted unless `IncludeValues` is set, and password values are always redacted.

The package supports Windows x86 and x64 processes. Build the consuming process for one of those architectures; `AnyCPU` with `Prefer32Bit` can make native DLL selection ambiguous.
