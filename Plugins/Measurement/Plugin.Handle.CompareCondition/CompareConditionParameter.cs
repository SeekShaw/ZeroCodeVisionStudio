using System.Collections.ObjectModel;
using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.CompareCondition
{
    public enum CompareType
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Contains,
        NotContains,
        IsEmpty,
        IsNotEmpty
    }

    public enum LogicOperator
    {
        And,
        Or
    }

    public enum CompareValueType
    {
        Auto,
        Number,
        Text,
        Boolean
    }

    public enum CompareRightValueMode
    {
        Constant,
        Input
    }

    public class CompareConditionItem : BindableBase
    {
        private string bindingKey = Guid.NewGuid().ToString("N");
        private string name = "比较项";
        private CompareType compareType = CompareType.Equal;
        private CompareValueType valueType = CompareValueType.Auto;
        private CompareRightValueMode rightValueMode = CompareRightValueMode.Constant;
        private string referenceValue = "0";
        private bool enabled = true;

        [DisplayName("绑定键")]
        public string BindingKey
        {
            get => bindingKey;
            set => SetProperty(ref bindingKey, value);
        }

        [DisplayName("名称")]
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        [DisplayName("比较类型")]
        public CompareType CompareType
        {
            get => compareType;
            set => SetProperty(ref compareType, value);
        }

        [DisplayName("取值类型")]
        public CompareValueType ValueType
        {
            get => valueType;
            set => SetProperty(ref valueType, value);
        }

        [DisplayName("右值来源")]
        public CompareRightValueMode RightValueMode
        {
            get => rightValueMode;
            set => SetProperty(ref rightValueMode, value);
        }

        [DisplayName("固定右值")]
        public string ReferenceValue
        {
            get => referenceValue;
            set => SetProperty(ref referenceValue, value);
        }

        [DisplayName("启用")]
        public bool Enabled
        {
            get => enabled;
            set => SetProperty(ref enabled, value);
        }

        public CompareConditionItem CloneItem()
        {
            return new CompareConditionItem
            {
                Name = Name,
                BindingKey = BindingKey,
                CompareType = CompareType,
                ValueType = ValueType,
                RightValueMode = RightValueMode,
                ReferenceValue = ReferenceValue,
                Enabled = Enabled
            };
        }
    }

    public class CompareConditionParameter : BindableBase, IHandleParameter
    {
        private LogicOperator logicOperator = LogicOperator.And;
        private bool outputDetailText = true;

        public CompareConditionParameter()
        {
            Conditions = new ObservableCollection<CompareConditionItem>();
            EnsureDefaultItems();
        }

        [DisplayName("逻辑运算")]
        public LogicOperator LogicOperator
        {
            get => logicOperator;
            set => SetProperty(ref logicOperator, value);
        }

        [DisplayName("输出明细")]
        public bool OutputDetailText
        {
            get => outputDetailText;
            set => SetProperty(ref outputDetailText, value);
        }

        public ObservableCollection<CompareConditionItem> Conditions { get; }

        public IHandleParameter Clone()
        {
            var cloned = new CompareConditionParameter
            {
                LogicOperator = LogicOperator,
                OutputDetailText = OutputDetailText
            };

            cloned.Conditions.Clear();
            foreach (var condition in Conditions)
            {
                cloned.Conditions.Add(condition.CloneItem());
            }

            cloned.EnsureDefaultItems();
            return cloned;
        }

        public void Reset()
        {
            LogicOperator = LogicOperator.And;
            OutputDetailText = true;
            Conditions.Clear();
            EnsureDefaultItems();
        }

        public void EnsureDefaultItems()
        {
            EnsureBindingKeys();

            if (Conditions.Count > 0)
            {
                return;
            }

            Conditions.Add(new CompareConditionItem
            {
                Name = "比较项1",
                CompareType = CompareType.Equal,
                ValueType = CompareValueType.Auto,
                RightValueMode = CompareRightValueMode.Constant,
                ReferenceValue = "0",
                Enabled = true
            });
        }

        public void EnsureBindingKeys()
        {
            foreach (var item in Conditions)
            {
                if (item != null && string.IsNullOrWhiteSpace(item.BindingKey))
                {
                    item.BindingKey = Guid.NewGuid().ToString("N");
                }
            }
        }
    }
}
