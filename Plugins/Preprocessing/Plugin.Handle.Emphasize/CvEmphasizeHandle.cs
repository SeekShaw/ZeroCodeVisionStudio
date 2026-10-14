using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Emphasize
{
    /// <summary>
    /// OpenCV 版图像增强。使用非锐化掩膜（Unsharp Mask）方式：result = src + Factor * (src - blur)。
    /// </summary>
    [Handle("图像增强(OpenCV)", "CvEmphasize", "OpenCV版：使用非锐化掩膜增强图像局部对比度。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvEmphasizeHandle : ParameterHandleBase<EmphasizeParameter>
    {
        public CvEmphasizeHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int w = Math.Max(1, Parameter.MaskWidth);
            int h = Math.Max(1, Parameter.MaskHeight);

            // 高斯模糊版局部均值
            var blurred = new Mat();
            Cv2.GaussianBlur(src, blurred, new Size(w | 1, h | 1), 0);

            // 非锐化掩膜：dst = src + factor * (src - blurred)
            var diff = new Mat();
            Cv2.Subtract(src, blurred, diff);

            var amplified = new Mat();
            Cv2.Multiply(diff, Scalar.All(Parameter.Factor), amplified);

            var combined = new Mat();
            Cv2.Add(src, amplified, combined);

            // 裁剪到 0-255
            var normalized = new Mat();
            Cv2.Normalize(combined, normalized, 0, 255, NormTypes.MinMax);
            var result = new Mat();
            normalized.ConvertTo(result, MatType.CV_8UC1);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV图像增强完成 (Factor={Parameter.Factor})";

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
            throw new InvalidOperationException("CvEmphasize: 未找到可用的输入图像。");
        }
    }
}
