using System.Collections.Generic;
using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.RgbToGray
{
    [Handle("RGB转灰度", "RgbToGray", "将 RGB 图像转换为灰度图像。", Common.HandleType_Preprocessing)]
    public class RgbToGrayHandle : HandleBase, IHandleDataSlots
    {
        public RgbToGrayHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.RawImage, "输入图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.RawImage) ?? RunParameter?.RawImage ?? RunParameter?.HOutput,
                "RgbToGray",
                "RGB 图像");

            HOperatorSet.Rgb1ToGray(source, out var grayImage);
            RunParameter.GrayImage = grayImage;
            RunParameter.HOutput = grayImage;
            RunParameter.SetOutput(RunOutputKeys.Output, grayImage);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, grayImage);
            RunParameter.ResultMessage = "RGB 转灰度完成。";
            RaiseImageDisplay(grayImage);
        }
    }
}
