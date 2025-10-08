namespace DeviceBridge
{
    partial class MainForm
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
            this.StatusLabel = new System.Windows.Forms.Label();
            this.TitleLabel = new System.Windows.Forms.Label();
            this.StatusGroupBox = new System.Windows.Forms.GroupBox();
            this.ServerUrlLabel = new System.Windows.Forms.Label();
            this.AboutButton = new System.Windows.Forms.Button();
            this.MinimizeToTrayButton = new System.Windows.Forms.Button();
            this.ActionsGroupBox = new System.Windows.Forms.GroupBox();
            this.StatusGroupBox.SuspendLayout();
            this.ActionsGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // StatusLabel
            // 
            this.StatusLabel.AutoSize = true;
            this.StatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StatusLabel.Location = new System.Drawing.Point(15, 25);
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(150, 15);
            this.StatusLabel.TabIndex = 0;
            this.StatusLabel.Text = "Initializing Device Bridge...";
            // 
            // TitleLabel
            // 
            this.TitleLabel.AutoSize = true;
            this.TitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TitleLabel.Location = new System.Drawing.Point(20, 20);
            this.TitleLabel.Name = "TitleLabel";
            this.TitleLabel.Size = new System.Drawing.Size(141, 24);
            this.TitleLabel.TabIndex = 5;
            this.TitleLabel.Text = "Device Bridge";
            // 
            // StatusGroupBox
            // 
            this.StatusGroupBox.Controls.Add(this.ServerUrlLabel);
            this.StatusGroupBox.Controls.Add(this.StatusLabel);
            this.StatusGroupBox.Location = new System.Drawing.Point(20, 60);
            this.StatusGroupBox.Name = "StatusGroupBox";
            this.StatusGroupBox.Size = new System.Drawing.Size(400, 100);
            this.StatusGroupBox.TabIndex = 6;
            this.StatusGroupBox.TabStop = false;
            this.StatusGroupBox.Text = "Service Status";
            // 
            // ServerUrlLabel
            // 
            this.ServerUrlLabel.AutoSize = true;
            this.ServerUrlLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ServerUrlLabel.ForeColor = System.Drawing.Color.Gray;
            this.ServerUrlLabel.Location = new System.Drawing.Point(15, 50);
            this.ServerUrlLabel.Name = "ServerUrlLabel";
            this.ServerUrlLabel.Size = new System.Drawing.Size(0, 13);
            this.ServerUrlLabel.TabIndex = 2;
            // 
            // AboutButton
            // 
            this.AboutButton.Location = new System.Drawing.Point(15, 78);
            this.AboutButton.Name = "AboutButton";
            this.AboutButton.Size = new System.Drawing.Size(120, 30);
            this.AboutButton.TabIndex = 4;
            this.AboutButton.Text = "About";
            this.AboutButton.UseVisualStyleBackColor = true;
            this.AboutButton.Click += new System.EventHandler(this.AboutButton_Click);
            // 
            // MinimizeToTrayButton
            // 
            this.MinimizeToTrayButton.Location = new System.Drawing.Point(15, 25);
            this.MinimizeToTrayButton.Name = "MinimizeToTrayButton";
            this.MinimizeToTrayButton.Size = new System.Drawing.Size(120, 30);
            this.MinimizeToTrayButton.TabIndex = 1;
            this.MinimizeToTrayButton.Text = "Minimize to Tray";
            this.MinimizeToTrayButton.UseVisualStyleBackColor = true;
            this.MinimizeToTrayButton.Click += new System.EventHandler(this.MinimizeToTrayButton_Click);
            // 
            // ActionsGroupBox
            // 
            this.ActionsGroupBox.Controls.Add(this.MinimizeToTrayButton);
            this.ActionsGroupBox.Controls.Add(this.AboutButton);
            this.ActionsGroupBox.Location = new System.Drawing.Point(20, 180);
            this.ActionsGroupBox.Name = "ActionsGroupBox";
            this.ActionsGroupBox.Size = new System.Drawing.Size(400, 133);
            this.ActionsGroupBox.TabIndex = 7;
            this.ActionsGroupBox.TabStop = false;
            this.ActionsGroupBox.Text = "Actions";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(450, 336);
            this.Controls.Add(this.ActionsGroupBox);
            this.Controls.Add(this.StatusGroupBox);
            this.Controls.Add(this.TitleLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Device Bridge - Fingerprint Service";
            this.StatusGroupBox.ResumeLayout(false);
            this.StatusGroupBox.PerformLayout();
            this.ActionsGroupBox.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label StatusLabel;
        private System.Windows.Forms.Label TitleLabel;
        private System.Windows.Forms.GroupBox StatusGroupBox;
        private System.Windows.Forms.Label ServerUrlLabel;
        private System.Windows.Forms.Button AboutButton;
        private System.Windows.Forms.Button MinimizeToTrayButton;
        private System.Windows.Forms.GroupBox ActionsGroupBox;
    }
}
