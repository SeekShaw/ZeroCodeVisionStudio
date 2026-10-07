using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.GenCircle
{
    [Handle("生成圆", "GenCircle", "根据圆心坐标和半径生成圆形区域。支持交互式绘制。", Common.HandleType_Geometry)]
    public class GenCircleHandle : ParameterHandleBase<GenCircleParameter>
    {
        public GenCircleHandle(IDialogService dialogService) : base(dialogService) { }

        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "circle" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            HOperatorSet.GenCircle(out var circle, Parameter.Row, Parameter.Column, Parameter.Radius);
            RunParameter.HOutput = circle;
            RunParameter.SetOutput(RunOutputKeys.Output, circle);
            RunParameter.SetOutput(RunOutputKeys.Region, circle);
            RaiseImageDisplay(circle);
        }
    }
}
