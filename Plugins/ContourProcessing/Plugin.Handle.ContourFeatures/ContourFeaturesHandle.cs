using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ContourFeatures
{
    /// <summary>
    /// 轮廓特征提取算子基类，支持物理当量与工程单位配置。
    /// </summary>
    public abstract class ContourFeatureBase : ParameterHandleBase<FeatureMeasureParameter>, IHandleDataSlots
    {
        protected ContourFeatureBase(IDialogService dialogService) : base(dialogService)
        {
        }

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple))
        };

        protected HObject GetContour()
        {
            return HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);
        }

        protected void SetResult(HObject result, HTuple value, string featureName, string message)
        {
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput("FeatureValue", value);
            RunParameter.SetOutput(featureName, value);
            RunParameter.ResultMessage = message;
            RaiseImageDisplay(result);
        }

        protected static string TupleText(HTuple tuple, string format = "0.###")
        {
            if (tuple == null || tuple.Length == 0)
                return string.Empty;

            var values = new List<string>();
            for (var i = 0; i < tuple.Length; i++)
            {
                values.Add(tuple[i].D.ToString(format));
            }
            return string.Join(", ", values);
        }
    }

    /// <summary>
    /// 轮廓长度测量。
    /// </summary>
    [Handle("轮廓长度", "LengthXld", "计算XLD轮廓的总长度，支持标定换算。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class LengthXldHandle : ContourFeatureBase
    {
        public LengthXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.LengthXld(contour, out var length);
            var totalLength = length.TupleSum().D;
            var param = Parameter ?? new FeatureMeasureParameter();
            var msg = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"轮廓长度 = {param.FormatMeasure(totalLength)}"
                : $"轮廓长度 = {totalLength:0.###} px";
            SetResult(contour, length, RunOutputKeys.ContourLength, msg);
        }
    }

    /// <summary>
    /// 轮廓面积测量。
    /// </summary>
    [Handle("轮廓面积", "AreaCenterXld", "计算XLD轮廓围成的面积和重心，支持标定换算。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class AreaCenterXldHandle : ContourFeatureBase
    {
        public AreaCenterXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.AreaCenterXld(contour, out var area, out var row, out var column, out var pointOrder);
            var param = Parameter ?? new FeatureMeasureParameter();
            double areaVal = area.Length > 0 ? area[0].D : 0;
            double areaPhys = areaVal * param.CalibScale * param.CalibScale;
            string unit = string.IsNullOrWhiteSpace(param.UnitName) ? "px" : param.UnitName;
            var msg = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"面积 = {areaVal:0.##} px² ({areaPhys:0.###} {unit}²)  重心({TupleText(row)}, {TupleText(column)})"
                : $"面积 = {TupleText(area)} px²  重心({TupleText(row)}, {TupleText(column)})";
            SetResult(contour, area, "ContourArea", msg);
        }
    }

    /// <summary>
    /// 轮廓方向测量。
    /// </summary>
    [Handle("轮廓方向", "OrientationXld", "计算XLD轮廓的主轴方向。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class OrientationXldHandle : ContourFeatureBase
    {
        public OrientationXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.OrientationXld(contour, out var phi);
            var angleDeg = phi.D * 180.0 / Math.PI;
            SetResult(contour, phi, "ContourOrientation", $"方向 = {angleDeg:0.###}°");
        }
    }

    /// <summary>
    /// 轮廓圆度测量。
    /// </summary>
    [Handle("轮廓圆度", "CircularityXld", "计算XLD轮廓的圆度。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class CircularityXldHandle : ContourFeatureBase
    {
        public CircularityXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.CircularityXld(contour, out var circularity);
            SetResult(contour, circularity, "ContourCircularity", $"圆度 = {TupleText(circularity, "0.####")}");
        }
    }

    /// <summary>
    /// 轮廓紧凑度测量。
    /// </summary>
    [Handle("轮廓紧凑度", "CompactnessXld", "计算XLD轮廓的紧凑度。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class CompactnessXldHandle : ContourFeatureBase
    {
        public CompactnessXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.CompactnessXld(contour, out var compactness);
            SetResult(contour, compactness, "ContourCompactness", $"紧凑度 = {TupleText(compactness, "0.####")}");
        }
    }

    /// <summary>
    /// 轮廓凸性测量。
    /// </summary>
    [Handle("轮廓凸性", "ConvexityXld", "计算XLD轮廓的凸性。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class ConvexityXldHandle : ContourFeatureBase
    {
        public ConvexityXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.ConvexityXld(contour, out var convexity);
            SetResult(contour, convexity, "ContourConvexity", $"凸性 = {TupleText(convexity, "0.####")}");
        }
    }

    /// <summary>
    /// 轮廓偏心率测量。
    /// </summary>
    [Handle("轮廓偏心率", "EccentricityXld", "计算XLD轮廓的偏心率。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class EccentricityXldHandle : ContourFeatureBase
    {
        public EccentricityXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.EccentricityXld(contour, out var anisometry, out var bulkiness, out var structureFactor);
            SetResult(contour, anisometry, "ContourEccentricity",
                $"各向异性 = {TupleText(anisometry, "0.####")}  体积度 = {TupleText(bulkiness, "0.####")}");
        }
    }

    /// <summary>
    /// 轮廓最小外接圆。
    /// </summary>
    [Handle("轮廓最小外接圆", "SmallestCircleXld", "计算XLD轮廓的最小外接圆，支持标定换算。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class SmallestCircleXldHandle : ContourFeatureBase
    {
        public SmallestCircleXldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.SmallestCircleXld(contour, out var row, out var column, out var radius);
            HOperatorSet.GenCircleContourXld(out var circle, row, column, radius, 0, 6.28318, "positive", 1.0);
            var param = Parameter ?? new FeatureMeasureParameter();
            double r = radius.Length > 0 ? radius[0].D : 0;
            var msg = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最小外接圆 Row={TupleText(row)}, Column={TupleText(column)}, Radius={param.FormatMeasure(r)}"
                : $"最小外接圆 Row={TupleText(row)}, Column={TupleText(column)}, Radius={TupleText(radius)}";
            SetResult(circle, radius, "SmallestCircleRadius", msg);
        }
    }

    /// <summary>
    /// 轮廓最小外接矩形。
    /// </summary>
    [Handle("轮廓最小外接矩形", "SmallestRectangle2Xld", "计算XLD轮廓的最小外接矩形，支持标定换算。", "轮廓处理", OperatorBackend = OperatorBackend.Halcon)]
    public class SmallestRectangle2XldHandle : ContourFeatureBase
    {
        public SmallestRectangle2XldHandle(IDialogService dialogService) : base(dialogService) { }

        public override void Handle()
        {
            var contour = GetContour();
            HOperatorSet.SmallestRectangle2Xld(contour, out var row, out var column, out var phi, out var length1, out var length2);
            HOperatorSet.GenRectangle2ContourXld(out var rectangle, row, column, phi, length1, length2);
            var param = Parameter ?? new FeatureMeasureParameter();
            double l1 = length1.Length > 0 ? length1[0].D : 0;
            double l2 = length2.Length > 0 ? length2[0].D : 0;
            var msg = Math.Abs(param.CalibScale - 1.0) > 1e-6
                ? $"最小外接矩形 Row={TupleText(row)}, Column={TupleText(column)}, Phi={TupleText(phi)}, L1={param.FormatMeasure(l1)}, L2={param.FormatMeasure(l2)}"
                : $"最小外接矩形 Row={TupleText(row)}, Column={TupleText(column)}, Phi={TupleText(phi)}, Length1={TupleText(length1)}, Length2={TupleText(length2)}";
            SetResult(rectangle, length1, "SmallestRectangle2Length1", msg);
        }
    }
}
