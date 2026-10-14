using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FillUp
{
    [Handle("区域填充", "FillUp", "填充缺陷区域内部的孔洞。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class FillUpHandle : HandleBase, IHandleDataSlots
    {
        public FillUpHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "填充区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "FillUp");

            HOperatorSet.FillUp(region, out var result);
            RunParameter.HOutput = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.Region, result);
            RunParameter.ResultMessage = "区域填充完成。";
            RaiseImageDisplay(result);
        }
    }
}
