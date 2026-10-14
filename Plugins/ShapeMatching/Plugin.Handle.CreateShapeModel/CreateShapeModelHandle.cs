using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.CreateShapeModel
{
    [Handle("创建形状模板", "CreateShapeModel", "基于当前图像创建 HALCON 形状模板，支持交互式模板区域。", Common.HandleType_ShapeMatching, OperatorBackend = OperatorBackend.Halcon)]
    public class CreateShapeModelHandle : ParameterHandleBase<CreateShapeModelParameter>, IHandleDataSlots, IHandleRuntimeResourceOwner
    {
        private HTuple ownedModelId;

        public CreateShapeModelHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public HObject TemplateRegion { get; set; }

        protected override string DialogName => nameof(Views.CreateShapeModelView);

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
            var source = HandleInputGuard.RequireImage(GetInputImage(), "CreateShapeModel");

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
                using var optimization = HalconTupleHelper.ToAutoTuple(Parameter.Optimization);
                using var contrast = HalconTupleHelper.ToAutoTuple(Parameter.Contrast);
                using var minContrast = HalconTupleHelper.ToAutoTuple(Parameter.MinContrast);

                HOperatorSet.CreateShapeModel(
                    modelSource,
                    Math.Max(0, Parameter.NumLevels),
                    angleStart,
                    angleExtent,
                    angleStep,
                    optimization,
                    Parameter.Metric.ToHalconString(),
                    contrast,
                    minContrast,
                    out var modelId);

                HOperatorSet.GetShapeModelContours(out var contours, modelId, 1);
                var displayContours = TransformContoursToImage(contours, source, region);

                RunParameter.HOutput = displayContours;
                RunParameter.SetOutput(RunOutputKeys.ShapeModelId, modelId);
                RunParameter.SetOutput(RunOutputKeys.ShapeModelContours, contours);
                RunParameter.SetOutput(RunOutputKeys.Output, displayContours);
                RunParameter.SetOutput(RunOutputKeys.Xld, displayContours);
                ownedModelId = modelId;
                RunParameter.ResultMessage = "Shape model created.";

                if (!string.IsNullOrWhiteSpace(Parameter.SavePath))
                {
                    HOperatorSet.WriteShapeModel(modelId, Parameter.SavePath);
                    RunParameter.ResultMessage = $"Shape model created and saved: {Parameter.SavePath}";
                }

                RaiseImageDisplay(displayContours);
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

        private static HObject TransformContoursToImage(HObject contours, HObject source, HObject region)
        {
            GetDisplayOrigin(source, region, out var row, out var column);
            using (row)
            using (column)
            {
                HOperatorSet.VectorAngleToRigid(0, 0, 0, row, column, 0, out var homMat2D);
                using (homMat2D)
                {
                    HOperatorSet.AffineTransContourXld(contours, out var displayContours, homMat2D);
                    return displayContours;
                }
            }
        }

        private static void GetDisplayOrigin(HObject source, HObject region, out HTuple row, out HTuple column)
        {
            if (region != null)
            {
                HOperatorSet.AreaCenter(region, out var area, out row, out column);
                var regionHasArea = false;
                using (area)
                {
                    // 读入托管类型后立即释放临时 HTuple，防止 HALCON 内存泄漏
                    regionHasArea = area.Length > 0 && area[0].D > 0;
                }

                if (regionHasArea)
                {
                    return;
                }

                // 区域无效时释放本次结果，改用图像定义域重新计算
                row.Dispose();
                column.Dispose();
            }

            HOperatorSet.GetDomain(source, out var domain);
            try
            {
                HOperatorSet.AreaCenter(domain, out var domainArea, out row, out column);
                domainArea.Dispose();
            }
            finally
            {
                domain.Dispose();
            }
        }

        private HObject GetInputRegion()
        {
            if (TemplateRegion != null)
            {
                return TemplateRegion;
            }

            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.Region, out var regionInput) == true && regionInput != null)
            {
                return regionInput;
            }

            return RunParameter?.GetOutput<HObject>(RunOutputKeys.Region);
        }
    }
}
