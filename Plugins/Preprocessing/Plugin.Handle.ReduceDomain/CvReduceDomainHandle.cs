using System;
using OpenCvSharp;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ReduceDomain
{
    /// <summary>
    /// OpenCV 版裁剪图像域。OpenCV Mat 不支持 Halcon 的 reduce_domain 域约束，
    /// 这里退化为对 ROI 区域外像素置零（Mask 方式），形状由原 ReduceDomainParameter 定义。
    /// </summary>
    [Handle("裁剪图像域(OpenCV)", "CvReduceDomain", "OpenCV版：使用 Mask 方式按 ROI 裁剪图像域。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class CvReduceDomainHandle : ParameterHandleBase<ReduceDomainParameter>
    {
        public CvReduceDomainHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            Mat src = GetSourceMat();

            var mask = new Mat(src.Size(), MatType.CV_8UC1, Scalar.All(0));

            switch (Parameter.ShapeType)
            {
                case EShapeType.rectangle1:
                    int rx1 = (int)Math.Round(Math.Max(0, Parameter.Column1));
                    int ry1 = (int)Math.Round(Math.Max(0, Parameter.Row1));
                    int rx2 = (int)Math.Round(Math.Min(src.Width - 1, Parameter.Column2));
                    int ry2 = (int)Math.Round(Math.Min(src.Height - 1, Parameter.Row2));
                    if (rx2 > rx1 && ry2 > ry1)
                        Cv2.Rectangle(mask, new Point(rx1, ry1), new Point(rx2, ry2), Scalar.All(255), -1);
                    break;
                case EShapeType.rectangle2:
                    // 旋转矩形近似：用 RotatedRect 画
                    var rr = new RotatedRect(
                        new Point2f((float)Parameter.Column, (float)Parameter.Row),
                        new Size2f((float)(Parameter.Length2 * 2), (float)(Parameter.Length1 * 2)),
                        (float)(Parameter.Phi * 180.0 / Math.PI));
                    Cv2.FillConvexPoly(mask, Cv2.BoxPoints(rr).Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray(), Scalar.All(255));
                    break;
                case EShapeType.circle:
                    Cv2.Circle(mask, (int)Parameter.Column, (int)Parameter.Row, (int)Math.Round(Parameter.Radius), Scalar.All(255), -1);
                    break;
                default:
                    // undefined：整张图放行
                    mask.SetTo(Scalar.All(255));
                    break;
            }

            var result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);

            RunParameter.MatOutput = result.Clone();
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"OpenCV裁剪图像域完成 (Shape={Parameter.ShapeType})";

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
            throw new InvalidOperationException("CvReduceDomain: 未找到可用的输入图像。");
        }
    }
}
