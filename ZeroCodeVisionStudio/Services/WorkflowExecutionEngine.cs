using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using HalconDotNet;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Diagnostics;
using ZeroCodeVisionStudio.ViewModels;

namespace ZeroCodeVisionStudio.Services
{
    /// <summary>
    /// 工作流执行引擎：从 MainViewModel 拆出的执行核心。
    /// 职责：拓扑排序 → 条件跳过 → 节点执行（超时看门狗）→ 结果汇总；
    /// 以及节点输入装配（连线传播 + 显式绑定 + 变量源注入）与条件求值。
    /// <para>
    /// 与宿主（MainViewModel）通过回调解耦——引擎不持有 UI 状态属性，
    /// 状态写入（IsExecuting/ResultMessage/SelectedExecutionRecord 等）
    /// 全部经 <see cref="IExecutionEngineHost"/> 回到宿主，保证拆分不改行为。
    /// </para>
    /// </summary>
    internal class WorkflowExecutionEngine
    {
        /// <summary>执行引擎对宿主 ViewModel 的最小依赖面。</summary>
        internal interface IExecutionEngineHost
        {
            WorkflowViewModel ActiveWorkflow { get; }
            bool IsExecuting { get; set; }
            bool IsContinuousRunning { get; }
            WorkflowExecutionRecord SelectedExecutionRecord { get; set; }
            string ResultMessage { get; set; }
            int NodeExecutionTimeoutSeconds { get; }
            

            /// <summary>构建连线传播的共享输入参数（单上游共享/多上游合并）。</summary>
            RunParameter BuildConnectedInputParameter(IHandleRunModel node, Dictionary<object, object> clonedValues);

            /// <summary>写入/移除执行历史记录。</summary>
            WorkflowExecutionRecord AddExecutionRecord(IHandleRunModel node, HandleStatus status, string resultData);

            /// <summary>执行完成后把视口切到最后一个有输出的节点。</summary>
            void ShowLastExecutedNodeOutput(IReadOnlyList<IHandleRunModel> executionOrder);

            /// <summary>刷新选中节点的输入绑定面板。</summary>
            void RefreshSelectedInputBindings();

            /// <summary>刷新全部工作流显示输出。</summary>
            void RefreshAllWorkflowDisplayOutputs();

            /// <summary>单节点显示输出捕获（非工作流模式）。</summary>
            void CaptureDisplayOutputs(IHandleRunModel node, bool append);
        }

        private readonly IExecutionEngineHost host;

        private CancellationTokenSource executionCts;
        private bool isWorkflowExec;
        private int workflowNodeExecutingCount;

        public WorkflowExecutionEngine(IExecutionEngineHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsWorkflowExecuting => isWorkflowExec;

        /// <summary>请求停止当前执行（用户停止按钮 / 超时连带取消）。</summary>
        public void CancelExecution()
        {
            try { executionCts?.Cancel(); } catch { }
        }

        /// <summary>
        /// 强制重置所有执行状态 — 连续运行快速切换后出现卡死时用。
        /// 把 isWorkflowExec / executionCts / workflowNodeExecutingCount 全归零，
        /// 确保下一次 ExecuteWorkflowCoreAsync 能干净地启动。
        /// </summary>
        public void ForceResetState()
        {
            try { executionCts?.Cancel(); executionCts?.Dispose(); } catch { }
            executionCts = null;
            isWorkflowExec = false;
            workflowNodeExecutingCount = 0;
        }

        /// <summary>
        /// 等待正在执行的流程自然结束（取消完成 + IsExecuting 归位）。
        /// 用于停止按钮 / 连续运行退出路径，确保清理干净再允许下一次启动。
        /// 超时后强制重置状态 — 不允许永久卡死。
        /// </summary>
        public async Task WaitForQuiesceAsync(int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while ((isWorkflowExec || host.IsExecuting) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(30);
            }

            // 超时兜底：强制重置所有状态，确保下一次启动不被卡住。
            // 典型场景：Halcon 长节点不响应取消 + 单节点超时也还没到。
            if (isWorkflowExec || host.IsExecuting)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WaitForQuiesceAsync] 超时强制重置 isWorkflowExec={isWorkflowExec} IsExecuting={host.IsExecuting}");
                isWorkflowExec = false;
                host.IsExecuting = false;
            }
        }

        /// <summary>
        /// 执行当前工作流（从 MainViewModel 平移，行为不变）。
        /// </summary>
        /// <param name="fromContinuousLoop">仅限连续运行循环内部传 true（循环自身串行调用，绕过重入守卫）。</param>
        public async Task<bool> ExecuteWorkflowCoreAsync(bool fromContinuousLoop = false, IHandleRunModel stopAfterNode = null)
        {
            var wf = host.ActiveWorkflow;
            if (wf == null) return false;

            if (host.IsExecuting && !fromContinuousLoop) return false;

            host.IsExecuting = true;
            if (executionCts == null || executionCts.IsCancellationRequested)
            {
                executionCts = new CancellationTokenSource();
            }
            var ct = executionCts.Token;

            host.ResultMessage = string.Empty;
            wf.ResetDisplaySources();
            foreach (var item in wf.Nodes)
                item.Reset();

            IReadOnlyList<IHandleRunModel> executionOrder;
            // 指定"执行到某节点为止"时强制串行模式（按拓扑顺序自然命中目标节点）
            var runSerialOnly = stopAfterNode != null;
            var parallelMode = !runSerialOnly && wf.ParallelExecution;
            List<string> parallelWarnings = null;
            try
            {
                executionOrder = GetExecutionOrder(wf);
                if (parallelMode)
                {
                    parallelWarnings = ScanParallelHazards(wf);
                    foreach (var warning in parallelWarnings)
                    {
                        ExecutionLogService.Workflow($"并行执行风险：{warning}", wf.WorkflowName, ExecLogLevel.Warn, toFile: true);
                    }
                }
            }
            catch (Exception ex)
            {
                host.ResultMessage = $"流程启动失败（构建执行顺序异常）：{ExceptionFormatter.Describe(ex)}";
                ExecutionLogService.Error(ExecLogCategory.Workflow, wf.WorkflowName, "流程启动失败：构建执行顺序异常", ex);
                if (!(fromContinuousLoop && host.IsContinuousRunning)) host.IsExecuting = false;
                return false;
            }

            WorkflowVariableStore.CurrentWorkflowName = wf.WorkflowName;
            isWorkflowExec = true;
            ExecutionLogService.Workflow(
                $"{(fromContinuousLoop && host.IsContinuousRunning ? "[连续运行] " : "")}流程开始：{executionOrder.Count} 个节点",
                wf.WorkflowName);
            var workflowStopwatch = Stopwatch.StartNew();
            var executedCount = 0;
            var softFailedCount = 0;
            var skippedCount = 0;
            var skippedByFailureCount = 0;
            IHandleRunModel failedNode = null;
            bool cancelled = false;
            var failureSkipSet = new HashSet<IHandleRunModel>();
            IHandleRunModel failureSkipSource = null;

            // 软失败传播标记：节点未执行（上游失败），状态输出按"未执行"写入
            void MarkSkippedByUpstreamFailure(IHandleRunModel node)
            {
                node.Status = HandleStatus.Success;
                node.StatusMessage = $"未执行（上游 [{failureSkipSource}] 失败）";
                node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "未执行");
                var skippedRecord = host.AddExecutionRecord(node, HandleStatus.Success, node.StatusMessage);
                host.SelectedExecutionRecord = skippedRecord;
                ExecutionLogService.Node("未执行（上游失败，数据消费者自动跳过）",
                    node.DisplayName, ExecLogLevel.Warn, toFile: true);
            }
            try
            {
                if (parallelMode)
                {
                    await ExecuteDataflowParallelAsync();
                }
                else
                {
                    await ExecuteSerialOrderAsync();
                }
            }
            finally
            {
                workflowStopwatch.Stop();
                isWorkflowExec = false;
            }

            var success = failedNode == null && !cancelled;
            var summary = cancelled
                ? $"流程耗时：{ExecutionTimeFormatter.Format(workflowStopwatch.Elapsed)}；完成：{executedCount}/{executionOrder.Count}；已手动停止"
                : BuildWorkflowElapsedMessage(workflowStopwatch.Elapsed, executedCount, executionOrder.Count, failedNode, host.ResultMessage);
            if (skippedCount > 0)
            {
                summary += $"；按执行条件跳过 {skippedCount} 个节点";
            }
            if (softFailedCount > 0)
            {
                summary += $"；{softFailedCount} 个节点执行失败（业务）";
            }
            if (skippedByFailureCount > 0)
            {
                summary += $"；{skippedByFailureCount} 个节点因上游失败未执行（数据消费者已跳过，错误处理分支照常）";
            }
            if (parallelMode)
            {
                summary += "；并行执行（数据流调度：就绪即执行，汇合点等齐全部上游）";
            }
            if (parallelWarnings is { Count: > 0 })
            {
                summary += $"；并行风险 {parallelWarnings.Count} 项（同层共享资源，详见执行日志）";
            }
            host.ResultMessage = summary;
            host.ShowLastExecutedNodeOutput(executionOrder);

            if (!(fromContinuousLoop && host.IsContinuousRunning)) host.IsExecuting = false;
            return success;

            // ---------- 串行调度（原行为，逐节点按拓扑顺序执行） ----------
            async Task ExecuteSerialOrderAsync()
            {
                foreach (var curHandle in executionOrder)
                {
                    if (ct.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    // 软失败传播：上游失败的数据消费者不执行（仅绑定状态输出的错误处理分支放行）
                    if (failureSkipSet.Contains(curHandle))
                    {
                        MarkSkippedByUpstreamFailure(curHandle);
                        skippedByFailureCount++;
                        continue;
                    }
                    failureSkipSource = curHandle;

                    var inputParameter = BuildInputParameter(curHandle);
                    curHandle.Handle.SetRunParameter(inputParameter ?? new RunParameter());

                    // 节点级执行条件：为假时跳过本节点（不执行算子、不计失败）。
                    if (!EvaluateExecutionCondition(inputParameter, curHandle.ExecutionCondition))
                    {
                        skippedCount++;
                        curHandle.Status = HandleStatus.Success;
                        curHandle.StatusMessage = "已跳过（执行条件不满足）";
                        curHandle.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                        curHandle.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "未执行");
                        var skippedRecord = host.AddExecutionRecord(curHandle, HandleStatus.Success, $"跳过（执行条件不满足：{curHandle.ExecutionCondition}）");
                        host.SelectedExecutionRecord = skippedRecord;
                        ExecutionLogService.Node($"跳过（执行条件不满足：{curHandle.ExecutionCondition}）",
                            curHandle.DisplayName, ExecLogLevel.Warn, toFile: true);
                        continue;
                    }

                    if (await ExecuteNodeAsync(curHandle, ct))
                    {
                        executedCount++;
                        // "执行到选定节点为止"：命中目标节点后停止后续
                        if (stopAfterNode != null && ReferenceEquals(curHandle, stopAfterNode))
                        {
                            ExecutionLogService.Node($"执行到选定节点 [{curHandle.DisplayName}]，停止后续",
                                wf?.WorkflowName, ExecLogLevel.Info);
                            break;
                        }
                        if (curHandle.Status == HandleStatus.Failed)
                        {
                            softFailedCount++;
                            foreach (var skippedNode in ComputeFailureSkipSet(wf, curHandle))
                            {
                                if (failureSkipSet.Add(skippedNode))
                                {
                                    MarkSkippedByUpstreamFailure(skippedNode);
                                    skippedByFailureCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        failedNode = curHandle;
                        break;
                    }
                }
            }

            // ---------- 并行调度（数据流）：节点就绪（全部上游完成）即执行、完成即放行下游，
            // 兄弟分支互不等待；多上游汇合点等齐所有上游后才执行。硬失败后不再启动新节点。 ----------
            async Task ExecuteDataflowParallelAsync()
            {
                var maxParallel = Math.Max(2, Environment.ProcessorCount - 1);
                using var throttle = new SemaphoreSlim(maxParallel, maxParallel);

                // 上游集合（同一对节点的多条槽连线只算一个上游）→ 剩余等待数 + 下游邻接表
                var upstreamSets = wf.Nodes.ToDictionary(n => n, n => new HashSet<IHandleRunModel>());
                foreach (var connection in wf.Connections)
                {
                    if (connection.From == null || connection.To == null) continue;
                    if (upstreamSets.TryGetValue(connection.To, out var targets) && upstreamSets.ContainsKey(connection.From))
                        targets.Add(connection.From);
                }
                var remaining = upstreamSets.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
                var successors = wf.Nodes.ToDictionary(
                    n => n,
                    n => wf.Nodes.Where(m => remaining.ContainsKey(m) && upstreamSets[m].Contains(n)).ToList());

                var ready = new Queue<IHandleRunModel>(
                    wf.Nodes.Where(n => remaining[n] == 0).OrderBy(n => n.Y).ThenBy(n => n.X));
                var running = new List<(IHandleRunModel Node, Task<bool> Task)>();

                // 完成放行：下游节点的剩余上游数归零即入就绪队列；
                // suppressed 为软失败传播的跳过集合——计数照减（状态消费者不悬挂）但不入队执行
                var suppressed = new HashSet<IHandleRunModel>();
                void Release(IHandleRunModel completed)
                {
                    foreach (var next in successors[completed])
                    {
                        if (--remaining[next] == 0 && !suppressed.Contains(next)) ready.Enqueue(next);
                    }
                }

                while (running.Count > 0 || ready.Count > 0)
                {
                    if (ct.IsCancellationRequested) { cancelled = true; break; }
                    if (failedNode != null) break;

                    // 就绪节点出队准备并启动（准备阶段在 UI 线程：装配输入 + 执行条件评估）
                    while (ready.Count > 0 && failedNode == null && !ct.IsCancellationRequested)
                    {
                        var node = ready.Dequeue();
                        var inputParameter = BuildInputParameter(node);
                        node.Handle.SetRunParameter(inputParameter ?? new RunParameter());

                        if (!EvaluateExecutionCondition(inputParameter, node.ExecutionCondition))
                        {
                            skippedCount++;
                            node.Status = HandleStatus.Success;
                            node.StatusMessage = "已跳过（执行条件不满足）";
                            node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                            node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "未执行");
                            var skippedRecord = host.AddExecutionRecord(node, HandleStatus.Success, $"跳过（执行条件不满足：{node.ExecutionCondition}）");
                            host.SelectedExecutionRecord = skippedRecord;
                            ExecutionLogService.Node($"跳过（执行条件不满足：{node.ExecutionCondition}）",
                                node.DisplayName, ExecLogLevel.Warn, toFile: true);
                            Release(node); // 条件跳过视为完成，下游可继续
                            continue;
                        }

                        if (running.Count > 0)
                        {
                            ExecutionLogService.Workflow(
                                $"并行执行：[{node.DisplayName}] 与 {running.Count} 个在跑节点并发（并行度上限 {maxParallel}）",
                                wf.WorkflowName);
                        }
                        running.Add((node, RunNodeThrottledAsync(node, ct, throttle)));
                    }

                    if (running.Count == 0) break;

                    var finished = await Task.WhenAny(running.Select(t => t.Task));
                    // 处理全部已完成节点（UI 线程），失败则停止调度新节点
                    for (var idx = running.Count - 1; idx >= 0; idx--)
                    {
                        if (!running[idx].Task.IsCompleted) continue;
                        var (node, task) = running[idx];
                        running.RemoveAt(idx);

                        bool ok;
                        try { ok = task.Result; }
                        catch (OperationCanceledException) { ok = false; }

                        if (ok)
                        {
                            executedCount++;
                            if (node.Status == HandleStatus.Failed)
                            {
                                // 软失败传播：数据消费者（含传递）标记未执行；仅绑定状态输出的错误处理分支放行
                                softFailedCount++;
                                var skipSet = ComputeFailureSkipSet(wf, node);
                                foreach (var suppressedNode in skipSet) suppressed.Add(suppressedNode);
                                foreach (var skippedNode in skipSet)
                                {
                                    skippedByFailureCount++;
                                    skippedNode.Status = HandleStatus.Success;
                                    skippedNode.StatusMessage = $"未执行（上游 [{node.DisplayName}] 失败）";
                                    skippedNode.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                                    skippedNode.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "未执行");
                                    var skippedRecord = host.AddExecutionRecord(skippedNode, HandleStatus.Success, skippedNode.StatusMessage);
                                    host.SelectedExecutionRecord = skippedRecord;
                                    ExecutionLogService.Node("未执行（上游失败，数据消费者自动跳过）",
                                        skippedNode.DisplayName, ExecLogLevel.Warn, toFile: true);
                                    Release(skippedNode); // 释放其下游计数（抑制入队）
                                }
                            }
                            Release(node);
                        }
                        else if (failedNode == null)
                        {
                            failedNode = node;
                        }
                    }
                }

                // 失败/取消后等待在跑节点自然收尾（ExecuteNodeAsync 内部处理取消，结果不再计入）
                if (running.Count > 0)
                {
                    try { await Task.WhenAll(running.Select(t => t.Task)); }
                    catch (OperationCanceledException) { }
                    running.Clear();
                }

                // 环路节点兜底：拓扑无法归零的成环节点，与串行模式一致按位置追加执行
                var cycleNodes = wf.Nodes.Where(n => remaining[n] > 0).OrderBy(n => n.Y).ThenBy(n => n.X).ToList();
                if (cycleNodes.Count > 0 && failedNode == null && !ct.IsCancellationRequested)
                {
                    foreach (var node in cycleNodes)
                    {
                        var inputParameter = BuildInputParameter(node);
                        node.Handle.SetRunParameter(inputParameter ?? new RunParameter());
                        if (EvaluateExecutionCondition(inputParameter, node.ExecutionCondition))
                        {
                            if (await ExecuteNodeAsync(node, ct)) executedCount++;
                            else failedNode ??= node;
                        }
                        else skippedCount++;
                        if (failedNode != null) break;
                    }
                }
            }

            async Task<bool> RunNodeThrottledAsync(IHandleRunModel n, CancellationToken token, SemaphoreSlim gate)
            {
                await gate.WaitAsync(token);
                try
                {
                    return await ExecuteNodeAsync(n, token);
                }
                finally
                {
                    gate.Release();
                }
            }
        }

        /// <summary>
        /// 执行单个节点（从 MainViewModel 平移，行为不变）。
        /// 算子本体在后台线程执行；单节点超时视为失败并中止整个流程。
        /// </summary>
        private async Task<bool> ExecuteNodeAsync(IHandleRunModel node, CancellationToken ct)
        {
            try
            {
                // === Halcon 运行时防呆守卫（仅诊断 —— HandleBase.Execute 内部会完整处理）===
                // 读 Handle.Backend（HandleAttribute 反射），确保与 Execute() 内部判断一致。
                var handleBackend = node.Handle is HandleBase hb ? hb.Backend : (node.RegisterModel?.Backend ?? OperatorBackend.Unknown);
                if (HalconRuntimeProbe.ShouldSkipOperator(handleBackend))
                {
                    node.Status = HandleStatus.Failed;
                    node.StatusMessage = HalconRuntimeProbe.GetSkipReason(node.DisplayName);
                    node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                    node.Handle?.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "未执行");
                    ExecutionLogService.Node("跳过：Halcon 运行时不可用", node.DisplayName, ExecLogLevel.Warn, toFile: true);
                    host.AddExecutionRecord(node, HandleStatus.Failed, node.StatusMessage);
                    host.ResultMessage = node.StatusMessage;
                    return false;
                }

                node.Status = HandleStatus.Running;
                node.StatusMessage = "Running";

                if (isWorkflowExec)
                {
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
                else
                {
                    await Task.Delay(1);
                }

                var stopwatch = Stopwatch.StartNew();

                var work = Task.Run(() =>
                {
                    Interlocked.Increment(ref workflowNodeExecutingCount);
                    try
                    {
                        node.Handle.Execute();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref workflowNodeExecutingCount);
                    }
                }, CancellationToken.None);

                var timeout = TimeSpan.FromSeconds(Math.Max(5, host.NodeExecutionTimeoutSeconds));
                var completed = await Task.WhenAny(work, Task.Delay(timeout, ct));
                if (completed != work)
                {
                    if (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(ct);
                    }

                    executionCts?.Cancel();
                    await Task.WhenAny(work, Task.Delay(2000));
                    throw new TimeoutException($"节点执行超时（>{timeout.TotalSeconds:F0} 秒），已停止整个流程");
                }

                await work;
                stopwatch.Stop();
                node.LastElapsed = stopwatch.Elapsed;

                // 软失败：算子正常返回但业务失败（RunParameter.Success == false，如通讯发送失败）。
                // 节点与执行记录标记为失败，但流程继续（下游可按输出键分支处理）。
                var softFailed = node.Handle.RunParameter?.Success == false;
                node.Status = softFailed ? HandleStatus.Failed : HandleStatus.Success;
                node.StatusMessage = $"{(softFailed ? "Failed" : "Success")} ({node.LastElapsedText})";

                // 执行状态写入标准输出：下游节点可绑定（输入槽/参数引用/执行条件）
                var runParam = node.Handle.RunParameter;
                runParam?.SetOutput(RunOutputKeys.ExecutionSuccess, !softFailed);
                runParam?.SetOutput(RunOutputKeys.ExecutionStatus, softFailed ? "失败" : "成功");

                var resultData = BuildResultData(node);
                var record = host.AddExecutionRecord(node, node.Status, resultData);
                host.SelectedExecutionRecord = record;

                var detail = !string.IsNullOrWhiteSpace(node.Handle.RunParameter?.ResultMessage)
                    ? node.Handle.RunParameter.ResultMessage
                    : resultData;

                if (softFailed)
                {
                    ExecutionLogService.Node($"业务失败：{detail}", node.DisplayName, ExecLogLevel.Error, toFile: true);
                }

                var nodeElapsedMsg = $"[{node.DisplayName}] 耗时：{node.LastElapsedText}  状态：{(softFailed ? "失败" : "已执行")}";
                if (!string.IsNullOrWhiteSpace(detail) && detail != node.StatusMessage)
                {
                    nodeElapsedMsg += $"{Environment.NewLine}{detail}";
                }
                host.ResultMessage = nodeElapsedMsg;

                if (!isWorkflowExec)
                {
                    host.CaptureDisplayOutputs(node, append: true);
                }

                host.RefreshSelectedInputBindings();
                return true;
            }
            catch (OperationCanceledException)
            {
                node.Status = HandleStatus.Failed;
                node.StatusMessage = "已取消";
                host.AddExecutionRecord(node, HandleStatus.Failed, "已取消");
                return false;
            }
            catch (Exception ex)
            {
                node.Status = HandleStatus.Failed;
                node.StatusMessage = ExceptionFormatter.Brief(ex);
                node.Handle.RunParameter?.SetOutput(RunOutputKeys.ExecutionSuccess, false);
                node.Handle.RunParameter?.SetOutput(RunOutputKeys.ExecutionStatus, "失败");
                ExecutionLogService.Error(ExecLogCategory.Node, node.DisplayName, $"算子执行失败：{ExceptionFormatter.Brief(ex)}", ex);
                host.AddExecutionRecord(node, HandleStatus.Failed, node.StatusMessage);
                host.ResultMessage = $"[{node.DisplayName}]（{node.RegisterModel?.HandleName ?? "未知算子"}）耗时：{node.LastElapsedText}  状态：失败{Environment.NewLine}错误：{ExceptionFormatter.Describe(ex)}";
                return false;
            }
        }


        /// <summary>单节点执行的输入装配入口（供宿主单节点执行路径复用引擎逻辑）。</summary>
        public static RunParameter BuildInputParameterForNode(IExecutionEngineHost host, IHandleRunModel node)
        {
            var explicitBindings = node.InputBindings
                .Where(x => (x.SourceNode?.Handle?.RunParameter != null && !string.IsNullOrWhiteSpace(x.SourceOutputKey))
                            || x.IsVariableSource)
                .ToArray();

            var clonedValues = new Dictionary<object, object>(Shared.ReferenceEqualityComparer.Instance);

            // 数据只来自显式绑定（连线在建立/加载时自动生成默认绑定）：
            // 不再走"连线隐式克隆上游整包"的旁路——未绑定的输入没有数据，算子守卫将给出明确报错。
            RunParameter parameter = null;
            foreach (var binding in explicitBindings)
            {
                var value = binding.IsVariableSource
                    ? ResolveVariableBindingValue(binding)
                    : GetSlotValue(binding.SourceNode.Handle.RunParameter, binding.SourceOutputKey);

                if (binding.IsParameterBinding)
                    SetParameterValue(node, binding.TargetParameterName, value);
                else
                {
                    parameter ??= new RunParameter();
                    SetSlotValue(parameter, binding.TargetInputKey, value, clonedValues);

                    // 主输入约定：绑定值统一回填到主输入字段 —— HObject 回填 HOutput/RawImage/GrayImage，
                    // Mat 回填 MatOutput/MatGrayImage。算子守卫按多种顺序读取主图像，统一回填保证兼容；
                    // 引用登记进共享集合，Reset 不释放上游图像。AdoptMainInput 幂等（??=）。
                    if (value is HObject boundImage)
                        parameter.AdoptMainInput(boundImage);
                    else if (value is OpenCvSharp.Mat boundMat)
                        parameter.AdoptMainInput(boundMat);
                }
            }

            return HasRunData(parameter) ? parameter : null;
        }

        /// <summary>参数值类型转换（方案加载参数恢复复用）。</summary>
        public static object ConvertParameterForLoad(object value, Type targetType) =>
            ConvertParameterValue(value, targetType);

        /// <summary>单节点执行（供宿主单节点执行路径）。</summary>
        public async Task<bool> ExecuteSingleNodeAsync(IHandleRunModel node, CancellationToken ct) =>
            await ExecuteNodeAsync(node, ct);

        // ==================== 拓扑排序 ====================

        /// <summary>按连线拓扑求执行顺序：Kahn 分层（上游先执行），孤立节点按画布位置排在最前。</summary>
        private static IReadOnlyList<IHandleRunModel> GetExecutionOrder(WorkflowViewModel wf)
        {
            var nodes = wf.Nodes.ToList();
            var inDegree = nodes.ToDictionary(n => n, _ => 0);
            var adjacency = nodes.ToDictionary(n => n, _ => new List<IHandleRunModel>());
            foreach (var connection in wf.Connections)
            {
                if (connection.From == null || connection.To == null) continue;
                if (!inDegree.ContainsKey(connection.To) || !adjacency.ContainsKey(connection.From)) continue;
                inDegree[connection.To]++;
                adjacency[connection.From].Add(connection.To);
            }

            var queue = new Queue<IHandleRunModel>(
                nodes.Where(n => inDegree[n] == 0).OrderBy(n => n.Y).ThenBy(n => n.X));
            var order = new List<IHandleRunModel>(nodes.Count);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                order.Add(current);
                foreach (var next in adjacency[current])
                {
                    if (--inDegree[next] == 0)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            // 环路兜底：剩余节点按位置追加（不静默丢节点）。
            if (order.Count < nodes.Count)
            {
                foreach (var node in nodes.Where(n => !order.Contains(n)).OrderBy(n => n.Y).ThenBy(n => n.X))
                {
                    order.Add(node);
                }
            }

            return order;
        }

        /// <summary>状态类输出键：绑定这些输出的下游属于"错误处理分支"，软失败传播时放行不跳过。</summary>
        private static readonly string[] StatusOutputKeys =
        {
            RunOutputKeys.ExecutionSuccess,
            RunOutputKeys.ExecutionStatus,
            RunOutputKeys.ResultMessage,
        };

        /// <summary>
        /// 判断 target 是否对 from 存在数据依赖（软失败传播判定用）：
        /// - target 对 from 有显式绑定：全部为状态键（是否成功/执行结果/结果信息）时视为状态依赖——
        ///   即错误处理分支（虽然也连了线以建立上游关系，但只消费状态，不消费数据）；
        ///   任一绑定指向数据输出则为数据依赖。
        /// - 无显式绑定但有连线：默认图像/区域数据流，数据依赖。
        /// </summary>
        private static bool HasDataDependency(WorkflowViewModel wf, IHandleRunModel from, IHandleRunModel to)
        {
            var bindings = to.InputBindings?.Where(b => ReferenceEquals(b.SourceNode, from)).ToList()
                           ?? new List<WorkflowInputBinding>();
            if (bindings.Count > 0)
                return bindings.Any(b => string.IsNullOrEmpty(b.SourceOutputKey) || !StatusOutputKeys.Contains(b.SourceOutputKey));

            return wf.Connections.Any(c => ReferenceEquals(c.From, from) && ReferenceEquals(c.To, to));
        }

        /// <summary>
        /// 软失败传播集合：从失败节点沿"数据依赖"边向下传递闭包——
        /// 集合内节点的本轮输入必然含无效/缺失数据，标记未执行；
        /// 仅绑定状态输出（是否成功/执行结果/结果信息）的错误处理分支不在集合内，照常执行。
        /// </summary>
        private static List<IHandleRunModel> ComputeFailureSkipSet(WorkflowViewModel wf, IHandleRunModel failed)
        {
            var skip = new HashSet<IHandleRunModel> { failed };
            var queue = new Queue<IHandleRunModel>();
            queue.Enqueue(failed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var candidate in wf.Nodes)
                {
                    if (skip.Contains(candidate) || !HasDataDependency(wf, current, candidate)) continue;
                    skip.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
            skip.Remove(failed);
            return skip.ToList();
        }

        /// <summary>
        /// 并行风险静态扫描：访问同一通讯驱动/变量、且两者之间没有拓扑先后关系（谁也不依赖谁）
        /// 的节点对——数据流调度下它们可能同时运行或顺序不确定，提前告警。
        /// </summary>
        private static List<string> ScanParallelHazards(WorkflowViewModel wf)
        {
            var hazards = new List<string>();
            var nodes = wf.Nodes.Where(n => SharedResourceKeys(n).Any()).ToList();
            if (nodes.Count < 2) return hazards;

            // 每个节点的全部上游（传递闭包），用于判断两节点是否存在先后约束
            var ancestors = nodes.ToDictionary(
                n => n,
                n =>
                {
                    var set = new HashSet<IHandleRunModel>();
                    var queue = new Queue<IHandleRunModel>();
                    queue.Enqueue(n);
                    while (queue.Count > 0)
                    {
                        foreach (var connection in wf.Connections.Where(c => ReferenceEquals(c.To, queue.Peek())))
                        {
                            if (connection.From == null || !set.Add(connection.From)) continue;
                            queue.Enqueue(connection.From);
                        }
                        queue.Dequeue();
                    }
                    return (IReadOnlyCollection<IHandleRunModel>)set;
                });

            for (var i = 0; i < nodes.Count; i++)
            {
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];
                    // 有上下游关系 → 调度器保证先后，无风险
                    if (ancestors[a].Contains(b) || ancestors[b].Contains(a)) continue;

                    var shared = SharedResourceKeys(a).Intersect(SharedResourceKeys(b), StringComparer.OrdinalIgnoreCase).ToList();
                    if (shared.Count > 0)
                    {
                        hazards.Add($"[{a.DisplayName}] 与 [{b.DisplayName}] 同时访问 {string.Join("、", shared)}，两者无拓扑先后约束，执行顺序不确定");
                    }
                }
            }
            return hazards;
        }

        /// <summary>读取节点参数中声明的共享资源键（通讯驱动名 / 变量名），未声明返回空。</summary>
        private static IEnumerable<string> SharedResourceKeys(IHandleRunModel node)
        {
            var parameter = node?.Handle?.HandleParameter;
            if (parameter == null) yield break;

            var type = parameter.GetType();
            foreach (var propertyName in new[] { "DriverName", "VariableName" })
            {
                var property = type.GetProperty(propertyName);
                if (property?.PropertyType == typeof(string) &&
                    property.GetValue(parameter) is string value &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    yield return (propertyName == "DriverName" ? "通讯驱动 [" : "变量 [") + value + "]";
                }
            }
        }

        // ==================== 节点输入装配 ====================

        /// <summary>
        /// 为节点构建输入参数：连线传播（共享引用/写时复制）+ 显式绑定（含变量源）。
        /// </summary>
        private RunParameter BuildInputParameter(IHandleRunModel node)
        {
            var explicitBindings = node.InputBindings
                .Where(x => (x.SourceNode?.Handle?.RunParameter != null && !string.IsNullOrWhiteSpace(x.SourceOutputKey))
                            || x.IsVariableSource)
                .ToArray();

            var clonedValues = new Dictionary<object, object>(Shared.ReferenceEqualityComparer.Instance);

            // 数据只来自显式绑定（连线在建立/加载时自动生成默认绑定）：
            // 不再走"连线隐式克隆上游整包"的旁路——未绑定的输入没有数据，算子守卫将给出明确报错。
            RunParameter parameter = null;
            foreach (var binding in explicitBindings)
            {
                var value = binding.IsVariableSource
                    ? ResolveVariableBindingValue(binding)
                    : GetSlotValue(binding.SourceNode.Handle.RunParameter, binding.SourceOutputKey);

                if (binding.IsParameterBinding)
                    SetParameterValue(node, binding.TargetParameterName, value);
                else
                {
                    parameter ??= new RunParameter();
                    SetSlotValue(parameter, binding.TargetInputKey, value, clonedValues);

                    // 主输入约定：绑定值统一回填到主输入字段 —— HObject 回填 HOutput/RawImage/GrayImage，
                    // Mat 回填 MatOutput/MatGrayImage。算子守卫按多种顺序读取主图像，统一回填保证兼容；
                    // 引用登记进共享集合，Reset 不释放上游图像。AdoptMainInput 幂等（??=）。
                    if (value is HObject boundImage)
                        parameter.AdoptMainInput(boundImage);
                    else if (value is OpenCvSharp.Mat boundMat)
                        parameter.AdoptMainInput(boundMat);
                }
            }

            return HasRunData(parameter) ? parameter : null;
        }

        internal static object GetSlotValue(RunParameter parameter, string outputKey)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(outputKey)) return null;
            var value = parameter.GetOutput<object>(outputKey);

            // 主输出回退：上游未显式写 output 键时，依次尝试各主输出字段，
            // 兼容不透传图像的中间算子（绑定语义仍是"上游主输出"）。
            if (value == null && outputKey == RunOutputKeys.Output)
            {
                // OpenCV Mat 优先（现在 ReadImage 等输入算子无论 Halcon 可不可用都写 Mat）
                if (parameter.MatOutput != null && !parameter.MatOutput.Empty())
                    value = parameter.MatOutput;
                else if (parameter.HOutput != null)
                    value = parameter.HOutput;
            }
            return value;
        }

        internal static void SetSlotValue(RunParameter parameter, string targetInputKey, object value, Dictionary<object, object> clonedValues)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(targetInputKey)) return;

            // HALCON 对象按共享引用传递（写时复制语义）：同一上游输出被多个下游消费时不做深拷贝，
            // 首次克隆登记进 clonedValues，后续节点复用同一克隆。
            if (value is HObject hObject && !clonedValues.TryGetValue(hObject, out _))
            {
                // 首次进入该参数：记录共享引用。深拷贝决策留给算子自身（Dispose 归属由共享集合管理）。
                clonedValues[hObject] = hObject;
            }

            parameter.SetOutput(targetInputKey, value);
        }

        internal static void SetParameterValue(IHandleRunModel node, string propertyName, object value)
        {
            if (node?.Handle?.HandleParameter == null || string.IsNullOrWhiteSpace(propertyName) || value == null) return;

            var parameter = node.Handle.HandleParameter;
            var property = parameter.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite) return;

            property.SetValue(parameter, ConvertParameterValue(value, property.PropertyType));
        }

        public static object ConvertJsonElement(System.Text.Json.JsonElement element, Type targetType) =>
            ConvertJsonElementValue(element, targetType);

        internal static object ConvertParameterValue(object value, Type targetType)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsInstanceOfType(value)) return value;

            if (value is System.Text.Json.JsonElement element)
            {
                return ConvertJsonElementValue(element, targetType);
            }

            if (value is System.Collections.IEnumerable enumerable && targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                var itemType = targetType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);
                var list = System.Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");
                foreach (var item in enumerable)
                {
                    addMethod.Invoke(list, new[] { ConvertParameterValue(item, itemType) });
                }
                return System.Activator.CreateInstance(targetType, list);
            }

            if (value is IConvertible)
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

            return value;
        }

        internal static object ConvertJsonElementValue(System.Text.Json.JsonElement element, Type targetType) =>
            Shared.Helper.JsonValueConverter.Convert(element, targetType);

        internal static bool HasRunData(RunParameter parameter)
        {
            if (parameter == null) return false;
            return parameter.Outputs.Count > 0 || parameter.Inputs.Count > 0;
        }

        // ==================== 变量源绑定 ====================

        /// <summary>
        /// 解析变量源绑定的当前值：流程级按当前流程解析，
        /// 并按目标槽/参数类型把字符串值转换成强类型。
        /// </summary>
        internal static object ResolveVariableBindingValue(WorkflowInputBinding binding)
        {
            var parsed = binding.ParseVariableSource();
            if (parsed == null) return null;

            var (scope, name) = parsed.Value;
            var raw = scope == WorkflowVariableScope.Global
                ? WorkflowVariableStore.GetScoped(name, WorkflowVariableScope.Global)
                : WorkflowVariableStore.GetScoped(name, WorkflowVariableScope.Workflow);
            if (raw == null) return null;

            return ConvertVariableText(raw, binding.TargetInputType);
        }

        /// <summary>把变量文本按目标类型转换。</summary>
        private static object ConvertVariableText(string raw, Type targetType)
        {
            try
            {
                var type = Nullable.GetUnderlyingType(targetType ?? typeof(object)) ?? targetType ?? typeof(object);
                if (type == typeof(bool)) return bool.TryParse(raw, out var b) ? b : LogicTruth(raw);
                if (type == typeof(double)) return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
                if (type == typeof(int)) return int.TryParse(raw, out var i) ? i : 0;
                if (type == typeof(long)) return long.TryParse(raw, out var l) ? l : 0;
                return raw;
            }
            catch
            {
                return raw;
            }
        }

        private static bool LogicTruth(string value) =>
            value?.Trim() is "1" or "OK" or "ok" or "True" or "true";

        // ==================== 节点级执行条件 ====================

        /// <summary>
        /// 求值节点执行条件：先做 $Key$ 上游输出插值，再解析单个比较表达式。
        /// 空条件恒真；解析异常按假处理（不中断流程）。
        /// </summary>
        internal static bool EvaluateExecutionCondition(RunParameter parameter, string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;

            try
            {
                var expression = condition.Trim();
                if (parameter != null)
                {
                    foreach (var kv in parameter.Outputs)
                    {
                        expression = expression.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
                    }
                }

                return EvaluateConditionExpression(expression);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>解析比较表达式：可选前置 !、两侧比较，裸值按真值判断。</summary>
        internal static bool EvaluateConditionExpression(string expression)
        {
            expression = expression.Trim();
            var negate = false;
            while (expression.StartsWith("!"))
            {
                negate = !negate;
                expression = expression.Substring(1).Trim();
            }
            if (expression.Length == 0) return negate;

            string[] operators = { "==", "!=", ">=", "<=", ">", "<" };
            foreach (var op in operators)
            {
                var idx = expression.IndexOf(op, StringComparison.Ordinal);
                if (idx <= 0 || idx == expression.Length - op.Length) continue;

                var left = expression.Substring(0, idx).Trim();
                var right = expression.Substring(idx + op.Length).Trim();
                var result = CompareConditionValues(left, right, op);
                return negate ? !result : result;
            }

            var truthy = IsTruthyConditionValue(expression);
            return negate ? !truthy : truthy;
        }

        internal static bool CompareConditionValues(string left, string right, string op)
        {
            if (double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var ln) &&
                double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rn))
            {
                return op switch
                {
                    "==" => Math.Abs(ln - rn) < 1e-12,
                    "!=" => Math.Abs(ln - rn) >= 1e-12,
                    ">" => ln > rn,
                    ">=" => ln >= rn,
                    "<" => ln < rn,
                    _ => ln <= rn
                };
            }

            if (bool.TryParse(left, out var lb) && bool.TryParse(right, out var rb))
            {
                return op switch { "==" => lb == rb, "!=" => lb != rb, _ => false };
            }

            var equals = string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            return op switch { "==" => equals, "!=" => !equals, _ => false };
        }

        internal static bool IsTruthyConditionValue(string value)
        {
            if (bool.TryParse(value, out var b)) return b;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return Math.Abs(d) > 1e-12;
            return !string.IsNullOrEmpty(value) && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        // ==================== 结果汇总 ====================

        private static string BuildResultData(IHandleRunModel node)
        {
            var message = node.Handle?.RunParameter?.ResultMessage;
            return string.IsNullOrWhiteSpace(message) ? node.StatusMessage : message;
        }

        private static string BuildWorkflowElapsedMessage(
            TimeSpan elapsed,
            int executedCount,
            int totalCount,
            IHandleRunModel failedNode,
            string lastResultMessage)
        {
            var elapsedText = ExecutionTimeFormatter.Format(elapsed);
            if (failedNode != null)
            {
                var failureDetail = !string.IsNullOrWhiteSpace(lastResultMessage)
                    ? lastResultMessage.Split('\n').FirstOrDefault(l => l.Contains("错误")) ?? lastResultMessage
                    : "";
                return $"流程耗时：{elapsedText}；完成：{executedCount}/{totalCount}；失败节点：[{failedNode.DisplayName}]{Environment.NewLine}{failureDetail}";
            }

            return $"流程耗时：{elapsedText}；完成：{executedCount}/{totalCount}";
        }
    }
}
