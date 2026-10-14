using ZeroCodeVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.GenEllipse
{
    [Handle("生成椭圆", "GenEllipse", "根据中心坐标、角度和半轴长生成椭圆区域。支持交互式绘制。", Common.HandleType_Geometry, OperatorBackend = OperatorBackend.OpenCV)]
    public class GenEllipseHandle : ParameterHandleBase<GenEllipseParameter>
    {
        public GenEllipseHandle(IDialogService dialogService) : base(dialogService) { }

        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "ellipse" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            HOperatorSet.GenEllipse(out var ellipse, Parameter.Row, Parameter.Column, Parameter.Phi, Parameter.Radius1, Parameter.Radius2);
            RunParameter.HOutput = ellipse;
            RunParameter.SetOutput(RunOutputKeys.Output, ellipse);
            RunParameter.SetOutput(RunOutputKeys.Region, ellipse);
            RaiseImageDisplay(ellipse);
        }
    }
}
