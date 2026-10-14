using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Prism.Mvvm;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 算子参数对话框 ViewModel 基类。
    /// 提供所有参数编辑对话框的公共命令绑定：执行（预览）和重置。
    /// 子类通过重写 <see cref="OnExecute"/> 和 <see cref="OnReset"/> 实现具体逻辑。
    /// </summary>
    public abstract class HandleDialogBase : BindableBase, IDialogAware
    {
        /// <summary>
        /// 初始化对话框基类，创建执行和重置命令。
        /// </summary>
        protected HandleDialogBase()
        {
            ExecuteCommand = new DelegateCommand(OnExecute);
            ResetCommand = new DelegateCommand(OnReset);
        }

        /// <summary>
        /// 重置参数到默认值（子类实现）。
        /// 通常绑定到对话框的"重置"按钮。
        /// </summary>
        protected abstract void OnReset();

        /// <summary>
        /// 执行算子并预览结果（子类实现）。
        /// 通常绑定到对话框的"执行"或"预览"按钮。
        /// </summary>
        protected abstract void OnExecute();

        /// <summary>
        /// 判断对话框是否可以关闭，默认始终允许。
        /// 子类可重写以实现关闭前的校验逻辑。
        /// </summary>
        public virtual bool CanCloseDialog()
        {
            return true;
        }

        /// <summary>
        /// 对话框关闭时的回调，子类可重写以执行清理操作。
        /// </summary>
        public virtual void OnDialogClosed()
        {
        }

        /// <summary>
        /// 对话框打开时的回调，子类可重写以从参数中提取初始数据。
        /// </summary>
        /// <param name="parameters">由调用方通过 IDialogService.ShowDialog 传入的参数集合。</param>
        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
        }

        /// <summary>执行/预览命令，对应"执行"按钮的点击操作。</summary>
        public ICommand ExecuteCommand { get; private set; }

        /// <summary>重置命令，对应"重置"按钮的点击操作。</summary>
        public ICommand ResetCommand { get; private set; }

        /// <summary>Prism 对话框关闭请求监听器，用于主动关闭对话框。</summary>
        public DialogCloseListener RequestClose { get; set; }
    }
}
