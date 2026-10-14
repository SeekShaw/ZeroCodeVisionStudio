using System;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Prism.Commands;

namespace Plugin.Handle.DistanceMeasure.ViewModels
{
    public class DistancePPViewModel : MeasureDialogViewModelBase<DistancePPHandle, DistancePPParameter>
    {
        public DistancePPViewModel()
        {
            DrawPoint1Command = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawPoint1, "点 1 采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);

            DrawPoint2Command = new DelegateCommand(async () => await ExecuteDrawActionAsync(DrawPoint2, "点 2 采集完成"), () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);
        }

        public DelegateCommand DrawPoint1Command { get; }
        public DelegateCommand DrawPoint2Command { get; }

        /// <summary>
        /// 默认点击「连线采集两点」：在图像上按住左键从点1拖拽至点2，右键确认结束。
        /// 单次操作即可同时确定两点坐标与连线，完全避免右键冲突。
        /// </summary>
        protected override void PerformDraw(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("cyan");
            HOperatorSet.DrawLine(hwin, out var r1, out var c1, out var r2, out var c2);
            if (r1.Length > 0 && c1.Length > 0 && r2.Length > 0 && c2.Length > 0)
            {
                Parameter.Row1 = Math.Round(r1.D, 2);
                Parameter.Col1 = Math.Round(c1.D, 2);
                Parameter.Row2 = Math.Round(r2.D, 2);
                Parameter.Col2 = Math.Round(c2.D, 2);
            }
        }

        private void DrawPoint1(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("yellow");
            HOperatorSet.DrawPoint(hwin, out var r1, out var c1);
            if (r1.Length > 0 && c1.Length > 0)
            {
                Parameter.Row1 = Math.Round(r1.D, 2);
                Parameter.Col1 = Math.Round(c1.D, 2);
            }
        }

        private void DrawPoint2(HWindow hwin)
        {
            hwin.SetDraw("margin");
            hwin.SetColor("yellow");
            HOperatorSet.DrawPoint(hwin, out var r2, out var c2);
            if (r2.Length > 0 && c2.Length > 0)
            {
                Parameter.Row2 = Math.Round(r2.D, 2);
                Parameter.Col2 = Math.Round(c2.D, 2);
            }
        }
    }
}
