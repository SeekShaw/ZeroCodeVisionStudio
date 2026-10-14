namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 数据传输槽，描述算子的一个命名输入/输出端口。
    /// <see cref="IHandleDataSlots"/> 接口通过此类声明算子支持的所有输入/输出项，
    /// 框架据此在节点属性面板中生成输入绑定选择器。
    /// </summary>
    public class RunSlot
    {
        /// <summary>
        /// 初始化空的数据槽（供序列化/反序列化使用）。
        /// </summary>
        public RunSlot()
        {
        }

        /// <summary>
        /// 初始化数据槽并设置所有元数据。
        /// </summary>
        /// <param name="key">槽的唯一键名，与 <see cref="RunOutputKeys"/> 中的常量对应。</param>
        /// <param name="displayName">槽的友好显示名称，在绑定选择器 UI 中展示。</param>
        /// <param name="valueType">槽所传递数据的 CLR 类型，用于类型兼容性检查。</param>
        public RunSlot(string key, string displayName, Type valueType)
        {
            Key = key;
            DisplayName = displayName;
            ValueType = valueType;
        }

        /// <summary>槽的唯一键名，对应 RunParameter.Outputs/Inputs 字典中的键。</summary>
        public string Key { get; set; }

        /// <summary>槽的友好显示名称，在输入绑定选择器和工具提示中显示。</summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 槽所传递数据的期望 CLR 类型。
        /// 框架在自动绑定时会检查上下游槽的类型兼容性，类型兼容的槽优先自动匹配。
        /// </summary>
        public Type ValueType { get; set; }

        /// <summary>
        /// 当前槽中存储的数据值。
        /// 仅在运行时使用，序列化时不持久化。
        /// </summary>
        public object Value { get; set; }
    }
}
