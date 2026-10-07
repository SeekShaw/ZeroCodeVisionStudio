using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.GenRectangle2
{
    [Handle("生成矩形2", "GenRectangle2", "根据中心坐标、角度和半边长生成可旋转的矩形区域。支持交互式绘制。", Common.HandleType_Geometry)]
    public class GenRectangle2Handle : ParameterHandleBase<GenRectangle2Parameter>
    {
        public GenRectangle2Handle(IDialogService dialogService) : base(dialogService) { }

        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "rectangle2" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            HOperatorSet.GenRectangle2(out var rect, Parameter.Row, Parameter.Column, Parameter.Phi, Parameter.Length1, Parameter.Length2);
            RunParameter.HOutput = rect;
            RunParameter.SetOutput(RunOutputKeys.Output, rect);
            RunParameter.SetOutput(RunOutputKeys.Region, rect);
            RaiseImageDisplay(rect);
        }
    }
}
