using System.Collections.ObjectModel;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// 单个输入绑定的编辑器视图模型，供绑定配置面板使用。
    /// 包装了 WorkflowInputBinding，并暴露 Options 和 SelectedOption 供 XAML ComboBox 数据绑定。
    /// </summary>
    public class WorkflowInputBindingEditorItem : BindableBase
    {
        private WorkflowOutputOption selectedOption;

        public WorkflowInputBindingEditorItem(WorkflowInputBinding binding, IEnumerable<WorkflowOutputOption> options)
        {
            Binding = binding;
            Options = new ObservableCollection<WorkflowOutputOption>(options);
            selectedOption = Options.FirstOrDefault(x => Matches(x, binding)) ?? Options.FirstOrDefault();
            ClearCommand = new DelegateCommand(Clear);
        }

        public DelegateCommand ClearCommand { get; }

        public WorkflowInputBinding Binding { get; }

        /// <summary>选项与当前绑定是否一致（节点输出源按引用比较；变量源按 var: 键解析比较）。</summary>
        private static bool Matches(WorkflowOutputOption option, WorkflowInputBinding binding)
        {
            if (binding.IsVariableSource)
            {
                var parsed = binding.ParseVariableSource();
                return parsed != null && option.IsVariable &&
                       string.Equals(option.VariableName, parsed.Value.Name, StringComparison.Ordinal) &&
                       option.VariableScope == parsed.Value.Scope;
            }

            return !option.IsVariable &&
                   ReferenceEquals(option.SourceNode, binding.SourceNode) &&
                   option.OutputKey == binding.SourceOutputKey;
        }

        public string InputName => Binding.TargetInputName;

        /// <summary>参数的详细描述说明，用于 ToolTip 显示。</summary>
        public string Description => Binding.Description;

        public ObservableCollection<WorkflowOutputOption> Options { get; }

        /// <summary>
        /// 选中某个输出选项时，同步更新底层 WorkflowInputBinding 的来源节点和输出键。
        /// 选中"未绑定"占位项时清空绑定。
        /// </summary>
        public WorkflowOutputOption SelectedOption
        {
            get => selectedOption;
            set
            {
                if (!SetProperty(ref selectedOption, value))
                {
                    return;
                }

                if (value == null || value.IsEmpty)
                {
                    Binding.Clear();
                    return;
                }

                if (value.IsVariable)
                {
                    Binding.SourceNode = null;
                    Binding.SourceOutputKey = WorkflowInputBinding.MakeVariableSourceKey(value.VariableScope, value.VariableName);
                }
                else
                {
                    Binding.SourceNode = value.SourceNode;
                    Binding.SourceOutputKey = value.OutputKey;
                }
            }
        }

        /// <summary>
        /// 清除当前绑定，并将下拉框重置到"未绑定"选项。
        /// </summary>
        public void Clear()
        {
            Binding.Clear();
            SelectedOption = Options.FirstOrDefault();
        }
    }
}

