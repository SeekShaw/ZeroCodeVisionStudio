using System.Collections.Specialized;
using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.CompareCondition.ViewModels
{
    public class CompareConditionViewModel : DefectDialogViewModel<CompareConditionHandle, CompareConditionParameter>
    {
        public CompareConditionViewModel()
        {
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand<CompareConditionItem>(RemoveItem);
        }

        public DelegateCommand AddItemCommand { get; }

        public DelegateCommand<CompareConditionItem> RemoveItemCommand { get; }

        public IReadOnlyList<CompareType> CompareTypes { get; } = Enum.GetValues<CompareType>();

        public IReadOnlyList<CompareValueType> ValueTypes { get; } = Enum.GetValues<CompareValueType>();

        public IReadOnlyList<CompareRightValueMode> RightValueModes { get; } = Enum.GetValues<CompareRightValueMode>();

        public IReadOnlyList<LogicOperator> LogicOperators { get; } = Enum.GetValues<LogicOperator>();

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);

            if (Parameter?.Conditions != null)
            {
                Parameter.EnsureDefaultItems();
                Parameter.Conditions.CollectionChanged -= Conditions_CollectionChanged;
                Parameter.Conditions.CollectionChanged += Conditions_CollectionChanged;
                SubscribeConditionItems(Parameter.Conditions);
            }

            Message = "每个比较项都会生成左值输入槽；右值来源为“Input”时会额外生成右值输入槽。";
        }

        public override void OnDialogClosed()
        {
            if (Parameter?.Conditions != null)
            {
                Parameter.Conditions.CollectionChanged -= Conditions_CollectionChanged;
                UnsubscribeConditionItems(Parameter.Conditions);
            }
        }

        protected override void OnExecute()
        {
            base.OnExecute();
        }

        protected override void OnReset()
        {
            base.OnReset();
            RefreshInputBindings();
            Message = "比较条件已重置。";
        }

        private void AddItem()
        {
            if (Parameter?.Conditions == null)
            {
                return;
            }

            Parameter.Conditions.Add(new CompareConditionItem
            {
                Name = $"比较项{Parameter.Conditions.Count + 1}",
                CompareType = CompareType.Equal,
                ValueType = CompareValueType.Auto,
                RightValueMode = CompareRightValueMode.Constant,
                ReferenceValue = "0",
                Enabled = true
            });

            Message = "已新增比较项。";
        }

        private void RemoveItem(CompareConditionItem item)
        {
            if (Parameter?.Conditions == null || item == null)
            {
                return;
            }

            Parameter.Conditions.Remove(item);
            Parameter.EnsureDefaultItems();
            Message = "已删除比较项。";
        }

        private void Conditions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CompareConditionItem item in e.OldItems)
                {
                    item.PropertyChanged -= ConditionItem_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (CompareConditionItem item in e.NewItems)
                {
                    item.PropertyChanged -= ConditionItem_PropertyChanged;
                    item.PropertyChanged += ConditionItem_PropertyChanged;
                }
            }

            RefreshInputBindings();
            Message = $"当前比较项数量：{Parameter?.Conditions.Count ?? 0}";
        }

        private void ConditionItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CompareConditionItem.Name) ||
                e.PropertyName == nameof(CompareConditionItem.RightValueMode))
            {
                RefreshInputBindings();
            }
        }

        private void SubscribeConditionItems(IEnumerable<CompareConditionItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                item.PropertyChanged -= ConditionItem_PropertyChanged;
                item.PropertyChanged += ConditionItem_PropertyChanged;
            }
        }

        private void UnsubscribeConditionItems(IEnumerable<CompareConditionItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                item.PropertyChanged -= ConditionItem_PropertyChanged;
            }
        }

        private void RefreshInputBindings()
        {
            Handle?.ContextRunModel?.RefreshInputBindings();
        }
    }
}
