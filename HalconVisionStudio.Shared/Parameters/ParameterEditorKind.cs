namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 参数属性编辑器类型枚举。
    /// 通用参数对话框根据此枚举选择为每个参数属性呈现哪种 UI 控件。
    /// </summary>
    public enum ParameterEditorKind
    {
        /// <summary>文本输入框，适用于字符串、数值（int、float、double）类型的属性。</summary>
        Text,

        /// <summary>滑条 + 数值输入框，适用于带 Range 约束的数值属性。</summary>
        Slider,

        /// <summary>复选框，适用于 bool 类型的属性。</summary>
        Boolean,

        /// <summary>下拉列表，适用于枚举类型的属性。</summary>
        Enum,

        /// <summary>文件打开对话框，适用于标记了 FileDialogAttribute(Open) 的字符串属性。</summary>
        FileOpen,

        /// <summary>文件保存对话框，适用于标记了 FileDialogAttribute(Save) 的字符串属性。</summary>
        FileSave,

        /// <summary>通讯驱动下拉框，适用于标记了 DriverOptionsAttribute 的字符串属性。</summary>
        DriverSelect,

        /// <summary>全局流程变量名下拉（可编辑），适用于标记了 VariableNameAttribute 的字符串属性。</summary>
        VariableName
    }
}
