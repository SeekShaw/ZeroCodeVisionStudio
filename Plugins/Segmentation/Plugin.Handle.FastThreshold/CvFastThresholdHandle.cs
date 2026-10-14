using System.Collections.Generic;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FastThreshold
{
    /// <summary>
    /// OpenCV 版快速阈值。逻辑上等价于 Cv2.InRange 按 MinGray/MaxGray 分割；MinSize 通过 ConnectedComponents 过滤掉小连通域。
    /// </summary>
    [Handle("快速阈值分割(OpenCV)", "CvFastThreshold", "OpenCV版：按灰度范围快速分割，可过滤小于 MinSize 的小区域。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvFastThresholdHandle : ParameterHandleBase<FastThresholdParameter>
    {
        public CvFastThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            double min = Math.Min(Parameter.MinGray, Parameter.MaxGray);
            double max = Math.Max(Parameter.MinGray, Parameter.MaxGray);

            var mask = new Mat();
            Cv2.InRange(src, new Scalar(min), new Scalar(max), mask);

            int minSize = Math.Max(1, Parameter.MinSize);
            if (minSize > 1)
            {
                mask = FilterSmallConnectedComponents(mask, minSize);
            }

            RunParameter.MatOutput = mask;
            RunParameter.SetOutput(RunOutputKeys.Output, mask);
            RunParameter.SetOutput(RunOutputKeys.Region, mask);
            RunParameter.ResultMessage = $"OpenCV快速阈值分割完成，灰度范围 [{min}, {max}]，MinSize={minSize}。";

            if (HalconRuntimeProbe.IsAvailable)
            {
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(mask);
            }

            RaiseImageDisplay(mask);
        }

        private static Mat FilterSmallConnectedComponents(Mat mask, int minArea)
        {
            // ConnectedComponents 过滤小连通域
            var labels = new Mat();
            var stats = new Mat();
            var centroids = new Mat();
            int num = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids);

            // 收集保留的 label（跳过背景 label=0）
            var keep = new HashSet<int>();
            for (int i = 1; i < num; i++)
            {
                int area = stats.Get<int>(i, 4);
                if (area >= minArea) keep.Add(i);
            }

            // 逐像素重建结果（labels 是 CV_32SC1）
            var result = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
            var labelsData = new int[labels.Rows * labels.Cols];
            labels.GetArray(out labelsData);
            for (int idx = 0; idx < labelsData.Length; idx++)
            {
                if (keep.Contains(labelsData[idx]))
                {
                    int r = idx / labels.Cols;
                    int c = idx % labels.Cols;
                    result.Set(r, c, (byte)255);
                }
            }
            return result;
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
            throw new InvalidOperationException("CvFastThreshold: 未找到可用的输入图像。");
        }
    }
}
