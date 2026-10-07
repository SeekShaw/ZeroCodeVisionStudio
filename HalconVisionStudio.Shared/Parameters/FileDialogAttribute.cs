using System;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 标记参数属性需要使用文件对话框进行编辑。
    /// 应用于字符串类型的属性，会在参数编辑对话框中显示浏览按钮。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FileDialogAttribute : Attribute
    {
        /// <summary>
        /// 初始化文件对话框特性。
        /// </summary>
        /// <param name="dialogType">对话框类型：打开或保存。</param>
        /// <param name="filter">文件过滤器，例如 "Shape Model (*.shm)|*.shm|All files (*.*)|*.*"。</param>
        /// <param name="defaultExtension">默认扩展名，例如 ".shm"。</param>
        public FileDialogAttribute(FileDialogType dialogType = FileDialogType.Open, string filter = "All files (*.*)|*.*", string defaultExtension = null)
        {
            DialogType = dialogType;
            Filter = filter;
            DefaultExtension = defaultExtension;
        }

        /// <summary>对话框类型：打开或保存。</summary>
        public FileDialogType DialogType { get; }

        /// <summary>文件过滤器。</summary>
        public string Filter { get; }

        /// <summary>默认扩展名。</summary>
        public string DefaultExtension { get; }
    }

    /// <summary>
    /// 文件对话框类型枚举。
    /// </summary>
    public enum FileDialogType
    {
        /// <summary>打开文件对话框。</summary>
        Open,

        /// <summary>保存文件对话框。</summary>
        Save
    }
}
