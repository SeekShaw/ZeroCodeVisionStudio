using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.DynThreshold
{
    /// <summary>
    /// OpenCV 版动态阈值。使用 Cv2.AdaptiveThreshold (MeanC)，MaskWidth/MaskHeight 取较大的一个作为 blockSize（强制奇数）。
    /// Offset 参数直接作为常数 C。LightDark=dark 时使用 BinaryInv 反转极性。
    /// </summary>
    [Handle("动态阈值(OpenCV)", "CvDynThreshold", "OpenCV版：通过自适应阈值分割局部亮缺陷或暗缺陷。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvDynThresholdHandle : ParameterHandleBase<DynThresholdParameter>
    {
        public CvDynThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            int blockSize = Math.Max(3, Math.Max(Parameter.MaskWidth, Parameter.MaskHeight));
            if (blockSize % 2 == 0) blockSize++; // AdaptiveThreshold 要求奇数

            double c = Parameter.Offset;

            ThresholdTypes threshType = Parameter.LightDark == DynThresholdMode.dark
                ? ThresholdTypes.BinaryInv
                : ThresholdTypes.Binary;

            var mask = new Mat();
            Cv2.AdaptiveThreshold(
                gray, mask,
                255,
                AdaptiveThresholdTypes.MeanC,
                threshType,
                blockSize,
                c);

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV动态阈值分割完成 (blockSize: {blockSize}, Offset: {c:0.#})。";

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
            throw new InvalidOperationException("CvDynThreshold: 未找到可用的输入图像。");
        }
    }
}
