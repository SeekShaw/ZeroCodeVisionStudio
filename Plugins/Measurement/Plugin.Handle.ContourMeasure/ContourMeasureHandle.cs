using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Plugin.Handle.ContourMeasure.Views;

namespace Plugin.Handle.ContourMeasure
{
    /// <summary>
    /// 轮廓测量算子基类。
    /// </summary>
    public abstract class ContourMeasureBase : HandleBase, IHandleDataSlots
    {
        public ContourMeasureParameter Parameter { get; }

        protected ContourMeasureBase(IDialogService dialogService) : base(dialogService)
        {
            Parameter = new ContourMeasureParameter();
            HandleParameter = Parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(ContourMeasureView);

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像/轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.ContourLength, "轮廓长度(px)", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理测量值", typeof(double))
        };

        protected HObject GetInputContour(string slotKey = RunOutputKeys.Contour)
        {
            var runModel = ContextRunModel;
            if (runModel?.InputBindings != null)
            {
                foreach (var binding in runModel.InputBindings)
                {
                    var src = binding.SourceNode?.Handle?.RunParameter;
                    if (src == null) continue;
                    var c = src.GetOutput<HObject>(slotKey) ?? src.GetOutput<HObject>(RunOutputKeys.Contour) ?? src.HOutput;
                    if (HalconDisplayHelper.IsValidObject(c))
                        return c;
                }
            }

            var local = RunParameter?.GetOutput<HObject>(slotKey) ?? RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour) ?? RunParameter?.HOutput;
            return HandleInputGuard.RequireContour(local, GetType().Name);
        }

        protected HObject GetInputRegion(string slotKey = RunOutputKeys.Region)
        {
            var runModel = ContextRunModel;
            if (runModel?.InputBindings != null)
            {
                foreach (var binding in runModel.InputBindings)
                {
                    var src = binding.SourceNode?.Handle?.RunParameter;
                    if (src == null) continue;
                    var r = src.GetOutput<HObject>(slotKey) ?? src.GetOutput<HObject>(RunOutputKeys.Region) ?? src.HOutput;
                    if (HalconDisplayHelper.IsValidObject(r))
                        return r;
                }
            }

            var local = RunParameter?.GetOutput<HObject>(slotKey) ?? RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput;
            return HandleInputGuard.RequireRegion(local, GetType().Name);
        }
    }

    /// <summary>
    /// 轮廓长度测量算子。
    /// </summary>
    [Handle("轮廓长度", "ContourLength", "计算XLD轮廓的总长度并支持物理标定换算。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class ContourLengthHandle : ContourMeasureBase
    {
        public ContourLengthHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.ContourLength, "轮廓长度(px)", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理长度", typeof(double))
        };

        public override void Handle()
        {
            var contour = GetInputContour();

            var targetContour = contour;
            if (Parameter.SmoothContours)
            {
                HOperatorSet.SmoothContoursXld(contour, out var smoothed, Parameter.SmoothNumPoints);
                targetContour = smoothed;
            }

            HOperatorSet.LengthXld(targetContour, out var length);
            var totalLength = length.Length > 0 ? length.TupleSum().D : 0.0;
            var physicalVal = Parameter.ToPhysical(totalLength);
            var msg = Parameter.FormatMeasure(totalLength);

            RunParameter.HOutput = targetContour;
            RunParameter.SetOutput(RunOutputKeys.Output, targetContour);
            RunParameter.SetOutput(RunOutputKeys.Contour, targetContour);
            RunParameter.SetOutput(RunOutputKeys.ContourLength, totalLength);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);

            RunParameter.ResultMessage = $"轮廓长度: {msg}";
            RaiseImageDisplay(targetContour);
        }
    }

    /// <summary>
    /// 区域周长测量算子。
    /// </summary>
    [Handle("区域周长", "ContourPerimeter", "计算闭合区域或边界的周长（像素与物理单位）。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class ContourPerimeterHandle : ContourMeasureBase
    {
        public const string PerimeterSlotKey = "Perimeter";

        public ContourPerimeterHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject)),
            new RunSlot(PerimeterSlotKey, "周长(px)", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理周长", typeof(double))
        };

        public override void Handle()
        {
            var region = GetInputRegion();

            HOperatorSet.RegionFeatures(region, "perimeter", out var perimeter);
            var perimeterVal = perimeter.Length > 0 ? perimeter.D : 0.0;
            var physicalVal = Parameter.ToPhysical(perimeterVal);
            var msg = Parameter.FormatMeasure(perimeterVal);

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.SetOutput(PerimeterSlotKey, perimeterVal);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);

            RunParameter.ResultMessage = $"区域周长: {msg}";
            RaiseImageDisplay(region);
        }
    }

    /// <summary>
    /// 轮廓综合特征测量算子。
    /// </summary>
    [Handle("轮廓综合特征", "ContourFeatures", "综合提取XLD轮廓的长度、面积、圆度及凸性等特征。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class ContourFeaturesHandle : ContourMeasureBase
    {
        public const string ContourAreaSlotKey = "ContourArea";
        public const string CircularitySlotKey = "ContourCircularity";
        public const string ConvexitySlotKey = "ContourConvexity";

        public ContourFeaturesHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Contour, "输入轮廓", typeof(HObject))
        };

        public override IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Contour, "轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.ContourLength, "轮廓长度(px)", typeof(HTuple)),
            new RunSlot(ContourAreaSlotKey, "轮廓面积(px²)", typeof(HTuple)),
            new RunSlot(CircularitySlotKey, "轮廓圆度", typeof(HTuple)),
            new RunSlot(ConvexitySlotKey, "轮廓凸性", typeof(HTuple)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理长度", typeof(double))
        };

        public override void Handle()
        {
            var contour = GetInputContour();

            var targetContour = contour;
            if (Parameter.SmoothContours)
            {
                HOperatorSet.SmoothContoursXld(contour, out var smoothed, Parameter.SmoothNumPoints);
                targetContour = smoothed;
            }

            HOperatorSet.LengthXld(targetContour, out var length);
            HOperatorSet.AreaCenterXld(targetContour, out var area, out _, out _, out _);
            HOperatorSet.CircularityXld(targetContour, out var circularity);
            HOperatorSet.ConvexityXld(targetContour, out var convexity);

            var totalLength = length.Length > 0 ? length.TupleSum().D : 0.0;
            var totalArea = area.Length > 0 ? area.TupleSum().D : 0.0;
            var avgCircularity = circularity.Length > 0 ? circularity.TupleMean().D : 0.0;
            var avgConvexity = convexity.Length > 0 ? convexity.TupleMean().D : 0.0;

            var physicalVal = Parameter.ToPhysical(totalLength);
            var msg = Parameter.FormatMeasure(totalLength);

            RunParameter.HOutput = targetContour;
            RunParameter.SetOutput(RunOutputKeys.Output, targetContour);
            RunParameter.SetOutput(RunOutputKeys.Contour, targetContour);
            RunParameter.SetOutput(RunOutputKeys.ContourLength, totalLength);
            RunParameter.SetOutput(ContourAreaSlotKey, totalArea);
            RunParameter.SetOutput(CircularitySlotKey, avgCircularity);
            RunParameter.SetOutput(ConvexitySlotKey, avgConvexity);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);

            RunParameter.ResultMessage = $"轮廓特征: 长度={msg}, 面积={totalArea:0.##}px², 圆度={avgCircularity:0.###}, 凸性={avgConvexity:0.###}";
            RaiseImageDisplay(targetContour);
        }
    }
}
