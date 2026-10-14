using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Illuminate
{
    /// <summary>
    /// OpenCV 版光照校正。使用 Cv2.Divide：result = src / blur(src, bigKernel) * Factor + 偏移。
    /// </summary>
    [Handle("光照校正(OpenCV)", "CvIlluminate", "OpenCV版：使用大核均值除法校正不均匀光照。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvIlluminateHandle : ParameterHandleBase<IlluminateParameter>
    {
        public CvIlluminateHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            int w = Math.Max(1, Parameter.MaskWidth);
            int h = Math.Max(1, Parameter.MaskHeight);

            // 转为浮点做除法
            Mat srcF = new Mat();
            src.ConvertTo(srcF, MatType.CV_32F);

            var blur = new Mat();
            Cv2.Blur(srcF, blur, new Size(w, h));

            // 避免除零：给分母加一个很小的偏置
            var blurSafe = new Mat();
            Cv2.Max(blur, Scalar.All(1e-6), blurSafe);

            var divided = new Mat();
            Cv2.Divide(srcF, blurSafe, divided);

            // 乘以 Factor + 归一化到 8bit
            var scaled = new Mat();
            Cv2.Multiply(divided, Scalar.All(Parameter.Factor), scaled);

            var normalized = new Mat();
            Cv2.Normalize(scaled, normalized, 0, 255, NormTypes.MinMax);
            var result = new Mat();
            normalized.ConvertTo(result, MatType.CV_8UC1);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV光照校正完成 (Mask:{w}x{h}, Factor={Parameter.Factor})";

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
            throw new InvalidOperationException("CvIlluminate: 未找到可用的输入图像。");
        }
    }
}
