namespace WinUiAutomation.Studio
{
    using System;

    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            NativeMethods.SetProcessDPIAware();
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}