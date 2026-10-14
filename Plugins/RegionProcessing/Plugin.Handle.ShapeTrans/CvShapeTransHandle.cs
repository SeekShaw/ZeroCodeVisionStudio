using System;
using System.Collections.Generic;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ShapeTrans
{
    /// <summary>
    /// OpenCV 版区域形状转换。使用 FindContours + ConvexHull / BoundingRect / MinAreaRect / MinEnclosingCircle。
    /// </summary>
    [Handle("区域形状转换(OpenCV)", "CvShapeTrans", "OpenCV版：将区域 mask 转换为凸包/外接矩形/外接圆。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvShapeTransHandle : ParameterHandleBase<ShapeTransParameter>
    {
        public CvShapeTransHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMask();

            Mat gray = src.Channels() > 1 ? new Mat() : src;
            if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);

            // FindContours 返回 void，通过 out 参数拿到 contours 和 hierarchy
            Cv2.FindContours(
                binary,
                out Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            var result = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));

            if (contours != null)
            {
                foreach (var contour in contours)
                {
                    switch (Parameter.Mode)
                    {
                        case ShapeTransMode.Convex:
                            var hull = Cv2.ConvexHull(contour);
                            Cv2.FillConvexPoly(result, hull, Scalar.All(255));
                            break;
                        case ShapeTransMode.Rectangle1:
                            var rect1 = Cv2.BoundingRect(contour);
                            Cv2.Rectangle(result, rect1, Scalar.All(255), -1);
                            break;
                        case ShapeTransMode.Rectangle2:
                            var rr = Cv2.MinAreaRect(contour);
                            var pts = Cv2.BoxPoints(rr).Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
                            Cv2.FillConvexPoly(result, pts, Scalar.All(255));
                            break;
                        case ShapeTransMode.InnerCircle:
                        case ShapeTransMode.OuterCircle:
                            Cv2.MinEnclosingCircle(contour, out Point2f centerF, out float radius);
                            Cv2.Circle(result, (int)Math.Round(centerF.X), (int)Math.Round(centerF.Y), (int)Math.Round(radius), Scalar.All(255), -1);
                            break;
                        default:
                            Cv2.FillPoly(result, new[] { contour }, Scalar.All(255));
                            break;
                    }
                }
            }

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = $"OpenCV形状变换完成 ({Parameter.Mode})";

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
            throw new InvalidOperationException("CvShapeTrans: 未找到可用的输入 mask。");
        }
    }
}
