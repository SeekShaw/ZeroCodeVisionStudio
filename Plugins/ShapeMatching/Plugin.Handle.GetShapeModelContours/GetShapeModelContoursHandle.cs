using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.GetShapeModelContours
{
    [Handle("获取模板轮廓", "GetShapeModelContours", "从形状模板 ID 获取 XLD 模板轮廓。", Common.HandleType_ShapeMatching)]
    public class GetShapeModelContoursHandle : ParameterHandleBase<GetShapeModelContoursParameter>, IHandleDataSlots
    {
        public GetShapeModelContoursHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelContours, "模板轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Output, "模板轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Xld, "模板 XLD", typeof(HObject))
        };

        public override void Handle()
        {
            var modelId = HandleInputGuard.RequireModelId(GetInputModelId(), "GetShapeModelContours");

            HOperatorSet.GetShapeModelContours(out var contours, modelId, Math.Max(1, Parameter.Level));
            RunParameter.HOutput = contours;
            RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
            RunParameter.SetOutput(RunOutputKeys.Output, contours);
            RunParameter.SetOutput(RunOutputKeys.Xld, contours);
            RunParameter.ResultMessage = "Shape model contours generated.";
            RaiseImageDisplay(contours);
        }

        private HTuple GetInputModelId()
        {
            if (RunParameter?.TryGetInput<HTuple>(RunOutputKeys.ShapeModelId, out var modelIdInput) == true && modelIdInput != null)
            {
                return modelIdInput;
            }

            return RunParameter?.GetOutput<HTuple>(RunOutputKeys.ShapeModelId);
        }
    }
}
