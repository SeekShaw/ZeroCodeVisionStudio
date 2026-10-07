using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.CreateScaledShapeModel
{
    [Handle("创建缩放形状模板", "CreateScaledShapeModel", "创建支持等比例缩放搜索的 HALCON 形状模板。", Common.HandleType_ShapeMatching)]
    public class CreateScaledShapeModelHandle : ParameterHandleBase<CreateScaledShapeModelParameter>, IHandleDataSlots, IHandleRuntimeResourceOwner
    {
        private HTuple ownedModelId;

        public CreateScaledShapeModelHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.RawImage, "输入原始图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "模板区域", typeof(HObject))
        };

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
            var source = HandleInputGuard.RequireImage(GetInputImage(), "CreateScaledShapeModel");

            var region = GetInputRegion();
            var modelSource = source;
            var ownsModelSource = false;

            if (region != null)
            {
                HOperatorSet.ReduceDomain(source, region, out var reduced);
                modelSource = reduced;
                ownsModelSource = true;
            }

            try
            {
                using var angleStart = HalconTupleHelper.ToRadians(Parameter.AngleStartDeg);
                using var angleExtent = HalconTupleHelper.ToRadians(Parameter.AngleExtentDeg);
                using var angleStep = HalconTupleHelper.ToAngleStep(Parameter.AngleStepDeg);
                using var scaleStep = Parameter.ScaleStep <= 0 ? new HTuple("auto") : new HTuple(Parameter.ScaleStep);
                using var optimization = HalconTupleHelper.ToAutoTuple(Parameter.Optimization);
                using var contrast = HalconTupleHelper.ToAutoTuple(Parameter.Contrast);
                using var minContrast = HalconTupleHelper.ToAutoTuple(Parameter.MinContrast);

                HOperatorSet.CreateScaledShapeModel(
                    modelSource,
                    Math.Max(0, Parameter.NumLevels),
                    angleStart,
                    angleExtent,
                    angleStep,
                    Math.Max(0.0001, Parameter.ScaleMin),
                    Math.Max(0.0001, Parameter.ScaleMax),
                    scaleStep,
                    optimization,
                    Parameter.Metric.ToHalconString(),
                    contrast,
                    minContrast,
                    out var modelId);

                HOperatorSet.GetShapeModelContours(out var contours, modelId, 1);
                RunParameter.HOutput = contours;
                RunParameter.SetOutput(RunOutputKeys.ShapeModelId, modelId);
                RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
                RunParameter.SetOutput(RunOutputKeys.Output, contours);
                RunParameter.SetOutput(RunOutputKeys.Xld, contours);
                ownedModelId = modelId;
                RunParameter.ResultMessage = "Scaled shape model created.";
                RaiseImageDisplay(contours);
            }
            finally
            {
                if (ownsModelSource)
                {
                    modelSource.Dispose();
                }
            }
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

        private HObject GetInputImage()
        {
            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.GrayImage, out var grayInput) == true && grayInput != null)
            {
                return grayInput;
            }

            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.RawImage, out var rawInput) == true && rawInput != null)
            {
                return rawInput;
            }

            return RunParameter?.GrayImage ?? RunParameter?.RawImage ?? RunParameter?.HOutput;
        }

        private HObject GetInputRegion()
        {
            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.Region, out var regionInput) == true && regionInput != null)
            {
                return regionInput;
            }

            return RunParameter?.GetOutput<HObject>(RunOutputKeys.Region);
        }
    }
}
