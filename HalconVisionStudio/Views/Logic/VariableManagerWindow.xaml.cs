using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HalconVisionStudio.Shared;

namespace HalconVisionStudio.Views.Logic
{
    /// <summary>
    /// 全局流程变量管理窗口：实时查看/写入/编辑/删除/清空 WorkflowVariableStore。
    /// 「变量写入/读取」算子、流程执行与本窗口操作的是同一份存储。
    /// </summary>
    public partial class VariableManagerWindow
    {
        /// <summary>列表行模型。</summary>
        public sealed class VariableRow
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Scope { get; set; }
            public WorkflowVariableScope ScopeKind { get; set; }
        }

        private readonly List<VariableRow> rows = new();
        private readonly DispatcherTimer refreshTimer;

        public VariableManagerWindow()
        {
            InitializeComponent();
            VarGrid.ItemsSource = rows;

            // 实时刷新：连续运行时可观察变量变化（仅刷新，不抢焦点）。
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            refreshTimer.Tick += (_, __) => Refresh(false);
            refreshTimer.Start();

            Refresh(true);
        }

        protected override void OnClosed(EventArgs e)
        {
            refreshTimer.Stop();
            base.OnClosed(e);
        }

        /// <summary>从存储重建列表（保留当前选中名；全局级与当前流程的流程级都显示）。</summary>
        private void Refresh(bool showCount)
        {
            if (!IsLoaded) return;

            var selected = (VarGrid.SelectedItem as VariableRow);
            var globals = WorkflowVariableStore.GetAllGlobal();
            var workflows = WorkflowVariableStore.GetAllWorkflow();

            var changed = rows.Count != globals.Count + workflows.Count;
            if (!changed)
            {
                foreach (var r in rows)
                {
                    var dict = r.ScopeKind == WorkflowVariableScope.Global ? globals : workflows;
                    if (!dict.TryGetValue(r.Name, out var v) || v != r.Value) { changed = true; break; }
                }
            }
            if (!changed) return;

            rows.Clear();
            foreach (var kv in globals)
            {
                rows.Add(new VariableRow { Name = kv.Key, Value = kv.Value, Scope = "全局", ScopeKind = WorkflowVariableScope.Global });
            }
            foreach (var kv in workflows)
            {
                rows.Add(new VariableRow { Name = kv.Key, Value = kv.Value, Scope = "流程级", ScopeKind = WorkflowVariableScope.Workflow });
            }
            VarGrid.Items.Refresh();

            if (selected != null)
            {
                VarGrid.SelectedItem = rows.FirstOrDefault(r => r.Name == selected.Name && r.ScopeKind == selected.ScopeKind);
            }

            if (showCount)
            {
                StatusText.Text = $"共 {rows.Count} 个变量（全局 {globals.Count} · 流程级 {workflows.Count}）";
            }
        }

        private void OnWriteVariable(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text?.Trim();
            var value = ValueBox.Text;
            if (string.IsNullOrEmpty(name))
            {
                StatusText.Text = "✗ 请填写变量名";
                return;
            }

            var scope = ScopeBox.SelectedIndex == 1 ? WorkflowVariableScope.Workflow : WorkflowVariableScope.Global;
            WorkflowVariableStore.Set(name, value, scope);
            StatusText.Text = $"✓ 已写入 [{(scope == WorkflowVariableScope.Global ? "全局" : "流程级")}] {name} = {value}";
            NameBox.Text = "";
            ValueBox.Text = "";
            Refresh(true);
        }

        private void OnEditVariable(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VariableRow row)
            {
                NameBox.Text = row.Name;
                ValueBox.Text = row.Value ?? "";
                ScopeBox.SelectedIndex = row.ScopeKind == WorkflowVariableScope.Workflow ? 1 : 0;
                StatusText.Text = $"已载入 [{row.Scope}] {row.Name}，修改后点击「写入」保存";
                NameBox.Focus();
            }
        }

        private void OnDeleteVariable(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is VariableRow row)
            {
                WorkflowVariableStore.Remove(row.Name, row.ScopeKind);
                StatusText.Text = $"✓ 已删除 [{row.Scope}] {row.Name}";
                Refresh(true);
            }
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            if (rows.Count == 0)
            {
                StatusText.Text = "当前没有变量";
                return;
            }

            if (MessageBox.Show(this, $"确定清空全部 {rows.Count} 个变量（全局与流程级）？\n运行中的流程读取将得到缺省值。",
                    "清空确认", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }

            WorkflowVariableStore.ClearAll(WorkflowVariableScope.Global);
            WorkflowVariableStore.ClearAll(WorkflowVariableScope.Workflow);
            StatusText.Text = "✓ 已清空全部变量";
            Refresh(true);
        }
    }
}
