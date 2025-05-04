using System.ComponentModel;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public partial class AboutView : Form, IAboutView
    {
        public AboutView()
        {
            InitializeComponent();
            CreateEventHandlers();
        }

        public event EventHandler? OnIconLicenseLinkClick;

        public event EventHandler? OnLicenseLinkClick;

        public event EventHandler? OnOK;

        public Form Form => this;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Version { get => lblVersion.Text; set => lblVersion.Text = value; }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            OnOK?.Invoke(this, EventArgs.Empty);
        }

        private void CreateEventHandlers()
        {
            lkbIcons.Click += LkbIcons_Click;
            lkbLicense.Click += LkbLicense_Click;
            btnOK.Click += BtnOK_Click;
        }

        private void LkbIcons_Click(object? sender, EventArgs e)
        {
            OnIconLicenseLinkClick?.Invoke(this, EventArgs.Empty);
        }

        private void LkbLicense_Click(object? sender, EventArgs e)
        {
            OnLicenseLinkClick?.Invoke(this, EventArgs.Empty);
        }
    }
}