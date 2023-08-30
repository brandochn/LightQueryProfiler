using LightQueryProfiler.WinFormsApp.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public partial class MainView : Form, IMainView
    {
        private static Bitmap? clearBmp;
        private static Bitmap? clearFiltersBmp;
        private static Bitmap? pauseBmp;
        private static Bitmap? playBmp;
        private static Bitmap? resumeBmp;
        private static Bitmap? searchBmp;
        private static Bitmap? stopBmp;
        private readonly WebBrowser webBrowser = new WebBrowser();

        private ToolStrip toolStripMain = new ToolStrip();

        private ToolStripSeparator toolStripSeparator3 = new ToolStripSeparator();

        private ToolStripSeparator toolStripSeparator4 = new ToolStripSeparator();

        private ToolStripButton tsbClearEvents = new ToolStripButton();

        private ToolStripButton tsbClearFilters = new ToolStripButton();

        private ToolStripButton tsbFilters = new ToolStripButton();

        private ToolStripButton tsbPause = new ToolStripButton();

        private ToolStripButton tsbResume = new ToolStripButton();

        private ToolStripButton tsbSearch = new ToolStripButton();

        private ToolStripButton tsbStart = new ToolStripButton();

        private ToolStripButton tsbStop = new ToolStripButton();

        private ToolStripComboBox tscAuthentication = new ToolStripComboBox();

        private ToolStripLabel tslAUTH = new ToolStripLabel();

        private ToolStripLabel tslPassword = new ToolStripLabel();

        private ToolStripLabel tslServer = new ToolStripLabel();

        private ToolStripLabel tslUser = new ToolStripLabel();

        private ToolStripTextBox tstPassWord = new ToolStripTextBox();

        private ToolStripTextBox tstSearch = new ToolStripTextBox();

        private ToolStripTextBox tstServer = new ToolStripTextBox();

        private ToolStripTextBox tstUser = new ToolStripTextBox();

        public MainView()
        {
            InitializeComponent();
            CreateEventHandlers();
        }

        public event EventHandler? OnClearEvents;

        public event EventHandler? OnClearFiltersClick;

        public event EventHandler? OnFiltersClick;

        public event EventHandler? OnPause;

        public event EventHandler? OnResume;

        public event EventHandler? OnSearch;

        public event EventHandler? OnStart;

        public event EventHandler? OnStop;

        public event EventHandler? RowEnter;

        ToolStripComboBox IMainView.AuthenticationComboBox => tscAuthentication;

        IList<AuthenticationMode> IMainView.AuthenticationModes
        {
            set
            {
                tscAuthentication.ComboBox.DataSource = value;
            }
        }

        string? IMainView.Password { get => tstPassWord.Text; set => tstPassWord.Text = value; }

        ToolStripTextBox IMainView.PasswordTextBox => tstPassWord;
        ToolStripButton IMainView.PauseButton => tsbPause;
        DataGridViewColumn[] IMainView.ProfilerColumns { set => dgvEvents.Columns.AddRange(value); }

        ListView IMainView.ProfilerDetails => lvDetails;

        DataGridView IMainView.ProfilerGridView => dgvEvents;

        ToolStripButton IMainView.ResumeButton => tsbResume;

        ToolStripButton IMainView.SearchButton => tsbSearch;

        string? IMainView.SearchValue { get => tstSearch.Text; set => tstSearch.Text = value; }

        object? IMainView.SelectedAuthenticationMode
        {
            get
            {
                return tscAuthentication.ComboBox.SelectedValue;
            }
            set
            {
                tscAuthentication.ComboBox.SelectedValue = value;
            }
        }

        string? IMainView.Server { get => tstServer.Text; set => tstServer.Text = value; }
        ToolStripTextBox IMainView.ServerTexBox => tstServer;
        string IMainView.SessionName { get => "lqpSession"; }
        string? IMainView.SqlTextArea { get => webBrowser.DocumentText; set => webBrowser.DocumentText = value; }
        ToolStripButton IMainView.StartButton => tsbStart;
        StatusStrip IMainView.StatusBar => statusBar;
        ToolStripButton IMainView.StopButton => tsbStop;
        string? IMainView.User { get => tstUser.Text; set => tstUser.Text = value; }
        ToolStripTextBox IMainView.UserTextBox => tstUser;

        private static void CreateBitmaps()
        {
            playBmp = DecodeFromBase64(Constants.PLAY_BMP_ENC);
            stopBmp = DecodeFromBase64(Constants.STOP_BMP_ENC);
            pauseBmp = DecodeFromBase64(Constants.PAUSE_BMP_ENC);
            resumeBmp = DecodeFromBase64(Constants.RESUME_BMP_ENC);
            clearBmp = DecodeFromBase64(Constants.CLEAR_BMP_ENC);
            clearFiltersBmp = DecodeFromBase64(Constants.CLEAR_FILTERS_BMP_ENC);
            searchBmp = DecodeFromBase64(Constants.SEARCH_BMP_ENC);
        }

        private static Bitmap DecodeFromBase64(string data)
        {
            MemoryStream stream = new(Convert.FromBase64String(data));

            return new Bitmap(stream);
        }

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

        private void BtnSearch_Click(object? sender, EventArgs e)
        {
            OnSearch?.Invoke(sender, e);
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            OnStart?.Invoke(this, EventArgs.Empty);
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            OnStop?.Invoke(this, EventArgs.Empty);
        }

        private void ComboAuthentication_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            ComboBox? senderComboBox = sender as ComboBox;

            if (senderComboBox?.SelectedIndex >= 0)
            {
                int selectedAuthenticationMode = Convert.ToInt32(senderComboBox.SelectedValue);
                if ((Shared.Enums.AuthenticationMode)selectedAuthenticationMode == Shared.Enums.AuthenticationMode.WindowsAuth)
                {
                    tstUser.Visible = false;
                    tslUser.Visible = false;
                    tslPassword.Visible = false;
                    tstPassWord.Visible = false;
                    toolStripSeparator3.Visible = false;
                    toolStripSeparator4.Visible = false;
                }
                else
                {
                    tstUser.Visible = true;
                    tslUser.Visible = true;
                    tslPassword.Visible = true;
                    tstPassWord.Visible = true;
                    toolStripSeparator3.Visible = true;
                    toolStripSeparator4.Visible = true;
                }
            }
        }

        private void CreateEventHandlers()
        {
            Load += MainWindow_Load;
            tsbStart.Click += BtnStart_Click;
            tsbStop.Click += BtnStop_Click;
            dgvEvents.RowEnter += DgvEvents_RowEnter;
            tsbPause.Click += BtnPause_Click;
            tsbResume.Click += BtnResume_Click;
            tsbClearEvents.Click += BtnClearEvents_Click;
            tsbFilters.Click += BtnFilters_Click;
            tsbClearFilters.Click += BtnClearFilters_Click;
            tsbSearch.Click += BtnSearch_Click;
        }

        private void CreateMainMenu()
        {
            MenuStrip ms = new MenuStrip
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem exitMenu = new ToolStripMenuItem("Exit");
            ToolStripMenuItem aboutMenu = new ToolStripMenuItem("About");

            exitMenu.Click += ExitMenu_Click;
            fileMenu.DropDownItems.Add(exitMenu);

            ms.Items.Add(fileMenu);
            ms.Items.Add(aboutMenu);

            Controls.Add(ms);
        }

        private void CreateMainToolBar()
        {
            toolStripMain.Dock = DockStyle.Top;
            toolStripMain.AutoSize = true;

            tslServer.Text = "Server";
            toolStripMain.Items.Add(tslServer);

            tstServer.Size = new Size(150, 27);
            toolStripMain.Items.Add(tstServer);

            ToolStripSeparator toolStripSeparator1 = new ToolStripSeparator();
            toolStripMain.Items.Add(toolStripSeparator1);

            tslAUTH.Text = "Authentication";
            toolStripMain.Items.Add(tslAUTH);

            tscAuthentication.Size = new Size(150, 27);
            tscAuthentication.DropDownStyle = ComboBoxStyle.DropDownList;
            toolStripMain.Items.Add(tscAuthentication);

            ToolStripSeparator toolStripSeparator2 = new ToolStripSeparator();
            toolStripMain.Items.Add(toolStripSeparator2);

            tslUser.Text = "User";
            toolStripMain.Items.Add(tslUser);

            tstUser.Size = new Size(150, 27);
            toolStripMain.Items.Add(tstUser);

            toolStripMain.Items.Add(toolStripSeparator3);

            tslPassword.Text = "Password";
            toolStripMain.Items.Add(tslPassword);

            tstPassWord.Size = new Size(150, 27);
            toolStripMain.Items.Add(tstPassWord);

            toolStripMain.Items.Add(toolStripSeparator4);

            tsbStart.ToolTipText = "Start";
            tsbStart.Text = "Start";
            tsbStart.Image = playBmp;
            toolStripMain.Items.Add(tsbStart);

            tsbPause.ToolTipText = "Pause";
            tsbPause.Text = "Pause";
            tsbPause.Image = pauseBmp;
            toolStripMain.Items.Add(tsbPause);

            tsbResume.ToolTipText = "Resume";
            tsbResume.Text = "Resume";
            tsbResume.Image = resumeBmp;
            toolStripMain.Items.Add(tsbResume);

            tsbStop.ToolTipText = "Stop";
            tsbStop.Text = "Stop";
            tsbStop.Image = stopBmp;
            toolStripMain.Items.Add(tsbStop);

            ToolStripSeparator toolStripSeparator5 = new ToolStripSeparator();
            toolStripMain.Items.Add(toolStripSeparator5);

            tsbClearEvents.ToolTipText = "Clear Events";
            tsbClearEvents.Text = "Clear Events";
            tsbClearEvents.Image = clearBmp;
            toolStripMain.Items.Add(tsbClearEvents);

            ToolStripSeparator toolStripSeparator6 = new ToolStripSeparator();
            toolStripMain.Items.Add(toolStripSeparator6);

            tsbFilters.ToolTipText = "Filters";
            tsbFilters.Text = "Filters";
            toolStripMain.Items.Add(tsbFilters);

            tsbClearFilters.ToolTipText = "Clear Filters";
            tsbClearFilters.Text = "Clear Filters";
            tsbClearFilters.Image = clearFiltersBmp;
            toolStripMain.Items.Add(tsbClearFilters);

            ToolStripSeparator toolStripSeparator7 = new ToolStripSeparator();
            toolStripMain.Items.Add(toolStripSeparator7);

            tstSearch.Size = new Size(100, 27);
            toolStripMain.Items.Add(tstSearch);

            tsbSearch.ToolTipText = "Search";
            tsbSearch.Text = "Search";
            tsbSearch.Image = searchBmp;
            toolStripMain.Items.Add(tsbSearch);

            pnlHeader.Controls.Add(toolStripMain);
        }

        private void DgvEvents_RowEnter(object? sender, DataGridViewCellEventArgs e)
        {
            RowEnter?.Invoke(sender, e);
        }

        private void ExitMenu_Click(object? sender, EventArgs e)
        {
            Application.Exit();
        }

        private void InitializeComboAuthentication()
        {
            tscAuthentication.ComboBox.DisplayMember = "Name";
            tscAuthentication.ComboBox.ValueMember = "Value";
            tscAuthentication.ComboBox.SelectionChangeCommitted += ComboAuthentication_SelectionChangeCommitted;
            tscAuthentication.ComboBox.SelectedIndex = 0;
            ComboAuthentication_SelectionChangeCommitted(tscAuthentication.ComboBox, EventArgs.Empty);
        }

        private void MainWindow_Load(object? sender, EventArgs e)
        {
            CreateBitmaps();
            CreateMainMenu();
            CreateMainToolBar();
            InitializeComboAuthentication();
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