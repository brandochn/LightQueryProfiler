using LightQueryProfiler.WinFormsApp.Views;
using System.Reflection;

namespace LightQueryProfiler.WinFormsApp.Presenters
{
    public class AboutPresenter
    {
        private readonly IAboutView view;

        public AboutPresenter(IAboutView aboutView)
        {
            view = aboutView;
            view.OnIconLicenseLinkClick += OnIconLicenseLinkClick;
            view.OnLicenseLinkClick += OnLicenseLinkClick;
            SetVersion();
            view.OnOK += View_OnOK;
        }

        private void View_OnOK(object? sender, EventArgs e)
        {
            view.Form.Close();
        }

        private void OnIconLicenseLinkClick(object? sender, EventArgs e)
        {
            // Navigate to a URL.
            System.Diagnostics.Process.Start("explorer.exe", "https://icons8.com/");
        }

        private void OnLicenseLinkClick(object? sender, EventArgs e)
        {
            // Navigate to a URL.
            System.Diagnostics.Process.Start("explorer.exe", "https://github.com/brandochn/LightQueryProfiler/blob/main/LICENSE.md");
        }

        private void SetVersion()
        {
            string version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "No version found.";
            view.Version = $"Version: {version.Split('+')[0]}";
        }
    }
}