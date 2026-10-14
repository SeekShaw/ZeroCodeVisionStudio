using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using ZeroCodeVisionStudio.Shared;
using HalconDotNet;
using Prism.Ioc;

namespace ZeroCodeVisionStudio.Core
{
    /// <summary>
    /// 算子运行模型，是流程画布上一个节点的完整运行时视图对象。
    /// <para>
    /// 职责：
    /// <list type="bullet">
    ///   <item><description>通过 Prism DI 容器解析并持有算子实例（<see cref="Handle"/>）。</description></item>
    ///   <item><description>维护节点的执行状态（<see cref="Status"/>）和状态消息（<see cref="StatusMessage"/>）。</description></item>
    ///   <item><description>管理输入绑定列表（<see cref="InputBindings"/>），支持数据槽绑定和参数属性绑定两种模式。</description></item>
    ///   <item><description>实现 <see cref="IWorkflowNodePosition"/>，提供画布节点的坐标属性。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class HandleRunModel : BindableBase, IHandleRunModel, IWorkflowNodePosition
    {
        private bool disposed;
        private TimeSpan lastElapsed;
        private string executionCondition;

        /// <summary>节点级执行条件（$Key$ 插值 + 单比较，空=无条件执行；为假时流程引擎跳过本节点）。</summary>
        public string ExecutionCondition
        {
            get => executionCondition;
            set => SetProperty(ref executionCondition, value);
        }

        /// <summary>
        /// 初始化算子运行模型，通过 DI 容器解析算子实例并构建输入绑定列表。
        /// </summary>
        /// <param name="registerModel">算子注册信息（类型、名称、键名等元数据）。</param>
        /// <param name="dispname">节点在画布上的初始显示名称。</param>
        public HandleRunModel(IHandleRegisterModel registerModel, string dispname)
        {
            RegisterModel = registerModel;

            try
            {
                Handle = (IHandle)ContainerLocator.Container.Resolve(registerModel.Handle);
            }
            catch (Exception ex)
            {
                // 算子实例化失败——常见原因：Halcon 运行时缺失导致的 DllNotFoundException / TypeLoadException。
                // 不崩溃、不弹窗（批量加载/方案加载时弹窗会死循环），构造一个 ErrorStubHandle 占位。
                Handle = new ErrorStubHandle(registerModel, ex);
            }

            DisplayName = dispname;
            Status = HandleStatus.Pending;
            InitializeInputBindings();
        }

        /// <summary>当前节点对应的算子实例，负责执行实际图像处理逻辑。</summary>
        public IHandle Handle { get; private set; }

        /// <summary>算子注册信息（类型、名称、键名、分类、图标等元数据）。</summary>
        public IHandleRegisterModel RegisterModel { get; }

        /// <summary>节点在画布上的显示名称（由 SequenceNameGenerator 生成，用户与 AI 助手可重命名）。</summary>
        public string DisplayName { get => field; set => SetProperty(ref field, value); }

        /// <summary>算子参数类的类型名称，显示在"模块结果"表格的参数列中。</summary>
        public string ParameterName => Handle?.HandleParameter?.GetType().Name ?? string.Empty;

        /// <summary>节点当前执行状态（Pending/Running/Success/Failed），变化时通知 UI 更新状态图标。</summary>
        public HandleStatus Status { get => field; set => SetProperty(ref field, value); }

        /// <summary>执行状态的附加说明，成功时为结果摘要，失败时为错误信息。</summary>
        public string StatusMessage { get => field; set => SetProperty(ref field, value); }

        public TimeSpan LastElapsed
        {
            get => lastElapsed;
            set
            {
                if (SetProperty(ref lastElapsed, value))
                {
                    RaisePropertyChanged(nameof(LastElapsedText));
                }
            }
        }

        public string LastElapsedText => ExecutionTimeFormatter.Format(LastElapsed);

        /// <summary>节点是否处于选中状态，用于 UI 高亮选中节点。</summary>
        public bool IsSelected { get => field; set => SetProperty(ref field, value); }

        /// <summary>
        /// 输入绑定列表，描述每个输入槽当前绑定的上游数据源。
        /// 使用 ObservableCollection 以支持 UI 实时更新。
        /// </summary>
        public IList<WorkflowInputBinding> InputBindings { get; } = new ObservableCollection<WorkflowInputBinding>();

        /// <summary>节点在流程画布上的水平坐标（像素），变化时通知 UI 更新节点位置。</summary>
        public double X { get => field; set => SetProperty(ref field, value); }

        /// <summary>节点在流程画布上的垂直坐标（像素），变化时通知 UI 更新节点位置。</summary>
        public double Y { get => field; set => SetProperty(ref field, value); }

        /// <summary>
        /// 重置节点状态：清空 RunParameter 数据，将状态恢复为 Pending。
        /// 在每次完整流程执行前会对所有节点调用此方法。
        /// </summary>
        public void Reset()
        {
            if (Handle is IHandleRuntimeResourceOwner resourceOwner)
            {
                resourceOwner.ReleaseRuntimeResources();
            }

            Handle?.RunParameter?.Reset();
            Status = HandleStatus.Pending;
            StatusMessage = string.Empty;
            LastElapsed = TimeSpan.Zero;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Handle?.ReleaseResources();
            Handle = null;
            InputBindings.Clear();
            disposed = true;
        }

        /// <summary>
        /// 根据算子当前配置重新构建输入绑定列表。
        /// <para>
        /// 构建策略：
        /// <list type="number">
        ///   <item><description>若算子实现 <see cref="IHandleDataSlots"/>，按声明的 InputSlots 生成绑定项。</description></item>
        ///   <item><description>否则（非图像输入类算子），生成默认的"输入图像/区域"绑定项。</description></item>
        ///   <item><description>扫描算子参数属性，为可绑定的简单类型属性生成参数绑定项。</description></item>
        /// </list>
        /// </para>
        /// 已存在且键名相同的绑定会保留原有来源节点和来源输出，避免用户重新配置。
        /// </summary>
        public void RefreshInputBindings()
        {
            var existingBindings = InputBindings.ToDictionary(x => x.TargetInputKey, x => x);
            InputBindings.Clear();

            foreach (var binding in BuildDataSlotBindings())
            {
                RestoreBindingSource(binding, existingBindings);
                InputBindings.Add(binding);
            }

            foreach (var binding in BuildParameterBindings())
            {
                RestoreBindingSource(binding, existingBindings);
                InputBindings.Add(binding);
            }
        }

        /// <summary>构造时调用，完成初始输入绑定列表的构建。</summary>
        private void InitializeInputBindings()
        {
            RefreshInputBindings();
        }

        /// <summary>
        /// 构建数据槽绑定项：
        /// 若算子实现 <see cref="IHandleDataSlots"/>，按声明槽构建；否则添加默认图像输入槽。
        /// </summary>
        private IEnumerable<WorkflowInputBinding> BuildDataSlotBindings()
        {
            if (Handle is IHandleDataSlots slots)
            {
                var declared = slots.InputSlots?
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Key))
                    .ToList();

                if (declared is { Count: > 0 })
                {
                    foreach (var slot in declared)
                    {
                        yield return new WorkflowInputBinding(this, slot.Key, slot.DisplayName ?? slot.Key, slot.ValueType);
                    }

                    yield break;
                }

                // 实现了槽接口但未声明任何输入槽（如 ReadShapeModel 这类中间处理算子）：
                // 回退默认主输入槽——纯绑定引擎下没有槽就没有数据入口。
            }

            // 非图像输入类算子（相机、文件读取等）不需要默认图像输入槽
            if (RegisterModel?.HandleType != Common.HandleType_ImageInput)
            {
                yield return new WorkflowInputBinding(this, RunOutputKeys.Output, "输入图像/区域", typeof(HObject));
            }
        }

        /// <summary>
        /// 构建参数属性绑定项：
        /// 扫描算子参数对象的可读写公共属性，对枚举、字符串、数值等可绑定类型生成参数绑定项。
        /// </summary>
        private IEnumerable<WorkflowInputBinding> BuildParameterBindings()
        {
            var parameter = Handle?.HandleParameter;
            if (parameter == null)
            {
                yield break;
            }

            var properties = parameter.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsBindableParameterProperty);

            foreach (var property in properties)
            {
                var displayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
                var name = string.IsNullOrWhiteSpace(displayName) ? property.Name : displayName;
                var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
                yield return new WorkflowInputBinding(
                    this,
                    WorkflowInputBinding.ParameterBindingPrefix + property.Name,
                    $"{name}（参数）",
                    Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType,
                    description);
            }
        }

        /// <summary>
        /// 从 existingBindings 中恢复绑定的来源节点和来源输出键，保留用户已配置的绑定关系。
        /// </summary>
        private static void RestoreBindingSource(WorkflowInputBinding binding, IDictionary<string, WorkflowInputBinding> existingBindings)
        {
            if (binding == null || existingBindings == null)
            {
                return;
            }

            if (existingBindings.TryGetValue(binding.TargetInputKey, out var existingBinding))
            {
                binding.SourceNode = existingBinding.SourceNode;
                binding.SourceOutputKey = existingBinding.SourceOutputKey;
            }
        }

        /// <summary>
        /// 判断一个属性是否适合作为参数绑定目标。
        /// 可绑定类型：枚举、string、bool、int、double、float、long、HTuple。
        /// 只读、索引器和集合类属性不参与绑定。
        /// </summary>
        private static bool IsBindableParameterProperty(PropertyInfo property)
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return type.IsEnum ||
                type == typeof(string) ||
                type == typeof(bool) ||
                type == typeof(int) ||
                type == typeof(double) ||
                type == typeof(float) ||
                type == typeof(long) ||
                type == typeof(HTuple);
        }

        /// <summary>返回节点显示名称，方便调试和 ListBox ItemTemplate 的默认文本。</summary>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
