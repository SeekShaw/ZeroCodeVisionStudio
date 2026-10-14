using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.HysteresisThreshold
{
    /// <summary>
    /// OpenCV 版滞后阈值分割。
    /// 先按 High 得到强边缘/强区域，再按 Low 得到弱区域，最后通过形态学膨胀 + 与强区域连通性判定实现 "滞后连接"。
    /// MaxLength 近似为膨胀核大小。
    /// </summary>
    [Handle("滞后阈值分割(OpenCV)", "CvHysteresisThreshold", "OpenCV版：通过高低双阈值 + 形态学滞后连接实现分割。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvHysteresisThresholdHandle : ParameterHandleBase<HysteresisThresholdParameter>
    {
        public CvHysteresisThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            double low = Math.Min(Parameter.Low, Parameter.High);
            double high = Math.Max(Parameter.Low, Parameter.High);
            int maxLength = Math.Max(1, Parameter.MaxLength);

            // 双阈值：High -> strong；Low -> weak
            var strong = new Mat();
            var weak = new Mat();
            Cv2.Threshold(gray, strong, high, 255, ThresholdTypes.Binary);
            Cv2.Threshold(gray, weak, low, 255, ThresholdTypes.Binary);

            // 从 strong 做形态学膨胀，得到可接受弱像素的区域
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(maxLength * 2 + 1, maxLength * 2 + 1));
            var dilatedStrong = new Mat();
            Cv2.Dilate(strong, dilatedStrong, kernel);

            // 最终 mask = weak ∩ dilatedStrong（弱像素必须连接到强区域）
            var mask = new Mat();
            Cv2.BitwiseAnd(weak, dilatedStrong, mask);

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV滞后阈值分割完成 (Low: {low}, High: {high}, MaxLength: {maxLength})。";

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
            throw new InvalidOperationException("CvHysteresisThreshold: 未找到可用的输入图像。");
        }
    }
}
