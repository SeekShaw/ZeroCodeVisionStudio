namespace Demo.SDK.Halcon
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pictureBox1 = new PictureBox();
            cbcamlist = new ComboBox();
            bnreflush = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(11, 16);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(305, 253);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // cbcamlist
            // 
            cbcamlist.FormattingEnabled = true;
            cbcamlist.Location = new Point(322, 16);
            cbcamlist.Name = "cbcamlist";
            cbcamlist.Size = new Size(203, 25);
            cbcamlist.TabIndex = 1;
            // 
            // bnreflush
            // 
            bnreflush.Location = new Point(536, 16);
            bnreflush.Name = "bnreflush";
            bnreflush.Size = new Size(72, 25);
            bnreflush.TabIndex = 2;
            bnreflush.Text = "刷新";
            bnreflush.UseVisualStyleBackColor = true;
            bnreflush.Click += bnreflush_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(620, 283);
            Controls.Add(bnreflush);
            Controls.Add(cbcamlist);
            Controls.Add(pictureBox1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox pictureBox1;
        private ComboBox cbcamlist;
        private Button bnreflush;
    }
}
