using System;
using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Emphasize
{
    /// <summary>
    /// 图像增强算子处理器。
    /// 调用 HALCON 算子 <see cref="HOperatorSet.Emphasize"/> 增强图像局部对比度。
    /// </summary>
    [Handle("图像增强", "Emphasize", "在分割前增强图像局部对比度。", Common.HandleType_Preprocessing)]
    public class EmphasizeHandle : ParameterHandleBase<EmphasizeParameter>, IHandleDataSlots
    {
        public EmphasizeHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "增强图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "Emphasize");

            HOperatorSet.Emphasize(source, out var result, Math.Max(1, Parameter.MaskWidth), Math.Max(1, Parameter.MaskHeight), Parameter.Factor);
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"图像增强完成 (Factor: {Parameter.Factor:0.##}, Mask: {Parameter.MaskWidth}x{Parameter.MaskHeight})";
            RaiseImageDisplay(result);
        }
    }
}
