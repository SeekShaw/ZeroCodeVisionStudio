using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ScaleImage
{
    /// <summary>
    /// OpenCV 版灰度拉伸。使用 Cv2.ConvertScaleAbs 执行 dst = src * Multiplier + Add。
    /// </summary>
    [Handle("灰度拉伸(OpenCV)", "CvScaleImage", "OpenCV版：通过乘数和加数调整图像灰度值。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvScaleImageHandle : ParameterHandleBase<ScaleImageParameter>
    {
        public CvScaleImageHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var result = new Mat();
            Cv2.ConvertScaleAbs(src, result, Parameter.Multiplier, Parameter.Add);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV灰度拉伸完成 (Mult={Parameter.Multiplier}, Add={Parameter.Add})";

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
            throw new InvalidOperationException("CvScaleImage: 未找到可用的输入图像。");
        }
    }
}
