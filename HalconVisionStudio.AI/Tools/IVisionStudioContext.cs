using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconVisionStudio.Shared;

namespace HalconVisionStudio.AI.Tools
{
    /// <summary>
    /// 宿主环境上下文接口，桥接主界面 ViewModel 与 AI 自动化服务，
    /// 消除 HalconVisionStudio.AI 对主界面的直接依赖。
    /// </summary>
    public interface IVisionStudioContext
    {
        Task RunOnUiAsync(Action action);
        Task<T> RunOnUiAsync<T>(Func<T> func);

        string SchemeTitle { get; }
        string ActiveWorkflowName { get; }

        /// <summary>当前活动流程是否启用并行执行（同层兄弟分支并发，层间串行）。</summary>
        bool ActiveWorkflowParallelExecution { get; }

        /// <summary>设置流程并行执行开关（UI 线程）。workflowName 为空=当前活动流程。</summary>
        bool SetWorkflowParallelExecution(bool enabled, string workflowName);

        /// <summary>新建空白方案（UI 线程，丢弃未保存修改）。</summary>
        void NewScheme();
        IReadOnlyList<string> WorkflowNames { get; }

        IReadOnlyList<IHandleRunModel> GetActiveNodes();
        IReadOnlyList<(string FromNode, string ToNode)> GetActiveConnections();
        IReadOnlyList<(int Seq, DateTime Time, string Module, string Status, TimeSpan Elapsed, string Result)> GetExecutionRecords();
        IReadOnlyList<IHandleRegisterModel> GetRegisteredOperators();

        IHandleRunModel AddNode(string operatorNameOrKey, double? x = null, double? y = null);
        bool RemoveNode(string nodeName);

        /// <summary>
        /// 建立节点间连线。fromPin/toPin 为可选的数据槽键名（如 Region/Contour/Output），
        /// 指定时按槽位精确绑定，留空时按类型自动匹配。
        /// </summary>
        bool ConnectNodes(string fromNode, string toNode, string fromPin = null, string toPin = null);
        bool DisconnectNodes(string fromNode, string toNode);
        bool RenameNode(string oldName, string newName);
        bool MoveNode(string nodeName, double x, double y);

        /// <summary>触发工作流执行并等待其真正完成，返回执行是否成功（而非仅"已触发"）。</summary>
        Task<bool> ExecuteWorkflowAsync();
        /// <summary>单步执行指定节点并等待完成，返回该节点执行是否成功。</summary>
        Task<bool> ExecuteSingleNodeAsync(string nodeName);
        /// <summary>请求停止当前正在执行的工作流/连续运行。</summary>
        bool StopExecution();

        bool CreateWorkflow(string workflowName);
        bool SwitchWorkflow(string workflowName);
        bool RemoveWorkflowByName(string workflowName);
        bool RenameWorkflow(string oldName, string newName);

        /// <summary>读取指定节点全部输入绑定的当前状态（数据槽 + 参数引用）。</summary>
        System.Collections.Generic.List<WorkflowInputBinding> GetNodeInputBindings(string nodeName);

        /// <summary>设置指定节点的一个输入绑定；sourceNode 为空表示清除该绑定。</summary>
        bool SetNodeInputBinding(string nodeName, string targetInputKey, string sourceNode, string sourceOutputKey);

        /// <summary>复制节点（含全部参数），返回新节点。</summary>
        IHandleRunModel DuplicateNode(string nodeName, double offsetX = 40, double offsetY = 40);

        /// <summary>清空当前活动流程的全部节点与连线（不可撤销，供 AI 批量重建流程前使用）。</summary>
        bool ClearWorkflow();

        /// <summary>将主视口切换为显示指定节点的输出。</summary>
        bool ShowNodeOutput(string nodeName);

        /// <summary>保存当前方案。path 为空时保存到当前路径（无路径时存入默认方案目录）。</summary>
        Task<string> SaveSchemeAsync(string path = null);
        /// <summary>打开并加载指定方案文件，返回加载报告（缺失算子/失败参数等）。</summary>
        Task<string> OpenSchemeAsync(string path);
        /// <summary>列出默认方案目录中的所有方案文件。</summary>
        IReadOnlyList<string> ListSchemes();

        Task<string> CaptureViewportImageAsync(string targetPath = null);
        Dictionary<string, object> GetNodeRuntimeOutputs(string nodeName);
        bool OpenNodeDialog(string nodeName);
        bool AutoLayoutNodes();
        /// <summary>图像灰度统计。nodeName 为空时分析当前视口；否则分析指定节点的主输出图像。</summary>
        (double Mean, double Dev, double Min, double Max, List<int> Percentiles) GetImageStats(string nodeName = null);
        void SelectNode(string nodeName);

        /// <summary>撤销上一步画布操作（增删节点/连线/参数），无可撤销操作返回 false。</summary>
        bool Undo();

        /// <summary>重做被撤销的操作，无可重做操作返回 false。</summary>
        bool Redo();

        /// <summary>启动或停止连续运行循环（循环执行当前流程）。返回是否发生了状态切换。</summary>
        bool SetContinuousRun(bool enabled);
    }
}
