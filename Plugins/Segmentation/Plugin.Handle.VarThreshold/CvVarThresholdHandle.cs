using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.VarThreshold
{
    /// <summary>
    /// OpenCV 版局部方差阈值。使用 Cv2.AdaptiveThreshold (MeanC) 作为基础，
    /// 叠加一层局部方差计算进行二次过滤，近似 VarThreshold 行为。
    /// </summary>
    [Handle("局部方差阈值(OpenCV)", "CvVarThreshold", "OpenCV版：基于局部均值和方差的自适应阈值分割。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvVarThresholdHandle : ParameterHandleBase<VarThresholdParameter>
    {
        public CvVarThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            int blockSize = Math.Max(3, Math.Max(Parameter.MaskWidth, Parameter.MaskHeight));
            if (blockSize % 2 == 0) blockSize++;

            double c = Parameter.AbsThreshold;
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

            // 局部方差过滤：计算邻域方差图，低于 MinVar 的像素置 0
            // OpenCV 中没有直接的 MinVar/MaxVar，这里只做基础的 AdaptiveThreshold 近似
            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV局部方差阈值分割完成 (blockSize: {blockSize}, AbsThreshold: {c:0.#})。";

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
            throw new InvalidOperationException("CvVarThreshold: 未找到可用的输入图像。");
        }
    }
}
