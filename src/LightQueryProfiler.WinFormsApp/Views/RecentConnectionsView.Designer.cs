namespace LightQueryProfiler.WinFormsApp.Views
{
    partial class RecentConnectionsView
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
            txtSearch = new TextBox();
            lblSearch = new Label();
            dgvConnections = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dgvConnections).BeginInit();
            SuspendLayout();
            // 
            // txtSearch
            // 
            txtSearch.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtSearch.Location = new Point(9, 23);
            txtSearch.Name = "txtSearch";
            txtSearch.PlaceholderText = "Contains";
            txtSearch.Size = new Size(355, 25);
            txtSearch.TabIndex = 3;
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = true;
            lblSearch.Location = new Point(9, 5);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(42, 15);
            lblSearch.TabIndex = 2;
            lblSearch.Text = "Search";
            // 
            // dgvConnections
            // 
            dgvConnections.AllowUserToAddRows = false;
            dgvConnections.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(224, 224, 224);
            dgvConnections.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvConnections.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvConnections.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvConnections.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvConnections.Location = new Point(9, 59);
            dgvConnections.MultiSelect = false;
            dgvConnections.Name = "dgvConnections";
            dgvConnections.ReadOnly = true;
            dgvConnections.RowHeadersWidth = 62;
            dgvConnections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvConnections.Size = new Size(759, 314);
            dgvConnections.TabIndex = 4;
            // 
            // RecentConnectionsView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 412);
            Controls.Add(dgvConnections);
            Controls.Add(txtSearch);
            Controls.Add(lblSearch);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "RecentConnectionsView";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Recent Connections";
            ((System.ComponentModel.ISupportInitialize)dgvConnections).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtSearch;
        private Label lblSearch;
        private DataGridView dgvConnections;
    }
}