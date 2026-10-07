using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HalconVisionStudio.AI.Tools;
using HalconVisionStudio.Shared;
using HalconVisionStudio.UI.Controls;
using HalconDotNet;

namespace HalconVisionStudio.Services
{
    /// <summary>
    /// 将 MainViewModel 的能力桥接给 AI 自动化服务。
    /// 所有对 UI 状态的读写都调度到 UI 线程执行。
    /// </summary>
    public class VisionStudioContextAdapter : IVisionStudioContext
    {
        private readonly MainViewModel _mainVm;

        public VisionStudioContextAdapter(MainViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public async Task RunOnUiAsync(Action action)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(action);
            }
            else
            {
                action();
            }
        }

        public async Task<T> RunOnUiAsync<T>(Func<T> func)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                return await Application.Current.Dispatcher.InvokeAsync(func);
            }
            else
            {
                return func();
            }
        }

        public string SchemeTitle => _mainVm.SchemeTitle;
        public string ActiveWorkflowName => _mainVm.ActiveWorkflow?.WorkflowName ?? "无活动流程";
        public bool ActiveWorkflowParallelExecution => _mainVm.ActiveWorkflow?.ParallelExecution == true;

        public bool SetWorkflowParallelExecution(bool enabled, string workflowName) => RunOnUi(() =>
        {
            var wf = string.IsNullOrWhiteSpace(workflowName)
                ? _mainVm.ActiveWorkflow
                : _mainVm.Workflows.FirstOrDefault(w => string.Equals(w.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase));
            if (wf == null) return false;
            wf.ParallelExecution = enabled;
            return true;
        });

        public void NewScheme() => RunOnUi<object>(() =>
        {
            _mainVm.NewSchemeCommand.Execute();
            return null;
        });
        public IReadOnlyList<string> WorkflowNames => _mainVm.Workflows.Select(w => w.WorkflowName).ToList();

        public IReadOnlyList<IHandleRunModel> GetActiveNodes() =>
            _mainVm.ActiveWorkflow?.Nodes?.ToList() ?? new List<IHandleRunModel>();

        public IReadOnlyList<(string FromNode, string ToNode)> GetActiveConnections() =>
            _mainVm.ActiveWorkflow?.Connections?
                .Select(c => (c.From?.DisplayName, c.To?.DisplayName))
                .Where(t => t.Item1 != null && t.Item2 != null)
                .ToList() ?? new List<(string, string)>();

        public IReadOnlyList<(int Seq, DateTime Time, string Module, string Status, TimeSpan Elapsed, string Result)> GetExecutionRecords() =>
            _mainVm.ActiveWorkflow?.ExecutionRecords?
                .Select(r => (r.Sequence, r.ExecuteTime, r.Module, r.StatusText, r.Elapsed, r.ResultData))
                .ToList() ?? new List<(int, DateTime, string, string, TimeSpan, string)>();

        public IReadOnlyList<IHandleRegisterModel> GetRegisteredOperators() =>
            _mainVm.HandleControl?.HandleView?.SourceCollection?.OfType<IHandleRegisterModel>().ToList()
            ?? new List<IHandleRegisterModel>();

        public IHandleRunModel AddNode(string operatorNameOrKey, double? x = null, double? y = null)
        {
            var registered = GetRegisteredOperators();
            var target = registered.FirstOrDefault(r =>
                string.Equals(r.Key, operatorNameOrKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.HandleName, operatorNameOrKey, StringComparison.OrdinalIgnoreCase));

            if (target == null) return null;

            double posX = x ?? (80 + (_mainVm.ActiveWorkflow?.Nodes.Count ?? 0) * 20);
            double posY = y ?? (80 + (_mainVm.ActiveWorkflow?.Nodes.Count ?? 0) * 80);

            return _mainVm.AddHandleToWorkflow(target, posX, posY);
        }

        public bool RemoveNode(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            _mainVm.DeleteNodeCommand?.Execute(node);
            return true;
        }

        public bool RenameNode(string oldName, string newName)
        {
            return _mainVm.RenameNodeInternal(oldName, newName);
        }

        public bool MoveNode(string nodeName, double x, double y)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            return _mainVm.MoveNodeInternal(node, x, y);
        }

        public bool ConnectNodes(string fromNode, string toNode, string fromPin = null, string toPin = null)
        {
            var from = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, fromNode, StringComparison.OrdinalIgnoreCase));
            var to = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, toNode, StringComparison.OrdinalIgnoreCase));

            if (from == null || to == null) return false;

            _mainVm.AddConnection(from, to, fromPin, toPin);
            return true;
        }

        public bool DisconnectNodes(string fromNode, string toNode)
        {
            var conn = _mainVm.ActiveWorkflow?.Connections.FirstOrDefault(c =>
                string.Equals(c.From?.DisplayName, fromNode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.To?.DisplayName, toNode, StringComparison.OrdinalIgnoreCase));

            if (conn == null) return false;

            _mainVm.DeleteConnection(conn);
            return true;
        }

        public async Task<bool> ExecuteWorkflowAsync()
        {
            // 等待工作流真正执行完成，AI 才能读到真实结果。
            return await _mainVm.ExecuteWorkflowForAutomationAsync();
        }

        public async Task<bool> ExecuteSingleNodeAsync(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            return await _mainVm.ExecuteSingleNodeForAutomationAsync(node);
        }

        public bool StopExecution()
        {
            return _mainVm.RequestStopExecution();
        }

        public List<WorkflowInputBinding> GetNodeInputBindings(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            return node?.InputBindings?.ToList() ?? new List<WorkflowInputBinding>();
        }

        public bool SetNodeInputBinding(string nodeName, string targetInputKey, string sourceNode, string sourceOutputKey)
        {
            return _mainVm.SetNodeInputBindingInternal(nodeName, targetInputKey, sourceNode, sourceOutputKey);
        }

        public IHandleRunModel DuplicateNode(string nodeName, double offsetX = 40, double offsetY = 40)
        {
            return _mainVm.DuplicateNodeInternal(nodeName, offsetX, offsetY);
        }

        public bool ClearWorkflow()
        {
            return _mainVm.ClearWorkflowInternal();
        }

        public bool RemoveWorkflowByName(string workflowName)
        {
            return _mainVm.RemoveWorkflowByNameInternal(workflowName);
        }

        public bool RenameWorkflow(string oldName, string newName)
        {
            return _mainVm.RenameWorkflowInternal(oldName, newName);
        }

        public bool ShowNodeOutput(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                _mainVm.SelectNode(node);
            });
            return true;
        }

        public bool CreateWorkflow(string workflowName)
        {
            try
            {
                _mainVm.AddWorkflowCommand?.Execute();
                var newWf = _mainVm.Workflows.LastOrDefault();
                if (newWf != null && !string.IsNullOrWhiteSpace(workflowName))
                {
                    newWf.WorkflowName = workflowName;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SwitchWorkflow(string workflowName)
        {
            return _mainVm.SwitchWorkflowByName(workflowName);
        }

        public async Task<string> SaveSchemeAsync(string path = null)
        {
            return await _mainVm.SaveSchemeForAutomationAsync(path);
        }

        public async Task<string> OpenSchemeAsync(string path)
        {
            return await _mainVm.OpenSchemeForAutomationAsync(path);
        }

        public IReadOnlyList<string> ListSchemes()
        {
            try
            {
                return Shared.Data.SchemePersistenceService.ListSavedSchemes().ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 截取视口图像。targetPath 仅允许位于临时目录、用户图片目录或应用数据目录内，
        /// 防止外部客户端借该工具覆盖磁盘任意路径的文件。
        /// </summary>
        public async Task<string> CaptureViewportImageAsync(string targetPath = null)
        {
            var dir = Path.Combine(Path.GetTempPath(), "HalconVisionStudio");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var defaultPath = Path.Combine(dir, $"viewport_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = defaultPath;
            }
            else
            {
                targetPath = Path.GetFullPath(targetPath);
                if (!IsPathAllowed(targetPath))
                {
                    throw new UnauthorizedAccessException(
                        $"保存路径不在允许范围内（仅允许临时目录/用户图片目录/应用数据目录）: {targetPath}");
                }
            }

            return await RunOnUiAsync(() =>
            {
                try
                {
                    var halconWin = _mainVm.HWindow?.HalconWindow;
                    if (halconWin != null)
                    {
                        using var img = halconWin.DumpWindowImage();
                        img.WriteImage("png", 0, targetPath);
                        return targetPath;
                    }
                }
                catch
                {
                }

                return targetPath;
            });
        }

        private static bool IsPathAllowed(string fullPath)
        {
            var allowedRoots = new[]
            {
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            return allowedRoots
                .Where(root => !string.IsNullOrEmpty(root))
                .Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, object> GetNodeRuntimeOutputs(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            result["NodeName"] = node.DisplayName;
            result["Status"] = node.Status.ToString();
            result["StatusMessage"] = node.StatusMessage ?? "";
            result["LastElapsedMs"] = node.LastElapsed.TotalMilliseconds;

            var rp = node.Handle?.RunParameter;
            if (rp != null && rp.Outputs != null)
            {
                foreach (var kvp in rp.Outputs)
                {
                    if (kvp.Value is HTuple tuple)
                    {
                        // 句柄/混合型元组的 .S / ToDArr 会抛 Illegal operation，逐值兜底
                        try
                        {
                            if (tuple.Length == 1)
                            {
                                if (tuple.Type == HTupleType.INTEGER) result[kvp.Key] = tuple.I;
                                else if (tuple.Type == HTupleType.DOUBLE) result[kvp.Key] = tuple.D;
                                else if (tuple.Type == HTupleType.STRING) result[kvp.Key] = tuple.S;
                                else result[kvp.Key] = tuple.ToString();
                            }
                            else
                            {
                                result[kvp.Key] = tuple.ToDArr().ToList();
                            }
                        }
                        catch
                        {
                            result[kvp.Key] = tuple.ToString();
                        }
                    }
                    else if (kvp.Value is HObject ho)
                    {
                        try
                        {
                            result[kvp.Key] = $"HObject[{ho.GetObjClass()}]";
                        }
                        catch
                        {
                            result[kvp.Key] = "HObject";
                        }
                    }
                    else
                    {
                        result[kvp.Key] = kvp.Value?.ToString() ?? "null";
                    }
                }
            }

            return result;
        }

        public bool OpenNodeDialog(string nodeName)
        {
            var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node?.Handle == null) return false;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                node.Handle.EditParameters();
            });
            return true;
        }

        public bool AutoLayoutNodes()
        {
            var wf = _mainVm.ActiveWorkflow;
            if (wf == null || wf.Nodes == null || wf.Nodes.Count == 0) return false;

            // 复用画布按钮与 AI 工具共用的统一布局实现（含分层/对齐/连线刷新）。
            Application.Current.Dispatcher.Invoke(() => _mainVm.AutoLayoutWorkflowNodes());
            return true;
        }

        public (double Mean, double Dev, double Min, double Max, List<int> Percentiles) GetImageStats(string nodeName = null)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                // 指定节点：分析该节点的主输出图像；否则分析当前视口截图。
                HObject sourceImage = null;
                bool disposeImage = false;

                if (!string.IsNullOrWhiteSpace(nodeName))
                {
                    var node = _mainVm.ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                        string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
                    var rp = node?.Handle?.RunParameter;
                    sourceImage = (rp?.HOutput as HObject) ?? (rp?.GrayImage as HObject) ?? (rp?.RawImage as HObject);
                }
                else
                {
                    var halconWin = _mainVm.HWindow?.HalconWindow;
                    if (halconWin != null)
                    {
                        sourceImage = halconWin.DumpWindowImage();
                        disposeImage = true;
                    }
                }

                if (sourceImage == null)
                {
                    return (0, 0, 0, 255, new List<int>());
                }

                try
                {
                    return ComputeImageStats(sourceImage);
                }
                finally
                {
                    if (disposeImage) sourceImage.Dispose();
                }
            });
        }

        private static (double Mean, double Dev, double Min, double Max, List<int> Percentiles) ComputeImageStats(HObject sourceImage)
        {
            try
            {
                HImage processImg = sourceImage as HImage;
                bool needDispose = false;
                try
                {
                    HOperatorSet.CountChannels(sourceImage, out HTuple channels);
                    if (channels.I == 3)
                    {
                        HOperatorSet.Rgb1ToGray(sourceImage, out HObject gray);
                        processImg = new HImage(gray);
                        needDispose = true;
                    }

                    HOperatorSet.Intensity(processImg, processImg, out HTuple mean, out HTuple dev);
                    HOperatorSet.MinMaxGray(processImg, processImg, 0, out HTuple min, out HTuple max, out HTuple range);
                    HOperatorSet.GrayHisto(processImg, processImg, out HTuple absHisto, out HTuple relHisto);

                    int[] histoArr = absHisto.ToIArr();
                    return (mean.D, dev.D, min.D, max.D, histoArr.ToList());
                }
                finally
                {
                    if (needDispose) processImg.Dispose();
                }
            }
            catch
            {
                return (0, 0, 0, 255, new List<int>());
            }
        }

        public bool Undo() => RunOnUi(() =>
        {
            if (_mainVm.UndoCommand?.CanExecute() != true) return false;
            _mainVm.UndoCommand.Execute();
            return true;
        });

        public bool Redo() => RunOnUi(() =>
        {
            if (_mainVm.RedoCommand?.CanExecute() != true) return false;
            _mainVm.RedoCommand.Execute();
            return true;
        });

        public bool SetContinuousRun(bool enabled) => RunOnUi(() =>
        {
            if (_mainVm.IsContinuousRunning == enabled) return false;
            _mainVm.ToggleContinuousRunCommand?.Execute();
            return true;
        });

        private T RunOnUi<T>(Func<T> func)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.Invoke(func);
            return func();
        }

        public void SelectNode(string nodeName)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var wf = _mainVm.ActiveWorkflow;
                if (wf == null) return;

                foreach (var n in wf.Nodes)
                {
                    bool match = string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase);
                    n.IsSelected = match;
                    if (match)
                    {
                        _mainVm.SelectedNode = n;
                    }
                }
            });
        }
    }
}
