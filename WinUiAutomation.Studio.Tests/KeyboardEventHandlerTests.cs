using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinUiAutomation.Studio;

namespace WinUiAutomation.Studio.Tests;

[TestClass]
[DoNotParallelize]
public sealed class KeyboardEventHandlerTests
{
    [TestMethod]
    public void FromStringTaskNameMapsKnownNamesAndRejectsUnknownNames()
    {
        Assert.AreEqual(EnumUiTaskName.LeftClick, KeyboardEventHandler.FromStringTaskName("LeftClick"));
        Assert.AreEqual(EnumUiTaskName.UnknownUiTask, KeyboardEventHandler.FromStringTaskName("not-a-task"));
        Assert.AreEqual(EnumUiTaskName.UnknownUiTask, KeyboardEventHandler.FromStringTaskName(null));
    }

    [TestMethod]
    public void KeyMapsAndXmlEncodingProduceExpectedValues()
    {
        KeyboardEventHandler.s_Vk2String.Clear();
        KeyboardEventHandler.s_Vk2CharPair.Clear();
        KeyboardEventHandler.InitVk2StringMap();

        Assert.AreEqual("NumberPad0", KeyboardEventHandler.Vk2string(nameof(VirtualKeys.VK_NUMPAD0)));
        Assert.AreEqual("unmapped", KeyboardEventHandler.Vk2string("unmapped"));
        Assert.AreEqual('a', KeyboardEventHandler.Vk2char((int)VirtualKeys.A, false));
        Assert.AreEqual('A', KeyboardEventHandler.Vk2char((int)VirtualKeys.A, true));
        Assert.AreEqual((char)0, KeyboardEventHandler.Vk2char(0xFFFF, false));
        Assert.AreEqual("A&amp;B'' &lt;tag&gt;", KeyboardEventHandler.XmlEncode("A&B' <tag>"));
        Assert.IsNull(KeyboardEventHandler.XmlEncode(null));
    }
}
