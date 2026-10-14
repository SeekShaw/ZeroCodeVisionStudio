using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.MedianImage
{
    /// <summary>
    /// OpenCV 版中值滤波。使用 Cv2.MedianBlur 抑制脉冲噪声。
    /// </summary>
    [Handle("中值滤波(OpenCV)", "CvMedianImage", "OpenCV版：使用 Cv2.MedianBlur 抑制脉冲噪声。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvMedianImageHandle : ParameterHandleBase<MedianImageParameter>
    {
        public CvMedianImageHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            // OpenCV ksize 须为奇数
            int ksize = Math.Max(1, Parameter.Radius * 2 + 1);
            if (ksize % 2 == 0) ksize++;

            var result = new Mat();
            Cv2.MedianBlur(src, result, ksize);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV中值滤波完成 (ksize={ksize})";

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
            throw new InvalidOperationException("CvMedianImage: 未找到可用的输入图像。");
        }
    }
}
