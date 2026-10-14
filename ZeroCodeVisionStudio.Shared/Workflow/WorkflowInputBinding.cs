namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 工作流输入绑定，描述一个目标节点输入槽与上游节点输出槽之间的映射关系。
    /// <para>
    /// 每个 <see cref="IHandleRunModel"/> 节点拥有一个 InputBindings 列表，
    /// 列表中的每项描述该节点的一个输入端口应从哪个上游节点（<see cref="SourceNode"/>）
    /// 的哪个输出键（<see cref="SourceOutputKey"/>）获取数据。
    /// </para>
    /// <para>
    /// 特殊绑定：若 <see cref="TargetInputKey"/> 以 <see cref="ParameterBindingPrefix"/> 开头，
    /// 表示此绑定将上游输出的值直接写入下游节点的算子参数属性，而非 RunParameter 的输入槽。
    /// </para>
    /// </summary>
    public class WorkflowInputBinding : BindableBase
    {
        /// <summary>参数绑定键名的前缀。以此前缀开头的 TargetInputKey 表示绑定目标是算子参数属性。</summary>
        public const string ParameterBindingPrefix = "parameter:";

        /// <summary>
        /// 变量源前缀：SourceNode 为空且 SourceOutputKey 以此开头时，绑定源是全局/流程级变量
        /// （格式 var:g:名称 / var:w:名称），执行时由变量存储取值注入。
        /// </summary>
        public const string VariableSourcePrefix = "var:";

        private IHandleRunModel sourceNode;
        private string sourceOutputKey;

        /// <summary>
        /// 初始化输入绑定，指定目标节点、输入槽键名、显示名称和期望数据类型。
        /// </summary>
        /// <param name="targetNode">拥有此输入槽的目标节点。</param>
        /// <param name="targetInputKey">输入槽的唯一键名，对应 RunParameter.Inputs 或参数属性名。</param>
        /// <param name="targetInputName">输入槽的友好显示名称，在属性面板 UI 中展示。</param>
        /// <param name="targetInputType">输入槽期望接收的数据类型，用于类型兼容性检查。</param>
        /// <param name="description">输入槽的详细描述说明，用于 ToolTip 显示。</param>
        public WorkflowInputBinding(IHandleRunModel targetNode, string targetInputKey, string targetInputName, Type targetInputType = null, string description = null)
        {
            TargetNode = targetNode;
            TargetInputKey = targetInputKey;
            TargetInputName = targetInputName;
            TargetInputType = targetInputType;
            Description = description;
        }

        /// <summary>拥有此输入槽的目标节点（下游节点）。</summary>
        public IHandleRunModel TargetNode { get; }

        /// <summary>
        /// 输入槽的唯一键名。
        /// 若以 <see cref="ParameterBindingPrefix"/> 开头，表示绑定目标为算子参数属性。
        /// </summary>
        public string TargetInputKey { get; }

        /// <summary>输入槽的友好显示名称，在属性面板 UI 中展示给用户。</summary>
        public string TargetInputName { get; }

        /// <summary>输入槽的详细描述说明，用于 ToolTip 显示，帮助用户理解该参数的用途。</summary>
        public string Description { get; }

        /// <summary>输入槽期望的数据类型，框架据此进行自动匹配时的类型兼容性检查。</summary>
        public Type TargetInputType { get; }

        /// <summary>是否为参数绑定（目标是算子参数的某个属性，而非 RunParameter 输入槽）。</summary>
        public bool IsParameterBinding => TargetInputKey?.StartsWith(ParameterBindingPrefix, StringComparison.Ordinal) == true;

        /// <summary>是否为变量源绑定（SourceNode 为空且 SourceOutputKey 以 var: 开头）。</summary>
        public bool IsVariableSource =>
            SourceNode == null &&
            SourceOutputKey?.StartsWith(VariableSourcePrefix, StringComparison.Ordinal) == true;

        /// <summary>构造变量源键（var:g:名 / var:w:名）。</summary>
        public static string MakeVariableSourceKey(WorkflowVariableScope scope, string name) =>
            $"{VariableSourcePrefix}{(scope == WorkflowVariableScope.Global ? "g" : "w")}:{name}";

        /// <summary>解析变量源键；非变量源返回 null。</summary>
        public (WorkflowVariableScope Scope, string Name)? ParseVariableSource()
        {
            if (!IsVariableSource) return null;
            var body = SourceOutputKey.Substring(VariableSourcePrefix.Length);
            var colon = body.IndexOf(':');
            if (colon <= 0 || colon == body.Length - 1) return null;
            var scope = body.Substring(0, colon) == "g" ? WorkflowVariableScope.Global : WorkflowVariableScope.Workflow;
            return (scope, body.Substring(colon + 1));
        }

        /// <summary>
        /// 参数绑定时对应的参数属性名（去掉前缀后的部分）。
        /// 若不是参数绑定，则返回 null。
        /// </summary>
        public string TargetParameterName => IsParameterBinding ? TargetInputKey.Substring(ParameterBindingPrefix.Length) : null;

        /// <summary>
        /// 提供数据的上游节点。
        /// 为 null 表示当前绑定未设置，运行时将使用连线传入的 RunParameter 默认值。
        /// </summary>
        public IHandleRunModel SourceNode
        {
            get => sourceNode;
            set => SetProperty(ref sourceNode, value);
        }

        /// <summary>
        /// 上游节点中要读取的输出槽键名（对应 RunParameter.Outputs 中的键）。
        /// 为 null 或空时使用 <see cref="RunOutputKeys.Output"/> 默认输出。
        /// </summary>
        public string SourceOutputKey
        {
            get => sourceOutputKey;
            set => SetProperty(ref sourceOutputKey, value);
        }

        /// <summary>
        /// 清除绑定，将上游节点和来源键名均置为 null。
        /// 清除后此输入槽将不再主动接收特定数据，回退到默认连线传播行为。
        /// </summary>
        public void Clear()
        {
            SourceNode = null;
            SourceOutputKey = null;
        }
    }
}
