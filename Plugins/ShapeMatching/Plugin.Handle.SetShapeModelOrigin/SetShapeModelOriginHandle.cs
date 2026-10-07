using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.SetShapeModelOrigin
{
    [Handle("设置模板原点", "SetShapeModelOrigin", "设置形状模板匹配返回坐标所使用的原点。", Common.HandleType_ShapeMatching)]
    public class SetShapeModelOriginHandle : ParameterHandleBase<SetShapeModelOriginParameter>, IHandleDataSlots
    {
        public SetShapeModelOriginHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple)),
            new RunSlot(RunOutputKeys.ShapeModelContours, "模板轮廓", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple)),
            new RunSlot(RunOutputKeys.ShapeModelContours, "模板轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var modelId = HandleInputGuard.RequireModelId(GetInputModelId(), "SetShapeModelOrigin");

            HOperatorSet.SetShapeModelOrigin(modelId, Parameter.OriginRow, Parameter.OriginColumn);
            RunParameter.SetOutput(RunOutputKeys.ShapeModelId, modelId);

            var contours = GetInputModelContours();
            if (contours != null)
            {
                RunParameter.HOutput = contours;
                RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
                RunParameter.SetOutput(RunOutputKeys.Output, contours);
                RunParameter.SetOutput(RunOutputKeys.Xld, contours);
            }

            RunParameter.ResultMessage = $"Shape model origin set: row={Parameter.OriginRow:0.###}, col={Parameter.OriginColumn:0.###}.";
            RaiseImageDisplay(contours ?? RunParameter.HOutput);
        }

        private HTuple GetInputModelId()
        {
            if (RunParameter?.TryGetInput<HTuple>(RunOutputKeys.ShapeModelId, out var modelIdInput) == true && modelIdInput != null)
            {
                return modelIdInput;
            }

            return RunParameter?.GetOutput<HTuple>(RunOutputKeys.ShapeModelId);
        }

        private HObject GetInputModelContours()
        {
            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.ShapeModelContours, out var contoursInput) == true && contoursInput != null)
            {
                return contoursInput;
            }

            return RunParameter?.GetOutput<HObject>(RunOutputKeys.ShapeModelContours);
        }
    }
}
