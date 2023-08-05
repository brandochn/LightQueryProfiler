namespace LightQueryProfiler.WinFormsApp.Views
{
    partial class FiltersView
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
            lblEventClass = new Label();
            txtEventClass = new TextBox();
            txtTextData = new TextBox();
            label2 = new Label();
            txtApplicationName = new TextBox();
            label3 = new Label();
            txtNTUserName = new TextBox();
            label4 = new Label();
            txtLoginName = new TextBox();
            label5 = new Label();
            txtDatabaseName = new TextBox();
            label6 = new Label();
            btnApply = new Button();
            btnClose = new Button();
            SuspendLayout();
            // 
            // lblEventClass
            // 
            lblEventClass.AutoSize = true;
            lblEventClass.Location = new Point(19, 25);
            lblEventClass.Name = "lblEventClass";
            lblEventClass.Size = new Size(63, 15);
            lblEventClass.TabIndex = 0;
            lblEventClass.Text = "EventClass";
            // 
            // txtEventClass
            // 
            txtEventClass.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtEventClass.Location = new Point(19, 43);
            txtEventClass.Name = "txtEventClass";
            txtEventClass.PlaceholderText = "Contains";
            txtEventClass.Size = new Size(355, 25);
            txtEventClass.TabIndex = 1;
            // 
            // txtTextData
            // 
            txtTextData.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtTextData.Location = new Point(19, 97);
            txtTextData.Name = "txtTextData";
            txtTextData.PlaceholderText = "Contains";
            txtTextData.Size = new Size(355, 25);
            txtTextData.TabIndex = 3;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(19, 79);
            label2.Name = "label2";
            label2.Size = new Size(52, 15);
            label2.TabIndex = 2;
            label2.Text = "TextData";
            // 
            // txtApplicationName
            // 
            txtApplicationName.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtApplicationName.Location = new Point(19, 154);
            txtApplicationName.Name = "txtApplicationName";
            txtApplicationName.PlaceholderText = "Contains";
            txtApplicationName.Size = new Size(355, 25);
            txtApplicationName.TabIndex = 5;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(19, 136);
            label3.Name = "label3";
            label3.Size = new Size(100, 15);
            label3.TabIndex = 4;
            label3.Text = "ApplicationName";
            // 
            // txtNTUserName
            // 
            txtNTUserName.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtNTUserName.Location = new Point(19, 212);
            txtNTUserName.Name = "txtNTUserName";
            txtNTUserName.PlaceholderText = "Contains";
            txtNTUserName.Size = new Size(355, 25);
            txtNTUserName.TabIndex = 7;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(19, 194);
            label4.Name = "label4";
            label4.Size = new Size(77, 15);
            label4.TabIndex = 6;
            label4.Text = "NTUserName";
            // 
            // txtLoginName
            // 
            txtLoginName.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtLoginName.Location = new Point(19, 268);
            txtLoginName.Name = "txtLoginName";
            txtLoginName.PlaceholderText = "Contains";
            txtLoginName.Size = new Size(355, 25);
            txtLoginName.TabIndex = 9;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(19, 250);
            label5.Name = "label5";
            label5.Size = new Size(69, 15);
            label5.TabIndex = 8;
            label5.Text = "LoginName";
            // 
            // txtDatabaseName
            // 
            txtDatabaseName.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            txtDatabaseName.Location = new Point(19, 324);
            txtDatabaseName.Name = "txtDatabaseName";
            txtDatabaseName.PlaceholderText = "Contains";
            txtDatabaseName.Size = new Size(355, 25);
            txtDatabaseName.TabIndex = 11;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(19, 306);
            label6.Name = "label6";
            label6.Size = new Size(87, 15);
            label6.TabIndex = 10;
            label6.Text = "DatabaseName";
            // 
            // btnApply
            // 
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.Location = new Point(19, 372);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(75, 30);
            btnApply.TabIndex = 12;
            btnApply.Text = "Apply";
            btnApply.UseVisualStyleBackColor = true;
            // 
            // btnClose
            // 
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Location = new Point(100, 372);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(75, 30);
            btnClose.TabIndex = 13;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            // 
            // FiltersView
            // 
            AcceptButton = btnApply;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnClose;
            ClientSize = new Size(392, 422);
            Controls.Add(btnClose);
            Controls.Add(btnApply);
            Controls.Add(txtDatabaseName);
            Controls.Add(label6);
            Controls.Add(txtLoginName);
            Controls.Add(label5);
            Controls.Add(txtNTUserName);
            Controls.Add(label4);
            Controls.Add(txtApplicationName);
            Controls.Add(label3);
            Controls.Add(txtTextData);
            Controls.Add(label2);
            Controls.Add(txtEventClass);
            Controls.Add(lblEventClass);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FiltersView";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Filters";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblEventClass;
        private TextBox txtEventClass;
        private TextBox txtTextData;
        private Label label2;
        private TextBox txtApplicationName;
        private Label label3;
        private TextBox txtNTUserName;
        private Label label4;
        private TextBox txtLoginName;
        private Label label5;
        private TextBox txtDatabaseName;
        private Label label6;
        private Button btnApply;
        private Button btnClose;
    }
}