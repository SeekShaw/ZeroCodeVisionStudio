using System;
using System.Collections.Generic;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Logic
{
    /// <summary>
    /// 变量存储入口（已迁移到框架级 <see cref="WorkflowVariableStore"/>，本类仅做转发保持插件内调用不变）。
    /// 管理窗口（工具 → 全局变量）与参数对话框的变量下拉共用同一份框架存储。
    /// </summary>
    public static class LogicStateStore
    {
        public static void Set(string name, string value) =>
            WorkflowVariableStore.Set(name, value, WorkflowVariableScope.Global);
        public static string Get(string name) => WorkflowVariableStore.Get(name);
        public static void ClearAll() => WorkflowVariableStore.ClearAll();
    }

    /// <summary>逻辑算子共用的输入读取辅助：从绑定输入或共享上游输出取布尔值。</summary>
    internal static class LogicInput
    {
        /// <summary>读取布尔输入：优先显式绑定输入，其次共享上游输出；未绑定返回 null。</summary>
        public static bool? ReadBool(HalconVisionStudio.Shared.RunParameter parameter, string key)
        {
            if (parameter == null) return null;

            if (parameter.TryGetInput<object>(key, out var bound) && bound != null)
                return ToBool(bound);

            var shared = parameter.GetOutput<object>(key);
            return shared == null ? null : ToBool(shared);
        }

        /// <summary>读取通用输入（同上顺序），返回原始对象。</summary>
        public static object ReadObject(HalconVisionStudio.Shared.RunParameter parameter, string key)
        {
            if (parameter == null) return null;
            if (parameter.TryGetInput<object>(key, out var bound) && bound != null) return bound;
            return parameter.GetOutput<object>(key);
        }

        public static bool ToBool(object value) => value switch
        {
            bool b => b,
            double d => Math.Abs(d) > 1e-12,
            int i => i != 0,
            long l => l != 0,
            string s => bool.TryParse(s, out var parsed)
                ? parsed
                : s.Trim() == "1" || string.Equals(s.Trim(), "OK", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
