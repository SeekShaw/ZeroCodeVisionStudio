namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 空参数标记类，适用于没有任何可编辑参数的算子。
    /// 使用此类可使算子符合 <see cref="IHandleParameter"/> 约定，
    /// 同时避免为无参数算子额外创建专属参数类。
    /// <para>
    /// Clone() 返回自身引用，Reset() 为空操作——因为没有任何状态需要管理。
    /// </para>
    /// </summary>
    public class EmptyParameter : IHandleParameter
    {
        /// <summary>
        /// 返回当前实例自身（无状态，无需深拷贝）。
        /// </summary>
        public IHandleParameter Clone()
        {
            return this;
        }

        /// <summary>
        /// 空实现（无状态，无需重置）。
        /// </summary>
        public void Reset()
        {
        }
    }
}
