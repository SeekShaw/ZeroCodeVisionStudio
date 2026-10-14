using System;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using HalconDotNet;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 图像显示事件参数。支持三种载荷：Halcon HObject、OpenCV Mat、WPF BitmapSource。
    /// 主窗口 <see cref="OnImageDisplay"/> 事件接收此参数后，优先走 Mat 路径（不依赖 Halcon），
    /// 再走 BitmapSource 路径（不依赖 OpenCV native），最后走 HObject 路径（依赖 Halcon runtime）。
    /// </summary>
    public class ImageDisplayEventArgs : EventArgs
    {
        public ImageDisplayEventArgs(HObject image)
        {
            Image = image;
        }

        public ImageDisplayEventArgs(Mat mat)
        {
            Mat = mat;
        }

        public ImageDisplayEventArgs(BitmapSource bitmap)
        {
            Bitmap = bitmap;
        }

        /// <summary>Halcon HObject 图像（HImage/HRegion/HXLD）。</summary>
        public HObject Image { get; }

        /// <summary>OpenCV Mat 图像（Halcon runtime 不可用时优先从此路径显示）。</summary>
        public Mat Mat { get; }

        /// <summary>WPF BitmapSource —— 终极兜底（Halcon + OpenCV 原生都不可用时用）。</summary>
        public BitmapSource Bitmap { get; }
    }
}
