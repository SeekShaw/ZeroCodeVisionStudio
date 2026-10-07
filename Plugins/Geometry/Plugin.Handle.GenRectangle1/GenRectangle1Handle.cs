using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.GenRectangle1
{
    [Handle("生成矩形1", "GenRectangle1", "根据左上角和右下角坐标生成平行于坐标轴的矩形区域。支持交互式绘制。", Common.HandleType_Geometry)]
    public class GenRectangle1Handle : ParameterHandleBase<GenRectangle1Parameter>
    {
        public GenRectangle1Handle(IDialogService dialogService) : base(dialogService) { }

        protected override string DialogName => Common.ShapeDrawDialogName;

        public override void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel },
                { "ShapeType", "rectangle1" }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        public override void Handle()
        {
            HOperatorSet.GenRectangle1(out var rect, Parameter.Row1, Parameter.Column1, Parameter.Row2, Parameter.Column2);
            RunParameter.HOutput = rect;
            RunParameter.SetOutput(RunOutputKeys.Output, rect);
            RunParameter.SetOutput(RunOutputKeys.Region, rect);
            RaiseImageDisplay(rect);
        }
    }
}
