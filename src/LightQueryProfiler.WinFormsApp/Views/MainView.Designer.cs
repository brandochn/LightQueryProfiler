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
            splitContainer = new SplitContainer();
            dgvEvents = new DataGridView();
            tabControl1 = new TabControl();
            tabPageText = new TabPage();
            btnCopyToClipboard = new Button();
            tabPageDetails = new TabPage();
            lvDetails = new ListView();
            ColName = new ColumnHeader();
            ColValue = new ColumnHeader();
            statusBar = new StatusStrip();
            toolStripStatusEvents = new ToolStripStatusLabel();
            pnlHeader = new FlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEvents).BeginInit();
            tabControl1.SuspendLayout();
            tabPageDetails.SuspendLayout();
            statusBar.SuspendLayout();
            SuspendLayout();
            //
            // splitContainer
            //
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 40);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            //
            // splitContainer.Panel1
            //
            splitContainer.Panel1.Controls.Add(dgvEvents);
            //
            // splitContainer.Panel2
            //
            splitContainer.Panel2.Controls.Add(tabControl1);
            splitContainer.Panel2.Controls.Add(statusBar);
            splitContainer.Size = new Size(1774, 689);
            splitContainer.SplitterDistance = 342;
            splitContainer.TabIndex = 3;
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
            dgvEvents.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvEvents.Size = new Size(1774, 342);
            dgvEvents.TabIndex = 1;
            //
            // tabControl1
            //
            tabControl1.Controls.Add(tabPageText);
            tabControl1.Controls.Add(tabPageDetails);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1774, 321);
            tabControl1.TabIndex = 3;
            //
            // tabPageText
            //
            tabPageText.Controls.Add(btnCopyToClipboard);
            tabPageText.Location = new Point(4, 24);
            tabPageText.Name = "tabPageText";
            tabPageText.Padding = new Padding(3);
            tabPageText.Size = new Size(1766, 293);
            tabPageText.TabIndex = 0;
            tabPageText.Text = "Text";
            tabPageText.UseVisualStyleBackColor = true;
            //
            // btnCopyToClipboard
            //
            btnCopyToClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopyToClipboard.Location = new Point(1665, 6);
            btnCopyToClipboard.Name = "btnCopyToClipboard";
            btnCopyToClipboard.Size = new Size(75, 28);
            btnCopyToClipboard.TabIndex = 0;
            btnCopyToClipboard.Text = "Copy";
            btnCopyToClipboard.UseVisualStyleBackColor = true;
            //
            // tabPageDetails
            //
            tabPageDetails.Controls.Add(lvDetails);
            tabPageDetails.Location = new Point(4, 24);
            tabPageDetails.Name = "tabPageDetails";
            tabPageDetails.Padding = new Padding(3);
            tabPageDetails.Size = new Size(1766, 293);
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
            lvDetails.Size = new Size(1760, 287);
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
            // statusBar
            //
            statusBar.Items.AddRange(new ToolStripItem[] { toolStripStatusEvents });
            statusBar.Location = new Point(0, 321);
            statusBar.Name = "statusBar";
            statusBar.Size = new Size(1774, 22);
            statusBar.TabIndex = 2;
            statusBar.Text = "statusStrip1";
            //
            // toolStripStatusEvents
            //
            toolStripStatusEvents.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            toolStripStatusEvents.Name = "toolStripStatusEvents";
            toolStripStatusEvents.Size = new Size(48, 17);
            toolStripStatusEvents.Text = "Events:";
            //
            // pnlHeader
            //
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(1774, 40);
            pnlHeader.TabIndex = 0;
            //
            // MainView
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1774, 729);
            Controls.Add(splitContainer);
            Controls.Add(pnlHeader);
            Name = "MainView";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Light Query Profiler";
            WindowState = FormWindowState.Maximized;
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEvents).EndInit();
            tabControl1.ResumeLayout(false);
            tabPageDetails.ResumeLayout(false);
            statusBar.ResumeLayout(false);
            statusBar.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private SplitContainer splitContainer;
        private DataGridView dgvEvents;
        private TabControl tabControl1;
        private TabPage tabPageText;
        private Button btnCopyToClipboard;
        private TabPage tabPageDetails;
        private ListView lvDetails;
        private ColumnHeader ColName;
        private ColumnHeader ColValue;
        private StatusStrip statusBar;
        private ToolStripStatusLabel toolStripStatusEvents;
        private FlowLayoutPanel pnlHeader;
    }
}
