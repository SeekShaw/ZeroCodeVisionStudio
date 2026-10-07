using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.WriteShapeModel
{
    [Handle("保存形状模板", "WriteShapeModel", "将当前 HALCON 形状模板保存到磁盘。", Common.HandleType_ShapeMatching)]
    public class WriteShapeModelHandle : ParameterHandleBase<WriteShapeModelParameter>, IHandleDataSlots
    {
        public WriteShapeModelHandle(IDialogService dialogService) : base(dialogService)
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
            var modelId = HandleInputGuard.RequireModelId(GetInputModelId(), "WriteShapeModel");
            var modelPath = HandleInputGuard.RequirePath(Parameter.ModelPath, "WriteShapeModel", "保存路径");

            HOperatorSet.WriteShapeModel(modelId, modelPath);
            RunParameter.SetOutput(RunOutputKeys.ShapeModelId, modelId);
            var contours = GetInputModelContours();
            if (contours != null)
            {
                RunParameter.HOutput = contours;
                RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
                RunParameter.SetOutput(RunOutputKeys.Output, contours);
                RunParameter.SetOutput(RunOutputKeys.Xld, contours);
            }

            RunParameter.ResultMessage = $"Shape model saved: {modelPath}";
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
