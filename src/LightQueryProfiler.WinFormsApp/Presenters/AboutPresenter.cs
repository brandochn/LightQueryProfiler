using LightQueryProfiler.WinFormsApp.Views;

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
    }
}