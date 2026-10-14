using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    /// <summary>计数模式。</summary>
    public enum CounterMode
    {
        [Description("每轮 +1（累计产量）")] EveryRun = 0,
        [Description("输入为真时 +1（命中计数/NG 计数）")] OnTrue = 1,
        [Description("连续为真计数（清零式，连续 NG 报警用）")] Consecutive = 2
    }

    public class CounterParameter : BindableBase, IHandleParameter
    {
        private CounterMode mode = CounterMode.EveryRun;
        private int modulo;

        [DisplayName("计数模式")]
        [Description("每轮+1 统计产量；输入为真+1 统计 NG 数；连续为真计数在输入变假的轮次自动清零（连续 NG 停机报警用）")]
        public CounterMode Mode { get => mode; set => SetProperty(ref mode, value); }

        [DisplayName("取模 N（0 关闭）")]
        [Description("输出 ModIndex = 计数 % N；IsModHit 在余数为 0 时为真——用于每 N 件触发一次的节拍（如每 5 件抽检）")]
        public int Modulo { get => modulo; set => SetProperty(ref modulo, value); }

        public IHandleParameter Clone() => (CounterParameter)MemberwiseClone();

        public void Reset()
        {
            Mode = CounterMode.EveryRun;
            Modulo = 0;
        }
    }

    /// <summary>
    /// 计数器：跨执行周期持续计数（算子实例存活期间保持）。
    /// 支持累计 / 命中 / 连续命中三种模式与取模输出，配合节点执行条件实现
    /// "连续 3 件 NG 停机"“每 5 件抽检 1 件”等产线联锁。
    /// </summary>
    [Handle("计数器", "Counter", "跨执行周期计数：累计/命中/连续命中三种模式，支持取模输出与复位输入。用于产量统计、连续 NG 报警、每 N 件抽检等联锁。", Common.HandleType_Logic, OperatorBackend = OperatorBackend.Hybrid)]
    public class CounterHandle : ParameterHandleBase<CounterParameter>, IHandleDataSlots
    {
        private const string CountInputKey = "logic.counter.input";
        private const string ResetInputKey = "logic.counter.reset";

        // 节点存续期间保持的实例状态（撤销/重做或删除节点后重置）。
        private int totalCount;
        private int consecutiveCount;

        public CounterHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(CountInputKey, "计数输入(布尔,可选)", typeof(bool)),
            new RunSlot(ResetInputKey, "复位(为真时清零)", typeof(bool))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.CounterCount, "计数值", typeof(int)),
            new RunSlot(RunOutputKeys.CounterConsecutive, "连续命中数", typeof(int)),
            new RunSlot(RunOutputKeys.CounterModIndex, "取模余数", typeof(int)),
            new RunSlot(RunOutputKeys.LogicResult, "取模命中", typeof(bool))
        };

        public override void Handle()
        {
            var reset = LogicInput.ReadBool(RunParameter, ResetInputKey) == true;
            if (reset)
            {
                totalCount = 0;
                consecutiveCount = 0;
            }

            var input = LogicInput.ReadBool(RunParameter, CountInputKey);

            switch (Parameter.Mode)
            {
                case CounterMode.OnTrue:
                    if (input != false) totalCount++;   // 未绑定视为真，等价每轮+1
                    break;

                case CounterMode.Consecutive:
                    totalCount++;
                    // 未绑定视为真；输入为假时连续计数清零。
                    consecutiveCount = input == false ? 0 : consecutiveCount + 1;
                    break;

                default:
                    totalCount++;
                    break;
            }

            var modulo = Math.Max(0, Parameter.Modulo);
            var modIndex = modulo > 0 ? totalCount % modulo : 0;
            var modHit = modulo > 0 && modIndex == 0;

            RunParameter.SetOutput(RunOutputKeys.CounterCount, totalCount);
            RunParameter.SetOutput(RunOutputKeys.CounterConsecutive, consecutiveCount);
            RunParameter.SetOutput(RunOutputKeys.CounterModIndex, modIndex);
            RunParameter.SetOutput(RunOutputKeys.LogicResult, modHit);
            RunParameter.ResultMessage = reset
                ? $"计数器已复位（此前计数清零）"
                : $"计数={totalCount}，连续={consecutiveCount}" + (modulo > 0 ? $"，取模 {totalCount}%{modulo}={modIndex}{(modHit ? "（命中）" : "")}" : "");
        }
    }
}
