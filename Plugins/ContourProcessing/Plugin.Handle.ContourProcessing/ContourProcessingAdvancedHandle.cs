using System;
using System.Collections.Generic;
using System.ComponentModel;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ContourProcessing
{
    /// <summary>
    /// 区域转轮廓参数。
    /// </summary>
    public class GenContoursSkeletonXldParameter : BindableBase, IHandleParameter
    {
        private int length = 1;
        private int mode = 1;

        [DisplayName("长度")]
        [Description("最小分支长度")]
        public int Length { get => length; set => SetProperty(ref length, value); }

        [DisplayName("模式")]
        [Description("骨架模式：1-标准，2-保留端点")]
        public int Mode { get => mode; set => SetProperty(ref mode, value); }

        public IHandleParameter Clone() => new GenContoursSkeletonXldParameter
        {
            Length = Length,
            Mode = Mode
        };

        public void Reset()
        {
            Length = 1;
            Mode = 1;
        }
    }

    /// <summary>
    /// 轮廓仿射变换参数。
    /// </summary>
    public class AffineTransContourXldParameter : BindableBase, IHandleParameter
    {
        private double rowOffset = 0.0;
        private double columnOffset = 0.0;
        private double angleDeg = 0.0;
        private double scale = 1.0;

        [DisplayName("行平移")]
        [Description("Row 方向平移量 (px)")]
        public double RowOffset { get => rowOffset; set => SetProperty(ref rowOffset, value); }

        [DisplayName("列平移")]
        [Description("Column 方向平移量 (px)")]
        public double ColumnOffset { get => columnOffset; set => SetProperty(ref columnOffset, value); }

        [DisplayName("旋转角度")]
        [Description("旋转角度 (度)")]
        public double AngleDeg { get => angleDeg; set => SetProperty(ref angleDeg, value); }

        [DisplayName("缩放比例")]
        [Description("各向同性缩放比例")]
        public double Scale { get => scale; set => SetProperty(ref scale, value); }

        public IHandleParameter Clone() => new AffineTransContourXldParameter
        {
            RowOffset = RowOffset,
            ColumnOffset = ColumnOffset,
            AngleDeg = AngleDeg,
            Scale = Scale
        };

        public void Reset()
        {
            RowOffset = 0.0;
            ColumnOffset = 0.0;
            AngleDeg = 0.0;
            Scale = 1.0;
        }
    }

    /// <summary>
    /// 共圆轮廓联合参数。
    /// </summary>
    public class UnionCoclircularContoursXldParameter : BindableBase, IHandleParameter
    {
        private double maxDistCircle = 2.0;
        private double maxRadius = 1000.0;
        private double maxAngle = 0.2;
        private double maxArcOverlap = 0.2;
        private double maxOverlap = 0.5;
        private double minArea = 10.0;
        private string mode = "attr_keep";
        private string mergeSmallContours = "false";

        [DisplayName("最大圆距离")]
        [Description("轮廓到拟合圆的最大法向距离")]
        public double MaxDistCircle { get => maxDistCircle; set => SetProperty(ref maxDistCircle, value); }

        [DisplayName("最大半径")]
        [Description("允许的最大拟合圆半径")]
        public double MaxRadius { get => maxRadius; set => SetProperty(ref maxRadius, value); }

        [DisplayName("最大角度差异")]
        [Description("轮廓切线与圆切线的最大允许夹角（弧度）")]
        public double MaxAngle { get => maxAngle; set => SetProperty(ref maxAngle, value); }

        [DisplayName("最大圆弧重叠")]
        [Description("轮廓在圆周上的最大重叠度")]
        public double MaxArcOverlap { get => maxArcOverlap; set => SetProperty(ref maxArcOverlap, value); }

        [DisplayName("最大重叠")]
        [Description("轮廓端点最大重叠")]
        public double MaxOverlap { get => maxOverlap; set => SetProperty(ref maxOverlap, value); }

        [DisplayName("最小面积")]
        [Description("最小圆面积")]
        public double MinArea { get => minArea; set => SetProperty(ref minArea, value); }

        [DisplayName("属性模式")]
        [Description("属性模式：attr_keep, attr_forget")]
        public string Mode { get => mode; set => SetProperty(ref mode, value); }

        [DisplayName("合并小轮廓")]
        [Description("是否合并小轮廓：true, false")]
        public string MergeSmallContours { get => mergeSmallContours; set => SetProperty(ref mergeSmallContours, value); }

        public IHandleParameter Clone() => new UnionCoclircularContoursXldParameter
        {
            MaxDistCircle = MaxDistCircle,
            MaxRadius = MaxRadius,
            MaxAngle = MaxAngle,
            MaxArcOverlap = MaxArcOverlap,
            MaxOverlap = MaxOverlap,
            MinArea = MinArea,
            Mode = Mode,
            MergeSmallContours = MergeSmallContours
        };

        public void Reset()
        {
            MaxDistCircle = 2.0;
            MaxRadius = 1000.0;
            MaxAngle = 0.2;
            MaxArcOverlap = 0.2;
            MaxOverlap = 0.5;
            MinArea = 10.0;
            Mode = "attr_keep";
            MergeSmallContours = "false";
        }
    }

    /// <summary>
    /// 轮廓排序参数。
    /// </summary>
    public class SortContoursXldParameter : BindableBase, IHandleParameter
    {
        private string mode = "upper_left";
        private string order = "true";
        private string rowOrCol = "row";

        [DisplayName("基准位置")]
        [Description("排序参考位置：upper_left, upper_right, lower_left, lower_right, character")]
        public string Mode { get => mode; set => SetProperty(ref mode, value); }

        [DisplayName("升降序")]
        [Description("true 为升序，false 为降序")]
        public string Order { get => order; set => SetProperty(ref order, value); }

        [DisplayName("行列优先")]
        [Description("row 为行优先，column 为列优先")]
        public string RowOrCol { get => rowOrCol; set => SetProperty(ref rowOrCol, value); }

        public IHandleParameter Clone() => new SortContoursXldParameter
        {
            Mode = Mode,
            Order = Order,
            RowOrCol = RowOrCol
        };

        public void Reset()
        {
            Mode = "upper_left";
            Order = "true";
            RowOrCol = "row";
        }
    }

    /// <summary>
    /// 共线轮廓联合参数。
    /// </summary>
    public class UnionCollinearContoursXldParameter : BindableBase, IHandleParameter
    {
        private double maxDistAbs = 10.0;
        private double maxDistRel = 10.0;
        private double maxShift = 2.0;
        private double maxAngle = 0.1;

        [DisplayName("最大绝对距离")]
        [Description("两轮廓端点之间的最大绝对距离")]
        public double MaxDistAbs { get => maxDistAbs; set => SetProperty(ref maxDistAbs, value); }

        [DisplayName("最大相对距离")]
        [Description("两轮廓端点之间的最大相对距离")]
        public double MaxDistRel { get => maxDistRel; set => SetProperty(ref maxDistRel, value); }

        [DisplayName("最大法向偏离")]
        [Description("两轮廓端点到拟合直线的最大法向距离")]
        public double MaxShift { get => maxShift; set => SetProperty(ref maxShift, value); }

        [DisplayName("最大允许夹角")]
        [Description("两轮廓方向之间的最大允许夹角（弧度）")]
        public double MaxAngle { get => maxAngle; set => SetProperty(ref maxAngle, value); }

        public IHandleParameter Clone() => new UnionCollinearContoursXldParameter
        {
            MaxDistAbs = MaxDistAbs,
            MaxDistRel = MaxDistRel,
            MaxShift = MaxShift,
            MaxAngle = MaxAngle
        };

        public void Reset()
        {
            MaxDistAbs = 10.0;
            MaxDistRel = 10.0;
            MaxShift = 2.0;
            MaxAngle = 0.1;
        }
    }

    /// <summary>
    /// 轮廓属性分割参数。
    /// </summary>
    public class SegmentContourAttribXldParameter : BindableBase, IHandleParameter
    {
        private string attribute = "edge_direction";
        private string operation = "and";
        private double min = 0;
        private double max = 3.14159;

        [DisplayName("属性名称")]
        [Description("用于分割的属性名称，例如 edge_direction, angle 等")]
        public string Attribute { get => attribute; set => SetProperty(ref attribute, value); }

        [DisplayName("逻辑操作")]
        [Description("条件组合方式：and 或 or")]
        public string Operation { get => operation; set => SetProperty(ref operation, value); }

        [DisplayName("属性下限")]
        [Description("属性允许的最小值")]
        public double Min { get => min; set => SetProperty(ref min, value); }

        [DisplayName("属性上限")]
        [Description("属性允许的最大值")]
        public double Max { get => max; set => SetProperty(ref max, value); }

        public IHandleParameter Clone() => new SegmentContourAttribXldParameter
        {
            Attribute = Attribute,
            Operation = Operation,
            Min = Min,
            Max = Max
        };

        public void Reset()
        {
            Attribute = "edge_direction";
            Operation = "and";
            Min = 0;
            Max = 3.14159;
        }
    }

    /// <summary>
    /// 区域转轮廓算子。
    /// </summary>
    [Handle("区域转轮廓", "GenContoursSkeletonXld", "从区域的骨架生成XLD轮廓。", "轮廓处理")]
    public class GenContoursSkeletonXldHandle : ParameterHandleBase<GenContoursSkeletonXldParameter>, IHandleDataSlots
    {
        public GenContoursSkeletonXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.GenContoursSkeletonXld(region, out var contours, Parameter.Length, Parameter.Mode);

            RunParameter.HOutput = contours;
            RunParameter.SetOutput(RunOutputKeys.Output, contours);
            RunParameter.SetOutput(RunOutputKeys.Contour, contours);
            RunParameter.ResultMessage = "区域转轮廓完成";
            RaiseImageDisplay(contours);
        }
    }

    /// <summary>
    /// 轮廓仿射变换算子。
    /// </summary>
    [Handle("轮廓仿射变换", "AffineTransContourXld", "对XLD轮廓执行平移、旋转与缩放变换。", "轮廓处理")]
    public class AffineTransContourXldHandle : ParameterHandleBase<AffineTransContourXldParameter>, IHandleDataSlots
    {
        public AffineTransContourXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "变换轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.HomMat2dIdentity(out var homMat2D);
            if (Math.Abs(Parameter.Scale - 1.0) > 0.0001)
            {
                HOperatorSet.HomMat2dScale(homMat2D, Math.Max(0.001, Parameter.Scale), Math.Max(0.001, Parameter.Scale), 0, 0, out homMat2D);
            }
            if (Math.Abs(Parameter.AngleDeg) > 0.0001)
            {
                var rad = Parameter.AngleDeg * Math.PI / 180.0;
                HOperatorSet.HomMat2dRotate(homMat2D, rad, 0, 0, out homMat2D);
            }
            if (Math.Abs(Parameter.RowOffset) > 0.0001 || Math.Abs(Parameter.ColumnOffset) > 0.0001)
            {
                HOperatorSet.HomMat2dTranslate(homMat2D, Parameter.RowOffset, Parameter.ColumnOffset, out homMat2D);
            }

            HOperatorSet.AffineTransContourXld(contour, out var transformed, homMat2D);

            RunParameter.HOutput = transformed;
            RunParameter.SetOutput(RunOutputKeys.Output, transformed);
            RunParameter.SetOutput(RunOutputKeys.Contour, transformed);
            RunParameter.ResultMessage = $"轮廓仿射变换完成 (平移: {Parameter.RowOffset:0.#}, {Parameter.ColumnOffset:0.#}, 旋转: {Parameter.AngleDeg:0.#}°)";
            RaiseImageDisplay(transformed);
        }
    }

    /// <summary>
    /// 轮廓联合（共圆）算子。
    /// </summary>
    [Handle("轮廓联合(共圆)", "UnionCoclircularContoursXld", "联合处于同一圆周上的XLD轮廓。", "轮廓处理")]
    public class UnionCoclircularContoursXldHandle : ParameterHandleBase<UnionCoclircularContoursXldParameter>, IHandleDataSlots
    {
        public UnionCoclircularContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

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

            HOperatorSet.UnionCocircularContoursXld(
                contour,
                out var united,
                Math.Max(0.1, Parameter.MaxDistCircle),
                Math.Max(1.0, Parameter.MaxRadius),
                Math.Max(0.01, Parameter.MaxAngle),
                Parameter.MaxArcOverlap,
                Parameter.MaxOverlap,
                Math.Max(0.0, Parameter.MinArea),
                Parameter.Mode ?? "attr_keep",
                Parameter.MergeSmallContours ?? "false");

            RunParameter.HOutput = united;
            RunParameter.SetOutput(RunOutputKeys.Output, united);
            RunParameter.SetOutput(RunOutputKeys.Contour, united);
            RunParameter.ResultMessage = "共圆轮廓联合完成";
            RaiseImageDisplay(united);
        }
    }

    /// <summary>
    /// 轮廓排序算子。
    /// </summary>
    [Handle("轮廓排序", "SortContoursXld", "按照指定特征和空间顺序对XLD轮廓进行排序。", "轮廓处理")]
    public class SortContoursXldHandle : ParameterHandleBase<SortContoursXldParameter>, IHandleDataSlots
    {
        public SortContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "排序轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.SortContoursXld(
                contour,
                out var sorted,
                Parameter.Mode ?? "upper_left",
                Parameter.Order ?? "true",
                Parameter.RowOrCol ?? "row");

            RunParameter.HOutput = sorted;
            RunParameter.SetOutput(RunOutputKeys.Output, sorted);
            RunParameter.SetOutput(RunOutputKeys.Contour, sorted);
            RunParameter.ResultMessage = $"轮廓排序完成 ({Parameter.RowOrCol}, {Parameter.Mode})";
            RaiseImageDisplay(sorted);
        }
    }

    /// <summary>
    /// 轮廓分割成线段算子。
    /// </summary>
    [Handle("轮廓分割成线段", "SegmentContourAttribXld", "根据属性将XLD轮廓分割成多个线段。", "轮廓处理")]
    public class SegmentContourAttribXldHandle : ParameterHandleBase<SegmentContourAttribXldParameter>, IHandleDataSlots
    {
        public SegmentContourAttribXldHandle(IDialogService dialogService) : base(dialogService) { }

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

            var min = Math.Min(Parameter.Min, Parameter.Max);
            var max = Math.Max(Parameter.Min, Parameter.Max);

            HOperatorSet.SegmentContourAttribXld(
                contour,
                out var segmented,
                Parameter.Attribute ?? "edge_direction",
                Parameter.Operation ?? "and",
                min,
                max);

            RunParameter.HOutput = segmented;
            RunParameter.SetOutput(RunOutputKeys.Output, segmented);
            RunParameter.SetOutput(RunOutputKeys.Contour, segmented);
            RunParameter.ResultMessage = $"轮廓属性分割完成 (属性: {Parameter.Attribute})";
            RaiseImageDisplay(segmented);
        }
    }

    /// <summary>
    /// 共线轮廓联合算子。
    /// </summary>
    [Handle("共线轮廓联合", "UnionCollinearContoursXld", "联合处于同一直线上的相邻XLD轮廓。", "轮廓处理")]
    public class UnionCollinearContoursXldHandle : ParameterHandleBase<UnionCollinearContoursXldParameter>, IHandleDataSlots
    {
        public UnionCollinearContoursXldHandle(IDialogService dialogService) : base(dialogService) { }

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

            HOperatorSet.UnionCollinearContoursXld(
                contour,
                out var united,
                Math.Max(0.1, Parameter.MaxDistAbs),
                Math.Max(0.1, Parameter.MaxDistRel),
                Math.Max(0.1, Parameter.MaxShift),
                Math.Max(0.01, Parameter.MaxAngle),
                "attr_keep");

            RunParameter.HOutput = united;
            RunParameter.SetOutput(RunOutputKeys.Output, united);
            RunParameter.SetOutput(RunOutputKeys.Contour, united);
            RunParameter.ResultMessage = "共线轮廓联合完成";
            RaiseImageDisplay(united);
        }
    }

    /// <summary>
    /// 轮廓分割成点算子。
    /// </summary>
    [Handle("轮廓提取点坐标", "SegmentContoursXldPoint", "提取XLD轮廓所有控制点的行列坐标。", "轮廓处理")]
    public class SegmentContoursXldPointHandle : HandleBase, IHandleDataSlots
    {
        public SegmentContoursXldPointHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot("Rows", "行坐标", typeof(HTuple)),
            new RunSlot("Cols", "列坐标", typeof(HTuple))
        };

        public override void Handle()
        {
            var contour = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput,
                GetType().Name);

            HOperatorSet.GetContourXld(contour, out var rows, out var cols);

            RunParameter.HOutput = contour;
            RunParameter.SetOutput(RunOutputKeys.Output, contour);
            RunParameter.SetOutput("Rows", rows);
            RunParameter.SetOutput("Cols", cols);
            RunParameter.ResultMessage = $"轮廓点提取完成，共 {rows.Length} 个点";
            RaiseImageDisplay(contour);
        }
    }
}
