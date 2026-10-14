using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.BottomHat
{
    /// <summary>
    /// OpenCV 版底帽变换。使用 Cv2.MorphologyEx + MorphTypes.BlackHat。
    /// </summary>
    [Handle("底帽变换(OpenCV)", "CvBottomHat", "OpenCV版：使用形态学底帽变换突出暗色细小结构。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvBottomHatHandle : ParameterHandleBase<BottomHatParameter>
    {
        public CvBottomHatHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int half = Math.Max(1, (int)Math.Round(Parameter.Radius * 2));
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(half, half));

            var result = new Mat();
            Cv2.MorphologyEx(src, result, MorphTypes.BlackHat, kernel);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV底帽变换完成 (Radius={Parameter.Radius})";

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
            throw new InvalidOperationException("CvBottomHat: 未找到可用的输入图像。");
        }
    }
}
