using System.Collections.Generic;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.DistanceMeasure.ViewModels
{
    public class DistanceContoursViewModel : DefectDialogViewModel<DistanceContoursHandle, DistanceContoursParameter>
    {
        private string measureResultText = "尚未执行测量";

        public IReadOnlyList<string> ModeList { get; } = new[]
        {
            "point_to_point",
            "point_to_segment",
            "hausdorff"
        };

        public string MeasureResultText
        {
            get => measureResultText;
            set => SetProperty(ref measureResultText, value);
        }

        protected override void OnExecute()
        {
            base.OnExecute();
            if (Handle?.RunParameter != null)
            {
                MeasureResultText = Handle.RunParameter.ResultMessage ?? "测量完成";
            }
        }

        protected override void OnReset()
        {
            base.OnReset();
            MeasureResultText = "已恢复默认参数并清除绘制";
        }
    }
}
