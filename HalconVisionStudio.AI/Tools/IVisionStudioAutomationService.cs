using System.Collections.Generic;
using System.Threading.Tasks;

namespace HalconVisionStudio.AI.Tools
{
    /// <summary>
    /// 统一的机器视觉平台自动化服务接口。
    /// 无论来自内置 Copilot 还是外部 MCP Server，均通过此接口操控系统。
    /// </summary>
    public interface IVisionStudioAutomationService
    {
        /// <summary>获取当前活动工作流的完整节点图、连线及运行状态。</summary>
        Task<WorkflowStateDto> GetWorkflowStateAsync();

        /// <summary>获取算子目录。filter 按名称/显示名/分类子串过滤；includeDetail 附带说明与引脚（默认紧凑模式控制输出体积）。</summary>
        Task<List<OperatorCatalogItemDto>> GetOperatorCatalogAsync(string filter = null, bool includeDetail = false);

        /// <summary>在当前工作流中添加指定算子节点。</summary>
        Task<OperationResultDto> AddNodeAsync(string operatorName, double? x = null, double? y = null);

        /// <summary>从当前工作流中移除指定节点。</summary>
        Task<OperationResultDto> RemoveNodeAsync(string nodeName);

        /// <summary>在两个节点之间建立数据流连线（支持引脚级绑定）。</summary>
        Task<OperationResultDto> ConnectNodesAsync(string fromNode, string fromPin, string toNode, string toPin);

        /// <summary>断开两个节点之间的连线。</summary>
        Task<OperationResultDto> DisconnectNodesAsync(string fromNode, string toNode);

        /// <summary>重命名节点（解决同名节点歧义，便于后续工具可靠寻址）。</summary>
        Task<OperationResultDto> RenameNodeAsync(string nodeName, string newName);

        /// <summary>移动节点到指定画布坐标。</summary>
        Task<OperationResultDto> MoveNodeAsync(string nodeName, double x, double y);

        /// <summary>读取指定节点的参数配置。</summary>
        Task<List<ParameterDto>> GetNodeParametersAsync(string nodeName);

        /// <summary>批量修改指定节点的参数。</summary>
        Task<OperationResultDto> SetNodeParametersAsync(string nodeName, Dictionary<string, object> parameters);

        /// <summary>同步执行工作流（等待真正完成），返回各节点执行状态汇总。</summary>
        Task<OperationResultDto> ExecuteWorkflowAsync(string mode = "all");

        /// <summary>请求停止当前正在执行的工作流或连续运行循环。</summary>
        Task<OperationResultDto> StopExecutionAsync();

        /// <summary>获取最近的执行历史记录。</summary>
        Task<List<ExecutionRecordDto>> GetExecutionRecordsAsync(int maxCount = 20);

        /// <summary>读取指定节点全部输入绑定的当前状态。</summary>
        Task<List<InputBindingDto>> GetNodeInputBindingsAsync(string nodeName);

        /// <summary>设置指定节点的一个输入绑定（targetInputKey 支持 parameter:参数名 的参数引用）；sourceNode 为空表示清除。</summary>
        Task<OperationResultDto> SetNodeInputBindingAsync(string nodeName, string targetInputKey, string sourceNode, string sourceOutputKey);

        /// <summary>复制节点（含全部参数）。</summary>
        Task<OperationResultDto> DuplicateNodeAsync(string nodeName, double offsetX = 40, double offsetY = 40);

        /// <summary>清空当前活动流程（不可撤销）。</summary>
        Task<OperationResultDto> ClearWorkflowAsync();

        /// <summary>删除指定名称的流程 Tab。</summary>
        Task<OperationResultDto> RemoveWorkflowAsync(string workflowName);

        /// <summary>重命名流程 Tab。</summary>
        Task<OperationResultDto> RenameWorkflowAsync(string oldName, string newName);

        /// <summary>将主视口切换为显示指定节点的输出。</summary>
        Task<OperationResultDto> ShowNodeOutputAsync(string nodeName);

        /// <summary>获取指定节点的故障快照（用于 AI 深度排查诊断）。</summary>
        Task<FaultSnapshotDto> GetFaultSnapshotAsync(string nodeName = null);

        /// <summary>截取当前视口图像并保存到本地临时文件。</summary>
        Task<string> CaptureViewportImageAsync(string targetPath = null);

        /// <summary>新建工作流 Tab。</summary>
        Task<OperationResultDto> CreateWorkflowAsync(string workflowName);

        /// <summary>切换当前活动的工作流 Tab。</summary>
        Task<OperationResultDto> SwitchWorkflowAsync(string workflowName);

        /// <summary>保存当前方案到文件。path 为空时保存到当前路径或默认目录。</summary>
        Task<OperationResultDto> SaveSchemeAsync(string path = null);

        /// <summary>打开并加载指定方案文件，返回加载报告（缺失算子、恢复失败的参数等）。</summary>
        Task<OperationResultDto> OpenSchemeAsync(string path);

        /// <summary>列出默认方案目录中所有已保存的方案文件。</summary>
        Task<List<SchemeFileInfoDto>> ListSchemesAsync();

        /// <summary>深度透视算子规格定义：包含输入引脚、输出引脚、全部参数名称、数据类型、默认值与说明。</summary>
        Task<OperatorSchemaDto> GetOperatorSchemaAsync(string operatorKey);

        /// <summary>获取指定算子节点运行后产生的物理数据与输出结果（如测量尺寸、得分、区域面积等）。</summary>
        Task<Dictionary<string, object>> GetNodeRuntimeOutputsAsync(string nodeName);

        /// <summary>【闭环调参】修改指定节点参数并立即单步执行验证，返回修改前后的参数对比与最新输出数据。</summary>
        Task<TuneAndVerifyResultDto> TuneNodeAndVerifyAsync(string nodeName, Dictionary<string, object> parameters);

        /// <summary>在主界面调起指定算子的原生 WPF 参数配置窗口。</summary>
        Task<OperationResultDto> OpenNodeDialogAsync(string nodeName);

        /// <summary>在画布中高亮选中指定节点，便于向用户展示目标位置。</summary>
        Task<OperationResultDto> SelectNodeAsync(string nodeName);

        /// <summary>对画布上所有节点执行拓扑分层智能对齐，消除节点重叠与混乱。</summary>
        Task<OperationResultDto> AutoLayoutNodesAsync();

        /// <summary>分析当前视口或指定节点的图像灰度直方图分布、动态对比度范围与信噪比。</summary>
        Task<ImageHistogramDto> GetImageHistogramAsync(string nodeName = null);

        /// <summary>基于图像直方图自适应计算最佳二值化阈值（Otsu 大津法 / 最大熵）。</summary>
        Task<AdaptiveThresholdResultDto> AutoComputeThresholdAsync(string nodeName = null);

        /// <summary>一键注入工业标准方案模板（如：template_matching, dimension_measurement, surface_defect）。</summary>
        Task<OperationResultDto> ApplySolutionTemplateAsync(string templateType);

        /// <summary>将当前流程翻译并导出为原生 HALCON HDevelop (.hdev) 脚本代码。</summary>
        Task<string> ExportHDevelopScriptAsync();

        /// <summary>将当前流程翻译并导出为独立的 C# 控制台/工控机脱机运行源代码。</summary>
        Task<string> ExportCSharpCodeAsync();

        // ==================== 变量 / 执行条件 / 历史与运行控制 / PLC 值读写 ====================

        /// <summary>列出全局与流程级变量。scope 为 global/workflow/空(全部)；keyword 按名称过滤。</summary>
        Task<List<VariableDto>> GetVariablesAsync(string scope = null, string keyword = null);

        /// <summary>写入变量（不存在则创建）。scope: global=全局 / workflow=当前流程级。</summary>
        Task<OperationResultDto> SetVariableAsync(string name, string value, string scope = "global", string workflowName = null);

        /// <summary>删除变量。scope: global / workflow。</summary>
        Task<OperationResultDto> DeleteVariableAsync(string name, string scope = "global", string workflowName = null);

        /// <summary>设置节点执行条件（条件语言：$Key$ 插值 + == != > >= < <= + 前置 ! 非空即真）。condition 为空表示清除条件。</summary>
        Task<OperationResultDto> SetNodeExecutionConditionAsync(string nodeName, string condition = null);

        /// <summary>撤销上一步画布操作。</summary>
        Task<OperationResultDto> UndoAsync();

        /// <summary>重做被撤销的操作。</summary>
        Task<OperationResultDto> RedoAsync();

        /// <summary>启动/停止连续运行循环（反复执行当前流程）。</summary>
        Task<OperationResultDto> SetContinuousRunAsync(bool enabled);

        /// <summary>通过通讯驱动读取 PLC 地址值（支持 S7 / 三菱MC / 欧姆龙FINS 驱动）。type: bit/int16/uint16/int32/uint32/float。</summary>
        Task<OperationResultDto> CommReadValueAsync(string driverName, string address, string type = null);

        /// <summary>通过通讯驱动写值到 PLC 地址。value 按类型解析（bit 用 true/false 或 0/1）。</summary>
        Task<OperationResultDto> CommWriteValueAsync(string driverName, string address, object value, string type = null);

        /// <summary>设置流程的并行执行开关（同层兄弟分支并发、层间串行）。workflowName 为空=当前活动流程。</summary>
        Task<OperationResultDto> SetWorkflowParallelExecutionAsync(bool enabled, string workflowName = null);

        /// <summary>新建空白方案（未保存修改时按需丢弃；含一个空的"主流程"）。</summary>
        Task<OperationResultDto> NewSchemeAsync();

        /// <summary>列出最近打开的方案（MRU，最多 10 条，已剔除不存在的文件）。</summary>
        Task<List<SchemeFileInfoDto>> ListRecentSchemesAsync();
    }
}
