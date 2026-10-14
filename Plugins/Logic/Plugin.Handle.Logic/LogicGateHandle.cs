using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    /// <summary>逻辑运算模式。</summary>
    public enum LogicGateMode
    {
        [Description("与 AND（全部为真）")] And = 0,
        [Description("或 OR（任一为真）")] Or = 1,
        [Description("非 NOT（输入A取反）")] NotA = 2,
        [Description("与非 NAND")] Nand = 3,
        [Description("或非 NOR")] Nor = 4,
        [Description("异或 XOR")] Xor = 5,
        [Description("多数表决（过半为真）")] Majority = 6
    }

    public class LogicGateParameter : BindableBase, IHandleParameter
    {
        private LogicGateMode mode = LogicGateMode.And;
        private bool unboundAsFalse = true;

        [DisplayName("运算模式")]
        [Description("AND 全真 / OR 任一真 / NOT 输入A取反 / NAND / NOR / XOR / 多数表决（已绑定的输入中过半为真）")]
        public LogicGateMode Mode { get => mode; set => SetProperty(ref mode, value); }

        [DisplayName("未绑定输入处理")]
        [Description("勾选：未绑定的输入按 false 参与运算；取消：未绑定的输入被忽略（不参与）")]
        public bool UnboundAsFalse { get => unboundAsFalse; set => SetProperty(ref unboundAsFalse, value); }

        public IHandleParameter Clone() => (LogicGateParameter)MemberwiseClone();

        public void Reset()
        {
            Mode = LogicGateMode.And;
            UnboundAsFalse = true;
        }
    }

    /// <summary>
    /// 逻辑运算：把多路布尔判定（缺陷 OK/NG、尺寸合格、字符校验…）按 AND/OR/XOR/多数表决
    /// 合成为一个总判定，输出 Result 与各路明细。多工位合格判定的汇聚节点。
    /// </summary>
    [Handle("逻辑运算", "LogicGate", "多路布尔输入做与/或/非/异或/多数表决，合成总判定（多工位合格判定汇聚用）。未绑定的输入可按 false 参与或忽略。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class LogicGateHandle : ParameterHandleBase<LogicGateParameter>, IHandleDataSlots
    {
        private const string InputA = "logic.in.a";
        private const string InputB = "logic.in.b";
        private const string InputC = "logic.in.c";
        private const string InputD = "logic.in.d";

        public LogicGateHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(InputA, "输入A", typeof(bool)),
            new RunSlot(InputB, "输入B", typeof(bool)),
            new RunSlot(InputC, "输入C", typeof(bool)),
            new RunSlot(InputD, "输入D", typeof(bool))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.LogicResult, "运算结果", typeof(bool)),
            new RunSlot(RunOutputKeys.LogicDetail, "明细", typeof(string))
        };

        public override void Handle()
        {
            var keys = new[] { InputA, InputB, InputC, InputD };
            var labels = new[] { "A", "B", "C", "D" };

            var bound = new List<(string Label, bool Value)>();
            var unboundCount = 0;
            for (var i = 0; i < keys.Length; i++)
            {
                var value = LogicInput.ReadBool(RunParameter, keys[i]);
                if (value.HasValue) bound.Add((labels[i], value.Value));
                else unboundCount++;
            }

            // 未绑定输入处理：参与（按 false）或忽略。
            if (unboundCount > 0 && Parameter.UnboundAsFalse)
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    if (LogicInput.ReadBool(RunParameter, keys[i]) == null)
                        bound.Add((labels[i], false));
                }
            }

            if (bound.Count == 0)
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    "逻辑运算未绑定任何输入。请至少连接一路布尔判定（如 条件比较 / 缺陷测量 的 OK/NG 输出）。");

            bool result;
            switch (Parameter.Mode)
            {
                case LogicGateMode.NotA:
                    result = !bound[0].Value;
                    break;
                case LogicGateMode.Or:
                    result = bound.Any(x => x.Value);
                    break;
                case LogicGateMode.Nand:
                    result = !bound.All(x => x.Value);
                    break;
                case LogicGateMode.Nor:
                    result = !bound.Any(x => x.Value);
                    break;
                case LogicGateMode.Xor:
                    result = bound.Count(x => x.Value) % 2 == 1;
                    break;
                case LogicGateMode.Majority:
                    result = bound.Count(x => x.Value) * 2 > bound.Count;
                    break;
                default:
                    result = bound.All(x => x.Value);
                    break;
            }

            var detail = string.Join("，", bound.Select(x => $"{x.Label}={(x.Value ? "真" : "假")}")) +
                         $"（{Parameter.Mode}，{bound.Count} 路参与）";

            RunParameter.SetOutput(RunOutputKeys.LogicResult, result);
            RunParameter.SetOutput(RunOutputKeys.LogicDetail, detail);
            RunParameter.ResultMessage = $"逻辑运算：{detail} → {(result ? "真" : "假")}";
        }
    }
}
