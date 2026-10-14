using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.AutoThreshold
{
    /// <summary>
    /// OpenCV 版自动阈值。使用 Otsu 全局自适应阈值。Sigma 参数在 OpenCV 中无对应概念，忽略。
    /// </summary>
    [Handle("自动阈值(OpenCV)", "CvAutoThreshold", "OpenCV版：基于 Otsu 自动阈值分割图像。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvAutoThresholdHandle : ParameterHandleBase<AutoThresholdParameter>
    {
        public CvAutoThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var mask = new Mat();
            Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = "OpenCV自动阈值分割完成 (Otsu)。";

            if (HalconRuntimeProbe.IsAvailable)
            {
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mask);
            }

            RaiseImageDisplay(mask);
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
            throw new InvalidOperationException("CvAutoThreshold: 未找到可用的输入图像。");
        }
    }
}
