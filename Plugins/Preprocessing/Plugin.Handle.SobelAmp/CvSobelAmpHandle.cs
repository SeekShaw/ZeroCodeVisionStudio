using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.SobelAmp
{
    /// <summary>
    /// OpenCV 版 Sobel 边缘幅值。使用 Cv2.Sobel + Cv2.Magnitude 计算梯度幅值。
    /// </summary>
    [Handle("Sobel边缘幅值(OpenCV)", "CvSobelAmp", "OpenCV版：使用 Cv2.Sobel 计算 Sobel 边缘幅值。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvSobelAmpHandle : ParameterHandleBase<SobelAmpParameter>
    {
        public CvSobelAmpHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int ksize = Math.Max(3, Parameter.Size);
            if (ksize % 2 == 0) ksize++;

            // 转为灰度
            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var gradX = new Mat();
            var gradY = new Mat();
            Cv2.Sobel(gray, gradX, MatType.CV_32F, 1, 0, ksize);
            Cv2.Sobel(gray, gradY, MatType.CV_32F, 0, 1, ksize);

            var magnitude = new Mat();
            Cv2.Magnitude(gradX, gradY, magnitude);

            // 归一化到 8bit
            var normalized = new Mat();
            Cv2.Normalize(magnitude, normalized, 0, 255, NormTypes.MinMax);
            var result = new Mat();
            normalized.ConvertTo(result, MatType.CV_8UC1);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV Sobel 边缘幅值完成 (ksize={ksize})";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(result);

            RaiseImageDisplay(result);
        }

        protected Mat GetSourceMat()
        {
            var mat = RunParameter.MatGrayImage ?? RunParameter.MatOutput;
            if (mat != null && !mat.Empty()) return mat;

            if (RunParameter.GrayImage != null || RunParameter.HOutput != null)
            {
                var hImg = RunParameter.GrayImage ?? RunParameter.HOutput;
                if (hImg != null && HalconRuntimeProbe.IsAvailable)
                {
                    try { return MatHObjectConverter.HObjectToMat(hImg); }
                    catch { }
                }
            }
            throw new InvalidOperationException("CvSobelAmp: 未找到可用的输入图像。");
        }
    }
}
