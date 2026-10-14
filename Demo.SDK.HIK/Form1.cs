using System.Diagnostics;
using System.Drawing.Imaging;
using ZeroCode.Vision.SDK;
using ZeroCode.Vision.SDK.HK;
using HalconDotNet;

namespace Demo.SDK.HIK;

public partial class Form1 : Form
{
    private CameraHik hik = new CameraHik();
    private HObject img = null;
    private bool isConnected;
    private bool isGrabing;
    HObject template;
    private HWindow winhandle;
    public Form1()
    {
        InitializeComponent();
        this.Load += Form1_Load;
        this.FormClosing += Form1_FormClosing;
        dispwin.Load += Dispwin_Load;
    }

    private HObject BitmapToHObjectColor(Bitmap bitmap)
    {
        HObject result = null;
        try
        {
            bool flag = bitmap == null;
            if (flag)
            {
                return result;
            }
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            HOperatorSet.GenImageInterleaved(out result, bitmapData.Scan0, "bgr", bitmapData.Width, bitmapData.Height, -1, "byte", bitmapData.Width, bitmapData.Height, 0, 0, -1, 0);
            bitmap.UnlockBits(bitmapData);
        }
        catch (Exception)
        {
            Trace.WriteLine("转换为Halcon的彩色图像失败");
            return null;
        }
        return result;
    }

    private void bncalib_Click(object sender, EventArgs e)
    {
        if (template != null)
        {
            var numlevels = Convert.ToSingle(this.txtnumlevel.Text);
            var angstart = Convert.ToSingle(this.txtangStart.Text);
            var angend = Convert.ToSingle(this.txtangEnd.Text);
            var machnums = Convert.ToSingle(this.txtmatchnums.Text);
            var overlay = Convert.ToSingle(this.txtoverlay.Text);
            var score = Convert.ToSingle(this.txtscore.Text);
            HOperatorSet.CreateShapeModel(template, numlevels, new HTuple(angstart).TupleRad(), new HTuple(angend).TupleRad(), "auto", "auto", "use_polarity", "auto", "auto", out var modelID);

        }
    }

    private void bnclosecamera_Click(object sender, EventArgs e)
    {
        hik.CloseDevice();
        cbcameralist.Enabled = true;
    }

    private void bncontinue_Click(object sender, EventArgs e)
    {
        if (!isConnected)
        {
            return;
        }
        // 判断触发方式
        if (RadRealTime.Checked == false)
        {
            return;
        }
        // 调用方法
        hik.StartWitchSoftTriggerMode();
        isGrabing = true;
    }

    private async void bndrawcircle_Click(object sender, EventArgs e)
    {
        template?.Dispose();
        template = null;
        await Task.Run(() =>
        {
            HOperatorSet.DrawCircle(winhandle, out var row, out var col, out var radius);
            HOperatorSet.GenCircle(out var template, row, col, radius);
            HOperatorSet.DispObj(template, winhandle);
        });
    }

    private void bndrawrectange1_Click(object sender, EventArgs e)
    {

    }

    private void bndrawrectange2_Click(object sender, EventArgs e)
    {

    }

    private void bnopencamera_Click(object sender, EventArgs e)
    {
        // 初始化相机设备
        var flag = hik.InitizeDevice(cbcameralist.Text);
        if (flag)
        {
            isConnected = true;
            // 设置回到函数
            //   hik.ImageCallbackEvent += Hik_ImageCallbackEvent;
            // 获取触发模式
            hik.GetTriggerMode(out var triggerMode, out _);
            if (triggerMode.Equals(ETriggerMode.On))
            {
                RadTrigger.Checked = true;
            }
            else
            {
                RadRealTime.Checked = true;
            }
            // 获取相机对应曝光和增益数据
            hik.GetExpouseTime(out var expouseTime);
            hik.GetGain(out var gain);
            // 赋值给对应控件
            //TxtExTime.Text = expouseTime.ToString("F2");
            //TxtGain.Text = gain.ToString("F2");
            //TbExTime.Value = Convert.ToInt32(expouseTime);
            //TbGain.Value = Convert.ToInt32(gain);

            //LogTool.Info($"初始化相机【{sn}】成功");
        }
        else
        {
            // LogTool.Error($"初始化相机【{sn}】失败");
        }
    }

    private void bnopenfile_Click(object sender, EventArgs e)
    {
        using OpenFileDialog ofd = new OpenFileDialog();
        if (ofd.ShowDialog() == DialogResult.OK)
        {

            if (img != null)
            {
                img.Dispose();
                img = null;
            }
            img = new HImage(ofd.FileName);
            HOperatorSet.DispObj(img, winhandle);
        }
    }

    private void bnreflush_Click(object sender, EventArgs e)
    {
        var camlist = hik.GetCameraEnum();
        cbcameralist.DataSource = camlist;
    }

    private void bnsavefile_Click(object sender, EventArgs e)
    {

    }

    private void bnsingletrigger_Click(object sender, EventArgs e)
    {
        if (!isConnected)
        {
            return;
        }
        // 判断触发方式
        if (RadTrigger.Checked == false)
        {
            return;
        }
        // 调用方法
        hik.GetImageOnSoftTrigger(out var img);
        _ = 1;
    }

    private void bnstop_Click(object sender, EventArgs e)
    {
        if (!isConnected)
        {
            return;
        }

        if (!isGrabing)
        {
            return;
        }
        // 调用方法停止采集
        var flag = hik.StopGrabbing();
    }

    private void Dispwin_Load(object sender, EventArgs e)
    {
        dispwin.SetFullImagePart();
        winhandle = dispwin.HalconWindow;
        HOperatorSet.SetDraw(winhandle, "margin");
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        bnclosecamera_Click(default, default);
    }
    private void Form1_Load(object sender, EventArgs e)
    {
        var triggerMode = Enum.GetNames(typeof(ETriggerMode));
        cbtriggermode.DataSource = triggerMode;
        var triggerSource = Enum.GetNames(typeof(ETriggerSource));
        cbtriggersource.DataSource = triggerSource;
        hik.ImageCallbackEvent += this.Hik_ImageCallbackEvent;

    }
    private void Hik_ImageCallbackEvent(string arg1, Bitmap arg2)
    {
        HOperatorSet.DispImage(BitmapToHObjectColor(arg2), winhandle);
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        if (isConnected)
        {
            // 关闭设备
            hik.CloseDevice();
            hik.Dispose();
            hik = null;
        }
    }

    private void RadRealTime_CheckedChanged(object sender, EventArgs e)
    {
        if (RadRealTime.Checked)
        {
            //  LogTool.Info("当前相机的触发模式为: 实时");
        }

    }

    private void RadTrigger_CheckedChanged(object sender, EventArgs e)
    {
        if (RadTrigger.Checked)
        {
            //  LogTool.Info("当前相机的触发模式为: 触发");
        }
    }
}
