using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GrayClosingRect
{
    /// <summary>
    /// OpenCV 版灰度矩形闭运算。使用 Cv2.MorphologyEx + MorphTypes.Close + 矩形核。
    /// </summary>
    [Handle("灰度矩形闭运算(OpenCV)", "CvGrayClosingRect", "OpenCV版：使用形态学闭运算填充灰度图像暗洞。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvGrayClosingRectHandle : ParameterHandleBase<GrayClosingRectParameter>
    {
        public CvGrayClosingRectHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int width = Math.Max(1, Parameter.Width);
            int height = Math.Max(1, Parameter.Height);
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(width, height));

            var result = new Mat();
            Cv2.MorphologyEx(src, result, MorphTypes.Close, kernel);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV灰度闭运算完成 (W:{width}, H:{height})";

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
            throw new InvalidOperationException("CvGrayClosingRect: 未找到可用的输入图像。");
        }
    }
}
