using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.EquHistoImage
{
    [Handle("直方图均衡化", "EquHistoImage", "通过直方图均衡化增强低对比度图像细节。", Common.HandleType_Preprocessing)]
    public class EquHistoImageHandle : HandleBase, IHandleDataSlots
    {
        public EquHistoImageHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "EquHistoImage");

            HOperatorSet.EquHistoImage(source, out var result);
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "直方图均衡化完成。";
            RaiseImageDisplay(result);
        }
    }
}
