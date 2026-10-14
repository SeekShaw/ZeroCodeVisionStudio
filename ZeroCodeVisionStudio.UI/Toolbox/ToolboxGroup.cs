using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.UI.Controls
{
    /// <summary>
    /// 工具箱中的一个算子分组。
    /// 分组自身负责展开/选中状态通知，实际算子列表保存在 Handles 中。
    /// </summary>
    public class ToolboxGroup : INotifyPropertyChanged
    {
        private bool isExpanded;
        private bool isSelected;

        public ToolboxGroup(string handleType, string displayName, Geometry iconData, int sortIndex)
        {
            HandleType = handleType;
            DisplayName = displayName;
            IconData = iconData;
            SortIndex = sortIndex;
            Handles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(Count));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string HandleType { get; }

        public string DisplayName { get; }

        /// <summary>分组图标的几何路径数据，直接绑定到 XAML Path。</summary>
        public Geometry IconData { get; }

        /// <summary>分组排序值，数字越小越靠前。</summary>
        public int SortIndex { get; }

        /// <summary>该分组下实际可添加到流程中的算子。</summary>
        public ObservableCollection<IHandleRegisterModel> Handles { get; } = new ObservableCollection<IHandleRegisterModel>();

        /// <summary>算子计数（分组标题右侧徽标）。</summary>
        public int Count => Handles.Count;

        /// <summary>展开模式下是否显示该分组的算子列表。</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded == value)
                {
                    return;
                }

                isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Count));
            }
        }

        /// <summary>当前分组是否被选中，用于窄栏和浮层标题高亮。</summary>
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

