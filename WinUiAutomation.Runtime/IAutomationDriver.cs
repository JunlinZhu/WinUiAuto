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
}