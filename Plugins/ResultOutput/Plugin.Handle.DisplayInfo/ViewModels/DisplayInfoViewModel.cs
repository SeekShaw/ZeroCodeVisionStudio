using System.Collections.Specialized;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.DisplayInfo.ViewModels
{
    /// <summary>
    /// 显示信息算子参数窗口。
    /// 支持动态新增和删除显示项，并在每次变更后刷新节点可绑定输入槽。
    /// </summary>
    public class DisplayInfoViewModel : DefectDialogViewModel<DisplayInfoHandle, DisplayInfoParameter>
    {
        public DisplayInfoViewModel()
        {
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand<DisplayInfoItemParameter>(RemoveItem);
        }

        public DelegateCommand AddItemCommand { get; }

        public DelegateCommand<DisplayInfoItemParameter> RemoveItemCommand { get; }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (Parameter?.Items != null)
            {
                Parameter.Items.CollectionChanged -= Items_CollectionChanged;
                Parameter.Items.CollectionChanged += Items_CollectionChanged;
            }

            Message = "可在节点输入来源中绑定显示内容；新增显示项后，也可分别为每个显示项选择上游输出。";
        }

        public override void OnDialogClosed()
        {
            if (Parameter?.Items != null)
            {
                Parameter.Items.CollectionChanged -= Items_CollectionChanged;
            }
        }

        protected override void OnExecute()
        {
            try
            {
                base.OnExecute();
                Message = "执行完成，右侧显示窗口已刷新。";
            }
            catch (Exception ex)
            {
                Message = ex.Message;
            }
        }

        protected override void OnReset()
        {
            base.OnReset();
            Message = "参数已重置。";
        }

        private void AddItem()
        {
            Parameter?.Items.Add(new DisplayInfoItemParameter
            {
                Title = $"结果{Parameter.Items.Count + 1}"
            });

            Message = "已新增显示项。";
        }

        private void RemoveItem(DisplayInfoItemParameter item)
        {
            if (Parameter?.Items == null || item == null)
            {
                return;
            }

            Parameter.Items.Remove(item);
            Message = "已删除显示项。";
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Message = $"当前显示项数量：{Parameter?.Items.Count ?? 0}";
        }
    }
}
