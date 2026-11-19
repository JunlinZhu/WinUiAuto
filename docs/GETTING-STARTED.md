# Getting started

## Build

Build the native library for the process architecture, then build the managed solution:

```powershell
msbuild .\UiAutoLib\UiAutoLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v143
msbuild .\WinUiAuto.sln /t:Restore,Build /p:Configuration=Release /p:Platform=x64
```

## Runtime playback

Reference `WinUiAutomation.Runtime` and `UiAutomation.Windows`, create one `WinUiAutomation` per process/session, and keep calls on an STA thread:

```csharp
using var automation = new UiAutomation.Windows.WinUiAutomation();
var runner = new WinUiAutomation.Runtime.AutomationSkillRunner(automation);
var result = runner.RunJson(skillJson);
if (!result.Succeeded)
{
	Console.Error.WriteLine(result.Error?.Message ?? "The skill did not complete successfully.");
}
```

An interactive, unlocked Windows desktop is required for native UI Automation and input.

## Run generated C# from Studio

`WinUiAutomation.Studio` **Generate Code** copies a C# method to the clipboard. Paste that method into the `Program` class in [`templates/WinUiAutomation.GeneratedApp`](../templates/WinUiAutomation.GeneratedApp), update `Main` to call its timestamped method name, and run:

```powershell
dotnet run --project .\templates\WinUiAutomation.GeneratedApp\WinUiAutomation.GeneratedApp.csproj -c Release
```

Build `UiAutoLib` for x64 first so the template can copy `UiAutoLib.dll` beside the executable. Review generated code before running because it sends real mouse and keyboard input.
