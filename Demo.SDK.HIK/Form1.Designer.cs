namespace Demo.SDK.HIK
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            bnclosecamera = new Button();
            dispwin = new HalconDotNet.HSmartWindowControl();
            cbcameralist = new ComboBox();
            bnreflush = new Button();
            bnopencamera = new Button();
            bnsingletrigger = new Button();
            cbtriggermode = new ComboBox();
            cbtriggersource = new ComboBox();
            RadRealTime = new RadioButton();
            RadTrigger = new RadioButton();
            bncontinue = new Button();
            bnstop = new Button();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            bnsavefile = new Button();
            bnopenfile = new Button();
            tabPage2 = new TabPage();
            bndrawrectange2 = new Button();
            bndrawrectange1 = new Button();
            bndrawcircle = new Button();
            bncalibcontunie = new Button();
            bncalib = new Button();
            txtangEnd = new TextBox();
            txtscore = new TextBox();
            txtnumlevel = new TextBox();
            label6 = new Label();
            txtmatchnums = new TextBox();
            label5 = new Label();
            txtoverlay = new TextBox();
            label4 = new Label();
            txtangStart = new TextBox();
            label7 = new Label();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            SuspendLayout();
            // 
            // bnclosecamera
            // 
            bnclosecamera.Location = new Point(284, 68);
            bnclosecamera.Margin = new Padding(5, 6, 5, 6);
            bnclosecamera.Name = "bnclosecamera";
            bnclosecamera.Size = new Size(238, 39);
            bnclosecamera.TabIndex = 0;
            bnclosecamera.Text = "关闭相机";
            bnclosecamera.UseVisualStyleBackColor = true;
            bnclosecamera.Click += bnclosecamera_Click;
            // 
            // dispwin
            // 
            dispwin.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            dispwin.AutoValidate = AutoValidate.EnableAllowFocusChange;
            dispwin.HDoubleClickToFitContent = true;
            dispwin.HDrawingObjectsModifier = HalconDotNet.HSmartWindowControl.DrawingObjectsModifier.None;
            dispwin.HImagePart = new Rectangle(-65, 3, 769, 471);
            dispwin.HKeepAspectRatio = true;
            dispwin.HMoveContent = true;
            dispwin.HZoomContent = HalconDotNet.HSmartWindowControl.ZoomContent.WheelForwardZoomsIn;
            dispwin.Location = new Point(4, 11);
            dispwin.Margin = new Padding(0);
            dispwin.Name = "dispwin";
            dispwin.Size = new Size(616, 505);
            dispwin.TabIndex = 0;
            dispwin.WindowSize = new Size(616, 505);
            // 
            // cbcameralist
            // 
            cbcameralist.FormattingEnabled = true;
            cbcameralist.Location = new Point(40, 25);
            cbcameralist.Margin = new Padding(5, 6, 5, 6);
            cbcameralist.Name = "cbcameralist";
            cbcameralist.Size = new Size(372, 28);
            cbcameralist.TabIndex = 2;
            // 
            // bnreflush
            // 
            bnreflush.Location = new Point(422, 20);
            bnreflush.Margin = new Padding(5, 6, 5, 6);
            bnreflush.Name = "bnreflush";
            bnreflush.Size = new Size(100, 39);
            bnreflush.TabIndex = 0;
            bnreflush.Text = "刷新";
            bnreflush.UseVisualStyleBackColor = true;
            bnreflush.Click += bnreflush_Click;
            // 
            // bnopencamera
            // 
            bnopencamera.Location = new Point(40, 69);
            bnopencamera.Margin = new Padding(5, 6, 5, 6);
            bnopencamera.Name = "bnopencamera";
            bnopencamera.Size = new Size(238, 39);
            bnopencamera.TabIndex = 0;
            bnopencamera.Text = "打开相机";
            bnopencamera.UseVisualStyleBackColor = true;
            bnopencamera.Click += bnopencamera_Click;
            // 
            // bnsingletrigger
            // 
            bnsingletrigger.Location = new Point(40, 225);
            bnsingletrigger.Margin = new Padding(5, 6, 5, 6);
            bnsingletrigger.Name = "bnsingletrigger";
            bnsingletrigger.Size = new Size(143, 33);
            bnsingletrigger.TabIndex = 4;
            bnsingletrigger.Text = "单次触发";
            bnsingletrigger.UseVisualStyleBackColor = true;
            bnsingletrigger.Click += bnsingletrigger_Click;
            // 
            // cbtriggermode
            // 
            cbtriggermode.FormattingEnabled = true;
            cbtriggermode.Location = new Point(40, 268);
            cbtriggermode.Margin = new Padding(5, 6, 5, 6);
            cbtriggermode.Name = "cbtriggermode";
            cbtriggermode.Size = new Size(481, 28);
            cbtriggermode.TabIndex = 2;
            // 
            // cbtriggersource
            // 
            cbtriggersource.FormattingEnabled = true;
            cbtriggersource.Location = new Point(40, 311);
            cbtriggersource.Margin = new Padding(5, 6, 5, 6);
            cbtriggersource.Name = "cbtriggersource";
            cbtriggersource.Size = new Size(481, 28);
            cbtriggersource.TabIndex = 2;
            // 
            // RadRealTime
            // 
            RadRealTime.AutoSize = true;
            RadRealTime.Location = new Point(130, 159);
            RadRealTime.Margin = new Padding(4, 4, 4, 4);
            RadRealTime.Name = "RadRealTime";
            RadRealTime.Size = new Size(60, 24);
            RadRealTime.TabIndex = 5;
            RadRealTime.TabStop = true;
            RadRealTime.Text = "实时";
            RadRealTime.UseVisualStyleBackColor = true;
            RadRealTime.CheckedChanged += RadRealTime_CheckedChanged;
            // 
            // RadTrigger
            // 
            RadTrigger.AutoSize = true;
            RadTrigger.Location = new Point(284, 159);
            RadTrigger.Margin = new Padding(4, 4, 4, 4);
            RadTrigger.Name = "RadTrigger";
            RadTrigger.Size = new Size(60, 24);
            RadTrigger.TabIndex = 5;
            RadTrigger.TabStop = true;
            RadTrigger.Text = "触发";
            RadTrigger.UseVisualStyleBackColor = true;
            RadTrigger.CheckedChanged += RadTrigger_CheckedChanged;
            // 
            // bncontinue
            // 
            bncontinue.Location = new Point(198, 225);
            bncontinue.Margin = new Padding(4, 4, 4, 4);
            bncontinue.Name = "bncontinue";
            bncontinue.Size = new Size(143, 33);
            bncontinue.TabIndex = 6;
            bncontinue.Text = "连续采集";
            bncontinue.UseVisualStyleBackColor = true;
            bncontinue.Click += bncontinue_Click;
            // 
            // bnstop
            // 
            bnstop.Location = new Point(356, 225);
            bnstop.Margin = new Padding(4, 4, 4, 4);
            bnstop.Name = "bnstop";
            bnstop.Size = new Size(143, 33);
            bnstop.TabIndex = 6;
            bnstop.Text = "停止";
            bnstop.UseVisualStyleBackColor = true;
            bnstop.Click += bnstop_Click;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(624, 11);
            tabControl1.Margin = new Padding(4, 4, 4, 4);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(562, 725);
            tabControl1.TabIndex = 7;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(bnclosecamera);
            tabPage1.Controls.Add(bnstop);
            tabPage1.Controls.Add(bnsavefile);
            tabPage1.Controls.Add(bnopenfile);
            tabPage1.Controls.Add(bnopencamera);
            tabPage1.Controls.Add(bncontinue);
            tabPage1.Controls.Add(bnreflush);
            tabPage1.Controls.Add(RadTrigger);
            tabPage1.Controls.Add(cbcameralist);
            tabPage1.Controls.Add(RadRealTime);
            tabPage1.Controls.Add(cbtriggermode);
            tabPage1.Controls.Add(bnsingletrigger);
            tabPage1.Controls.Add(cbtriggersource);
            tabPage1.Location = new Point(4, 29);
            tabPage1.Margin = new Padding(4, 4, 4, 4);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(4, 4, 4, 4);
            tabPage1.Size = new Size(554, 692);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "相机配置";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // bnsavefile
            // 
            bnsavefile.Location = new Point(288, 352);
            bnsavefile.Margin = new Padding(5, 6, 5, 6);
            bnsavefile.Name = "bnsavefile";
            bnsavefile.Size = new Size(238, 39);
            bnsavefile.TabIndex = 0;
            bnsavefile.Text = "保存图像";
            bnsavefile.UseVisualStyleBackColor = true;
            bnsavefile.Click += bnsavefile_Click;
            // 
            // bnopenfile
            // 
            bnopenfile.Location = new Point(40, 352);
            bnopenfile.Margin = new Padding(5, 6, 5, 6);
            bnopenfile.Name = "bnopenfile";
            bnopenfile.Size = new Size(238, 39);
            bnopenfile.TabIndex = 0;
            bnopenfile.Text = "加载图像";
            bnopenfile.UseVisualStyleBackColor = true;
            bnopenfile.Click += bnopenfile_Click;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(bndrawrectange2);
            tabPage2.Controls.Add(bndrawrectange1);
            tabPage2.Controls.Add(bndrawcircle);
            tabPage2.Controls.Add(bncalibcontunie);
            tabPage2.Controls.Add(bncalib);
            tabPage2.Controls.Add(txtangEnd);
            tabPage2.Controls.Add(txtscore);
            tabPage2.Controls.Add(txtnumlevel);
            tabPage2.Controls.Add(label6);
            tabPage2.Controls.Add(txtmatchnums);
            tabPage2.Controls.Add(label5);
            tabPage2.Controls.Add(txtoverlay);
            tabPage2.Controls.Add(label4);
            tabPage2.Controls.Add(txtangStart);
            tabPage2.Controls.Add(label7);
            tabPage2.Controls.Add(label3);
            tabPage2.Controls.Add(label2);
            tabPage2.Controls.Add(label1);
            tabPage2.Location = new Point(4, 29);
            tabPage2.Margin = new Padding(4, 4, 4, 4);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(4, 4, 4, 4);
            tabPage2.Size = new Size(626, 682);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "模板定位";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // bndrawrectange2
            // 
            bndrawrectange2.Location = new Point(287, 271);
            bndrawrectange2.Margin = new Padding(4, 4, 4, 4);
            bndrawrectange2.Name = "bndrawrectange2";
            bndrawrectange2.Size = new Size(118, 32);
            bndrawrectange2.TabIndex = 3;
            bndrawrectange2.Text = "绘制矩形2";
            bndrawrectange2.UseVisualStyleBackColor = true;
            bndrawrectange2.Click += bndrawrectange2_Click;
            // 
            // bndrawrectange1
            // 
            bndrawrectange1.Location = new Point(161, 271);
            bndrawrectange1.Margin = new Padding(4, 4, 4, 4);
            bndrawrectange1.Name = "bndrawrectange1";
            bndrawrectange1.Size = new Size(118, 32);
            bndrawrectange1.TabIndex = 3;
            bndrawrectange1.Text = "绘制矩形1";
            bndrawrectange1.UseVisualStyleBackColor = true;
            bndrawrectange1.Click += bndrawrectange1_Click;
            // 
            // bndrawcircle
            // 
            bndrawcircle.Location = new Point(35, 271);
            bndrawcircle.Margin = new Padding(4, 4, 4, 4);
            bndrawcircle.Name = "bndrawcircle";
            bndrawcircle.Size = new Size(118, 32);
            bndrawcircle.TabIndex = 3;
            bndrawcircle.Text = "绘制圆";
            bndrawcircle.UseVisualStyleBackColor = true;
            bndrawcircle.Click += bndrawcircle_Click;
            // 
            // bncalibcontunie
            // 
            bncalibcontunie.Location = new Point(145, 236);
            bncalibcontunie.Margin = new Padding(4, 4, 4, 4);
            bncalibcontunie.Name = "bncalibcontunie";
            bncalibcontunie.Size = new Size(87, 27);
            bncalibcontunie.TabIndex = 2;
            bncalibcontunie.Text = "连续识别";
            bncalibcontunie.UseVisualStyleBackColor = true;
            // 
            // bncalib
            // 
            bncalib.Location = new Point(35, 236);
            bncalib.Margin = new Padding(4, 4, 4, 4);
            bncalib.Name = "bncalib";
            bncalib.Size = new Size(87, 27);
            bncalib.TabIndex = 2;
            bncalib.Text = "标定";
            bncalib.UseVisualStyleBackColor = true;
            bncalib.Click += bncalib_Click;
            // 
            // txtangEnd
            // 
            txtangEnd.Location = new Point(303, 66);
            txtangEnd.Margin = new Padding(4, 4, 4, 4);
            txtangEnd.Name = "txtangEnd";
            txtangEnd.Size = new Size(112, 27);
            txtangEnd.TabIndex = 1;
            txtangEnd.Text = "45";
            // 
            // txtscore
            // 
            txtscore.Location = new Point(130, 202);
            txtscore.Margin = new Padding(4, 4, 4, 4);
            txtscore.Name = "txtscore";
            txtscore.Size = new Size(102, 27);
            txtscore.TabIndex = 1;
            txtscore.Text = "0.8";
            // 
            // txtnumlevel
            // 
            txtnumlevel.Location = new Point(130, 168);
            txtnumlevel.Margin = new Padding(4, 4, 4, 4);
            txtnumlevel.Name = "txtnumlevel";
            txtnumlevel.Size = new Size(102, 27);
            txtnumlevel.TabIndex = 1;
            txtnumlevel.Text = "5";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(35, 206);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(84, 20);
            label6.TabIndex = 0;
            label6.Text = "匹配分数：";
            // 
            // txtmatchnums
            // 
            txtmatchnums.Location = new Point(130, 134);
            txtmatchnums.Margin = new Padding(4, 4, 4, 4);
            txtmatchnums.Name = "txtmatchnums";
            txtmatchnums.Size = new Size(102, 27);
            txtmatchnums.TabIndex = 1;
            txtmatchnums.Text = "1";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(35, 172);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(99, 20);
            label5.TabIndex = 0;
            label5.Text = "金字塔层数：";
            // 
            // txtoverlay
            // 
            txtoverlay.Location = new Point(130, 100);
            txtoverlay.Margin = new Padding(4, 4, 4, 4);
            txtoverlay.Name = "txtoverlay";
            txtoverlay.Size = new Size(102, 27);
            txtoverlay.TabIndex = 1;
            txtoverlay.Text = "0.1";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(35, 138);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(84, 20);
            label4.TabIndex = 0;
            label4.Text = "匹配数量：";
            // 
            // txtangStart
            // 
            txtangStart.Location = new Point(130, 66);
            txtangStart.Margin = new Padding(4, 4, 4, 4);
            txtangStart.Name = "txtangStart";
            txtangStart.Size = new Size(102, 27);
            txtangStart.TabIndex = 1;
            txtangStart.Text = "-45";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(255, 68);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(27, 20);
            label7.TabIndex = 0;
            label7.Text = "---";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(35, 104);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(69, 20);
            label3.TabIndex = 0;
            label3.Text = "重叠度：";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(240, 69);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(0, 20);
            label2.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(35, 69);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(84, 20);
            label1.TabIndex = 0;
            label1.Text = "角度范围：";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1201, 749);
            Controls.Add(tabControl1);
            Controls.Add(dispwin);
            Margin = new Padding(5, 6, 5, 6);
            Name = "Form1";
            Text = "Form1";
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button bnclosecamera;
        private HalconDotNet.HSmartWindowControl dispwin;
        // private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ComboBox cbcameralist;
        private System.Windows.Forms.Button bnreflush;
        private System.Windows.Forms.Button bnopencamera;
        private System.Windows.Forms.Button bnsingletrigger;
        private System.Windows.Forms.ComboBox cbtriggermode;
        private System.Windows.Forms.ComboBox cbtriggersource;
        private System.Windows.Forms.RadioButton RadRealTime;
        private System.Windows.Forms.RadioButton RadTrigger;
        private System.Windows.Forms.Button bncontinue;
        private System.Windows.Forms.Button bnstop;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private Button bnsavefile;
        private Button bnopenfile;
        private TabPage tabPage2;
        private TextBox txtangEnd;
        private TextBox txtscore;
        private TextBox txtnumlevel;
        private Label label6;
        private TextBox txtmatchnums;
        private Label label5;
        private TextBox txtoverlay;
        private Label label4;
        private TextBox txtangStart;
        private Label label7;
        private Label label3;
        private Label label2;
        private Label label1;
        private Button bncalibcontunie;
        private Button bncalib;
        private Button bndrawcircle;
        private Button bndrawrectange2;
        private Button bndrawrectange1;
    }
}

