using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GrayOpeningRect
{
    /// <summary>
    /// OpenCV 版灰度矩形开运算。使用 Cv2.MorphologyEx + MorphTypes.Open + 矩形核。
    /// </summary>
    [Handle("灰度矩形开运算(OpenCV)", "CvGrayOpeningRect", "OpenCV版：使用形态学开运算移除灰度图像亮噪声。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGrayOpeningRectHandle : ParameterHandleBase<GrayOpeningRectParameter>
    {
        public CvGrayOpeningRectHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int width = Math.Max(1, Parameter.Width);
            int height = Math.Max(1, Parameter.Height);
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(width, height));

            var result = new Mat();
            Cv2.MorphologyEx(src, result, MorphTypes.Open, kernel);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV灰度开运算完成 (W:{width}, H:{height})";

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
            throw new InvalidOperationException("CvGrayOpeningRect: 未找到可用的输入图像。");
        }
    }
}
