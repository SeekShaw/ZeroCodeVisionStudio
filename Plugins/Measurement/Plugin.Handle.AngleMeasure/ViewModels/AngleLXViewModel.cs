using System;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.AngleMeasure.ViewModels
{
    public class AngleLXViewModel : MeasureDialogViewModelBase<AngleLXHandle, AngleLXParameter>
    {
        protected override void PerformDraw(HWindow hwin)
        {
            hwin.SetDraw("margin");

            // 绘制直线
            hwin.SetColor("cyan");
            hwin.DrawLine(out double r1, out double c1, out double r2, out double c2);
            Parameter.Row1 = Math.Round(r1, 2);
            Parameter.Col1 = Math.Round(c1, 2);
            Parameter.Row2 = Math.Round(r2, 2);
            Parameter.Col2 = Math.Round(c2, 2);

            hwin.SetColor("yellow");
            HOperatorSet.GenContourPolygonXld(out var line, new HTuple(r1, r2), new HTuple(c1, c2));
            SafeDispObj(line, hwin);
            line.Dispose();
            try
            {
                HOperatorSet.DispText(hwin, "Line", "image", (r1 + r2) / 2 + 8, (c1 + c2) / 2 + 8, "yellow", "box", "false");
            }
            catch
            {
            }
        }
    }
}
