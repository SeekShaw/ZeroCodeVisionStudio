using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Connection
{
    [Handle("连通域", "Connection", "将区域拆分为独立连通的候选区域。", Common.HandleType_RegionProcessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class ConnectionHandle : HandleBase, IHandleDataSlots
    {
        public ConnectionHandle(IDialogService dialogService) : base(dialogService)
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
            new RunSlot(RunOutputKeys.Output, "连通域区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "Connection");

            HOperatorSet.Connection(region, out var connectedRegions);
            RunParameter.HOutput = connectedRegions;
            RunParameter.SetOutput(RunOutputKeys.Output, connectedRegions);
            RunParameter.SetOutput(RunOutputKeys.Region, connectedRegions);
            RunParameter.ResultMessage = "连通域分析完成。";
            RaiseImageDisplay(connectedRegions);
        }
    }
}
