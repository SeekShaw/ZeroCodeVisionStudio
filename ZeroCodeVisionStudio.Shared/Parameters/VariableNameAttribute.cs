using System;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 标记字符串参数属性为"全局流程变量名"编辑器：
    /// 参数对话框中渲染为可编辑下拉框——候选来自当前全部变量（<see cref="WorkflowVariableStore"/>），
    /// 也允许直接输入新变量名。用于「变量写入/读取」「延时等待」等算子的变量名参数，
    /// 避免手打名字拼写错误与记忆负担。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class VariableNameAttribute : Attribute
    {
    }
}
