using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;
using Plugin.Handle.DistanceMeasure.Views;

namespace Plugin.Handle.DistanceMeasure
{
    /// <summary>
    /// 距离测量算子基类。
    /// </summary>
    public abstract class DistanceMeasureBase : HandleBase, IHandleDataSlots
    {
        protected DistanceMeasureBase(IDialogService dialogService) : base(dialogService)
        {
            SetRunParameter(null);
        }

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像/区域", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像 (含测量标注)", typeof(HObject)),
            new RunSlot(RunOutputKeys.Distance, "距离像素值", typeof(double)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理距离值", typeof(double)),
            new RunSlot(RunOutputKeys.DistanceRow1, "起点行", typeof(double)),
            new RunSlot(RunOutputKeys.DistanceColumn1, "起点列", typeof(double)),
            new RunSlot(RunOutputKeys.DistanceRow2, "终点行", typeof(double)),
            new RunSlot(RunOutputKeys.DistanceColumn2, "终点列", typeof(double))
        };

        protected HObject GetInputImage()
        {
            var runModel = ContextRunModel;
            if (runModel?.InputBindings != null)
            {
                foreach (var binding in runModel.InputBindings)
                {
                    var src = binding.SourceNode?.Handle?.RunParameter;
                    if (src == null) continue;
                    var img = src.HOutput ?? src.GrayImage ?? src.RawImage;
                    if (MeasureVisualHelper.IsValidObject(img))
                        return img;
                }
            }

            var rp = RunParameter;
            var local = rp?.RawImage ?? rp?.GrayImage ?? rp?.HOutput;
            if (MeasureVisualHelper.IsValidObject(local))
                return local;

            return null;
        }
    }

    /// <summary>
    /// 点到点距离测量算子。
    /// </summary>
    [Handle("点到点距离", "DistancePP", "测量两个点之间的欧几里得距离，支持物理标定换算与专业标注叠加。", Common.HandleType_Measurement)]
    public class DistancePPHandle : DistanceMeasureBase
    {
        public DistancePPHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new DistancePPParameter();
        }

        protected override string DialogName => nameof(DistancePPView);

        private DistancePPParameter Parameter => HandleParameter as DistancePPParameter;

        public override IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像/区域", typeof(HObject)),
            new RunSlot("P1Row", "点1 行坐标 (可选)", typeof(double)),
            new RunSlot("P1Col", "点1 列坐标 (可选)", typeof(double)),
            new RunSlot("P2Row", "点2 行坐标 (可选)", typeof(double)),
            new RunSlot("P2Col", "点2 列坐标 (可选)", typeof(double))
        };

        public override void Handle()
        {
            var p = HandleInputGuard.RequireParameter(Parameter, "DistancePP");

            // 优先检查上游插槽绑定，否则使用参数设定的坐标
            var r1 = RunParameter?.TryGetOutput<double>("P1Row", out var p1r) == true ? p1r : p.Row1;
            var c1 = RunParameter?.TryGetOutput<double>("P1Col", out var p1c) == true ? p1c : p.Col1;
            var r2 = RunParameter?.TryGetOutput<double>("P2Row", out var p2r) == true ? p2r : p.Row2;
            var c2 = RunParameter?.TryGetOutput<double>("P2Col", out var p2c) == true ? p2c : p.Col2;

            HOperatorSet.DistancePp(r1, c1, r2, c2, out var distance);
            double distPixel;
            using (distance)
            {
                // 读入托管类型后立即释放临时 HTuple，防止 HALCON 内存泄漏
                distPixel = distance.Length > 0 ? distance.D : 0.0;
            }

            var physicalVal = p.ToPhysical(distPixel);
            var msg = p.FormatMeasure(distPixel);

            // 生成测量图元并烘焙叠加到输出图像中
            var overlay = MeasureVisualHelper.CreateDistancePPOverlay(r1, c1, r2, c2);
            var image = GetInputImage();
            var resultImage = MeasureVisualHelper.PaintOverlayToImage(image, overlay);

            if (resultImage != null)
            {
                RunParameter.HOutput = resultImage;
                RunParameter.SetOutput(RunOutputKeys.Output, resultImage);
            }
            RunParameter.SetOutput(RunOutputKeys.Distance, distPixel);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);
            if (overlay != null)
            {
                RunParameter.SetOutput(RunOutputKeys.MeasurementOverlay, overlay);
            }
            RunParameter.SetOutput(RunOutputKeys.DistanceRow1, r1);
            RunParameter.SetOutput(RunOutputKeys.DistanceColumn1, c1);
            RunParameter.SetOutput(RunOutputKeys.DistanceRow2, r2);
            RunParameter.SetOutput(RunOutputKeys.DistanceColumn2, c2);
            RunParameter.ResultMessage = $"点到点距离: {msg}";

            if (resultImage != null)
            {
                RaiseImageDisplay(resultImage);
            }
        }
    }

    /// <summary>
    /// 点到线距离测量算子。
    /// </summary>
    [Handle("点到线距离", "DistancePL", "测量点到基准直线的垂直距离，支持标定换算与专业标注叠加。", Common.HandleType_Measurement)]
    public class DistancePLHandle : DistanceMeasureBase
    {
        public DistancePLHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new DistancePLParameter();
        }

        protected override string DialogName => nameof(DistancePLView);

        private DistancePLParameter Parameter => HandleParameter as DistancePLParameter;

        public override void Handle()
        {
            var p = HandleInputGuard.RequireParameter(Parameter, "DistancePL");

            var pr = p.PointRow;
            var pc = p.PointCol;
            var lr1 = p.LineRow1;
            var lc1 = p.LineCol1;
            var lr2 = p.LineRow2;
            var lc2 = p.LineCol2;

            HOperatorSet.DistancePl(pr, pc, lr1, lc1, lr2, lc2, out var distance);
            HOperatorSet.ProjectionPl(pr, pc, lr1, lc1, lr2, lc2, out var projRow, out var projCol);
            double distPixel, prRow, prCol;
            using (distance)
            using (projRow)
            using (projCol)
            {
                // 读入托管类型后立即释放临时 HTuple，防止 HALCON 内存泄漏
                distPixel = distance.Length > 0 ? distance.D : 0.0;
                prRow = projRow.Length > 0 ? projRow.D : pr;
                prCol = projCol.Length > 0 ? projCol.D : pc;
            }

            var physicalVal = p.ToPhysical(distPixel);
            var msg = p.FormatMeasure(distPixel);

            var overlay = MeasureVisualHelper.CreateDistancePLOverlay(pr, pc, lr1, lc1, lr2, lc2, prRow, prCol);
            var image = GetInputImage();
            var resultImage = MeasureVisualHelper.PaintOverlayToImage(image, overlay);

            if (resultImage != null)
            {
                RunParameter.HOutput = resultImage;
                RunParameter.SetOutput(RunOutputKeys.Output, resultImage);
            }
            RunParameter.SetOutput(RunOutputKeys.Distance, distPixel);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);
            if (overlay != null)
            {
                RunParameter.SetOutput(RunOutputKeys.MeasurementOverlay, overlay);
            }
            RunParameter.SetOutput(RunOutputKeys.DistanceRow1, pr);
            RunParameter.SetOutput(RunOutputKeys.DistanceColumn1, pc);
            RunParameter.SetOutput(RunOutputKeys.DistanceRow2, prRow);
            RunParameter.SetOutput(RunOutputKeys.DistanceColumn2, prCol);
            RunParameter.ResultMessage = $"点到线距离: {msg}";

            if (resultImage != null)
            {
                RaiseImageDisplay(resultImage);
            }
        }
    }

    /// <summary>
    /// 轮廓距离测量算子。
    /// </summary>
    [Handle("轮廓距离", "DistanceContours", "测量两个轮廓之间的距离，支持标定换算。", Common.HandleType_Measurement)]
    public class DistanceContoursHandle : ParameterHandleBase<DistanceContoursParameter>, IHandleDataSlots
    {
        public DistanceContoursHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        protected override string DialogName => nameof(DistanceContoursView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot("Contour1", "轮廓1", typeof(HObject)),
            new RunSlot("Contour2", "轮廓2", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Distance, "距离像素值", typeof(double)),
            new RunSlot(RunOutputKeys.PhysicalDistance, "物理距离值", typeof(double))
        };

        public override void Handle()
        {
            var contour1 = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>("Contour1") ?? RunParameter?.GetOutput<HObject>(RunOutputKeys.Contour),
                "DistanceContours",
                "轮廓1");
            var contour2 = HandleInputGuard.RequireContour(
                RunParameter?.GetOutput<HObject>("Contour2"),
                "DistanceContours",
                "轮廓2");

            var mode = string.IsNullOrWhiteSpace(Parameter.Mode) ? "point_to_point" : Parameter.Mode;
            HOperatorSet.DistanceCcMin(contour1, contour2, mode, out var distanceMin);
            double distPixel;
            using (distanceMin)
            {
                // 读入托管类型后立即释放临时 HTuple，防止 HALCON 内存泄漏
                distPixel = distanceMin.Length > 0 ? distanceMin.D : 0.0;
            }
            var physicalVal = Parameter.ToPhysical(distPixel);
            var msg = Parameter.FormatMeasure(distPixel);

            RunParameter.HOutput = contour1;
            RunParameter.SetOutput(RunOutputKeys.Output, contour1);
            RunParameter.SetOutput(RunOutputKeys.Distance, distPixel);
            RunParameter.SetOutput(RunOutputKeys.PhysicalDistance, physicalVal);
            RunParameter.ResultMessage = $"轮廓距离 ({mode}): {msg}";

            RaiseImageDisplay(contour1);
        }
    }
}
