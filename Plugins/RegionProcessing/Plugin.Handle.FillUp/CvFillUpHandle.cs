using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FillUp
{
    /// <summary>
    /// OpenCV 版区域填充。使用 Cv2.FloodFill 从边界点填充背景洞。
    /// 算法：反相 → FloodFill(从 (0,0)) → 合并原前景 + 未被填充的背景部分。
    /// </summary>
    [Handle("区域填充(OpenCV)", "CvFillUp", "OpenCV版：使用 FloodFill 填充区域内部孔洞。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvFillUpHandle : HandleBase
    {
        public CvFillUpHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            Mat src = GetSourceMask();

            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 阈值二值化
            var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);

            // FloodFill 从左上角开始填充背景
            var flood = binary.Clone();
            Cv2.FloodFill(flood, new Point(0, 0), new Scalar(200));

            // 原前景（255）+ 未被 FloodFill 到的内部背景（0）都应该保留为 255
            var result = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
            result.SetTo(Scalar.All(255), binary);                       // 原前景
            result.SetTo(Scalar.All(255), flood.InRange(0, 5));          // FloodFill 未到的内部空洞

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = "OpenCV区域填充完成。";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(result);

            RaiseImageDisplay(result);
        }

        protected Mat GetSourceMask()
        {
            var mat = RunParameter.MatOutput ?? RunParameter.MatGrayImage;
            if (mat != null && !mat.Empty()) return mat;

            if (RunParameter.GrayImage != null || RunParameter.HOutput != null)
            {
                var hImg = RunParameter.HOutput ?? RunParameter.GrayImage;
                if (hImg != null && HalconRuntimeProbe.IsAvailable)
                {
                    try { return MatHObjectConverter.HObjectToMat(hImg); }
                    catch { }
                }
            }
            throw new InvalidOperationException("CvFillUp: 未找到可用的输入 mask。");
        }
    }
}
