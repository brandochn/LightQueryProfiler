namespace LightQueryProfiler.WinFormsApp.Views
{
    partial class AboutView
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
            lblAppName = new Label();
            lblVersion = new Label();
            lkbIcons = new LinkLabel();
            btnOK = new Button();
            lblDescription = new Label();
            lkbLicense = new LinkLabel();
            SuspendLayout();
            // 
            // lblAppName
            // 
            lblAppName.AutoSize = true;
            lblAppName.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            lblAppName.Location = new Point(66, 21);
            lblAppName.Name = "lblAppName";
            lblAppName.Size = new Size(241, 32);
            lblAppName.TabIndex = 0;
            lblAppName.Text = "Light Query Profiler";
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point);
            lblVersion.Location = new Point(118, 65);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(127, 25);
            lblVersion.TabIndex = 1;
            lblVersion.Text = "Version 1.0.0";
            // 
            // lkbIcons
            // 
            lkbIcons.AutoSize = true;
            lkbIcons.Location = new Point(147, 235);
            lkbIcons.Name = "lkbIcons";
            lkbIcons.Size = new Size(115, 15);
            lkbIcons.TabIndex = 2;
            lkbIcons.TabStop = true;
            lkbIcons.Text = "Icons by icons8.com";
            // 
            // btnOK
            // 
            btnOK.Location = new Point(291, 220);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(75, 30);
            btnOK.TabIndex = 3;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            // 
            // lblDescription
            // 
            lblDescription.AutoSize = true;
            lblDescription.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            lblDescription.Location = new Point(44, 130);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(278, 17);
            lblDescription.TabIndex = 4;
            lblDescription.Text = "Copyright (c) 2022 Hildebrando Chávez Núñez";
            // 
            // lkbLicense
            // 
            lkbLicense.AutoSize = true;
            lkbLicense.Location = new Point(12, 235);
            lkbLicense.Name = "lkbLicense";
            lkbLicense.Size = new Size(123, 15);
            lkbLicense.TabIndex = 5;
            lkbLicense.TabStop = true;
            lkbLicense.Text = "Licensing Information";
            // 
            // AboutView
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnOK;
            ClientSize = new Size(378, 261);
            Controls.Add(lkbLicense);
            Controls.Add(lblDescription);
            Controls.Add(btnOK);
            Controls.Add(lkbIcons);
            Controls.Add(lblVersion);
            Controls.Add(lblAppName);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutView";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "About";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblAppName;
        private Label lblVersion;
        private LinkLabel lkbIcons;
        private Button btnOK;
        private Label lblDescription;
        private LinkLabel lkbLicense;
    }
}