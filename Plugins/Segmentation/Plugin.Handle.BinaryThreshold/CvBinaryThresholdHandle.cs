using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.BinaryThreshold
{
    /// <summary>
    /// OpenCV 版二值化。Method=max_separability 对应 Otsu；Method=smooth_histo 对应 Triangle。
    /// LightDark=dark 时使用 BinaryInv 反转极性。
    /// </summary>
    [Handle("二值化(OpenCV)", "CvBinaryThreshold", "OpenCV版：使用 Otsu/Triangle 自动阈值将输入图像转换为二值区域。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvBinaryThresholdHandle : ParameterHandleBase<BinaryThresholdParameter>
    {
        public CvBinaryThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            ThresholdTypes type;
            if (Parameter.Method == EMethod.max_separability)
                type = ThresholdTypes.Binary | ThresholdTypes.Otsu;
            else
                type = ThresholdTypes.Binary | ThresholdTypes.Triangle;

            var mask = new Mat();
            Cv2.Threshold(gray, mask, 0, 255, type);

            if (Parameter.LightDark == ELightDark.dark)
            {
                Cv2.BitwiseNot(mask, mask);
            }

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV二值化完成 (Method: {Parameter.Method}, Polarity: {Parameter.LightDark})。";

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
            throw new InvalidOperationException("CvBinaryThreshold: 未找到可用的输入图像。");
        }
    }
}
