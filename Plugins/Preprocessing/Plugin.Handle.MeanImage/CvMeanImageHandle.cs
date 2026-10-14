using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.MeanImage
{
    /// <summary>
    /// OpenCV 版均值滤波。使用 Cv2.Blur 进行矩形核均值平滑。
    /// </summary>
    [Handle("均值滤波(OpenCV)", "CvMeanImage", "OpenCV版：使用 Cv2.Blur 平滑图像。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvMeanImageHandle : ParameterHandleBase<MeanImageParameter>
    {
        public CvMeanImageHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var kernelWidth = Math.Max(1, Parameter.MaskWidth);
            var kernelHeight = Math.Max(1, Parameter.MaskHeight);

            var result = new Mat();
            Cv2.Blur(src, result, new Size(kernelWidth, kernelHeight));

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV均值滤波完成 (Mask: {kernelWidth}x{kernelHeight})";

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
            throw new InvalidOperationException("CvMeanImage: 未找到可用的输入图像。");
        }
    }
}
