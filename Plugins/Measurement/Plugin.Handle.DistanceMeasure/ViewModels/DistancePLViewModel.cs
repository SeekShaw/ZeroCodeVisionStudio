using System;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Prism.Commands;

namespace Plugin.Handle.DistanceMeasure.ViewModels
{
    public class DistancePLViewModel : MeasureDialogViewModelBase<DistancePLHandle, DistancePLParameter>
    {
        public DistancePLViewModel()
        {
            DrawPointCommand = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawTargetPoint, "测点采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);

            DrawLineCommand = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawReferenceLine, "基准直线采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);
        }

        public DelegateCommand DrawPointCommand { get; }
        public DelegateCommand DrawLineCommand { get; }

        protected override void PerformDraw(HWindow hwin)
        {
            DrawReferenceLine(hwin);
        }

        private void DrawTargetPoint(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("yellow");
            HOperatorSet.DrawPoint(hwin, out var pr, out var pc);
            if (pr.Length > 0 && pc.Length > 0)
            {
                Parameter.PointRow = Math.Round(pr.D, 2);
                Parameter.PointCol = Math.Round(pc.D, 2);
            }
        }

        private void DrawReferenceLine(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("cyan");
            HOperatorSet.DrawLine(hwin, out var lr1, out var lc1, out var lr2, out var lc2);
            if (lr1.Length > 0 && lc1.Length > 0 && lr2.Length > 0 && lc2.Length > 0)
            {
                Parameter.LineRow1 = Math.Round(lr1.D, 2);
                Parameter.LineCol1 = Math.Round(lc1.D, 2);
                Parameter.LineRow2 = Math.Round(lr2.D, 2);
                Parameter.LineCol2 = Math.Round(lc2.D, 2);
            }
        }
    }
}
