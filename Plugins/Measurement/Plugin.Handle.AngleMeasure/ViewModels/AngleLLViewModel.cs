using System;
using HalconDotNet;
using HalconVisionStudio.Shared;
using Prism.Commands;

namespace Plugin.Handle.AngleMeasure.ViewModels
{
    public class AngleLLViewModel : MeasureDialogViewModelBase<AngleLLHandle, AngleLLParameter>
    {
        public AngleLLViewModel()
        {
            DrawLine1Command = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawLine1, "第 1 条直线采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);

            DrawLine2Command = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawLine2, "第 2 条直线采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);
        }

        public DelegateCommand DrawLine1Command { get; }
        public DelegateCommand DrawLine2Command { get; }

        protected override void PerformDraw(HWindow hwin)
        {
            DrawLine1(hwin);
        }

        private void DrawLine1(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("cyan");
            HOperatorSet.DrawLine(hwin, out var l1r1, out var l1c1, out var l1r2, out var l1c2);
            if (l1r1.Length > 0 && l1c1.Length > 0 && l1r2.Length > 0 && l1c2.Length > 0)
            {
                Parameter.Line1Row1 = Math.Round(l1r1.D, 2);
                Parameter.Line1Col1 = Math.Round(l1c1.D, 2);
                Parameter.Line1Row2 = Math.Round(l1r2.D, 2);
                Parameter.Line1Col2 = Math.Round(l1c2.D, 2);
            }
        }

        private void DrawLine2(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("magenta");
            HOperatorSet.DrawLine(hwin, out var l2r1, out var l2c1, out var l2r2, out var l2c2);
            if (l2r1.Length > 0 && l2c1.Length > 0 && l2r2.Length > 0 && l2c2.Length > 0)
            {
                Parameter.Line2Row1 = Math.Round(l2r1.D, 2);
                Parameter.Line2Col1 = Math.Round(l2c1.D, 2);
                Parameter.Line2Row2 = Math.Round(l2r2.D, 2);
                Parameter.Line2Col2 = Math.Round(l2c2.D, 2);
            }
        }
    }
}
