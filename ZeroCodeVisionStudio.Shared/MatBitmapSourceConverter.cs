using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// OpenCV Mat → WPF BitmapSource 转换器（纯 OpenCV + WPF，不依赖 Halcon runtime）。
    /// 用于 Halcon 不可用时的图像显示降级路径。
    /// </summary>
    public static class MatBitmapSourceConverter
    {
        /// <summary>
        /// Mat → BitmapSource（WPF Image 控件可直接绑定）。
        /// 使用 PngBitmapEncoder 走内存流 — 稳定、无需 unsafe、支持任意通道数。
        /// </summary>
        public static BitmapSource MatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty()) return null;

            try
            {
                // 统一转 BGR(3通道) 走 PNG 编码路径
                using var bgr = EnsureBgr(mat);

                using var ms = new MemoryStream();
                {
                    Cv2.ImEncode(".png", bgr, out var pngBytes);
                    ms.Write(pngBytes, 0, pngBytes.Length);
                    ms.Position = 0;
                }

                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                // 冻结允许跨线程使用（Dispatcher 从后台线程切 UI 线程显示）
                frame.Freeze();
                return frame;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 统一转 3 通道 BGR — 灰度图/4通道/已3通道但非Continuous 的都处理。
        /// </summary>
        private static Mat EnsureBgr(Mat mat)
        {
            var channels = mat.Channels();
            if (channels == 3)
            {
                return mat.IsContinuous() ? mat : mat.Clone();
            }

            if (channels == 1)
            {
                var bgr = new Mat();
                Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
                return bgr;
            }

            if (channels == 4)
            {
                var bgr = new Mat();
                Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);
                return bgr;
            }

            // 其他通道：先压成灰度再转 BGR
            var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            var fallback = new Mat();
            Cv2.CvtColor(gray, fallback, ColorConversionCodes.GRAY2BGR);
            gray.Dispose();
            return fallback;
        }
    }
}
