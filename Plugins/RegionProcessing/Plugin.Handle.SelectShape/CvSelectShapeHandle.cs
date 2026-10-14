using System;
using System.Collections.Generic;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.SelectShape
{
    /// <summary>
    /// OpenCV 版形状筛选。使用 ConnectedComponentsWithStats 计算每个连通域的 Area/Width/Height，
    /// 根据 SelectShapeParameter 的 Feature + Min/Max 筛选后合并输出 mask。
    /// </summary>
    [Handle("形状筛选(OpenCV)", "CvSelectShape", "OpenCV版：根据形状特征（面积/宽/高）筛选连通域。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvSelectShapeHandle : HandleBase
    {
        private readonly SelectShapeParameter parameter;

        public CvSelectShapeHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new SelectShapeParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => Common.ParameterDialogName;

        public override void Handle()
        {
            Mat src = GetSourceMask();

            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);

            var labels = new Mat();
            var stats = new Mat();
            var centroids = new Mat();
            int labelCount = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);

            var min = Math.Min(parameter.Min, parameter.Max);
            var max = Math.Max(parameter.Min, parameter.Max);

            // 收集保留的 label（跳过背景 label=0）
            var keep = new HashSet<int>();
            for (int lbl = 1; lbl < labelCount; lbl++)
            {
                int w = stats.Get<int>(lbl, 2);
                int h = stats.Get<int>(lbl, 3);
                int area = stats.Get<int>(lbl, 4);

                double featureValue;
                switch (parameter.Feature)
                {
                    case SelectShapeFeature.Width:
                        featureValue = w;
                        break;
                    case SelectShapeFeature.Height:
                        featureValue = h;
                        break;
                    case SelectShapeFeature.Circularity:
                        featureValue = w * h > 0 ? (double)area / (w * h) : 0;
                        break;
                    default: // Area
                        featureValue = area;
                        break;
                }

                if (featureValue >= min && featureValue <= max)
                    keep.Add(lbl);
            }

            // 用 labels.GetArray 逐像素重建结果（与 CvFastThreshold 一致）
            var result = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
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

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"OpenCV形状筛选完成 ({parameter.Feature} [{min:0.##}, {max:0.##}])";

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
            throw new InvalidOperationException("CvSelectShape: 未找到可用的输入 mask。");
        }
    }
}
