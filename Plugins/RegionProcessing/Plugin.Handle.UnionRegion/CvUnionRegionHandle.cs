using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.UnionRegion
{
    /// <summary>
    /// OpenCV 版区域合并。使用 Cv2.BitwiseOr 合并当前输入区域与上游 Mat 输出。
    /// 因为 OpenCV mask 本身就所有区域合并在一个图里，这里直接透传（或与 MatGrayImage 做 Or）。
    /// </summary>
    [Handle("区域合并(OpenCV)", "CvUnionRegion", "OpenCV版：使用 BitwiseOr 将所有区域合并为一个 mask。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvUnionRegionHandle : HandleBase
    {
        public CvUnionRegionHandle(IDialogService dialogService) : base(dialogService)
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

            // 如果同时有 MatGrayImage（图像类算子的输出），做 Or 合并；否则直接输出
            var result = new Mat();
            if (RunParameter.MatGrayImage != null && !RunParameter.MatGrayImage.Empty()
                && RunParameter.MatGrayImage.Size() == gray.Size()
                && RunParameter.MatGrayImage != RunParameter.MatOutput)
            {
                Mat gray2 = RunParameter.MatGrayImage.Channels() > 1 ? new Mat() : RunParameter.MatGrayImage;
                if (RunParameter.MatGrayImage.Channels() > 1)
                    Cv2.CvtColor(RunParameter.MatGrayImage, gray2, ColorConversionCodes.BGR2GRAY);

                Cv2.BitwiseOr(gray, gray2, result);
            }
            else
            {
                gray.CopyTo(result);
            }

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = "OpenCV区域合并完成。";

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
            throw new InvalidOperationException("CvUnionRegion: 未找到可用的输入 mask。");
        }
    }
}
