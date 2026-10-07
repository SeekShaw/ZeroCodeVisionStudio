using System.ComponentModel;
using System.Globalization;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    // ==================== 变量写入 ====================

    /// <summary>变量写入作用域。</summary>
    public enum VariableWriteScope
    {
        [Description("全局（所有流程可读）")] Global = 0,
        [Description("流程级（仅当前流程可读）")] Workflow = 1
    }

    public class VariableSetParameter : BindableBase, IHandleParameter
    {
        private string variableName = "";
        private string value = "";
        private VariableWriteScope scope = VariableWriteScope.Global;
        private bool clearAll;

        [DisplayName("变量名")]
        [Description("下拉选择已有变量或输入新名；同名写入即覆盖")]
        [VariableName]
        public string VariableName { get => variableName; set => SetProperty(ref variableName, value); }

        [DisplayName("写入作用域")]
        [Description("全局：所有流程可读；流程级：仅当前流程可读（同名时流程级优先被读取）")]
        public VariableWriteScope Scope { get => scope; set => SetProperty(ref scope, value); }

        [DisplayName("值")]
        [Description("支持 $Key$ 上游插值（如 $IsOK$ / $Distance$）；布尔写 true/false，数值直接写数字")]
        public string Value { get => value; set => SetProperty(ref this.value, value); }

        [DisplayName("清空全部变量")]
        [Description("勾选后本次执行清空所有流程变量（变量名与值不再写入）")]
        public bool ClearAll { get => clearAll; set => SetProperty(ref clearAll, value); }

        public IHandleParameter Clone() => (VariableSetParameter)MemberwiseClone();

        public void Reset()
        {
            VariableName = "";
            Value = "";
            Scope = VariableWriteScope.Global;
            ClearAll = false;
        }
    }

    /// <summary>
    /// 变量写入：把上游输出（或固定值）写入全局流程变量，供其它节点/其它流程/后续执行轮次读取。
    /// </summary>
    [Handle("变量写入", "VariableSet", "写入全局流程变量（值支持 $Key$ 上游插值）。跨节点/跨流程/跨执行周期共享数据：标定系数、工件类型、通讯握手标记等。", Common.HandleType_Logic)]
    public class VariableSetHandle : ParameterHandleBase<VariableSetParameter>, IHandleDataSlots
    {
        public VariableSetHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.VariableValueText, "写入的值", typeof(string))
        };

        public override void Handle()
        {
            if (Parameter.ClearAll)
            {
                WorkflowVariableStore.ClearAll(WorkflowVariableScope.Global);
                WorkflowVariableStore.ClearAll(WorkflowVariableScope.Workflow);
                RunParameter.ResultMessage = "已清空全部流程变量（全局与流程级）";
                return;
            }

            if (string.IsNullOrWhiteSpace(Parameter.VariableName))
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "变量写入未填写变量名。");

            var value = Interpolate(Parameter.Value ?? "");
            var scope = Parameter.Scope == VariableWriteScope.Workflow
                ? WorkflowVariableScope.Workflow
                : WorkflowVariableScope.Global;
            WorkflowVariableStore.Set(Parameter.VariableName.Trim(), value, scope);

            RunParameter.SetOutput(RunOutputKeys.VariableValueText, value);
            RunParameter.ResultMessage = $"变量 [{(scope == WorkflowVariableScope.Global ? "全局" : "流程")}:{Parameter.VariableName.Trim()}] = {value}";
        }

        /// <summary>$Key$ 插值（上游输出替换）。</summary>
        private string Interpolate(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains('$') || RunParameter == null)
                return template;

            foreach (var kv in RunParameter.Outputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            foreach (var kv in RunParameter.Inputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            return template;
        }
    }

    // ==================== 变量读取 ====================

    public class VariableGetParameter : BindableBase, IHandleParameter
    {
        private string variableName = "";
        private string fallback = "";

        [DisplayName("变量名")]
        [Description("下拉选择已有变量或输入新名")]
        [VariableName]
        public string VariableName { get => variableName; set => SetProperty(ref variableName, value); }

        [DisplayName("缺省值")]
        [Description("变量尚未写入时使用的默认值（首次执行 / 变量被清空时）")]
        public string Fallback { get => fallback; set => SetProperty(ref fallback, value); }

        public IHandleParameter Clone() => (VariableGetParameter)MemberwiseClone();

        public void Reset()
        {
            VariableName = "";
            Fallback = "";
        }
    }

    /// <summary>
    /// 变量读取：读取全局流程变量，输出文本/数值/布尔三种形态；变量不存在时使用缺省值。
    /// 配合节点执行条件（如 `$VariableValueBool$ == true`）可实现模式切换/流程门控。
    /// </summary>
    [Handle("变量读取", "VariableGet", "读取全局流程变量，输出文本/数值/布尔三形态（不存在用缺省值）。与变量写入、节点执行条件配合实现模式切换与流程门控。", Common.HandleType_Logic)]
    public class VariableGetHandle : ParameterHandleBase<VariableGetParameter>, IHandleDataSlots
    {
        public VariableGetHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = Array.Empty<RunSlot>();

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.VariableValueText, "值(文本)", typeof(string)),
            new RunSlot(RunOutputKeys.VariableValueNumber, "值(数值)", typeof(double)),
            new RunSlot(RunOutputKeys.VariableValueBool, "值(布尔)", typeof(bool))
        };

        public override void Handle()
        {
            if (string.IsNullOrWhiteSpace(Parameter.VariableName))
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "变量读取未填写变量名。");

            var name = Parameter.VariableName.Trim();
            // 解析式读取：流程级优先，其次全局，均无则用缺省值。
            var text = WorkflowVariableStore.Get(name) ?? Parameter.Fallback ?? "";

            var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.0;

            RunParameter.SetOutput(RunOutputKeys.VariableValueText, text);
            RunParameter.SetOutput(RunOutputKeys.VariableValueNumber, number);
            RunParameter.SetOutput(RunOutputKeys.VariableValueBool, LogicInput.ToBool(text));
            RunParameter.ResultMessage = $"变量 [{name}] = {text}（数值 {number}，布尔 {LogicInput.ToBool(text)}）";
        }
    }
}
