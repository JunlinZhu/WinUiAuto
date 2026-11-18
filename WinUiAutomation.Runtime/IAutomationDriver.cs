using UiAutomation.Windows;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.Runtime;

public interface IAutomationDriver
{
    UiLocateResult Locate(string nodePath, UiLocateOptions? options = null);
    void AbortLocate();
    void Flash(UiRectangle bounds, int red, int green, int blue, int durationMilliseconds, int inflate);
    void PerformMouseAction(UiAction action, UiRectangle bounds, int offsetX, int offsetY, int deltaX, int deltaY);
    void SendKeyboardInput(string text, bool capsLock, bool numLock, bool scrollLock);
    UiSnapshot GetSnapshot(nint rootWindow = 0, UiSnapshotOptions? options = null) => throw new NotSupportedException();
    UiSnapshot QuerySnapshot(UiSnapshotQuery query, nint rootWindow = 0, UiSnapshotOptions? options = null) => throw new NotSupportedException();
    byte[] GetScreenshot(nint rootWindow = 0) => throw new NotSupportedException();
    void ValidateRootWindow(nint rootWindow) { }
}

internal sealed class WindowsAutomationDriver : IAutomationDriver
{
    private readonly WindowsAutomation automation;

    internal WindowsAutomationDriver(WindowsAutomation automation)
    {
        this.automation = automation ?? throw new ArgumentNullException(nameof(automation));
    }

    public UiLocateResult Locate(string nodePath, UiLocateOptions? options = null) => automation.Locate(nodePath, options);
    public void AbortLocate() => automation.AbortLocate();
    public void Flash(UiRectangle bounds, int red, int green, int blue, int durationMilliseconds, int inflate) =>
        automation.Flash(bounds, red, green, blue, durationMilliseconds, inflate);
    public void PerformMouseAction(UiAction action, UiRectangle bounds, int offsetX, int offsetY, int deltaX, int deltaY) =>
        automation.PerformMouseAction(action, bounds, offsetX, offsetY, deltaX, deltaY);
    public void SendKeyboardInput(string text, bool capsLock, bool numLock, bool scrollLock) =>
        automation.SendKeyboardInput(text, capsLock, numLock, scrollLock);
    public UiSnapshot GetSnapshot(nint rootWindow = 0, UiSnapshotOptions? options = null) => automation.GetSnapshot(rootWindow, options);
    public UiSnapshot QuerySnapshot(UiSnapshotQuery query, nint rootWindow = 0, UiSnapshotOptions? options = null) => automation.QuerySnapshot(query, rootWindow, options);
    public byte[] GetScreenshot(nint rootWindow = 0) => automation.GetScreenshot(rootWindow);
    public void ValidateRootWindow(nint rootWindow) => automation.ValidateRootWindow(rootWindow);
}