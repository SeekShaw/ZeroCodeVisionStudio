using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ClearShapeModel
{
    [Handle("释放形状模板", "ClearShapeModel", "释放或清空当前流程中的 HALCON 形状模板。", Common.HandleType_ShapeMatching, OperatorBackend = OperatorBackend.Halcon)]
    public class ClearShapeModelHandle : ParameterHandleBase<ClearShapeModelParameter>, IHandleDataSlots
    {
        public ClearShapeModelHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = Array.Empty<RunSlot>();

        public override void Handle()
        {
            var modelId = GetInputModelId();
            if (Parameter.ClearHalconModel && modelId != null)
            {
                HOperatorSet.ClearShapeModel(modelId);
            }

            RunParameter.SetOutput(RunOutputKeys.ShapeModelId, null);
            RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, null);
            RunParameter.HOutput = null;
            RunParameter.ResultMessage = Parameter.ClearHalconModel
                ? "Shape model cleared."
                : "Shape model outputs cleared.";
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
