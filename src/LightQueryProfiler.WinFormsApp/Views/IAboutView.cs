namespace LightQueryProfiler.WinFormsApp.Views
{
    public interface IAboutView
    {
        event EventHandler? OnIconLicenseLinkClick;

        event EventHandler? OnLicenseLinkClick;

        event EventHandler? OnOK;
        Form Form { get; }
    }
}