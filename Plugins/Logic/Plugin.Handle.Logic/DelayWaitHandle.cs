using System.ComponentModel;
using System.Threading;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    /// <summary>等待超时后的动作。</summary>
    public enum WaitTimeoutAction
    {
        [Description("超时后继续（放行）")] Continue = 0,
        [Description("超时后报错（节点失败）")] Fail = 1
    }

    public class DelayWaitParameter : BindableBase, IHandleParameter
    {
        private int delayMs = 200;
        private string waitVariableName = "";
        private int pollIntervalMs = 100;
        private int timeoutMs = 5000;
        private WaitTimeoutAction timeoutAction = WaitTimeoutAction.Continue;

        [DisplayName("固定延时 (ms)")]
        [Description("执行前固定等待的毫秒数（等执行机构动作到位），0 表示不延时")]
        public int DelayMs { get => delayMs; set => SetProperty(ref delayMs, value); }

        [DisplayName("等待变量名（空=不等）")]
        [Description("下拉选择变量；轮询等待其变为真值（true/1/OK）再继续，常用于等通讯触发流程就绪。留空则只做固定延时")]
        [VariableName]
        public string WaitVariableName { get => waitVariableName; set => SetProperty(ref waitVariableName, value); }

        [DisplayName("轮询间隔 (ms)")]
        public int PollIntervalMs { get => pollIntervalMs; set => SetProperty(ref pollIntervalMs, Math.Max(10, value)); }

        [DisplayName("等待超时 (ms，0=无限)")]
        public int TimeoutMs { get => timeoutMs; set => SetProperty(ref timeoutMs, value); }

        [DisplayName("超时动作")]
        public WaitTimeoutAction TimeoutAction { get => timeoutAction; set => SetProperty(ref timeoutAction, value); }

        public IHandleParameter Clone() => (DelayWaitParameter)MemberwiseClone();

        public void Reset()
        {
            DelayMs = 200;
            WaitVariableName = "";
            PollIntervalMs = 100;
            TimeoutMs = 5000;
            TimeoutAction = WaitTimeoutAction.Continue;
        }
    }

    /// <summary>
    /// 延时等待：固定毫秒延时 + 可选轮询等待流程变量为真（带超时）。
    /// 只做节拍等待，不做硬实时控制——时序联锁是 PLC 的职责。
    /// </summary>
    [Handle("延时等待", "DelayWait", "流程中途延时固定毫秒，或轮询等待流程变量（变量存取写入）变为真，带超时与超时动作。用于等执行机构到位、等通讯流程就绪等节拍配合。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class DelayWaitHandle : ParameterHandleBase<DelayWaitParameter>, IHandleDataSlots
    {
        public DelayWaitHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.LogicResult, "等待完成（超时放行时为假）", typeof(bool))
        };

        public override void Handle()
        {
            var elapsedNote = "";

            if (Parameter.DelayMs > 0)
            {
                Thread.Sleep(Parameter.DelayMs);
                elapsedNote = $"延时 {Parameter.DelayMs}ms";
            }

            var completed = true;
            var name = Parameter.WaitVariableName?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var timeout = Parameter.TimeoutMs > 0 ? Parameter.TimeoutMs : int.MaxValue;
                while (!IsTruthyVariable(name))
                {
                    if (watch.ElapsedMilliseconds >= timeout)
                    {
                        completed = false;
                        break;
                    }
                    Thread.Sleep(Parameter.PollIntervalMs);
                }

                elapsedNote += string.IsNullOrEmpty(elapsedNote) ? "" : " + ";
                elapsedNote += completed
                    ? $"等待变量[{name}]就绪（{watch.ElapsedMilliseconds}ms）"
                    : $"等待变量[{name}]超时（>{timeout}ms）";

                if (!completed && Parameter.TimeoutAction == WaitTimeoutAction.Fail)
                {
                    throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                        $"延时等待超时：变量 [{name}] 在 {timeout}ms 内未就绪。");
                }
            }

            RunParameter.SetOutput(RunOutputKeys.LogicResult, completed);
            RunParameter.ResultMessage = string.IsNullOrEmpty(elapsedNote) ? "无延时配置，直接通过。" : $"{elapsedNote}。";
        }

        private static bool IsTruthyVariable(string name)
        {
            // 解析式读取：流程级优先，其次全局。
            var value = WorkflowVariableStore.Get(name);
            return value != null && LogicInput.ToBool(value);
        }
    }
}
