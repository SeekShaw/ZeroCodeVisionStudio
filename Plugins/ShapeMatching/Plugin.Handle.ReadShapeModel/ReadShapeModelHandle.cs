using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ReadShapeModel
{
    [Handle("读取形状模板", "ReadShapeModel", "加载 HALCON 形状模板文件。", Common.HandleType_ShapeMatching)]
    public class ReadShapeModelHandle : ParameterHandleBase<ReadShapeModelParameter>, IHandleDataSlots, IHandleRuntimeResourceOwner
    {
        private HTuple ownedModelId;

        public ReadShapeModelHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShapeModelId, "形状模板 ID", typeof(HTuple)),
            new RunSlot(RunOutputKeys.ShapeModelContours, "模板轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Output, "模板轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Xld, "模板 XLD", typeof(HObject))
        };

        public override void Handle()
        {
            ReleaseRuntimeResources();
            var modelPath = HandleInputGuard.RequireExistingFile(Parameter.ModelPath, "ReadShapeModel", "模板路径");

            HOperatorSet.ReadShapeModel(modelPath, out var modelId);
            HOperatorSet.GetShapeModelContours(out var contours, modelId, 1);

            // 主输出透传输入图像（链式传递给下游匹配算子）；模板轮廓仅在 Xld/模板轮廓键提供
            var inputImage = RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage)
                             ?? RunParameter?.GrayImage
                             ?? RunParameter?.RawImage
                             ?? RunParameter?.HOutput;
            RunParameter.HOutput = inputImage ?? contours;
            RunParameter.SetOutput(RunOutputKeys.ShapeModelId, modelId);
            RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
            RunParameter.SetOutput(RunOutputKeys.Output, RunParameter.HOutput);
            RunParameter.SetOutput(RunOutputKeys.Xld, contours);
            ownedModelId = modelId;
            RunParameter.ResultMessage = $"Shape model loaded: {modelPath}";
            RaiseImageDisplay(contours);
        }

        public override void SetRunParameter(RunParameter parameter)
        {
            ReleaseRuntimeResources();
            base.SetRunParameter(parameter);
        }

        public void ReleaseRuntimeResources()
        {
            var modelId = ownedModelId;
            ownedModelId = null;
            HalconShapeModelResource.TryClear(modelId);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseRuntimeResources();
            }

            base.Dispose(disposing);
        }
    }
}
