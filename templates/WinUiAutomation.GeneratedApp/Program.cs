using System;
using UiAutomation.Windows;

internal static class Program
{
    static void Main()
    {
        WinUiAutomation_202611181420();
    }

    // WinUiAutomation_202611181420 is created by UiAutomation Studio at 2026-11-18 14:20:00
    // Here are the steps to generate this method:
    // 1. Start WinUiAutomation Studio and record a new automation.
    // 2. LeftClick on Button "Search" at (41,10)
    // 3. Type "copilot" and Keys.Return 
    // 4. Wait for Copilot app to start
    // 5. RightClick on Button "Microsoft 365 Copilot - 1 runn"
    // 6. LeftClick on ListItem "Close window"
    // 7. Click "Stop recording" to finish recording and generate the code.
    // 8. Copy the generated code to this method.
    static void WinUiAutomation_202611181420()
    {
        using var automation = new UiAutomation.Windows.WinUiAutomation();

        // LeftClick on Button "Search" at (41,10)
        Console.WriteLine("LeftClick on Button \"Search\" at (41,10)");
        string json0 = """
	[{"controlType":"Pane","position":0,"className":"#32769","name":"Desktop 1","x":0,"y":0,"w":2560,"h":1440,"lx":918,"ly":1407},{"controlType":"Pane","position":3,"className":"Shell_TrayWnd","name":"Taskbar","x":0,"y":1392,"w":2560,"h":48,"lx":918,"ly":15},{"controlType":"Pane","position":1,"className":"Windows.UI.Composition.DesktopWindowContentBridge","name":"DesktopWindowXamlSource","x":0,"y":1392,"w":2560,"h":48,"lx":918,"ly":15},{"controlType":"Pane","position":1,"className":"Windows.UI.Input.InputSite.WindowClass","x":0,"y":1392,"w":2560,"h":48,"lx":918,"ly":15},{"controlType":"Pane","position":1,"automationId":"TaskbarFrame","className":"Taskbar.TaskbarFrameAutomationPeer","x":0,"y":1392,"w":2560,"h":48,"lx":918,"ly":15},{"controlType":"Group","position":1,"automationId":"TaskbarFrameRepeater","x":838,"y":1392,"w":885,"h":48,"lx":80,"ly":15},{"controlType":"Button","position":2,"automationId":"SearchButton","className":"ToggleButton","name":"Search","x":885,"y":1400,"w":220,"h":32,"lx":33,"ly":7}]
	""";
        UiLocateResult located0 = automation.Locate(json0, new UiLocateOptions { Flash = true });
        if (located0.Found)
        {
            automation.PerformMouseAction(UiAction.LeftClick, located0.Bounds, 41, 10, 0, 0);
            Console.WriteLine(located0.RawState);
        }
        else
        {
            Console.WriteLine($"Failed to find element {json0}");
            return;
        }

        // KeyboardInput VirtualKeys=""copilot"Keys.Return + Keys.Return" CapsLock=False NumLock=False ScrollLock=False
        System.Threading.Thread.Sleep(1000);
        Console.WriteLine("KeyboardInput VirtualKeys=\"\"copilot\"Keys.Return + Keys.Return\" CapsLock=False NumLock=False ScrollLock=False");
        automation.SendKeyboardInput("AEMBQwBPAU8AUAFQAEkBSQBMAUwATwFPAFQBVAANAQ0=", false, false, false);

        // RightClick on Button "Microsoft 365 Copilot - 1 runn" at (28,22)
        System.Threading.Thread.Sleep(1);
        Console.WriteLine("RightClick on Button \"Microsoft 365 Copilot - 1 runn\" at (28,22)");
        string json2 = """
	[{"controlType":"Pane","position":0,"className":"#32769","name":"Desktop 1","x":0,"y":0,"w":2560,"h":1440,"lx":1212,"ly":1414},{"controlType":"Pane","position":2,"className":"Shell_TrayWnd","name":"Taskbar","x":0,"y":1392,"w":2560,"h":48,"lx":1212,"ly":22},{"controlType":"Pane","position":1,"className":"Windows.UI.Composition.DesktopWindowContentBridge","name":"DesktopWindowXamlSource","x":0,"y":1392,"w":2560,"h":48,"lx":1212,"ly":22},{"controlType":"Pane","position":1,"className":"Windows.UI.Input.InputSite.WindowClass","x":0,"y":1392,"w":2560,"h":48,"lx":1212,"ly":22},{"controlType":"Pane","position":1,"automationId":"TaskbarFrame","className":"Taskbar.TaskbarFrameAutomationPeer","x":0,"y":1392,"w":2560,"h":48,"lx":1212,"ly":22},{"controlType":"Group","position":1,"automationId":"TaskbarFrameRepeater","x":838,"y":1392,"w":885,"h":48,"lx":374,"ly":22},{"controlType":"Button","position":5,"automationId":"Appid: Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe!Microsoft.MicrosoftOfficeHub","className":"Taskbar.TaskListButtonAutomationPeer","name":"Microsoft 365 Copilot - 1 running window pinned","x":1195,"y":1392,"w":44,"h":48,"lx":17,"ly":22}]
	""";
        UiLocateResult located2 = automation.Locate(json2, new UiLocateOptions { Flash = true });
        if (located2.Found)
        {
            automation.PerformMouseAction(UiAction.RightClick, located2.Bounds, 28, 22, 0, 0);
            Console.WriteLine(located2.RawState);
        }
        else
        {
            Console.WriteLine($"Failed to find element {json2}");
            return;
        }

        // LeftClick on ListItem "Close window" at (79,16)
        System.Threading.Thread.Sleep(2000);
        Console.WriteLine("LeftClick on ListItem \"Close window\" at (79,16)");
        string json3 = """
	[{"controlType":"Pane","position":0,"className":"#32769","name":"Desktop 1","x":0,"y":0,"w":2560,"h":1440,"lx":1152,"ly":1360},{"controlType":"Window","position":1,"className":"Windows.UI.Core.CoreWindow","name":"Jump List for Microsoft 365 Copilot","x":1057,"y":1166,"w":318,"h":226,"lx":95,"ly":194},{"controlType":"List","position":1,"automationId":"SystemItemList","className":"ListView","x":1069,"y":1281,"w":294,"h":96,"lx":83,"ly":79},{"controlType":"ListItem","position":3,"automationId":"Close","className":"ListViewItem","name":"Close window","x":1073,"y":1347,"w":286,"h":28,"lx":79,"ly":13}]
	""";
        UiLocateResult located3 = automation.Locate(json3, new UiLocateOptions { Flash = true });
        if (located3.Found)
        {
            automation.PerformMouseAction(UiAction.LeftClick, located3.Bounds, 79, 16, 0, 0);
            Console.WriteLine(located3.RawState);
        }
        else
        {
            Console.WriteLine($"Failed to find element {json3}");
            return;
        }
    }
}
