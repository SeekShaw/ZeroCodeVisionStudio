using System.Collections.ObjectModel;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Helper;
using ZeroCodeVisionStudio.UI.Controls;

namespace ZeroCodeVisionStudio
{
    /// <summary>
    /// 单个工作流的 ViewModel，封装该流程内的节点、连线、执行记录和输入绑定。
    /// MainViewModel 将多个 WorkflowViewModel 聚合在一起，每个流程独立运行。
    /// </summary>
    public class WorkflowViewModel : BindableBase
    {
        private readonly SequenceNameGenerator seqGenerator;
        private int executionSequence;
        private IHandleRunModel selectedNode;
        private WorkflowConnection selectedConnection;
        private DisplaySourceItem selectedDisplaySource;
        private string workflowName;

        public WorkflowViewModel(string name, SequenceNameGenerator seqGen)
        {
            workflowName = name ?? "主流程";
            seqGenerator = seqGen;
            Nodes = new ObservableCollection<IHandleRunModel>();
            Connections = new ObservableCollection<WorkflowConnection>();
            ExecutionRecords = new ObservableCollection<WorkflowExecutionRecord>();
            DisplaySources = new ObservableCollection<DisplaySourceItem>();
            SelectedInputBindings = new ObservableCollection<WorkflowInputBindingEditorItem>();
        }

        /// <summary>流程名称。</summary>
        public string WorkflowName
        {
            get => workflowName;
            set => SetProperty(ref workflowName, value);
        }

        private bool parallelExecution;

        /// <summary>
        /// 并行执行：同一拓扑层的已就绪节点（兄弟分支）并发执行，层间仍按拓扑顺序串行等待。
        /// 默认关闭（保持串行执行的确定顺序）。开启前注意兄弟分支不应写同一驱动/变量。
        /// </summary>
        public bool ParallelExecution
        {
            get => parallelExecution;
            set => SetProperty(ref parallelExecution, value);
        }

        /// <summary>该流程中所有运行节点的有序集合。</summary>
        public ObservableCollection<IHandleRunModel> Nodes { get; }

        /// <summary>该流程中所有连线的集合，用于执行排序和绘制画布。</summary>
        public ObservableCollection<WorkflowConnection> Connections { get; }

        /// <summary>右侧"模块结果"表格的数据源，记录每次执行的输出。</summary>
        public ObservableCollection<WorkflowExecutionRecord> ExecutionRecords { get; }

        /// <summary>该流程的显示源列表，支持叠加和排序。</summary>
        public ObservableCollection<DisplaySourceItem> DisplaySources { get; }

        /// <summary>当前选中节点的输入来源编辑项，供绑定配置面板使用。</summary>
        public ObservableCollection<WorkflowInputBindingEditorItem> SelectedInputBindings { get; }

        /// <summary>当前选中的节点。</summary>
        public IHandleRunModel SelectedNode
        {
            get => selectedNode;
            set
            {
                if (SetProperty(ref selectedNode, value))
                {
                    if (selectedNode != null)
                    {
                        selectedNode.IsSelected = true;
                    }
                    else
                    {
                        foreach (var node in Nodes)
                        {
                            node.IsSelected = false;
                        }
                    }
                }
            }
        }

        /// <summary>当前选中的连线。</summary>
        public WorkflowConnection SelectedConnection
        {
            get => selectedConnection;
            set
            {
                var old = selectedConnection;
                if (SetProperty(ref selectedConnection, value))
                {
                    if (old != null)
                    {
                        old.IsSelected = false;
                    }
                    if (selectedConnection != null)
                    {
                        selectedConnection.IsSelected = true;
                    }
                }
            }
        }

        /// <summary>当前选中的显示源。</summary>
        public DisplaySourceItem SelectedDisplaySource
        {
            get => selectedDisplaySource;
            set => SetProperty(ref selectedDisplaySource, value);
        }

        /// <summary>获取并递增执行序号，用于给节点运行记录排序。</summary>
        public int NextSequence() => ++executionSequence;

        /// <summary>根据基础名称生成带序号的显示名称。</summary>
        public string GenerateDisplayName(string baseName) => seqGenerator.Generate(baseName);

        /// <summary>
        /// 为新增节点计算下一个默认位置，按列排列以避免节点重叠。
        /// </summary>
        public (double X, double Y) GetNextNodePosition()
        {
            const double defaultNodeX = 80;
            const double defaultNodeY = 80;
            const double nodeGapX = WorkflowConnection.NodeWidth + 90;
            const double nodeGapY = WorkflowConnection.NodeHeight + 78;
            const int nodesPerColumn = 5;

            var index = Nodes.Count;
            return (
                defaultNodeX + index / nodesPerColumn * nodeGapX,
                defaultNodeY + index % nodesPerColumn * nodeGapY);
        }

        /// <summary>刷新所有连线的折线路径（节点移动后调用）。</summary>
        public void RefreshConnections()
        {
            foreach (var connection in Connections)
            {
                connection.Refresh();
            }
        }

        /// <summary>
        /// 清空该流程的图像显示源列表，同时重置当前选中的显示源。
        /// 通常在流程重新执行前调用，以清除上一轮的旧结果图像。
        /// </summary>
        public void ResetDisplaySources()
        {
            DisplaySources.Clear();
            SelectedDisplaySource = null;
        }
    }
}

