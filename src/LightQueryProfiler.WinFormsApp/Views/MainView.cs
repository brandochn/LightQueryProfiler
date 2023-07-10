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

        public event EventHandler? OnClearEvents;

        public event EventHandler OnClearFiltersClick;

        public event EventHandler? OnFiltersClick;

        public event EventHandler? OnPause;

        public event EventHandler? OnResume;

        public event EventHandler? OnStart;

        public event EventHandler? OnStop;

        public event EventHandler? RowEnter;
        ComboBox IMainView.AuthenticationComboBox => cboAuthentication;

        IList<AuthenticationMode> IMainView.AuthenticationModes
        {
            set
            {
                cboAuthentication.DataSource = value;
            }
        }

        string? IMainView.Password { get => txtPassword.Text; set => txtPassword.Text = value; }

        TextBox IMainView.PasswordTextBox => txtPassword;
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
        TextBox IMainView.ServerTexBox => txtServer;
        string IMainView.SessionName { get => "lqpSession"; }
        string? IMainView.SqlTextArea { get => webBrowser.DocumentText; set => webBrowser.DocumentText = value; }
        Button IMainView.StartButton => btnStart;
        Button IMainView.StopButton => btnStop;
        string? IMainView.User { get => txtUser.Text; set => txtUser.Text = value; }
        TextBox IMainView.UserTextBox => txtUser;

        private void BtnClearEvents_Click(object? sender, EventArgs e)
        {
            OnClearEvents?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClearFilters_Click(object? sender, EventArgs e)
        {
            OnClearFiltersClick?.Invoke(this, EventArgs.Empty);
        }

        private void BtnFilters_Click(object? sender, EventArgs e)
        {
            OnFiltersClick?.Invoke(this, EventArgs.Empty);
        }

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

        private void CboAuthentication_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            ComboBox? senderComboBox = sender as ComboBox;

            if (senderComboBox?.SelectionLength > 0)
            {
                int selectedAuthenticationMode = Convert.ToInt32(senderComboBox.SelectedValue);
                if ((Shared.Enums.AuthenticationMode)selectedAuthenticationMode == Shared.Enums.AuthenticationMode.WindowsAuth)
                {
                    txtUser.Visible = false;
                    txtPassword.Visible = false;
                }
                else
                {
                    txtUser.Visible = true;
                    txtPassword.Visible = true;
                }
            }
        }

        private void CreateEventHandlers()
        {
            Load += MainWindow_Load;
            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            dgvEvents.RowEnter += DgvEvents_RowEnter;
            btnPause.Click += BtnPause_Click;
            btnResume.Click += BtnResume_Click;
            btnClearEvents.Click += BtnClearEvents_Click;
            btnFilters.Click += BtnFilters_Click;
            btnClearFilters.Click += BtnClearFilters_Click;
        }
        private void DgvEvents_RowEnter(object? sender, DataGridViewCellEventArgs e)
        {
            RowEnter?.Invoke(sender, e);
        }

        private void InitializeCboAuthentication()
        {
            cboAuthentication.DisplayMember = "Name";
            cboAuthentication.ValueMember = "Value";
            cboAuthentication.SelectionChangeCommitted += CboAuthentication_SelectionChangeCommitted;
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