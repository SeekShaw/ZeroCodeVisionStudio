using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Plugin.Handle.BinaryThreshold.Views;

namespace Plugin.Handle.BinaryThreshold
{
    [Handle("二值化", "BinaryThreshold", "将输入图像转换为二值区域。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class BinaryThresholdHandle : HandleBase, IHandleDataSlots
    {
        private readonly BinaryThresholdParameter binaryParameter;

        public BinaryThresholdHandle(IDialogService dialogService) : base(dialogService)
        {
            binaryParameter = new BinaryThresholdParameter();
            HandleParameter = binaryParameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(BinaryThresholdView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(RunParameter?.HOutput, "BinaryThreshold");

            HOperatorSet.BinaryThreshold(
                source,
                out var binaryImage,
                binaryParameter.Method.ToString(),
                binaryParameter.LightDark.ToString(),
                out _);

            RunParameter.HOutput = binaryImage;
            RunParameter.SetOutput(RunOutputKeys.Output, binaryImage);
            RunParameter.SetOutput(RunOutputKeys.Region, binaryImage);
            RunParameter.ResultMessage = "二值化完成。";
            RaiseImageDisplay(binaryImage);
        }
    }
}
