using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MouseEventHandlerTests
{
    [TestMethod]
    public void WheelDeltasAccumulateAndFlushBeforeLeftPress()
    {
        MouseEventHandler.MouseWheel(10, 20, 80);
        MouseEventHandler.MouseWheel(10, 20, 40);

        Assert.AreEqual(MouseState.Wheel, MouseEventHandler.CurrentState);
        Assert.AreEqual(120, MouseEventHandler.PendingWheelAmount);

        MouseEventHandler.MouseWheel(10, 20, -30);

        Assert.AreEqual(MouseState.Wheel, MouseEventHandler.CurrentState);
        Assert.AreEqual(-30, MouseEventHandler.PendingWheelAmount);

        MouseEventHandler.LeftMouseDown(10, 20);

        Assert.AreEqual(MouseState.LeftDown, MouseEventHandler.CurrentState);
        Assert.AreEqual(0, MouseEventHandler.PendingWheelAmount);

        MouseEventHandler.LeftMouseUp(10, 20);

        Assert.AreEqual(MouseState.Idle, MouseEventHandler.CurrentState);
        Assert.AreEqual(0, MouseEventHandler.PendingWheelAmount);
    }
}
