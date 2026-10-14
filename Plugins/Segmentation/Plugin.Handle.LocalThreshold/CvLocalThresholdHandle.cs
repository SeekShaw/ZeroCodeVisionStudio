using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.LocalThreshold
{
    /// <summary>
    /// OpenCV 版局部阈值分割。使用 Cv2.AdaptiveThreshold (GaussianC)。
    /// MaskWidth/MaskHeight 取较大的一个作为 blockSize（强制奇数）。
    /// Scale 乘 255 作为常数 C。LightDark=dark 时使用 BinaryInv 反转极性。
    /// </summary>
    [Handle("局部阈值分割(OpenCV)", "CvLocalThreshold", "OpenCV版：基于高斯加权局部统计的自适应阈值分割。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvLocalThresholdHandle : ParameterHandleBase<LocalThresholdParameter>
    {
        public CvLocalThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            int blockSize = Math.Max(3, Math.Max(Parameter.MaskWidth, Parameter.MaskHeight));
            if (blockSize % 2 == 0) blockSize++;

            double c = Parameter.Scale * 255.0;

            ThresholdTypes threshType = Parameter.LightDark == ELightDark.dark
                ? ThresholdTypes.BinaryInv
                : ThresholdTypes.Binary;

            var mask = new Mat();
            Cv2.AdaptiveThreshold(
                gray, mask,
                255,
                AdaptiveThresholdTypes.GaussianC,
                threshType,
                blockSize,
                c);

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV局部阈值分割完成 (blockSize: {blockSize}, Scale: {Parameter.Scale:0.##})。";

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
            throw new InvalidOperationException("CvLocalThreshold: 未找到可用的输入图像。");
        }
    }
}
