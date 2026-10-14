using ZeroCodeVisionStudio.Shared;
using HalconDotNet;
using Plugin.Handle.ReduceDomain.Views;

namespace Plugin.Handle.ReduceDomain
{
    [Handle("裁剪图像域", "ReduceDomain", "按照输入区域 ROI 限定图像处理域，对应 HALCON: reduce_domain(Image, Region, ImageReduced)。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class ReduceDomainHandle : HandleBase, IHandleDataSlots
    {
        private readonly ReduceDomainParameter parameter;

        public ReduceDomainHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new ReduceDomainParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(ReduceDomainView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入图像 GrayImage", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输入区域 ROI", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "裁剪后图像 ImageReduced", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "裁剪后灰度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var sourceImage = HandleInputGuard.RequireImage(
                RunParameter?.GrayImage ?? RunParameter?.RawImage ?? RunParameter?.HOutput,
                "ReduceDomain");

            var region = GetInputRegion(out var shouldDisposeRegion);
            try
            {
                HOperatorSet.ReduceDomain(sourceImage, region, out var reducedImage);
                RunParameter.HOutput = reducedImage;
                RunParameter.GrayImage = reducedImage;
                RunParameter.SetOutput(RunOutputKeys.Output, reducedImage);
                RunParameter.SetOutput(RunOutputKeys.GrayImage, reducedImage);
                RunParameter.ResultMessage = "reduce_domain 执行完成";
                RaiseImageDisplay(reducedImage);
            }
            finally
            {
                if (shouldDisposeRegion)
                {
                    region?.Dispose();
                }
            }
        }

        private HObject GetInputRegion(out bool shouldDispose)
        {
            shouldDispose = false;
            if (RunParameter?.TryGetInput<HObject>(RunOutputKeys.Region, out var inputRegion) == true && inputRegion != null)
            {
                return inputRegion;
            }

            var outputRegion = RunParameter?.GetOutput<HObject>(RunOutputKeys.Region);
            if (outputRegion != null)
            {
                return outputRegion;
            }

            shouldDispose = true;
            return CreateRegionFromParameter();
        }

        private HObject CreateRegionFromParameter()
        {
            switch (parameter.ShapeType)
            {
                case EShapeType.rectangle1:
                    HOperatorSet.GenRectangle1(out var rectangle1, parameter.Row1, parameter.Column1, parameter.Row2, parameter.Column2);
                    return rectangle1;
                case EShapeType.rectangle2:
                    HOperatorSet.GenRectangle2(out var rectangle2, parameter.Row, parameter.Column, parameter.Phi, parameter.Length1, parameter.Length2);
                    return rectangle2;
                case EShapeType.circle:
                    HOperatorSet.GenCircle(out var circle, parameter.Row, parameter.Column, Math.Max(0.5, parameter.Radius));
                    return circle;
                default:
                    throw new HandleException(
                        ExceptionType.E_REGION_NULL,
                        "ReduceDomain 需要输入区域 ROI，请从上游区域算子选择，或在参数窗口中设置 ROI。");
            }
        }
    }
}
