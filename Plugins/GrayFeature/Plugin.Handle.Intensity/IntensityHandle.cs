using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Intensity
{
    [Handle("平均灰度", "Intensity", "计算区域内的平均灰度值和标准差，支持期望灰度与容差评估。", Common.HandleType_GrayFeature)]
    public class IntensityHandle : ParameterHandleBase<IntensityParameter>, IHandleDataSlots
    {
        public IntensityHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "输出区域 ROI", typeof(HObject)),
            new RunSlot(RunOutputKeys.IntensityMean, "平均灰度", typeof(HTuple)),
            new RunSlot(RunOutputKeys.IntensityDeviation, "灰度标准差", typeof(HTuple)),
            new RunSlot(RunOutputKeys.IsOK, "判定结果 (OK/NG)", typeof(bool))
        };

        public override void Handle()
        {
            var image = HandleInputGuard.RequireImage(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.GrayImage) ?? RunParameter?.GrayImage ?? RunParameter?.RawImage ?? RunParameter?.HOutput,
                "Intensity",
                "灰度图像");

            var candidateRegion = RunParameter?.GetOutput<HObject>(RunOutputKeys.Region);
            HObject evalRegion = null;
            bool disposeRegion = false;
            try
            {
                if (candidateRegion != null)
                {
                    try
                    {
                        HOperatorSet.GetObjClass(candidateRegion, out var cls);
                        if (cls.S == "region") evalRegion = candidateRegion;
                    }
                    catch { }
                }

                if (evalRegion == null)
                {
                    HOperatorSet.GetDomain(image, out evalRegion);
                    disposeRegion = true;
                }

                HOperatorSet.Intensity(evalRegion, image, out var mean, out var dev);

                RunParameter.SetOutput(RunOutputKeys.IntensityMean, mean);
                RunParameter.SetOutput(RunOutputKeys.IntensityDeviation, dev);
                RunParameter.HOutput = evalRegion;
                RunParameter.SetOutput(RunOutputKeys.Output, evalRegion);
                RunParameter.SetOutput(RunOutputKeys.Region, evalRegion);

                var param = Parameter ?? new IntensityParameter();
                double meanVal = mean.Length > 0 ? mean[0].D : 0;
                bool ok = Math.Abs(meanVal - param.ExpectedMean) <= param.Tolerance;
                RunParameter.SetOutput(RunOutputKeys.IsOK, ok);
                RunParameter.ResultMessage = param.Tolerance < 255.0
                    ? $"Mean={T(mean, "0.##")}  Deviation={T(dev, "0.##")} [{(ok ? "OK" : "NG")} 期望={param.ExpectedMean:0.#}±{param.Tolerance:0.#}]"
                    : $"Mean={T(mean, "0.##")}  Deviation={T(dev, "0.##")}";

                RaiseImageDisplay(evalRegion);
            }
            finally
            {
                if (disposeRegion && evalRegion != null) evalRegion.Dispose();
            }
        }

        private static string T(HTuple t, string fmt = "0.###") => t == null ? "" : string.Join(", ", Enumerable.Range(0, t.Length).Select(i => t[i].D.ToString(fmt)));
    }
}
