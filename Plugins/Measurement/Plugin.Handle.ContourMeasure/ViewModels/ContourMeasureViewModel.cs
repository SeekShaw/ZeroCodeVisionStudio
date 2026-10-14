using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ContourMeasure.ViewModels
{
    public class ContourMeasureViewModel : DefectDialogViewModel<ContourMeasureBase, ContourMeasureParameter>
    {
        private string measureResultText = "尚未执行测量";

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
