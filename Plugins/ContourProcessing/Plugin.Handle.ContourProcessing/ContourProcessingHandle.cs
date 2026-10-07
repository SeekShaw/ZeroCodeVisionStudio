using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ContourProcessing
{
    /// <summary>
    /// 轮廓平滑算子。
    /// </summary>
    [Handle("轮廓平滑", "SmoothContoursXld", "对XLD轮廓进行平滑处理。", "轮廓处理")]
    public class SmoothContoursXldHandle : ParameterHandleBase<SmoothContoursXldParameter>, IHandleDataSlots
    {
        public SmoothContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "平滑轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.SmoothContoursXld(contour, out var smoothed, Parameter.SmoothingFactor);

            RunParameter.HOutput = smoothed;
            RunParameter.SetOutput(RunOutputKeys.Output, smoothed);
            RunParameter.SetOutput(RunOutputKeys.Contour, smoothed);
            RunParameter.ResultMessage = $"轮廓平滑完成，平滑因子 = {Parameter.SmoothingFactor}";
            RaiseImageDisplay(smoothed);
        }
    }

    /// <summary>
    /// 轮廓分割算子。
    /// </summary>
    [Handle("轮廓分割", "SegmentContoursXld", "将XLD轮廓分割为直线段、圆弧或椭圆弧。", "轮廓处理")]
    public class SegmentContoursXldHandle : ParameterHandleBase<SegmentContoursXldParameter>, IHandleDataSlots
    {
        public SegmentContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "分割轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.SegmentContoursXld(
                contour,
                out var segmented,
                Parameter.Mode,
                Parameter.MaxLineWidth,
                Parameter.PointOrderThreshold,
                2);

            RunParameter.HOutput = segmented;
            RunParameter.SetOutput(RunOutputKeys.Output, segmented);
            RunParameter.SetOutput(RunOutputKeys.Contour, segmented);
            RunParameter.ResultMessage = $"轮廓分割完成，模式 = {Parameter.Mode}";
            RaiseImageDisplay(segmented);
        }
    }

    /// <summary>
    /// 轮廓选择算子。
    /// </summary>
    [Handle("轮廓选择", "SelectContoursXld", "根据特征值筛选XLD轮廓。", "轮廓处理")]
    public class SelectContoursXldHandle : ParameterHandleBase<SelectContoursXldParameter>, IHandleDataSlots
    {
        public SelectContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "选择轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            var min = Math.Min(Parameter.MinValue, Parameter.MaxValue);
            var max = Math.Max(Parameter.MinValue, Parameter.MaxValue);

            HOperatorSet.SelectContoursXld(
                contour,
                out var selected,
                Parameter.Feature,
                min,
                max,
                -0.5,
                0.5);

            RunParameter.HOutput = selected;
            RunParameter.SetOutput(RunOutputKeys.Output, selected);
            RunParameter.SetOutput(RunOutputKeys.Contour, selected);
            RunParameter.ResultMessage = $"轮廓选择完成，特征 = {Parameter.Feature}, 范围 = [{min:0.##}, {max:0.##}]";
            RaiseImageDisplay(selected);
        }
    }

    /// <summary>
    /// 轮廓直线拟合算子。
    /// </summary>
    [Handle("轮廓直线拟合", "FitLineContourXld", "对XLD轮廓进行直线拟合。", "轮廓处理")]
    public class FitLineContourXldHandle : ParameterHandleBase<FitLineContourXldParameter>, IHandleDataSlots
    {
        public FitLineContourXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot("RowBegin", "起点行坐标", typeof(HTuple)),
            new RunSlot("ColBegin", "起点列坐标", typeof(HTuple)),
            new RunSlot("RowEnd", "终点行坐标", typeof(HTuple)),
            new RunSlot("ColEnd", "终点列坐标", typeof(HTuple))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.FitLineContourXld(
                contour,
                Parameter.Algorithm,
                Parameter.MaxNumIterations,
                Parameter.ClippingFactor,
                0,
                0,
                out var rowBegin,
                out var colBegin,
                out var rowEnd,
                out var colEnd,
                out var nr,
                out var nc,
                out var dist);

            if (rowBegin.Length > 0)
            {
                HOperatorSet.GenContourPolygonXld(out var line, new HTuple(rowBegin, rowEnd), new HTuple(colBegin, colEnd));
                RunParameter.HOutput = line;
                RunParameter.SetOutput(RunOutputKeys.Output, line);
                RunParameter.SetOutput("RowBegin", rowBegin);
                RunParameter.SetOutput("ColBegin", colBegin);
                RunParameter.SetOutput("RowEnd", rowEnd);
                RunParameter.SetOutput("ColEnd", colEnd);
                RunParameter.ResultMessage = $"直线拟合完成，共 {rowBegin.Length} 条线段，首段起点({rowBegin[0].D:0.##}, {colBegin[0].D:0.##})，终点({rowEnd[0].D:0.##}, {colEnd[0].D:0.##})";
                RaiseImageDisplay(line);
            }
            else
            {
                HOperatorSet.GenEmptyObj(out var empty);
                RunParameter.HOutput = empty;
                RunParameter.SetOutput(RunOutputKeys.Output, empty);
                RunParameter.ResultMessage = "未拟合出有效直线。";
            }
        }
    }

    /// <summary>
    /// 轮廓圆拟合算子。
    /// </summary>
    [Handle("轮廓圆拟合", "FitCircleContourXld", "对XLD轮廓进行圆拟合。", "轮廓处理")]
    public class FitCircleContourXldHandle : ParameterHandleBase<FitCircleContourXldParameter>, IHandleDataSlots
    {
        public FitCircleContourXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出圆", typeof(HObject)),
            new RunSlot("Row", "圆心行坐标", typeof(HTuple)),
            new RunSlot("Column", "圆心列坐标", typeof(HTuple)),
            new RunSlot("Radius", "半径", typeof(HTuple))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.FitCircleContourXld(
                contour,
                Parameter.Algorithm,
                Parameter.MaxNumIterations,
                Parameter.ClippingFactor,
                0,
                0,
                0,
                out var row,
                out var column,
                out var radius,
                out var startPhi,
                out var endPhi,
                out var pointOrder);

            if (row.Length > 0 && radius.Length > 0)
            {
                HOperatorSet.GenCircleContourXld(out var circle, row, column, radius, 0, 6.28318, "positive", 1.0);
                RunParameter.HOutput = circle;
                RunParameter.SetOutput(RunOutputKeys.Output, circle);
                RunParameter.SetOutput("Row", row);
                RunParameter.SetOutput("Column", column);
                RunParameter.SetOutput("Radius", radius);
                RunParameter.ResultMessage = $"圆拟合完成，共 {row.Length} 个圆，首个圆心({row[0].D:0.##}, {column[0].D:0.##})，半径 = {radius[0].D:0.##}";
                RaiseImageDisplay(circle);
            }
            else
            {
                HOperatorSet.GenEmptyObj(out var empty);
                RunParameter.HOutput = empty;
                RunParameter.SetOutput(RunOutputKeys.Output, empty);
                RunParameter.ResultMessage = "未拟合出有效圆。";
            }
        }
    }

    /// <summary>
    /// 轮廓椭圆拟合算子。
    /// </summary>
    [Handle("轮廓椭圆拟合", "FitEllipseContourXld", "对XLD轮廓进行椭圆拟合。", "轮廓处理")]
    public class FitEllipseContourXldHandle : ParameterHandleBase<FitEllipseContourXldParameter>, IHandleDataSlots
    {
        public FitEllipseContourXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出椭圆", typeof(HObject)),
            new RunSlot("Row", "中心行坐标", typeof(HTuple)),
            new RunSlot("Column", "中心列坐标", typeof(HTuple)),
            new RunSlot("Phi", "方向角", typeof(HTuple)),
            new RunSlot("Radius1", "长半轴", typeof(HTuple)),
            new RunSlot("Radius2", "短半轴", typeof(HTuple))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.FitEllipseContourXld(
                contour,
                Parameter.Algorithm,
                Parameter.MaxNumIterations,
                Parameter.ClippingFactor,
                0,
                0,
                0,
                0,
                out var row,
                out var column,
                out var phi,
                out var radius1,
                out var radius2,
                out var startPhi,
                out var endPhi,
                out var pointOrder);

            if (row.Length > 0 && radius1.Length > 0)
            {
                HOperatorSet.GenEllipseContourXld(out var ellipse, row, column, phi, radius1, radius2, 0, 6.28318, "positive", 1.0);
                RunParameter.HOutput = ellipse;
                RunParameter.SetOutput(RunOutputKeys.Output, ellipse);
                RunParameter.SetOutput("Row", row);
                RunParameter.SetOutput("Column", column);
                RunParameter.SetOutput("Phi", phi);
                RunParameter.SetOutput("Radius1", radius1);
                RunParameter.SetOutput("Radius2", radius2);

                var angleDeg = phi[0].D * 180.0 / Math.PI;
                RunParameter.ResultMessage = $"椭圆拟合完成，共 {row.Length} 个椭圆，首个中心({row[0].D:0.##}, {column[0].D:0.##})，长轴={radius1[0].D:0.##}，短轴={radius2[0].D:0.##}，角度={angleDeg:0.##}°";
                RaiseImageDisplay(ellipse);
            }
            else
            {
                HOperatorSet.GenEmptyObj(out var empty);
                RunParameter.HOutput = empty;
                RunParameter.SetOutput(RunOutputKeys.Output, empty);
                RunParameter.ResultMessage = "未拟合出有效椭圆。";
            }
        }
    }

    /// <summary>
    /// 轮廓相邻联合算子。
    /// </summary>
    [Handle("轮廓联合", "UnionAdjacentContoursXld", "将相邻的XLD轮廓联合成一条轮廓。", "轮廓处理")]
    public class UnionAdjacentContoursXldHandle : ParameterHandleBase<UnionAdjacentContoursXldParameter>, IHandleDataSlots
    {
        public UnionAdjacentContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "联合轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.UnionAdjacentContoursXld(
                contour,
                out var united,
                Math.Max(0.1, Parameter.MaxDistAbs),
                Math.Max(0.1, Parameter.MaxDistRel),
                Parameter.Mode ?? "attr_keep");

            RunParameter.HOutput = united;
            RunParameter.SetOutput(RunOutputKeys.Output, united);
            RunParameter.SetOutput(RunOutputKeys.Contour, united);
            RunParameter.ResultMessage = $"轮廓相邻联合完成 (DistAbs: {Parameter.MaxDistAbs})";
            RaiseImageDisplay(united);
        }
    }

    /// <summary>
    /// 轮廓闭合算子。
    /// </summary>
    [Handle("轮廓闭合", "CloseContoursXld", "闭合开放的XLD轮廓。", "轮廓处理")]
    public class CloseContoursXldHandle : ParameterHandleBase<CloseContoursXldParameter>, IHandleDataSlots
    {
        public CloseContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "闭合轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.CloseContoursXld(contour, out var closed);

            RunParameter.HOutput = closed;
            RunParameter.SetOutput(RunOutputKeys.Output, closed);
            RunParameter.SetOutput(RunOutputKeys.Contour, closed);
            RunParameter.ResultMessage = "轮廓闭合完成";
            RaiseImageDisplay(closed);
        }
    }
}
