using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Threshold
{
    /// <summary>
    /// OpenCV 版阈值分割。使用 Cv2.InRange 按灰度范围生成二值 mask。
    /// </summary>
    [Handle("阈值分割(OpenCV)", "CvThreshold", "OpenCV版：按灰度范围分割图像像素。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvThresholdHandle : ParameterHandleBase<ThresholdParameter>
    {
        public CvThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            double min = Math.Min(Parameter.MinGray, Parameter.MaxGray);
            double max = Math.Max(Parameter.MinGray, Parameter.MaxGray);

            var mask = new Mat();
            Cv2.InRange(src, new Scalar(min), new Scalar(max), mask);

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV阈值分割完成，灰度范围 [{min}, {max}]";

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
            throw new InvalidOperationException("CvThreshold: 未找到可用的输入图像（MatGrayImage/MatOutput/HGrayImage/HOutput）。");
        }
    }
}
