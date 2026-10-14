using System;
using System.Runtime.InteropServices;
using System.IO;
using OpenCvSharp;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// OpenCV Mat ↔ Halcon HObject 双向转换工具。
    /// Halcon 运行时不可用时所有转换静默返回 null，不抛异常。
    /// </summary>
    public static class MatHObjectConverter
    {
        /// <summary>
        /// OpenCV Mat → Halcon HObject（图像）。
        /// 使用 HalconDotNet.HImage 实例方法 GenImage1 / GenImageInterleaved。
        /// </summary>
        public static HalconDotNet.HObject MatToHObject(Mat mat)
        {
            if (mat == null || mat.Empty()) return null;
            if (!HalconRuntimeProbe.IsAvailable) return null;

            try
            {
                using var contiguous = mat.IsContinuous() ? null : mat.Clone();
                var source = contiguous ?? mat;
                var dataPtr = source.Data;

                var channels = source.Channels();
                var width = source.Width;
                var height = source.Height;

                var hImage = new HalconDotNet.HImage();

                if (channels == 1)
                {
                    hImage.GenImage1("byte", width, height, dataPtr);
                }
                else if (channels == 3)
                {
                    // Mat 是 BGR，Halcon 也支持 "bgr" interleaved —— 直接用
                    using var rgb = new Mat();
                    Cv2.CvtColor(source, rgb, ColorConversionCodes.BGR2RGB);
                    if (!rgb.IsContinuous()) { rgb.Dispose(); return null; }

                    hImage.GenImageInterleaved(
                        rgb.Data,
                        "rgb",
                        width, height,
                        -1, "byte",
                        width, height,
                        0, 0, -1, 0);
                }
                else
                {
                    using var gray = new Mat();
                    Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
                    return MatToHObject(gray);
                }

                return hImage;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Halcon HObject → OpenCV Mat（图像）。
        /// 通过 WriteImage 写临时文件 + Cv2.ImRead 读回（通用、稳定、不依赖 Halcon 原生指针）。
        /// </summary>
        public static Mat HObjectToMat(HalconDotNet.HObject hobj)
        {
            if (hobj == null) return null;
            if (!HalconRuntimeProbe.IsAvailable) return null;

            try
            {
                // 判断类：region/xld 不转换
                HalconDotNet.HOperatorSet.GetObjClass(hobj, out var objClass);
                var className = objClass?.S?.ToString() ?? "";
                if (className.Contains("region") || className.Contains("xld"))
                    return null;

                // 临时文件
                var tmpPath = Path.GetTempFileName() + ".png";
                try
                {
                    HalconDotNet.HOperatorSet.WriteImage(hobj, "png", 0, tmpPath);
                    return Cv2.ImRead(tmpPath);
                }
                finally
                {
                    if (File.Exists(tmpPath))
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Halcon 区域 → OpenCV mask（0/255）。</summary>
        public static Mat HRegionToMask(HalconDotNet.HObject region, int width, int height)
        {
            if (region == null || !HalconRuntimeProbe.IsAvailable) return null;
            try
            {
                HalconDotNet.HOperatorSet.GenImageConst(out var blank, "byte", width, height);
                HalconDotNet.HOperatorSet.PaintRegion(region, blank, out blank, 255, "fill");
                var mat = HObjectToMat(blank);
                blank.Dispose();
                return mat;
            }
            catch { return null; }
        }

        /// <summary>OpenCV mask → Halcon 区域。</summary>
        public static HalconDotNet.HObject MaskToHRegion(Mat mask)
        {
            if (mask == null || mask.Empty() || !HalconRuntimeProbe.IsAvailable) return null;
            try
            {
                using var gray = mask.Channels() == 1 ? mask.Clone() : new Mat();
                if (mask.Channels() != 1) Cv2.CvtColor(mask, gray, ColorConversionCodes.BGR2GRAY);

                var hobj = MatToHObject(gray);
                if (hobj == null) return null;
                try
                {
                    HalconDotNet.HOperatorSet.Threshold(hobj, out var region, 128, 255);
                    hobj.Dispose();
                    return region;
                }
                catch { hobj.Dispose(); throw; }
            }
            catch { return null; }
        }
    }
}
