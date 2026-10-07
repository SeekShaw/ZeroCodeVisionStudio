namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 带类型参数的算子基类，适用于拥有自定义参数对象的算子。
    /// 继承此类的算子会在构造时自动创建一个 <typeparamref name="TParameter"/> 实例，
    /// 并在整个生命周期内持有它（对话框编辑的参数值可持续保留）。
    /// 对话框注册名称固定使用通用参数编辑对话框 (<see cref="Common.ParameterDialogName"/>)。
    /// </summary>
    /// <typeparam name="TParameter">
    /// 算子参数类型，必须实现 <see cref="IHandleParameter"/> 接口，并拥有无参构造函数。
    /// </typeparam>
    public abstract class ParameterHandleBase<TParameter> : HandleBase
        where TParameter : IHandleParameter, new()
    {
        /// <summary>
        /// 初始化算子，自动创建参数实例并初始化空的 RunParameter。
        /// </summary>
        /// <param name="dialogService">Prism 对话框服务实例，由 DI 容器注入。</param>
        protected ParameterHandleBase(IDialogService dialogService) : base(dialogService)
        {
            Parameter = new TParameter();
            HandleParameter = Parameter;
            SetRunParameter(null);
        }

        /// <summary>
        /// 强类型参数访问属性，子类使用此属性读写参数值，无需每次手动强转。
        /// </summary>
        protected TParameter Parameter { get; }

        /// <summary>使用通用参数对话框（ParameterDialog）的注册名称。</summary>
        protected override string DialogName => Common.ParameterDialogName;
    }
}
