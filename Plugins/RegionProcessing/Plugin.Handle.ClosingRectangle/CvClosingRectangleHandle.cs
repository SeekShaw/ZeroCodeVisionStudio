using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ClosingRectangle
{
    /// <summary>
    /// OpenCV 版矩形闭运算（区域）。使用 Cv2.MorphologyEx + MorphTypes.Close。
    /// </summary>
    [Handle("矩形闭运算(OpenCV)", "CvClosingRectangle", "OpenCV版：使用矩形结构元素对区域 mask 执行闭运算。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvClosingRectangleHandle : ParameterHandleBase<ClosingRectangleParameter>
    {
        public CvClosingRectangleHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMask();

            int width = Math.Max(1, Parameter.Width);
            int height = Math.Max(1, Parameter.Height);
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(width, height));

            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var result = new Mat();
            Cv2.MorphologyEx(gray, result, MorphTypes.Close, kernel);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"OpenCV矩形闭运算完成 (W:{width}, H:{height})";

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
            throw new InvalidOperationException("CvClosingRectangle: 未找到可用的输入 mask。");
        }
    }
}
