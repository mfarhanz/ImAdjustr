namespace ImAdjustr.Forms {
    partial class CustomMessageDialog {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.customMessageYesButton = new System.Windows.Forms.Button();
            this.customMessageNoButton = new System.Windows.Forms.Button();
            this.customMessageLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // customMessageYesButton
            // 
            this.customMessageYesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.customMessageYesButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.customMessageYesButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.customMessageYesButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.customMessageYesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.customMessageYesButton.Font = new System.Drawing.Font("Verdana", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.customMessageYesButton.ForeColor = System.Drawing.SystemColors.Info;
            this.customMessageYesButton.Location = new System.Drawing.Point(307, 75);
            this.customMessageYesButton.Name = "customMessageYesButton";
            this.customMessageYesButton.Size = new System.Drawing.Size(100, 30);
            this.customMessageYesButton.TabIndex = 14;
            this.customMessageYesButton.Text = "Yes";
            this.customMessageYesButton.UseVisualStyleBackColor = true;
            this.customMessageYesButton.Click += new System.EventHandler(this.customMessageYesButton_Click);
            // 
            // customMessageNoButton
            // 
            this.customMessageNoButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.customMessageNoButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.customMessageNoButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.customMessageNoButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.customMessageNoButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(38)))), ((int)(((byte)(38)))));
            this.customMessageNoButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.customMessageNoButton.Font = new System.Drawing.Font("Verdana", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.customMessageNoButton.ForeColor = System.Drawing.SystemColors.Info;
            this.customMessageNoButton.Location = new System.Drawing.Point(413, 75);
            this.customMessageNoButton.Name = "customMessageNoButton";
            this.customMessageNoButton.Size = new System.Drawing.Size(100, 30);
            this.customMessageNoButton.TabIndex = 15;
            this.customMessageNoButton.Text = "No";
            this.customMessageNoButton.UseVisualStyleBackColor = true;
            // 
            // customMessageLabel
            // 
            this.customMessageLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.customMessageLabel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.customMessageLabel.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.customMessageLabel.ForeColor = System.Drawing.SystemColors.Info;
            this.customMessageLabel.Location = new System.Drawing.Point(0, 0);
            this.customMessageLabel.Margin = new System.Windows.Forms.Padding(5);
            this.customMessageLabel.Name = "customMessageLabel";
            this.customMessageLabel.Padding = new System.Windows.Forms.Padding(10, 10, 10, 0);
            this.customMessageLabel.Size = new System.Drawing.Size(525, 69);
            this.customMessageLabel.TabIndex = 21;
            this.customMessageLabel.Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer commodo nec sem " +
    "in porta. Praesent gravida quis neque ut dapibus. Nullam quis enim eu quam digni" +
    "ssim volutpat at a tortor.";
            this.customMessageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CustomMessageDialog
            // 
            this.AcceptButton = this.customMessageYesButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 27F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.CancelButton = this.customMessageNoButton;
            this.ClientSize = new System.Drawing.Size(525, 111);
            this.Controls.Add(this.customMessageLabel);
            this.Controls.Add(this.customMessageNoButton);
            this.Controls.Add(this.customMessageYesButton);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Cascadia Mono SemiBold", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ForeColor = System.Drawing.SystemColors.Info;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "CustomMessageDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Confirmation";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button customMessageYesButton;
        private System.Windows.Forms.Button customMessageNoButton;
        private System.Windows.Forms.Label customMessageLabel;
    }
}