using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;
using Plugin.Handle.ShapeFeature.Views;

namespace Plugin.Handle.ShapeFeature
{
    /// <summary>
    /// 形状特征测量算子基类。
    /// </summary>
    public abstract class ShapeFeatureBase : HandleBase, IHandleDataSlots
    {
        public ShapeFeatureParameter Parameter { get; }

        protected ShapeFeatureBase(IDialogService dialogService) : base(dialogService)
        {
            Parameter = new ShapeFeatureParameter();
            HandleParameter = Parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(ShapeFeatureView);

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像/区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        protected HObject GetRegion()
        {
            var runModel = ContextRunModel;
            if (runModel?.InputBindings != null)
            {
                foreach (var binding in runModel.InputBindings)
                {
                    var src = binding.SourceNode?.Handle?.RunParameter;
                    if (src == null) continue;
                    var reg = src.GetOutput<HObject>(RunOutputKeys.Region) ?? src.HOutput;
                    if (reg != null && reg.IsInitialized() && reg.Key != IntPtr.Zero)
                        return reg;
                }
            }

            return HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                GetType().Name);
        }

        protected void SetResult(HObject result, double value, string featureName, string message, double physicalVal)
        {
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.SetOutput("FeatureValue", value);
            RunParameter.SetOutput(featureName, value);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);
            RunParameter.ResultMessage = message;
            RaiseImageDisplay(result);
        }
    }

    /// <summary>
    /// 矩形度测量算子。
    /// </summary>
    [Handle("矩形度", "Rectangularity", "计算区域的矩形度，值越接近1越接近矩形。", Common.HandleType_Measurement)]
    public class RectangularityHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Rectangularity";

        public RectangularityHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "矩形度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "rectangularity", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var physicalVal = Parameter.ToPhysical(val);
            var msg = Parameter.FormatMeasure(val);
            SetResult(region, val, SlotKey, $"矩形度 = {msg}", physicalVal);
        }
    }

    /// <summary>
    /// 圆度测量算子。
    /// </summary>
    [Handle("圆度", "Circularity", "计算区域的圆度，值越接近1越接近圆形。", Common.HandleType_Measurement)]
    public class CircularityHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Circularity";

        public CircularityHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "圆度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "circularity", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var physicalVal = Parameter.ToPhysical(val);
            var msg = Parameter.FormatMeasure(val);
            SetResult(region, val, SlotKey, $"圆度 = {msg}", physicalVal);
        }
    }

    /// <summary>
    /// 紧凑度测量算子。
    /// </summary>
    [Handle("紧凑度", "Compactness", "计算区域的紧凑度，值越小越紧凑。", Common.HandleType_Measurement)]
    public class CompactnessHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Compactness";

        public CompactnessHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "紧凑度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "compactness", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var physicalVal = Parameter.ToPhysical(val);
            var msg = Parameter.FormatMeasure(val);
            SetResult(region, val, SlotKey, $"紧凑度 = {msg}", physicalVal);
        }
    }

    /// <summary>
    /// 凸性测量算子。
    /// </summary>
    [Handle("凸性", "Convexity", "计算区域的凸性，值越接近1越接近凸形状。", Common.HandleType_Measurement)]
    public class ConvexityHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Convexity";

        public ConvexityHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "凸性", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "convexity", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var physicalVal = Parameter.ToPhysical(val);
            var msg = Parameter.FormatMeasure(val);
            SetResult(region, val, SlotKey, $"凸性 = {msg}", physicalVal);
        }
    }

    /// <summary>
    /// 伸长度测量算子。
    /// </summary>
    [Handle("伸长度", "Elongation", "计算区域的伸长度，值越大越细长。", Common.HandleType_Measurement)]
    public class ElongationHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Elongation";

        public ElongationHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "伸长度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "换算测量值", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "elongation", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var physicalVal = Parameter.ToPhysical(val);
            var msg = Parameter.FormatMeasure(val);
            SetResult(region, val, SlotKey, $"伸长度 = {msg}", physicalVal);
        }
    }

    /// <summary>
    /// 区域方向测量算子。
    /// </summary>
    [Handle("区域方向", "Orientation", "计算区域的主轴方向（度）。", Common.HandleType_Measurement)]
    public class OrientationHandle : ShapeFeatureBase
    {
        public const string SlotKey = "Orientation";

        public OrientationHandle(IDialogService dialogService) : base(dialogService)
        {
            Parameter.UnitName = "°";
        }

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot("FeatureValue", "特征值", typeof(HTuple)),
            new RunSlot(SlotKey, "主轴方向(度)", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "测量角度(度)", typeof(double))
        };

        public override void Handle()
        {
            var region = GetRegion();
            HOperatorSet.RegionFeatures(region, "orientation", out var value);
            var val = value.Length > 0 ? value.D : 0.0;
            var angleDeg = val * 180.0 / Math.PI;
            var physicalVal = Parameter.ToPhysical(angleDeg);
            var msg = Parameter.FormatMeasure(angleDeg);
            SetResult(region, angleDeg, SlotKey, $"方向 = {msg}", physicalVal);
        }
    }
}
