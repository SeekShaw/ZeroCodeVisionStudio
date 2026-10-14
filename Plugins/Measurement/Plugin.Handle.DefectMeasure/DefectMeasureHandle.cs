using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using Plugin.Handle.DefectMeasure.Views;

namespace Plugin.Handle.DefectMeasure
{
    [Handle("缺陷测量", "DefectMeasure", "统计缺陷区域、过滤噪点并判定 OK 或 NG。", Common.HandleType_Measurement, OperatorBackend = OperatorBackend.Halcon)]
    public class DefectMeasureHandle : HandleBase, IHandleDataSlots
    {
        private readonly DefectMeasureParameter parameter;

        public DefectMeasureHandle(IDialogService dialogService) : base(dialogService)
        {
            parameter = new DefectMeasureParameter();
            HandleParameter = parameter;
            SetRunParameter(null);
        }

        protected override string DialogName => nameof(DefectMeasureView);

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public virtual IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出缺陷区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "缺陷区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.DefectCount, "缺陷数量", typeof(int)),
            new RunSlot(RunOutputKeys.TotalDefectArea, "总缺陷面积", typeof(double)),
            new RunSlot(RunOutputKeys.IsOK, "判定结果 (OK/NG)", typeof(bool)),
            new RunSlot("DefectReport", "缺陷明细报告", typeof(string))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "DefectMeasure");

            // 先进行连通域打散，以便按单个缺陷对象统计
            HOperatorSet.Connection(region, out var connectedDefects);

            // 过滤微小噪点缺陷
            if (parameter.MinAreaFilter > 0)
            {
                HOperatorSet.SelectShape(connectedDefects, out var filteredDefects, "area", "and", parameter.MinAreaFilter, 99999999);
                connectedDefects.Dispose();
                connectedDefects = filteredDefects;
            }

            // 统计元组全部确定性释放：AreaCenter 丢弃的 out 参数同样是非托管资源，
            // 连续运行模式下每次泄漏会在长时间节拍下持续累积。
            int count;
            double totalArea = 0.0;

            HOperatorSet.CountObj(connectedDefects, out var countTuple);
            using (countTuple)
            {
                count = countTuple.Length > 0 ? countTuple.I : 0;
            }

            double maxSingleArea = 0.0;
            var detailBuilder = new System.Text.StringBuilder();

            if (count > 0)
            {
                HOperatorSet.AreaCenter(connectedDefects, out var areas, out var rows, out var columns);
                using (areas)
                using (rows)
                using (columns)
                {
                    if (areas.Length > 0)
                    {
                        HOperatorSet.TupleSum(areas, out var sumArea);
                        using (sumArea)
                        {
                            totalArea = sumArea.D;
                        }

                        // 单缺陷面积 + 逐缺陷明细（按面积降序，取前若干条进报告）
                        for (var i = 0; i < areas.Length; i++)
                        {
                            var area = areas[i].D;
                            if (area > maxSingleArea) maxSingleArea = area;
                        }

                        var indexes = Enumerable.Range(0, areas.Length)
                            .OrderByDescending(i => areas[i].D)
                            .Take(Math.Min(10, areas.Length));
                        foreach (var i in indexes)
                        {
                            detailBuilder.Append($"#{i + 1} 面积={areas[i].D:0.##}px 位置=({rows[i].D:0.##},{columns[i].D:0.##})；");
                        }
                    }
                }
            }

            var isNg = count > parameter.AllowedMaxCount ||
                       (parameter.AllowedMaxTotalArea > 0 && totalArea > parameter.AllowedMaxTotalArea) ||
                       (parameter.AllowedMaxSingleArea > 0 && maxSingleArea > parameter.AllowedMaxSingleArea);
            var isOk = !isNg;

            var ngReason = new List<string>();
            if (count > parameter.AllowedMaxCount) ngReason.Add($"数量超限({count}>{parameter.AllowedMaxCount})");
            if (parameter.AllowedMaxTotalArea > 0 && totalArea > parameter.AllowedMaxTotalArea) ngReason.Add($"总面积超限({totalArea:0.##}>{parameter.AllowedMaxTotalArea:0.##})");
            if (parameter.AllowedMaxSingleArea > 0 && maxSingleArea > parameter.AllowedMaxSingleArea) ngReason.Add($"单缺陷超限(最大{maxSingleArea:0.##}>{parameter.AllowedMaxSingleArea:0.##})");

            RunParameter.SetOutput(RunOutputKeys.DefectCount, count);
            RunParameter.SetOutput(RunOutputKeys.TotalDefectArea, totalArea);
            RunParameter.SetOutput(RunOutputKeys.IsOK, isOk);
            RunParameter.SetOutput("DefectReport", detailBuilder.ToString());
            RunParameter.ResultMessage = ngReason.Count == 0
                ? $"OK 缺陷数={count}, 总面积={totalArea:0.##} px, 最大单缺陷={maxSingleArea:0.##} px"
                : $"NG（{string.Join("；", ngReason)}）缺陷数={count}, 总面积={totalArea:0.##} px";
            RunParameter.HOutput = connectedDefects;
            RunParameter.SetOutput(RunOutputKeys.Output, connectedDefects);
            RunParameter.SetOutput(RunOutputKeys.Region, connectedDefects);

            RaiseImageDisplay(connectedDefects);
        }
    }
}
