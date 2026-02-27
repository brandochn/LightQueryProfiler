using LightQueryProfiler.WinFormsApp.Views;

namespace LightQueryProfiler.WinFormsApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // Enable modern visual styles
            Application.EnableVisualStyles();
            // Optional: Improve text rendering
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Poor Man's Dependency Injection/Pure Dependency Injection, Main() is the Composition Root.
            // See https://github.com/mrts/winforms-mvp/issues/2.
            var view = new MainView();
            _ = new Presenters.MainPresenter(view);
            Application.Run(view);
        }
    }
}
