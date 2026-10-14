using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    // ==================== 条件分流 ====================

    public class ConditionalBranchParameter : BindableBase, IHandleParameter
    {
        private string note = "";

        [DisplayName("分支说明")]
        [Description("备注该分流的作用（如 OK/NG 分流），仅作提示不影响执行")]
        public string Note { get => note; set => SetProperty(ref note, value); }

        public IHandleParameter Clone() => (ConditionalBranchParameter)MemberwiseClone();
        public void Reset() => Note = "";
    }

    /// <summary>
    /// 条件分流：把一路布尔判定拆成 True/False 两路输出。
    /// 下游节点通过「执行条件」挂接对应分支（$TrueBranch$==true / $FalseBranch$==true），
    /// 实现 OK/NG 两段子流程各走各的（如 NG 才保存缺陷图）。
    /// </summary>
    [Handle("条件分流", "ConditionalBranch", "把一路布尔判定拆为 True/False 两路输出；下游节点在右键「执行条件」中绑定 $TrueBranch$/$FalseBranch$ 实现分支子流程（NG 才保存图像等）。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class ConditionalBranchHandle : ParameterHandleBase<ConditionalBranchParameter>, IHandleDataSlots
    {
        private const string ConditionInputKey = "logic.branch.condition";

        public ConditionalBranchHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(ConditionInputKey, "条件输入", typeof(bool))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.TrueBranch, "真分支", typeof(bool)),
            new RunSlot(RunOutputKeys.FalseBranch, "假分支", typeof(bool)),
            new RunSlot(RunOutputKeys.LogicResult, "条件原值", typeof(bool)),
            new RunSlot(RunOutputKeys.LogicDetail, "当前分支名", typeof(string))
        };

        public override void Handle()
        {
            var value = LogicInput.ReadBool(RunParameter, ConditionInputKey)
                ?? throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    "条件分流未绑定条件输入。请连接一路布尔判定（条件比较 / 缺陷测量 / 逻辑运算 的输出）。");

            RunParameter.SetOutput(RunOutputKeys.TrueBranch, value);
            RunParameter.SetOutput(RunOutputKeys.FalseBranch, !value);
            RunParameter.SetOutput(RunOutputKeys.LogicResult, value);
            RunParameter.SetOutput(RunOutputKeys.LogicDetail, value ? "True" : "False");
            RunParameter.ResultMessage = $"条件分流：走 {(value ? "True" : "False")} 分支";
        }
    }

    // ==================== 边沿触发 ====================

    /// <summary>边沿模式。</summary>
    public enum EdgeMode
    {
        [Description("上升沿（假→真）")] Rising = 0,
        [Description("下降沿（真→假）")] Falling = 1,
        [Description("任意跳变")] Either = 2
    }

    public class EdgeTriggerParameter : BindableBase, IHandleParameter
    {
        private EdgeMode mode = EdgeMode.Rising;

        [DisplayName("触发沿")]
        [Description("上升沿：信号从假变真的那一轮触发一次；下降沿反之。用于 PLC 复位/新工件到位等只做一次的动作")]
        public EdgeMode Mode { get => mode; set => SetProperty(ref mode, value); }

        public IHandleParameter Clone() => (EdgeTriggerParameter)MemberwiseClone();

        public void Reset() => Mode = EdgeMode.Rising;
    }

    /// <summary>
    /// 边沿触发：只在输入信号发生指定跳变的那一轮输出真，其余轮次输出假。
    /// 首轮执行只记录当前值不触发。配合节点执行条件实现"信号变为 ON 时才执行一次"。
    /// </summary>
    [Handle("边沿触发", "EdgeTrigger", "只在输入信号发生指定跳变（上升/下降沿）的那一轮输出真，其余轮次为假。用于 PLC 复位信号只回写一次、新工件沿触发首件检测等。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class EdgeTriggerHandle : ParameterHandleBase<EdgeTriggerParameter>, IHandleDataSlots
    {
        private const string SignalInputKey = "logic.edge.signal";

        // 上一轮信号值（null=首轮未记录）。节点存续期间保持。
        private bool? lastValue;

        public EdgeTriggerHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(SignalInputKey, "信号输入", typeof(bool))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.EdgeTriggered, "本轮触发", typeof(bool)),
            new RunSlot(RunOutputKeys.LogicResult, "信号当前值", typeof(bool))
        };

        public override void Handle()
        {
            var signal = LogicInput.ReadBool(RunParameter, SignalInputKey)
                ?? throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    "边沿触发未绑定信号输入。请连接一路布尔信号。");

            var triggered = false;
            if (lastValue.HasValue)
            {
                triggered = Parameter.Mode switch
                {
                    EdgeMode.Rising => !lastValue.Value && signal,
                    EdgeMode.Falling => lastValue.Value && !signal,
                    EdgeMode.Either => lastValue.Value != signal,
                    _ => false
                };
            }

            lastValue = signal;

            RunParameter.SetOutput(RunOutputKeys.EdgeTriggered, triggered);
            RunParameter.SetOutput(RunOutputKeys.LogicResult, signal);
            RunParameter.ResultMessage = triggered
                ? $"边沿触发命中（{Parameter.Mode}，信号={signal}）"
                : $"无跳变（{Parameter.Mode}，信号={signal}）";
        }
    }

    // ==================== 抽检判定 ====================

    public class SamplingGateParameter : BindableBase, IHandleParameter
    {
        private int firstCount = 3;
        private int interval = 5;

        [DisplayName("首件必检数 N")]
        [Description("开机/复位后的前 N 件全部检测（换型后确认稳定），0 表示不做首件全检")]
        public int FirstCount { get => firstCount; set => SetProperty(ref firstCount, Math.Max(0, value)); }

        [DisplayName("抽检间隔 M（0=全检）")]
        [Description("首件之后每 M 件检测 1 件（M=5 即每 5 件抽 1 件）；0 或 1 表示件件全检")]
        public int Interval { get => interval; set => SetProperty(ref interval, value); }

        public IHandleParameter Clone() => (SamplingGateParameter)MemberwiseClone();

        public void Reset()
        {
            FirstCount = 3;
            Interval = 5;
        }
    }

    /// <summary>
    /// 抽检判定：首 N 件必检 + 之后每 M 件抽 1 件，输出本轮"是否需要检测"。
    /// 下游检测链路的节点用执行条件 `$ShouldInspect$ == true` 挂接即得"首件全检转抽检"节拍。
    /// </summary>
    [Handle("抽检判定", "SamplingGate", "首 N 件必检 + 之后每 M 件抽 1 件的节拍门控，输出本轮是否需要检测（ShouldInspect）。换型首件确认后自动转抽检提节拍。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class SamplingGateHandle : ParameterHandleBase<SamplingGateParameter>, IHandleDataSlots
    {
        private const string ResetInputKey = "logic.sampling.reset";

        // 累计件数（节点存续期间保持）。
        private int totalRuns;

        public SamplingGateHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(ResetInputKey, "复位(为真时重新首件)", typeof(bool))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.ShouldInspect, "本轮需检测", typeof(bool)),
            new RunSlot(RunOutputKeys.SampleIndex, "累计件数", typeof(int)),
            new RunSlot(RunOutputKeys.LogicDetail, "阶段", typeof(string))
        };

        public override void Handle()
        {
            if (LogicInput.ReadBool(RunParameter, ResetInputKey) == true)
            {
                totalRuns = 0;
            }

            totalRuns++;

            bool shouldInspect;
            string stage;
            var interval = Math.Max(0, Parameter.Interval);
            if (totalRuns <= Parameter.FirstCount)
            {
                shouldInspect = true;
                stage = $"首件必检（{totalRuns}/{Parameter.FirstCount}）";
            }
            else if (interval <= 1)
            {
                shouldInspect = true;
                stage = "全检";
            }
            else
            {
                var offset = totalRuns - Parameter.FirstCount;
                shouldInspect = offset % interval == 1;
                stage = shouldInspect ? $"抽检命中（第 {offset} 件，间隔 {interval}）" : $"抽检跳过（第 {offset} 件，间隔 {interval}）";
            }

            RunParameter.SetOutput(RunOutputKeys.ShouldInspect, shouldInspect);
            RunParameter.SetOutput(RunOutputKeys.SampleIndex, totalRuns);
            RunParameter.SetOutput(RunOutputKeys.LogicDetail, stage);
            RunParameter.ResultMessage = $"第 {totalRuns} 件：{stage} → {(shouldInspect ? "检测" : "跳过")}";
        }
    }
}
