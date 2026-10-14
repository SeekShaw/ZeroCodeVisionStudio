using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Plugin.Handle.AngleMeasure.Views;

namespace Plugin.Handle.AngleMeasure
{
    /// <summary>
    /// 角度测量算子基类。
    /// </summary>
    public abstract class AngleMeasureBase : HandleBase, IHandleDataSlots
    {
        protected AngleMeasureBase(IDialogService dialogService) : base(dialogService)
        {
            SetRunParameter(null);
        }

        public override void EditParameters()
        {
            var param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public virtual IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像 (含测量标注)", typeof(HObject)),
            new RunSlot(RunOutputKeys.Angle, "角度值 (°)", typeof(double)),
            new RunSlot(RunOutputKeys.Radian, "弧度值 (rad)", typeof(double)),
            new RunSlot(RunOutputKeys.MeasurementOverlay, "测量图形标注", typeof(HObject))
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
    /// 两条直线夹角测量算子。
    /// </summary>
    [Handle("线线夹角", "AngleLL", "测量两条直线之间的夹角（度），支持图形标注叠加。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class AngleLLHandle : AngleMeasureBase
    {
        public AngleLLHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new AngleLLParameter();
        }

        protected override string DialogName => nameof(AngleLLView);

        private AngleLLParameter Parameter => HandleParameter as AngleLLParameter;

        public override void Handle()
        {
            var p = Parameter;
            if (p == null) return;

            HOperatorSet.AngleLl(
                p.Line1Row1, p.Line1Col1, p.Line1Row2, p.Line1Col2,
                p.Line2Row1, p.Line2Col1, p.Line2Row2, p.Line2Col2,
                out var angle);

            var angleRad = angle.Length > 0 ? angle.D : 0.0;
            var angleDeg = angleRad * 180.0 / Math.PI;

            var overlay = MeasureVisualHelper.CreateAngleLLOverlay(
                p.Line1Row1, p.Line1Col1, p.Line1Row2, p.Line1Col2,
                p.Line2Row1, p.Line2Col1, p.Line2Row2, p.Line2Col2);
            var image = GetInputImage();
            var resultImage = MeasureVisualHelper.PaintOverlayToImage(image, overlay);

            if (resultImage != null)
            {
                RunParameter.HOutput = resultImage;
                RunParameter.SetOutput(RunOutputKeys.Output, resultImage);
            }
            RunParameter.SetOutput(RunOutputKeys.Angle, angleDeg);
            RunParameter.SetOutput(RunOutputKeys.Radian, angleRad);
            if (overlay != null)
            {
                RunParameter.SetOutput(RunOutputKeys.MeasurementOverlay, overlay);
            }
            RunParameter.ResultMessage = $"线线夹角: {angleDeg:0.###}° ({angleRad:0.####} rad)";

            if (resultImage != null)
            {
                RaiseImageDisplay(resultImage);
            }
        }
    }

    /// <summary>
    /// 直线与X轴夹角测量算子。
    /// </summary>
    [Handle("线与X轴夹角", "AngleLX", "测量直线与X轴正方向的夹角（度），支持图形叠加。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class AngleLXHandle : AngleMeasureBase
    {
        public AngleLXHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new AngleLXParameter();
        }

        protected override string DialogName => nameof(AngleLXView);

        private AngleLXParameter Parameter => HandleParameter as AngleLXParameter;

        public override void Handle()
        {
            var p = Parameter;
            if (p == null) return;

            HOperatorSet.AngleLx(p.Row1, p.Col1, p.Row2, p.Col2, out var angle);
            var angleRad = angle.Length > 0 ? angle.D : 0.0;
            var angleDeg = angleRad * 180.0 / Math.PI;

            HOperatorSet.GenContourPolygonXld(out var line, new HTuple(p.Row1, p.Row2), new HTuple(p.Col1, p.Col2));
            var image = GetInputImage();
            var resultImage = MeasureVisualHelper.PaintOverlayToImage(image, line);

            if (resultImage != null)
            {
                RunParameter.HOutput = resultImage;
                RunParameter.SetOutput(RunOutputKeys.Output, resultImage);
            }
            RunParameter.SetOutput(RunOutputKeys.Angle, angleDeg);
            RunParameter.SetOutput(RunOutputKeys.Radian, angleRad);
            if (line != null)
            {
                RunParameter.SetOutput(RunOutputKeys.MeasurementOverlay, line);
            }
            RunParameter.ResultMessage = $"线与X轴夹角: {angleDeg:0.###}° ({angleRad:0.####} rad)";

            if (resultImage != null)
            {
                RaiseImageDisplay(resultImage);
            }
        }
    }

    /// <summary>
    /// 两线相对角度测量算子。
    /// </summary>
    [Handle("两线相对角度", "RelAngleLL", "测量第二条线相对于第一条线的相对角度（度）。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class RelAngleLLHandle : AngleMeasureBase
    {
        public RelAngleLLHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new AngleLLParameter();
        }

        protected override string DialogName => nameof(AngleLLView);

        private AngleLLParameter Parameter => HandleParameter as AngleLLParameter;

        public override void Handle()
        {
            var p = Parameter;
            if (p == null) return;

            HOperatorSet.AngleLx(p.Line1Row1, p.Line1Col1, p.Line1Row2, p.Line1Col2, out var angle1);
            HOperatorSet.AngleLx(p.Line2Row1, p.Line2Col1, p.Line2Row2, p.Line2Col2, out var angle2);

            var a1 = angle1.Length > 0 ? angle1.D : 0.0;
            var a2 = angle2.Length > 0 ? angle2.D : 0.0;
            var angleDeg1 = a1 * 180.0 / Math.PI;
            var angleDeg2 = a2 * 180.0 / Math.PI;
            var relAngle = angleDeg2 - angleDeg1;

            while (relAngle > 180) relAngle -= 360;
            while (relAngle < -180) relAngle += 360;

            var overlay = MeasureVisualHelper.CreateAngleLLOverlay(
                p.Line1Row1, p.Line1Col1, p.Line1Row2, p.Line1Col2,
                p.Line2Row1, p.Line2Col1, p.Line2Row2, p.Line2Col2);
            var image = GetInputImage();
            var resultImage = MeasureVisualHelper.PaintOverlayToImage(image, overlay);

            if (resultImage != null)
            {
                RunParameter.HOutput = resultImage;
                RunParameter.SetOutput(RunOutputKeys.Output, resultImage);
            }
            RunParameter.SetOutput(RunOutputKeys.Angle, relAngle);
            RunParameter.SetOutput(RunOutputKeys.Radian, relAngle * Math.PI / 180.0);
            if (overlay != null)
            {
                RunParameter.SetOutput(RunOutputKeys.MeasurementOverlay, overlay);
            }
            RunParameter.ResultMessage = $"两线相对角度: {relAngle:0.###}°";

            if (resultImage != null)
            {
                RaiseImageDisplay(resultImage);
            }
        }
    }
}
