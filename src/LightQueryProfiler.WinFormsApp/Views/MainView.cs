using LightQueryProfiler.WinFormsApp.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public partial class MainView : Form, IMainView
    {
        private readonly WebBrowser webBrowser = new WebBrowser();

        public MainView()
        {
            InitializeComponent();
            CreateEventHandlers();
        }

        public event EventHandler? OnPause;

        public event EventHandler? OnResume;

        public event EventHandler? OnStart;

        public event EventHandler? OnStop;

        public event EventHandler? RowEnter;

        IList<AuthenticationMode> IMainView.AuthenticationModes
        {
            set
            {
                cboAuthentication.DataSource = value;
            }
        }

        string? IMainView.Password { get => txtPassword.Text; set => txtPassword.Text = value; }

        Button IMainView.PauseButton => btnPause;
        DataGridViewColumn[] IMainView.ProfilerColumns { set => dgvEvents.Columns.AddRange(value); }

        ListView IMainView.ProfilerDetails => lvDetails;

        DataGridView IMainView.ProfilerGridView => dgvEvents;

        Button IMainView.ResumeButton => btnResume;

        object? IMainView.SelectedAuthenticationMode
        {
            get
            {
                return cboAuthentication.SelectedValue;
            }
            set
            {
                cboAuthentication.SelectedValue = value;
            }
        }

        string? IMainView.Server { get => txtServer.Text; set => txtServer.Text = value; }
        string IMainView.SessionName { get => "lqpSession"; }
        string? IMainView.SqlTextArea { get => webBrowser.DocumentText; set => webBrowser.DocumentText = value; }
        Button IMainView.StartButton => btnStart;
        Button IMainView.StopButton => btnStop;
        string? IMainView.User { get => txtUser.Text; set => txtUser.Text = value; }

        private void BtnPause_Click(object? sender, EventArgs e)
        {
            OnPause?.Invoke(this, EventArgs.Empty);
        }

        private void BtnResume_Click(object? sender, EventArgs e)
        {
            OnResume?.Invoke(this, EventArgs.Empty);
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            OnStart?.Invoke(this, EventArgs.Empty);
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            OnStop?.Invoke(this, EventArgs.Empty);
        }

        private void CreateEventHandlers()
        {
            Load += MainWindow_Load;
            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            dgvEvents.RowEnter += DgvEvents_RowEnter;
            btnPause.Click += BtnPause_Click;
            btnResume.Click += BtnResume_Click;
        }

        private void DgvEvents_RowEnter(object? sender, DataGridViewCellEventArgs e)
        {
            RowEnter?.Invoke(sender, e);
        }

        private void InitializeCboAuthentication()
        {
            cboAuthentication.DisplayMember = "Name";
            cboAuthentication.ValueMember = "Value";
        }

        private void MainWindow_Load(object? sender, EventArgs e)
        {
            InitializeCboAuthentication();
            SetupDgvEvents();
            SetupWebBrowser();
        }

        private void SetupDgvEvents()
        {
            dgvEvents.ReadOnly = true;
        }

        private void SetupWebBrowser()
        {
            webBrowser.AllowWebBrowserDrop = false;
            webBrowser.Dock = DockStyle.Fill;
            tabPageText.Controls.Add(webBrowser);
        }
    }
}