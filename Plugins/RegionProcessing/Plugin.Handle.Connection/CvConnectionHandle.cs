using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Connection
{
    /// <summary>
    /// OpenCV 版连通域。使用 Cv2.ConnectedComponents 标记各连通区域，
    /// 简化处理：将所有非背景连通域合并为一个 mask 输出。
    /// </summary>
    [Handle("连通域(OpenCV)", "CvConnection", "OpenCV版：对二值 mask 执行连通域分析（合并输出）。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvConnectionHandle : HandleBase
    {
        public CvConnectionHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            Mat src = GetSourceMask();

            // 确保是单通道二值 mask
            Mat mask = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, mask, ColorConversionCodes.BGR2GRAY);

            // 阈值确保是二值
            var binary = new Mat();
            Cv2.Threshold(mask, binary, 0, 255, ThresholdTypes.Binary);

            // ConnectedComponents
            var labels = new Mat();
            Cv2.ConnectedComponents(binary, labels);

            // labels=0 是背景，其他 label 合并为一个 mask 输出
            var result = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
            result.SetTo(Scalar.All(255), labels.GreaterThan(0));

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = "OpenCV连通域分析完成（合并所有非背景区域）。";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(result);

            RaiseImageDisplay(result);
        }

        protected Mat GetSourceMask()
        {
            // Region 算子通常从 HOutput / MatOutput（上一个 Region 算子）或 GrayImage 来
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
            throw new InvalidOperationException("CvConnection: 未找到可用的输入 mask。");
        }
    }
}
