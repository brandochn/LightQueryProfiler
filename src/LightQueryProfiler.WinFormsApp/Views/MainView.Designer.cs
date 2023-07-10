namespace LightQueryProfiler.WinFormsApp.Views
{
    partial class MainView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            pnlHeader = new FlowLayoutPanel();
            txtServer = new TextBox();
            cboAuthentication = new ComboBox();
            txtUser = new TextBox();
            txtPassword = new TextBox();
            btnStart = new Button();
            btnPause = new Button();
            btnResume = new Button();
            btnStop = new Button();
            btnClearEvents = new Button();
            pnlGrid = new Panel();
            dgvEvents = new DataGridView();
            pnlDetails = new Panel();
            tabControl1 = new TabControl();
            tabPageText = new TabPage();
            tabPageDetails = new TabPage();
            lvDetails = new ListView();
            ColName = new ColumnHeader();
            ColValue = new ColumnHeader();
            btnFilters = new Button();
            btnClearFilters = new Button();
            pnlHeader.SuspendLayout();
            pnlGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEvents).BeginInit();
            pnlDetails.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPageDetails.SuspendLayout();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHeader.Controls.Add(txtServer);
            pnlHeader.Controls.Add(cboAuthentication);
            pnlHeader.Controls.Add(txtUser);
            pnlHeader.Controls.Add(txtPassword);
            pnlHeader.Controls.Add(btnStart);
            pnlHeader.Controls.Add(btnPause);
            pnlHeader.Controls.Add(btnResume);
            pnlHeader.Controls.Add(btnStop);
            pnlHeader.Controls.Add(btnClearEvents);
            pnlHeader.Controls.Add(btnFilters);
            pnlHeader.Controls.Add(btnClearFilters);
            pnlHeader.Location = new Point(3, 1);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(1443, 50);
            pnlHeader.TabIndex = 0;
            // 
            // txtServer
            // 
            txtServer.AcceptsTab = true;
            txtServer.Dock = DockStyle.Top;
            txtServer.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            txtServer.Location = new Point(3, 10);
            txtServer.Margin = new Padding(3, 10, 3, 3);
            txtServer.Name = "txtServer";
            txtServer.PlaceholderText = "Server or IP address";
            txtServer.Size = new Size(200, 27);
            txtServer.TabIndex = 0;
            txtServer.WordWrap = false;
            // 
            // cboAuthentication
            // 
            cboAuthentication.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            cboAuthentication.FormattingEnabled = true;
            cboAuthentication.Location = new Point(209, 11);
            cboAuthentication.Margin = new Padding(3, 11, 3, 3);
            cboAuthentication.Name = "cboAuthentication";
            cboAuthentication.Size = new Size(200, 25);
            cboAuthentication.TabIndex = 1;
            // 
            // txtUser
            // 
            txtUser.AcceptsTab = true;
            txtUser.Dock = DockStyle.Top;
            txtUser.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            txtUser.Location = new Point(415, 10);
            txtUser.Margin = new Padding(3, 10, 3, 3);
            txtUser.Name = "txtUser";
            txtUser.PlaceholderText = "User Name";
            txtUser.Size = new Size(200, 27);
            txtUser.TabIndex = 3;
            txtUser.Visible = false;
            txtUser.WordWrap = false;
            // 
            // txtPassword
            // 
            txtPassword.AcceptsTab = true;
            txtPassword.Dock = DockStyle.Top;
            txtPassword.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            txtPassword.Location = new Point(621, 10);
            txtPassword.Margin = new Padding(3, 10, 3, 3);
            txtPassword.Name = "txtPassword";
            txtPassword.PlaceholderText = "Password";
            txtPassword.Size = new Size(200, 27);
            txtPassword.TabIndex = 4;
            txtPassword.Visible = false;
            txtPassword.WordWrap = false;
            // 
            // btnStart
            // 
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.Location = new Point(827, 10);
            btnStart.Margin = new Padding(3, 10, 3, 3);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 27);
            btnStart.TabIndex = 2;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            // 
            // btnPause
            // 
            btnPause.FlatStyle = FlatStyle.Flat;
            btnPause.Location = new Point(908, 10);
            btnPause.Margin = new Padding(3, 10, 3, 3);
            btnPause.Name = "btnPause";
            btnPause.Size = new Size(75, 27);
            btnPause.TabIndex = 7;
            btnPause.Text = "Pause";
            btnPause.UseVisualStyleBackColor = true;
            // 
            // btnResume
            // 
            btnResume.FlatStyle = FlatStyle.Flat;
            btnResume.Location = new Point(989, 10);
            btnResume.Margin = new Padding(3, 10, 3, 3);
            btnResume.Name = "btnResume";
            btnResume.Size = new Size(75, 27);
            btnResume.TabIndex = 6;
            btnResume.Text = "Resume";
            btnResume.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.Location = new Point(1070, 10);
            btnStop.Margin = new Padding(3, 10, 3, 3);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 27);
            btnStop.TabIndex = 5;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            // 
            // btnClearEvents
            // 
            btnClearEvents.FlatStyle = FlatStyle.Flat;
            btnClearEvents.Location = new Point(1151, 10);
            btnClearEvents.Margin = new Padding(3, 10, 3, 3);
            btnClearEvents.Name = "btnClearEvents";
            btnClearEvents.Size = new Size(85, 27);
            btnClearEvents.TabIndex = 8;
            btnClearEvents.Text = "Clear Events";
            btnClearEvents.UseVisualStyleBackColor = true;
            // 
            // pnlGrid
            // 
            pnlGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlGrid.Controls.Add(dgvEvents);
            pnlGrid.Location = new Point(6, 57);
            pnlGrid.Name = "pnlGrid";
            pnlGrid.Size = new Size(1440, 317);
            pnlGrid.TabIndex = 1;
            // 
            // dgvEvents
            // 
            dgvEvents.AllowUserToAddRows = false;
            dgvEvents.AllowUserToDeleteRows = false;
            dgvEvents.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(224, 224, 224);
            dgvEvents.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvEvents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEvents.Dock = DockStyle.Fill;
            dgvEvents.Location = new Point(0, 0);
            dgvEvents.Name = "dgvEvents";
            dgvEvents.ReadOnly = true;
            dgvEvents.Size = new Size(1440, 317);
            dgvEvents.TabIndex = 0;
            // 
            // pnlDetails
            // 
            pnlDetails.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlDetails.Controls.Add(tabControl1);
            pnlDetails.Location = new Point(6, 380);
            pnlDetails.Name = "pnlDetails";
            pnlDetails.Size = new Size(1440, 347);
            pnlDetails.TabIndex = 2;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageText);
            tabControl1.Controls.Add(tabPageDetails);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1440, 347);
            tabControl1.TabIndex = 0;
            // 
            // tabPageText
            // 
            tabPageText.Location = new Point(4, 24);
            tabPageText.Name = "tabPageText";
            tabPageText.Padding = new Padding(3);
            tabPageText.Size = new Size(1432, 319);
            tabPageText.TabIndex = 0;
            tabPageText.Text = "Text";
            tabPageText.UseVisualStyleBackColor = true;
            // 
            // tabPageDetails
            // 
            tabPageDetails.Controls.Add(lvDetails);
            tabPageDetails.Location = new Point(4, 24);
            tabPageDetails.Name = "tabPageDetails";
            tabPageDetails.Padding = new Padding(3);
            tabPageDetails.Size = new Size(1270, 319);
            tabPageDetails.TabIndex = 1;
            tabPageDetails.Text = "Details";
            tabPageDetails.UseVisualStyleBackColor = true;
            // 
            // lvDetails
            // 
            lvDetails.Columns.AddRange(new ColumnHeader[] { ColName, ColValue });
            lvDetails.Dock = DockStyle.Fill;
            lvDetails.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            lvDetails.FullRowSelect = true;
            lvDetails.Location = new Point(3, 3);
            lvDetails.Name = "lvDetails";
            lvDetails.Size = new Size(1264, 313);
            lvDetails.TabIndex = 0;
            lvDetails.UseCompatibleStateImageBehavior = false;
            lvDetails.View = View.Details;
            // 
            // ColName
            // 
            ColName.Text = "Name";
            ColName.Width = 150;
            // 
            // ColValue
            // 
            ColValue.Text = "Value";
            ColValue.Width = 500;
            // 
            // btnFilters
            // 
            btnFilters.FlatStyle = FlatStyle.Flat;
            btnFilters.Location = new Point(1242, 10);
            btnFilters.Margin = new Padding(3, 10, 3, 3);
            btnFilters.Name = "btnFilters";
            btnFilters.Size = new Size(75, 27);
            btnFilters.TabIndex = 9;
            btnFilters.Text = "Filters";
            btnFilters.UseVisualStyleBackColor = true;
            // 
            // btnClearFilters
            // 
            btnClearFilters.FlatStyle = FlatStyle.Flat;
            btnClearFilters.Location = new Point(1323, 10);
            btnClearFilters.Margin = new Padding(3, 10, 3, 3);
            btnClearFilters.Name = "btnClearFilters";
            btnClearFilters.Size = new Size(82, 27);
            btnClearFilters.TabIndex = 10;
            btnClearFilters.Text = "Clear Filters";
            btnClearFilters.UseVisualStyleBackColor = true;
            // 
            // MainView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1449, 729);
            Controls.Add(pnlDetails);
            Controls.Add(pnlGrid);
            Controls.Add(pnlHeader);
            Name = "MainView";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ProfilerWindow";
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            pnlGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEvents).EndInit();
            pnlDetails.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPageDetails.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private FlowLayoutPanel pnlHeader;
        private TextBox txtServer;
        private ComboBox cboAuthentication;
        private Button btnStart;
        private Panel pnlGrid;
        private Panel pnlDetails;
        private DataGridView dgvEvents;
        private TabControl tabControl1;
        private TabPage tabPageText;
        private TabPage tabPageDetails;
        private TextBox txtUser;
        private TextBox txtPassword;
        private Button btnStop;
        private ListView lvDetails;
        private ColumnHeader ColName;
        private ColumnHeader ColValue;
        private Button btnResume;
        private Button btnPause;
        private Button btnClearEvents;
        private Button btnFilters;
        private Button btnClearFilters;
    }
}