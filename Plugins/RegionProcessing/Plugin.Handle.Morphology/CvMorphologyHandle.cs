using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Morphology
{
    /// <summary>
    /// OpenCV 版形态学处理（区域）。根据 MorphologyOperation 映射到 OpenCV MorphTypes。
    /// </summary>
    [Handle("形态学处理(OpenCV)", "CvMorphology", "OpenCV版：根据运算类型对区域 mask 执行形态学操作。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvMorphologyHandle : HandleBase
    {
        private readonly MorphologyParameter parameter;

        public CvMorphologyHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new MorphologyParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            Mat src = GetSourceMask();

            // 圆形结构元素：直径 = 2 * Radius + 1
            int diameter = Math.Max(1, (int)Math.Round(parameter.Radius * 2 + 1));
            if (diameter % 2 == 0) diameter++;
            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(diameter, diameter));

            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var result = new Mat();

            switch (parameter.Operation)
            {
                case MorphologyOperation.OpeningCircle:
                    Cv2.MorphologyEx(gray, result, MorphTypes.Open, kernel);
                    break;
                case MorphologyOperation.ClosingCircle:
                    Cv2.MorphologyEx(gray, result, MorphTypes.Close, kernel);
                    break;
                case MorphologyOperation.DilationCircle:
                    Cv2.Dilate(gray, result, kernel);
                    break;
                case MorphologyOperation.ErosionCircle:
                    Cv2.Erode(gray, result, kernel);
                    break;
                default:
                    Cv2.MorphologyEx(gray, result, MorphTypes.Close, kernel);
                    break;
            }

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"OpenCV形态学处理完成 ({parameter.Operation}, Radius={parameter.Radius})";

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
            throw new InvalidOperationException("CvMorphology: 未找到可用的输入 mask。");
        }
    }
}
