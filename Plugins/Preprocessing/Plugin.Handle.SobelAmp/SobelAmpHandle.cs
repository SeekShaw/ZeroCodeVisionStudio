using System;
using System.Collections.Generic;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.SobelAmp
{
    /// <summary>
    /// Sobel 梯度幅值算子处理器。
    /// 调用 HALCON 算子 <see cref="HOperatorSet.SobelAmp"/> 计算图像的 Sobel 边缘梯度幅值。
    /// </summary>
    [Handle("Sobel边缘幅值", "SobelAmp", "计算图像的 Sobel 边缘幅值。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class SobelAmpHandle : ParameterHandleBase<SobelAmpParameter>, IHandleDataSlots
    {
        public SobelAmpHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "输入灰度图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "梯度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.HOutput,
                "SobelAmp");

            HOperatorSet.SobelAmp(source, out var result, Parameter.FilterType.ToHalconString(), Math.Max(3, Parameter.Size));
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = $"Sobel 边缘计算完成 ({Parameter.FilterType} 尺寸: {Parameter.Size})";
            RaiseImageDisplay(result);
        }
    }
}
