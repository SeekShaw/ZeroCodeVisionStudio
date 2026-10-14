using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.TopHat
{
    /// <summary>
    /// OpenCV 版顶帽变换。使用 Cv2.MorphologyEx + MorphTypes.TopHat。
    /// </summary>
    [Handle("顶帽变换(OpenCV)", "CvTopHat", "OpenCV版：使用形态学顶帽变换突出明亮细小结构。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvTopHatHandle : ParameterHandleBase<TopHatParameter>
    {
        public CvTopHatHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            // Halcon 圆形半径 → OpenCV 矩形核（近似）
            int half = Math.Max(1, (int)Math.Round(Parameter.Radius * 2));
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(half, half));

            var result = new Mat();
            Cv2.MorphologyEx(src, result, MorphTypes.TopHat, kernel);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV顶帽变换完成 (Radius={Parameter.Radius})";

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
            throw new InvalidOperationException("CvTopHat: 未找到可用的输入图像。");
        }
    }
}
