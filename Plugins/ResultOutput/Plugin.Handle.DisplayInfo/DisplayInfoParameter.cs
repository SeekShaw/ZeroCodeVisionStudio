using System.Collections.ObjectModel;
using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.DisplayInfo
{
    /// <summary>
    /// 单个动态显示项。
    /// 每个显示项既可以绑定上游节点输出，也可以单独填写固定文本前缀和空值提示。
    /// </summary>
    public class DisplayInfoItemParameter : BindableBase
    {
        private string title = "显示项";
        private string prefixText = string.Empty;
        private bool includeLabel = true;
        private string emptyText = "-";

        [DisplayName("显示项标题")]
        public string Title
        {
            get => title;
            set => SetProperty(ref title, value);
        }

        [DisplayName("前置文本")]
        public string PrefixText
        {
            get => prefixText;
            set => SetProperty(ref prefixText, value);
        }

        [DisplayName("显示标题")]
        public bool IncludeLabel
        {
            get => includeLabel;
            set => SetProperty(ref includeLabel, value);
        }

        [DisplayName("空值文本")]
        public string EmptyText
        {
            get => emptyText;
            set => SetProperty(ref emptyText, value);
        }

        public DisplayInfoItemParameter CloneItem()
        {
            return new DisplayInfoItemParameter
            {
                Title = Title,
                PrefixText = PrefixText,
                IncludeLabel = IncludeLabel,
                EmptyText = EmptyText
            };
        }
    }

    /// <summary>
    /// 显示信息算子的参数。
    /// 支持任意数量的动态显示项，每一项都可以绑定不同节点的输出值。
    /// </summary>
    public class DisplayInfoParameter : BindableBase, IHandleParameter
    {
        private string headerText = "检测信息";
        private string freeText = string.Empty;
        private string textColor = "#F8FAFC";
        private string backgroundColor = "#B30F172A";

        public DisplayInfoParameter()
        {
            Items = new ObservableCollection<DisplayInfoItemParameter>();
            EnsureDefaultItems();
        }

        [DisplayName("标题文本")]
        public string HeaderText
        {
            get => headerText;
            set => SetProperty(ref headerText, value);
        }

        [DisplayName("附加文本")]
        public string FreeText
        {
            get => freeText;
            set => SetProperty(ref freeText, value);
        }

        [DisplayName("文字颜色")]
        public string TextColor
        {
            get => textColor;
            set => SetProperty(ref textColor, value);
        }

        [DisplayName("背景颜色")]
        public string BackgroundColor
        {
            get => backgroundColor;
            set => SetProperty(ref backgroundColor, value);
        }

        public ObservableCollection<DisplayInfoItemParameter> Items { get; }

        public IHandleParameter Clone()
        {
            var clone = new DisplayInfoParameter
            {
                HeaderText = HeaderText,
                FreeText = FreeText,
                TextColor = TextColor,
                BackgroundColor = BackgroundColor
            };

            clone.Items.Clear();
            foreach (var item in Items)
            {
                clone.Items.Add(item.CloneItem());
            }

            clone.EnsureDefaultItems();
            return clone;
        }

        public void Reset()
        {
            HeaderText = "检测信息";
            FreeText = string.Empty;
            TextColor = "#F8FAFC";
            BackgroundColor = "#B30F172A";
            Items.Clear();
            EnsureDefaultItems();
        }

        public void EnsureDefaultItems()
        {
            if (Items.Count > 0)
            {
                return;
            }

            Items.Add(new DisplayInfoItemParameter { Title = "结果1" });
            Items.Add(new DisplayInfoItemParameter { Title = "结果2" });
        }
    }
}
