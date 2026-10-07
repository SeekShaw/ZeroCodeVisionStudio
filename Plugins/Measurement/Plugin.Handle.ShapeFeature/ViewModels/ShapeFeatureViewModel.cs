using HalconVisionStudio.Shared;

namespace Plugin.Handle.ShapeFeature.ViewModels
{
    public class ShapeFeatureViewModel : DefectDialogViewModel<ShapeFeatureBase, ShapeFeatureParameter>
    {
        private string featureTitle = "形状特征测量";
        private string featureDescription = "计算区域的形状几何特征。";
        private string measureResultText = "尚未执行测量";

        public string FeatureTitle
        {
            get => featureTitle;
            set => SetProperty(ref featureTitle, value);
        }

        public string FeatureDescription
        {
            get => featureDescription;
            set => SetProperty(ref featureDescription, value);
        }

        public string MeasureResultText
        {
            get => measureResultText;
            set => SetProperty(ref measureResultText, value);
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (Handle != null)
            {
                var handleType = Handle.GetType();
                var attrs = handleType.GetCustomAttributes(typeof(HandleAttribute), true);
                if (attrs.Length > 0 && attrs[0] is HandleAttribute ha)
                {
                    FeatureTitle = $"{ha.HandleName} ({ha.Key})";
                    FeatureDescription = ha.HandleDescription;
                }
            }
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
