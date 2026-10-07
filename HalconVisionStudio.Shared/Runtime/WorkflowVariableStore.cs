using System;
using System.Collections.Generic;

namespace HalconVisionStudio.Shared
{
    /// <summary>变量作用域：全局（所有流程可用）或流程级（仅所属流程可用）。</summary>
    public enum WorkflowVariableScope
    {
        /// <summary>全局：任何流程、任何节点、参数对话框与绑定都可读写。</summary>
        Global = 0,

        /// <summary>流程级：仅所属流程内可读写（同名时优先于全局被读取）。</summary>
        Workflow = 1
    }

    /// <summary>
    /// 全局流程变量存储（框架级，分作用域）：
    /// <para>
    /// - <see cref="WorkflowVariableScope.Global"/> 全局级：所有流程共享；
    /// - <see cref="WorkflowVariableScope.Workflow"/> 流程级：仅所属流程可用，其它流程不可见；
    /// - 读取解析顺序：先流程级（按当前流程名）再全局——同名变量流程级优先；
    /// - 「变量写入/读取」算子、「全局变量」管理窗（工具菜单）、参数对话框的变量下拉、
    ///   节点绑定（var: 源）共用本存储。生命周期为应用会话，重启清空。
    /// </para>
    /// </summary>
    public static class WorkflowVariableStore
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<string, string> GlobalValues = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, string>> WorkflowValues = new(StringComparer.Ordinal);

        /// <summary>变量变更事件（写入/删除/清空后触发；后台线程可触发，UI 需自行调度）。</summary>
        public static event Action Changed;

        /// <summary>
        /// 当前流程名（流程级变量的归属与解析依据）。
        /// 由主程序在流程切换与执行开始时更新；为空时流程级读写按全局处理。
        /// </summary>
        public static string CurrentWorkflowName { get; set; } = "";

        /// <summary>写入/更新变量（值为 null 时移除该作用域下的变量）。</summary>
        public static void Set(string name, string value, WorkflowVariableScope scope = WorkflowVariableScope.Global, string workflowName = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (Gate)
            {
                var dict = GetDict(scope, workflowName);
                if (value == null) dict.Remove(name);
                else dict[name] = value;
            }
            TryFireChanged();
        }

        /// <summary>
        /// 解析式读取：先流程级（当前/指定流程），再全局；均不存在返回 null。
        /// </summary>
        public static string Get(string name, string workflowName = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var owner = ResolveWorkflow(workflowName);
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(owner) &&
                    WorkflowValues.TryGetValue(owner, out var wfDict) &&
                    wfDict.TryGetValue(name, out var wfValue))
                {
                    return wfValue;
                }
                return GlobalValues.TryGetValue(name, out var value) ? value : null;
            }
        }

        /// <summary>读取指定作用域的变量（不做解析回退；不存在返回 null）。</summary>
        public static string GetScoped(string name, WorkflowVariableScope scope, string workflowName = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            lock (Gate)
            {
                var dict = GetDict(scope, workflowName);
                return dict.TryGetValue(name, out var value) ? value : null;
            }
        }

        /// <summary>指定作用域下变量是否存在。</summary>
        public static bool Contains(string name, WorkflowVariableScope scope, string workflowName = null)
        {
            lock (Gate)
            {
                return GetDict(scope, workflowName).ContainsKey(name ?? "");
            }
        }

        /// <summary>全局级变量快照（按名称排序）。</summary>
        public static SortedDictionary<string, string> GetAllGlobal()
        {
            lock (Gate) return new SortedDictionary<string, string>(GlobalValues, StringComparer.Ordinal);
        }

        /// <summary>指定流程的流程级变量快照（按名称排序）。</summary>
        public static SortedDictionary<string, string> GetAllWorkflow(string workflowName = null)
        {
            var owner = ResolveWorkflow(workflowName);
            lock (Gate)
            {
                return WorkflowValues.TryGetValue(owner ?? "", out var dict)
                    ? new SortedDictionary<string, string>(dict, StringComparer.Ordinal)
                    : new SortedDictionary<string, string>(StringComparer.Ordinal);
            }
        }

        /// <summary>删除指定作用域的变量。</summary>
        public static void Remove(string name, WorkflowVariableScope scope, string workflowName = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (Gate)
            {
                GetDict(scope, workflowName).Remove(name);
            }
            TryFireChanged();
        }

        /// <summary>清空指定作用域全部变量（默认清全局）。</summary>
        public static void ClearAll(WorkflowVariableScope scope = WorkflowVariableScope.Global, string workflowName = null)
        {
            lock (Gate)
            {
                if (scope == WorkflowVariableScope.Global) GlobalValues.Clear();
                else WorkflowValues.Remove(ResolveWorkflow(workflowName) ?? "");
            }
            TryFireChanged();
        }

        /// <summary>变量名候选（变量名下拉用）：全局级 + 当前流程的流程级，带作用域前缀显示。</summary>
        public static List<(string Display, string Name, WorkflowVariableScope Scope)> GetCandidates(string workflowName = null)
        {
            var result = new List<(string, string, WorkflowVariableScope)>();
            foreach (var kv in GetAllGlobal())
            {
                result.Add(($"[全局] {kv.Key}", kv.Key, WorkflowVariableScope.Global));
            }
            foreach (var kv in GetAllWorkflow(workflowName))
            {
                result.Add(($"[流程] {kv.Key}", kv.Key, WorkflowVariableScope.Workflow));
            }
            return result;
        }

        private static Dictionary<string, string> GetDict(WorkflowVariableScope scope, string workflowName)
        {
            if (scope == WorkflowVariableScope.Global) return GlobalValues;
            var owner = ResolveWorkflow(workflowName) ?? "";
            if (!WorkflowValues.TryGetValue(owner, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.Ordinal);
                WorkflowValues[owner] = dict;
            }
            return dict;
        }

        private static string ResolveWorkflow(string workflowName) =>
            string.IsNullOrWhiteSpace(workflowName) ? CurrentWorkflowName?.Trim() : workflowName.Trim();

        private static void TryFireChanged()
        {
            try { Changed?.Invoke(); } catch { }
        }
    }
}
