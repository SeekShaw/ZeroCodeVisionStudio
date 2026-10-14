using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Skeleton
{
    /// <summary>
    /// OpenCV 版骨架提取。简化实现：OpenCvSharp 无内置 Thinning，这里使用形态学细化近似。
    /// </summary>
    [Handle("骨架提取(OpenCV)", "CvSkeleton", "OpenCV版：使用形态学细化近似提取骨架。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvSkeletonHandle : HandleBase
    {
        public CvSkeletonHandle(IDialogService dialogService) : base(dialogService)
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

            var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);

            // Zhang-Suen 细化简化：使用 8 次迭代开运算 + 差分
            var skeleton = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            var prev = binary.Clone();

            for (int i = 0; i < 10; i++)
            {
                var opened = new Mat();
                Cv2.MorphologyEx(prev, opened, MorphTypes.Open, kernel);

                var sub = new Mat();
                Cv2.Subtract(prev, opened, sub);

                Cv2.BitwiseOr(skeleton, sub, skeleton);

                var eroded = new Mat();
                Cv2.Erode(prev, eroded, kernel);

                // 全部被腐蚀空则退出
                if (Cv2.CountNonZero(eroded) == 0) break;
                prev = eroded;
            }

            RunParameter.MatOutput = skeleton.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, skeleton);
            RunParameter.SetOutput(RunOutputKeys.Region, skeleton);
            RunParameter.ResultMessage = "OpenCV骨架提取完成（形态学近似）。";

            if (HalconRuntimeProbe.IsAvailable)
                RunParameter.HOutput = MatHObjectConverter.MatToHObject(skeleton);

            RaiseImageDisplay(skeleton);
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
            throw new InvalidOperationException("CvSkeleton: 未找到可用的输入 mask。");
        }
    }
}
