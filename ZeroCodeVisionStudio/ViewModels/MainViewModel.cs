using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Data;
using OpenCvSharp;
using ZeroCodeVisionStudio.Services;
using ZeroCodeVisionStudio.Core;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Diagnostics;
using ZeroCodeVisionStudio.Shared.Data;
using ZeroCodeVisionStudio.Shared.Helper;
using ZeroCodeVisionStudio.UI.Controls;
using ZeroCodeVisionStudio.AI.Tools;
using ZeroCodeVisionStudio.AI.Copilot;
using ZeroCodeVisionStudio.AI.Mcp;
using ZeroCodeVisionStudio.ViewModels;
using HalconDotNet;
using Microsoft.Win32;

namespace ZeroCodeVisionStudio
{
    /// <summary>
    /// 主窗口 ViewModel。
    /// 负责协调算子工具箱、流程画布、节点连线、HALCON 图像显示、执行日志、输入来源绑定以及方案/多流程管理。
    /// </summary>
    public partial class MainViewModel : BindableBase, Services.WorkflowExecutionEngine.IExecutionEngineHost, Services.SchemeFileService.ISchemePersistenceHost
    {
        // 方案（Scheme）管理：一个方案可包含多个流程（Workflow）。
        private string schemeName = "未命名方案";
        private string schemeFilePath;
        private bool isSchemeModified;
        private bool isSchemeLoading;
        private string schemeLoadingMessage = "正在加载方案...";
        private string selectedThemeMode;
        private ThemeAccentOption selectedThemeAccent;
        private bool _toolboxStartExpanded = true;

        // 为运行节点生成稳定的显示名称，避免同类算子在流程中重名。
        private readonly SequenceNameGenerator SeqGenerator = new();

        /// <summary>最近打开的方案（文件菜单「最近打开」子菜单）。</summary>
        public ObservableCollection<SchemeMenuItem> RecentSchemes { get; } = new();

        /// <summary>示例方案（文件菜单「示例方案」子菜单，首次启动自动生成）。</summary>
        public ObservableCollection<SchemeMenuItem> DemoSchemes { get; } = new();
        private double workflowScale = 1.0;
        private readonly ObservableCollection<WorkflowOutputOption> selectedNodeDisplayOutputs = new();
        private readonly ObservableCollection<WorkflowOutputOption> allWorkflowDisplayOutputs = new();
        private WorkflowOutputOption selectedDisplayOutput;

        // 算子参数窗口中的预览也会触发图像显示事件。
        private bool isWorkflowNodeExecuting;
        private bool isSingleNodeExec;
        private bool isWorkflowExec;

        // ==================== 执行控制 ====================
        // 执行取消令牌：停止按钮 / AI stop_execution 工具通过它中止正在执行的流程。
        private CancellationTokenSource executionCts;
        // 连续运行模式：循环执行整个流程直到用户停止。
        private bool isContinuousRunning;
        private bool isSingleLoopRunning;
        // 任一类执行（单次/单节点/连续）进行中的互斥标志。
        private bool isExecutionInProgress;
        // 切换连续/停止的防并发门闩（防止快速连点产生多个 async void 实例互相踩状态）。
        private bool isToggleProcessing;

        /// <summary>单节点执行超时（秒）。超时视为失败并停止整个流程，避免僵尸任务与后续节点竞争 HALCON 资源。</summary>
        private int nodeExecutionTimeoutSeconds = 60;

        /// <summary>单节点执行超时（秒）：超时视为失败并中止整个流程。系统设置面板可改。</summary>
        public int NodeExecutionTimeoutSeconds
        {
            get => nodeExecutionTimeoutSeconds;
            set => SetProperty(ref nodeExecutionTimeoutSeconds, Math.Clamp(value, 5, 3600));
        }

        /// <summary>是否正在执行（单次/单节点/连续任一）。</summary>
        public bool IsExecuting
        {
            get => isExecutionInProgress;
            private set => SetProperty(ref isExecutionInProgress, value);
        }

        /// <summary>是否处于连续运行模式。</summary>
        public bool IsContinuousRunning
        {
            get => isContinuousRunning;
            private set
            {
                if (SetProperty(ref isContinuousRunning, value))
                {
                    RaisePropertyChanged(nameof(ContinuousRunButtonText));
                }
            }
        }

        /// <summary>连续运行按钮文本。</summary>
        public string ContinuousRunButtonText => IsContinuousRunning ? "停止连续" : "连续运行";

        /// <summary>是否处于单次循环（遍历缩略图一遍）模式。</summary>
        public bool IsSingleLoopRunning
        {
            get => isSingleLoopRunning;
            private set => SetProperty(ref isSingleLoopRunning, value);
        }

        public MainViewModel(IHandleControl handleControl)
        {
            if (TryPrepareHalconRuntime(out var halconRuntimeDirectory, out var halconUnavailableMessage))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                Environment.SetEnvironmentVariable(
                    "PATH",
                    halconRuntimeDirectory + Path.PathSeparator + currentPath,
                    EnvironmentVariableTarget.Process);

                HWindow = new HSmartWindowControlWPF
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch
                };
                HWindow.HInitWindow += HWindow_HInitWindow;
                HWindow.Loaded += HWindow_Loaded;
                HWindow.SizeChanged += HWindow_SizeChanged;
                HalconUnavailableMessage = string.Empty;
            }
            else
            {
                HWindow = null;
                HalconUnavailableMessage = halconUnavailableMessage;
                ExecutionLogService.App(
                    $"HALCON 图像控件未加载：{halconUnavailableMessage}",
                    ExecLogLevel.Warn);
            }

            // OpenCV probe 延迟到 MainWindow.Loaded 中执行（避免启动早期 native DLL 路径未就绪）。
            // 算子第一次执行时 EnsureProbed() 也会被自然触发。

            HandleControl = handleControl;

            // 工具箱按算子注册时声明的 HandleType 分组。
            HandleControl.HandleView.GroupDescriptions.Clear();
            HandleControl.HandleView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IHandleRegisterModel.HandleType)));

            // 初始化第一个默认流程。
            Workflows = new ObservableCollection<WorkflowViewModel>();
            AddWorkflowInternal("主流程");

            // 命令统一由 ViewModel 承接。
            HandleSectionChangedCommand = new DelegateCommand<object>(OnHandleDoubleClick);
            HandleEditCommand = new DelegateCommand<IHandleRunModel>(OnHandleEdit);
            HandleExecCommand = new DelegateCommand<IHandleRunModel>(OnHandleExec);
            DeleteNodeCommand = new DelegateCommand<IHandleRunModel>(DeleteNode);
            StartProcessCommand = new DelegateCommand(OnStartProcess);
            RunHandleClickCommand = new DelegateCommand<object>(OnRunHandleClick);
            AddNodeCommand = new DelegateCommand<WorkflowNodeCreateRequest>(OnAddNode);
            MoveNodeCommand = new DelegateCommand<WorkflowNodeMoveRequest>(OnMoveNode);
            AddConnectionCommand = new DelegateCommand<WorkflowConnectionRequest>(OnAddConnection);
            SelectConnectionCommand = new DelegateCommand<WorkflowConnection>(SelectConnection);
            SelectNodeCommand = new DelegateCommand<IHandleRunModel>(SelectNode);
            DeleteConnectionCommand = new DelegateCommand<WorkflowConnection>(DeleteConnection);
            DeleteSelectedConnectionCommand = new DelegateCommand(DeleteSelectedConnection);
            AddDisplaySourceCommand = new DelegateCommand<WorkflowOutputOption>(AddDisplaySource);
            RemoveDisplaySourceCommand = new DelegateCommand<DisplaySourceItem>(RemoveDisplaySource);
            MoveDisplaySourceUpCommand = new DelegateCommand<DisplaySourceItem>(MoveDisplaySourceUp);
            MoveDisplaySourceDownCommand = new DelegateCommand<DisplaySourceItem>(MoveDisplaySourceDown);
            ClearInputBindingCommand = new DelegateCommand<WorkflowInputBindingEditorItem>(ClearInputBinding);
            SetInputBindingCommand = new DelegateCommand<WorkflowInputBindingRequest>(SetInputBinding);
            ZoomInCommand = new DelegateCommand(() => WorkflowScale = Math.Min(2.5, WorkflowScale + 0.1));
            ZoomOutCommand = new DelegateCommand(() => WorkflowScale = Math.Max(0.4, WorkflowScale - 0.1));
            ZoomResetCommand = new DelegateCommand(() => WorkflowScale = 1.0);

            // 执行控制命令。
            StopExecutionCommand = new DelegateCommand(() => RequestStopExecution());
            ExecuteToSelectedNodeCommand = new DelegateCommand(ExecuteToSelectedNode,
                () => SelectedNode != null && !IsExecuting);
            ToggleContinuousRunCommand = new DelegateCommand(async () => await ToggleContinuousRunAsync());
            ToggleSingleLoopCommand = new DelegateCommand(async () => await ToggleSingleLoopAsync());

            // 缩略图面板命令。
            ToggleThumbnailPanelCommand = new DelegateCommand(() => ThumbnailPanelExpanded = !ThumbnailPanelExpanded);
            SelectThumbnailCommand = new DelegateCommand<ThumbnailItem>(t => SelectedThumbnail = t);
            SelectPrevThumbnailCommand = new DelegateCommand(SelectPrevThumbnail, () => Thumbnails.Count > 0);
            SelectNextThumbnailCommand = new DelegateCommand(SelectNextThumbnail, () => Thumbnails.Count > 0);
            SelectFirstThumbnailCommand = new DelegateCommand(SelectFirstThumbnail, () => Thumbnails.Count > 0);
            SelectLastThumbnailCommand = new DelegateCommand(SelectLastThumbnail, () => Thumbnails.Count > 0);

            // 竖向工具栏 → 下方 Tab 面板切换命令。
            SelectInfoTab0Command = new DelegateCommand(() => { SelectedInfoTab = 0; OpenToolbarPanel("模块结果"); });
            SelectInfoTab1Command = new DelegateCommand(() => { SelectedInfoTab = 1; OpenToolbarPanel("输入绑定"); });
            SelectInfoTab2Command = new DelegateCommand(() => OpenToolbarPanel("流程日志"));
            SelectInfoTab3Command = new DelegateCommand(() => OpenToolbarPanel("参数配置"));
            SelectInfoTab4Command = new DelegateCommand(() => OpenToolbarPanel("调试监控"));
            ToggleToolbarPanelCommand = new DelegateCommand(() => ToolbarPanelExpanded = !ToolbarPanelExpanded);

            // 图像缩放命令（画布右上角工具栏 + 鼠标滚轮）
            ImageZoomInCommand = new DelegateCommand(() => DisplayScale = Math.Min(DisplayScale * 1.25, 16.0));
            ImageZoomOutCommand = new DelegateCommand(() => DisplayScale = Math.Max(DisplayScale / 1.25, 0.05));
            ImageZoomFitCommand = new DelegateCommand(() =>
            {
                // Fit 由 MainWindow code-behind 根据图像和容器尺寸计算，ViewModel 只发出请求
                ZoomFitRequested?.Invoke(this, EventArgs.Empty);
            });
            ImageZoomActualCommand = new DelegateCommand(() => DisplayScale = 1.0);

            // 画布自动布局。
            AutoLayoutCommand = new DelegateCommand(AutoLayoutWorkflowNodes);

            // 节点级执行条件（右键菜单）。
            SetNodeConditionCommand = new DelegateCommand<IHandleRunModel>(OnSetNodeCondition);
            InitializeSchemeManagement();

            // 参数对话框内嵌绑定编辑区：候选与变更通知走环境上下文（模态期间注入）。
            ParameterBindingAmbient.GetCandidates = BuildBindingCandidates;
            ParameterBindingAmbient.OnBindingChanged = node =>
            {
                MarkSchemeModified();
                RefreshSelectedInputBindings();
            };

            // 撤销/重做命令。
            UndoCommand = new DelegateCommand(Undo, () => undoStack.Count > 0);
            RedoCommand = new DelegateCommand(Redo, () => redoStack.Count > 0);

            // 方案管理命令。
            NewSchemeCommand = new DelegateCommand(NewScheme);
            OpenSchemeCommand = new DelegateCommand(OpenScheme);
            OpenSchemePathCommand = new DelegateCommand<string>(async path => await OpenSchemeFileAsync(path));
            SaveSchemeCommand = new DelegateCommand(SaveScheme);
            SaveSchemeAsCommand = new DelegateCommand(async () => await SaveSchemeAsAsync());
            DeleteSchemeCommand = new DelegateCommand(DeleteScheme);
            RenameSchemeCommand = new DelegateCommand(RenameScheme);
            ManageSchemesCommand = new DelegateCommand(ManageSchemes);

            // 流程管理命令。
            AddWorkflowCommand = new DelegateCommand(AddWorkflow);
            RemoveWorkflowCommand = new DelegateCommand(RemoveWorkflow);
            SwitchWorkflowCommand = new DelegateCommand<WorkflowViewModel>(SwitchWorkflow);
            RenameWorkflowCommand = new DelegateCommand<WorkflowViewModel>(RenameWorkflow);

            // 节点重命名命令。
            NodeRenameCommand = new DelegateCommand<IHandleRunModel>(RenameNode);

            ThemeModes = new ObservableCollection<string>(AppThemeManager.ThemeModes);
            ThemeAccents = new ObservableCollection<ThemeAccentOption>(AppThemeManager.ThemeAccents);
            selectedThemeMode = AppThemeManager.CurrentMode;
            selectedThemeAccent = ThemeAccents.FirstOrDefault(x => x.Name == AppThemeManager.CurrentAccent) ?? ThemeAccents.FirstOrDefault();

            // 工作区设置：工具箱启动状态（持久化在 %APPDATA%/ZeroCodeVisionStudio/workspace.json）
            _toolboxStartExpanded = AppWorkspaceSettings.ToolboxStartExpanded;

            // ==================== 通讯驱动联动 ====================
            // 启动时自动连接标记 AutoConnect 的驱动（TCP/串口/MQTT 等）。
            Shared.Communication.CommunicationCenter.AutoConnectAll();
            // 接收触发：驱动收到数据 → 触发指定流程执行（PLC 给信号→检测→回写 工位节拍）。
            Shared.Communication.CommunicationCenter.TrafficReported += OnCommTrafficForTrigger;

            var studioContext = new VisionStudioContextAdapter(this);
            var automationService = new VisionStudioAutomationService(studioContext);
            Copilot = new VisionCopilotViewModel(automationService);
            Copilot.RequestClose = () => IsCopilotOpen = false;
            ToggleCopilotCommand = new DelegateCommand(() => IsCopilotOpen = !IsCopilotOpen);

            ExecutionEngine = new Services.WorkflowExecutionEngine(this);
            SchemeFile = new Services.SchemeFileService(this);
        }

        // ==================== HALCON 窗口管理 ====================

        private HWindow hWindow;
        private string displayOverlayText;
        private string displayOverlayForeground = "#F8FAFC";
        private string displayOverlayBackground = "#B30F172A";

        public bool IsHalconAvailable => HWindow != null;

        public string HalconUnavailableMessage { get; }

        private System.Windows.Media.Imaging.BitmapSource displayBitmap;
        /// <summary>
        /// OpenCV 图像降级显示源（Halcon runtime 不可用时由 MatBitmapSourceConverter 填充，WPF Image 控件直接绑定）。
        /// </summary>
        public System.Windows.Media.Imaging.BitmapSource DisplayBitmap
        {
            get => displayBitmap;
            private set => SetProperty(ref displayBitmap, value);
        }

        // ===================== 图像缩略图面板 =====================

        /// <summary>当前方案中所有"读取图像"节点的图像列表合并后的缩略图集合。</summary>
        public ObservableCollection<ThumbnailItem> Thumbnails { get; } = new();

        private ThumbnailItem _selectedThumbnail;
        /// <summary>缩略图面板里当前高亮的那张。</summary>
        public ThumbnailItem SelectedThumbnail
        {
            get => _selectedThumbnail;
            set
            {
                if (SetProperty(ref _selectedThumbnail, value) && value != null)
                {
                    // 点缩略图 → 立刻把这张加载到画布
                    var full = ThumbnailItem.CreateFull(value.FilePath);
                    if (full != null) DisplayBitmap = full;

                    // 更新所有缩略图的选中高亮
                    foreach (var t in Thumbnails) t.IsSelected = (t == value);

                    // 同步当前序号
                    SelectedThumbnailIndex = Thumbnails.IndexOf(value) + 1;

                    // 同步回工作流里第一个 ReadImage 节点的 CurrentIndex
                    SyncThumbnailIndexToWorkflow(value.Index);
                }
            }
        }

        /// <summary>缩略图面板是否有内容（XAML 里 Visibility 绑定用）。</summary>
        public bool HasThumbnails => Thumbnails.Count > 0;

        private bool _thumbnailPanelExpanded = false;
        /// <summary>缩略图面板是否展开（底部缩略图条可见）。</summary>
        public bool ThumbnailPanelExpanded
        {
            get => _thumbnailPanelExpanded;
            set => SetProperty(ref _thumbnailPanelExpanded, value);
        }

        private int _selectedThumbnailIndex;
        /// <summary>当前选中的缩略图序号（从 1 开始，0 = 未选中）。</summary>
        public int SelectedThumbnailIndex
        {
            get => _selectedThumbnailIndex;
            private set => SetProperty(ref _selectedThumbnailIndex, value);
        }

        public DelegateCommand ToggleThumbnailPanelCommand { get; }
        public DelegateCommand SelectPrevThumbnailCommand { get; }
        public DelegateCommand SelectNextThumbnailCommand { get; }
        public DelegateCommand SelectFirstThumbnailCommand { get; }
        public DelegateCommand SelectLastThumbnailCommand { get; }
        public DelegateCommand<ThumbnailItem> SelectThumbnailCommand { get; }

        /// <summary>下方信息面板（模块结果/输入绑定）当前选中 Tab 索引。</summary>
        public int SelectedInfoTab
        {
            get => _selectedInfoTab;
            set => SetProperty(ref _selectedInfoTab, value);
        }
        private int _selectedInfoTab;

        public DelegateCommand SelectInfoTab0Command { get; }
        public DelegateCommand SelectInfoTab1Command { get; }
        public DelegateCommand SelectInfoTab2Command { get; }
        public DelegateCommand SelectInfoTab3Command { get; }
        public DelegateCommand SelectInfoTab4Command { get; }
        public DelegateCommand ToggleToolbarPanelCommand { get; }

        // ====== 图像缩放 ======
        public DelegateCommand ImageZoomInCommand { get; }
        public DelegateCommand ImageZoomOutCommand { get; }
        public DelegateCommand ImageZoomFitCommand { get; }
        public DelegateCommand ImageZoomActualCommand { get; }

        /// <summary>请求 Fit 的事件（由 MainWindow code-behind 订阅，因为需要访问 Viewport 尺寸）。</summary>
        public event EventHandler ZoomFitRequested;

        private double _displayScale = 1.0;
        /// <summary>当前图像缩放比例（1.0 = 100% 实际大小）。</summary>
        public double DisplayScale
        {
            get => _displayScale;
            set { if (SetProperty(ref _displayScale, value)) RaisePropertyChanged(nameof(DisplayScalePercent)); }
        }

        /// <summary>缩放百分比字符串（如 "100%"、"125%"），供工具栏显示。</summary>
        public string DisplayScalePercent => $"{(int)(DisplayScale * 100)}%";
        // ====== END 图像缩放 ======

        private bool _toolbarPanelExpanded;
        /// <summary>竖向工具栏右侧信息面板是否展开。</summary>
        public bool ToolbarPanelExpanded
        {
            get => _toolbarPanelExpanded;
            set { if (SetProperty(ref _toolbarPanelExpanded, value)) RaisePropertyChanged(nameof(ToolbarArrowText)); }
        }

        /// <summary>竖栏底部折叠箭头文本（展开=◀，折叠=▶）。</summary>
        public string ToolbarArrowText => ToolbarPanelExpanded ? "◀" : "▶";

        private string _activeToolbarPanelTitle = "模块结果";
        /// <summary>右侧信息面板当前激活的标题。</summary>
        public string ActiveToolbarPanelTitle
        {
            get => _activeToolbarPanelTitle;
            set => SetProperty(ref _activeToolbarPanelTitle, value);
        }

        /// <summary>点击竖栏按钮时：已开+同按钮 → 关闭；已开+不同按钮 → 切内容；未开 → 打开。</summary>
        private void OpenToolbarPanel(string title)
        {
            if (ToolbarPanelExpanded && ActiveToolbarPanelTitle == title)
            {
                // 同一个按钮再次点击 → 关闭浮窗
                ToolbarPanelExpanded = false;
            }
            else
            {
                ActiveToolbarPanelTitle = title;
                ToolbarPanelExpanded = true;
            }
        }

        /// <summary>
        /// 扫描当前方案所有工作流的 ReadImage 节点，合并它们的 ImageFiles 到 Thumbnails 集合。
        /// 在方案加载 / 节点参数变更后调用。
        /// </summary>
        public void RefreshThumbnails()
        {
            Thumbnails.Clear();
            int globalIdx = 0;
            foreach (var wf in Workflows)
            {
                foreach (var node in wf.Nodes)
                {
                    var rap = node?.Handle?.HandleParameter as Plugin.Handle.ReadImage.ReadImageParameter;
                    if (rap?.ImageFiles == null) continue;

                    foreach (var file in rap.ImageFiles)
                    {
                        if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file)) continue;
                        var thumb = ThumbnailItem.Create(file, globalIdx);
                        if (thumb != null) Thumbnails.Add(thumb);
                        globalIdx++;
                    }
                }
            }

            RaisePropertyChanged(nameof(HasThumbnails));
            SelectPrevThumbnailCommand.RaiseCanExecuteChanged();
            SelectNextThumbnailCommand.RaiseCanExecuteChanged();
            SelectFirstThumbnailCommand.RaiseCanExecuteChanged();
            SelectLastThumbnailCommand.RaiseCanExecuteChanged();
        }

        private async void SelectPrevThumbnail()
        {
            if (Thumbnails.Count == 0) return;
            var idx = SelectedThumbnail != null ? Thumbnails.IndexOf(SelectedThumbnail) : 0;
            idx = (idx - 1 + Thumbnails.Count) % Thumbnails.Count;
            SelectedThumbnail = Thumbnails[idx];
            // 切换后自动执行流程（SelectedThumbnail setter 已同步 ReadImage 索引）
            if (!IsExecuting)
                await ExecutionEngine.ExecuteWorkflowCoreAsync();
        }

        private async void SelectNextThumbnail()
        {
            if (Thumbnails.Count == 0) return;
            var idx = SelectedThumbnail != null ? Thumbnails.IndexOf(SelectedThumbnail) : -1;
            idx = (idx + 1) % Thumbnails.Count;
            SelectedThumbnail = Thumbnails[idx];
            if (!IsExecuting)
                await ExecutionEngine.ExecuteWorkflowCoreAsync();
        }

        private async void SelectFirstThumbnail()
        {
            if (Thumbnails.Count == 0) return;
            SelectedThumbnail = Thumbnails[0];
            if (!IsExecuting)
                await ExecutionEngine.ExecuteWorkflowCoreAsync();
        }

        private async void SelectLastThumbnail()
        {
            if (Thumbnails.Count == 0) return;
            SelectedThumbnail = Thumbnails[Thumbnails.Count - 1];
            if (!IsExecuting)
                await ExecutionEngine.ExecuteWorkflowCoreAsync();
        }

        private void SyncThumbnailIndexToWorkflow(int index)
        {
            foreach (var wf in Workflows)
            {
                foreach (var node in wf.Nodes)
                {
                    var rap = node?.Handle?.HandleParameter as Plugin.Handle.ReadImage.ReadImageParameter;
                    if (rap != null)
                    {
                        rap.CurrentIndex = Math.Clamp(index, 0, Math.Max(0, rap.ImageFiles.Count - 1));
                        return; // 只同步第一个 ReadImage 节点
                    }
                }
            }
        }

        private static bool TryPrepareHalconRuntime(out string runtimeDirectory, out string errorMessage)
        {
            const string requiredEntryPoint = "HLIIsUTF8Encoding";
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var halconRoot = Environment.GetEnvironmentVariable("HALCONROOT");
            var halconArch = Environment.GetEnvironmentVariable("HALCONARCH");

            AddCandidate(halconRoot);
            if (!string.IsNullOrWhiteSpace(halconRoot) && !string.IsNullOrWhiteSpace(halconArch))
            {
                AddCandidate(Path.Combine(halconRoot, "bin", halconArch));
            }

            foreach (var programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                     })
            {
                var mvtecDirectory = Path.Combine(programFiles, "MVTec");
                if (!Directory.Exists(mvtecDirectory))
                {
                    continue;
                }

                foreach (var installationDirectory in Directory.EnumerateDirectories(mvtecDirectory))
                {
                    AddCandidate(installationDirectory);
                    if (!string.IsNullOrWhiteSpace(halconArch))
                    {
                        AddCandidate(Path.Combine(installationDirectory, "bin", halconArch));
                    }
                }
            }

            foreach (var pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                AddCandidate(pathEntry.Trim('"'));
            }

            var foundLibrary = false;
            foreach (var directory in candidates)
            {
                var libraryPath = Path.Combine(directory, "halcon.dll");
                if (!File.Exists(libraryPath))
                {
                    continue;
                }

                foundLibrary = true;
                if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(libraryPath, out var handle))
                {
                    continue;
                }

                try
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryGetExport(handle, requiredEntryPoint, out _))
                    {
                        runtimeDirectory = directory;
                        errorMessage = null;
                        return true;
                    }
                }
                finally
                {
                    System.Runtime.InteropServices.NativeLibrary.Free(handle);
                }
            }

            runtimeDirectory = null;
            errorMessage = foundLibrary
                ? $"找到 HALCON 原生库，但版本与当前 halcondotnet.dll 不兼容（缺少入口 {requiredEntryPoint}）。"
                : "未找到可用的 HALCON 原生库。请安装与项目 halcondotnet.dll 版本匹配的 HALCON x64 运行时。";
            return false;

            void AddCandidate(string directory)
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    candidates.Add(Path.GetFullPath(directory));
                }
            }
        }

        private void HWindow_Loaded(object sender, System.Windows.RoutedEventArgs e) => EnsureHalconWindowAsync();

        private void HWindow_HInitWindow(object sender, EventArgs e)
        {
            hWindow = HWindow.HalconWindow;
            RedisplayCurrentImage();
        }

        private void HWindow_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e) => RedisplayCurrentImage();

        private void OnRunHandleClick(object obj)
        {
            if (obj is IHandleRunModel model)
                DisplayNodeResult(model);
        }

        // ==================== 方案/流程核心属性 ====================

        /// <summary>算子注册中心，提供工具箱数据源。</summary>
        public IHandleControl HandleControl { get; }

        /// <summary>所有流程的集合（每个流程包含独立节点、连线/记录）。</summary>
        public ObservableCollection<WorkflowViewModel> Workflows { get; }

        /// <summary>当前激活的流程（支持 ListBox SelectedItem 双向绑定）。</summary>
        public WorkflowViewModel ActiveWorkflow
        {
            get => Workflows.Count > 0 && activeWorkflowIndex >= 0 && activeWorkflowIndex < Workflows.Count
                ? Workflows[activeWorkflowIndex]
                : null;
            set
            {
                if (value != null)
                {
                    var idx = Workflows.IndexOf(value);
                    if (idx >= 0)
                        ActiveWorkflowIndex = idx;
                }
            }
        }

        private int activeWorkflowIndex;

        /// <summary>当前激活流程的索引。</summary>
        public int ActiveWorkflowIndex
        {
            get => activeWorkflowIndex;
            set
            {
                if (value >= 0 && value < Workflows.Count && SetProperty(ref activeWorkflowIndex, value))
                {
                    RaisePropertyChanged(nameof(ActiveWorkflow));
                    RaisePropertyChanged(nameof(Nodes));
                    RaisePropertyChanged(nameof(Connections));
                    RaisePropertyChanged(nameof(ExecutionRecords));
                    RaisePropertyChanged(nameof(SelectedExecutionRecord));
                    RaisePropertyChanged(nameof(DisplaySources));
                    RaisePropertyChanged(nameof(SelectedNodeDisplayOutputs));
                    RaisePropertyChanged(nameof(SelectedInputBindings));
                    RaisePropertyChanged(nameof(SelectedNode));
                    RaisePropertyChanged(nameof(SelectedConnection));
                    RaisePropertyChanged(nameof(SelectedDisplaySource));
                    RaisePropertyChanged(nameof(SelectedDisplayOutput));
                    RefreshAllWorkflowDisplayOutputs();
                    SelectedExecutionRecord = ActiveWorkflow?.ExecutionRecords?.LastOrDefault();
                    UpdateExecutionResultForNode(ActiveWorkflow?.SelectedNode);
                    MarkSchemeModified();
                }
            }
        }

        /// <summary>方案名称（标题栏显示）。</summary>
        public string SchemeName
        {
            get => schemeName;
            set
            {
                if (SetProperty(ref schemeName, value))
                    MarkSchemeModified();
            }
        }

        /// <summary>方案标题（含修改标记）。只显示方案文件名，不显示完整路径。</summary>
        public string SchemeTitle
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(schemeName) ? "未命名方案" : schemeName;
                return $"{(isSchemeModified ? "* " : string.Empty)}{name}";
            }
        }

        /// <summary>方案文件路径（未保存时为 null）。</summary>
        public string SchemeFilePath
        {
            get => schemeFilePath;
            private set
            {
                if (SetProperty(ref schemeFilePath, value))
                    RaisePropertyChanged(nameof(SchemeTitle));
            }
        }

        public ObservableCollection<string> ThemeModes { get; }

        public ObservableCollection<ThemeAccentOption> ThemeAccents { get; }

        public string SelectedThemeMode
        {
            get => selectedThemeMode;
            set
            {
                if (SetProperty(ref selectedThemeMode, value))
                    AppThemeManager.SetTheme(selectedThemeMode, selectedThemeAccent?.Name);
            }
        }

        public ThemeAccentOption SelectedThemeAccent
        {
            get => selectedThemeAccent;
            set
            {
                if (SetProperty(ref selectedThemeAccent, value))
                    AppThemeManager.SetTheme(selectedThemeMode, selectedThemeAccent?.Name);
            }
        }

        /// <summary>
        /// 启动时左侧工具箱是否展开（持久化到 %APPDATA%/ZeroCodeVisionStudio/workspace.json）。
        /// 在系统设置窗口修改后立即保存，下次启动生效。
        /// </summary>
        public bool ToolboxStartExpanded
        {
            get => _toolboxStartExpanded;
            set
            {
                if (SetProperty(ref _toolboxStartExpanded, value))
                    AppWorkspaceSettings.SetToolboxStartExpanded(value);
            }
        }

        public bool IsSchemeLoading
        {
            get => isSchemeLoading;
            private set => SetProperty(ref isSchemeLoading, value);
        }

        public string SchemeLoadingMessage
        {
            get => schemeLoadingMessage;
            private set => SetProperty(ref schemeLoadingMessage, value);
        }

        private bool isCopilotOpen;

        public bool IsCopilotOpen
        {
            get => isCopilotOpen;
            set => SetProperty(ref isCopilotOpen, value);
        }

        public VisionCopilotViewModel Copilot { get; }

        /// <summary>工作流执行引擎（执行核心/条件求值/输入装配已拆至 Services.WorkflowExecutionEngine）。</summary>
        internal Services.WorkflowExecutionEngine ExecutionEngine { get; }

        /// <summary>方案文件服务（保存/加载已拆至 Services.SchemeFileService）。</summary>
        internal Services.SchemeFileService SchemeFile { get; private set; }

        /// <summary>
        /// UI 集合保护：引擎回调可能来自线程池延续（异步上下文丢失路径），
        /// 所有触碰 ObservableCollection（执行记录/显示源/绑定面板）的宿主回调统一调度到 UI 线程。
        /// 已在 UI 线程时同步执行，零开销。
        /// </summary>
        private static T OnUiForCollections<T>(Func<T> action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) return action();
            return dispatcher.Invoke(action);
        }

        private static void OnUiForCollections(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        WorkflowExecutionRecord Services.WorkflowExecutionEngine.IExecutionEngineHost.SelectedExecutionRecord
        {
            get => SelectedExecutionRecord;
            set => OnUiForCollections(() => { SelectedExecutionRecord = value; });
        }

        bool Services.WorkflowExecutionEngine.IExecutionEngineHost.IsExecuting
        {
            get => IsExecuting;
            set => IsExecuting = value;
        }

        string Services.WorkflowExecutionEngine.IExecutionEngineHost.ResultMessage
        {
            get => ResultMessage;
            set => ResultMessage = value;
        }


        RunParameter Services.WorkflowExecutionEngine.IExecutionEngineHost.BuildConnectedInputParameter(
            IHandleRunModel node, Dictionary<object, object> clonedValues) =>
            BuildConnectedInputParameter(node, clonedValues);

        WorkflowExecutionRecord Services.WorkflowExecutionEngine.IExecutionEngineHost.AddExecutionRecord(
            IHandleRunModel node, HandleStatus status, string resultData) =>
            OnUiForCollections(() => AddExecutionRecord(node, status, resultData));

        void Services.WorkflowExecutionEngine.IExecutionEngineHost.ShowLastExecutedNodeOutput(
            IReadOnlyList<IHandleRunModel> executionOrder) =>
            OnUiForCollections(() => ShowLastExecutedNodeOutput(executionOrder));

        void Services.WorkflowExecutionEngine.IExecutionEngineHost.RefreshSelectedInputBindings() =>
            OnUiForCollections(RefreshSelectedInputBindings);

        void Services.WorkflowExecutionEngine.IExecutionEngineHost.RefreshAllWorkflowDisplayOutputs() =>
            OnUiForCollections(RefreshAllWorkflowDisplayOutputs);

        void Services.WorkflowExecutionEngine.IExecutionEngineHost.CaptureDisplayOutputs(
            IHandleRunModel node, bool append) =>
            OnUiForCollections(() => CaptureDisplayOutputs(node, append));

        // ==================== 方案文件服务宿主实现 ====================

        IHandleRunModel Services.SchemeFileService.ISchemePersistenceHost.CreateNode(
            IHandleRegisterModel registerModel, string displayName, double x, double y)
        {
            var runModel = new ZeroCodeVisionStudio.Core.HandleRunModel(registerModel, SeqGenerator.Generate(displayName))
            {
                X = x,
                Y = y
            };
            runModel.DisplayName = displayName;
            return runModel;
        }

        IHandleRegisterModel Services.SchemeFileService.ISchemePersistenceHost.GetRegister(string key) =>
            HandleControl.GetHandle(key);

        SequenceNameGenerator Services.SchemeFileService.ISchemePersistenceHost.GetSequenceGenerator(WorkflowViewModel owner) =>
            SeqGenerator;

        void Services.SchemeFileService.ISchemePersistenceHost.OnNodeAdded(IHandleRunModel node, WorkflowViewModel owner)
        {
            node.Handle.OnImageDisplay += OnHandleDisplayEvent;
        }

        void Services.SchemeFileService.ISchemePersistenceHost.OnSchemeApplied(
            Shared.Data.SchemeData data, string filePath, string loadingReport)
        {
            schemeFilePath = filePath;
            if (!string.IsNullOrWhiteSpace(data?.Name))
            {
                schemeName = data.Name;
            }
            ClearSchemeModified();
            RefreshAllWorkflowDisplayOutputs();
            RaisePropertyChanged(nameof(SchemeName));
            RaisePropertyChanged(nameof(SchemeTitle));
            RaisePropertyChanged(nameof(SchemeFilePath));
        }


        public DelegateCommand ToggleCopilotCommand { get; }

        private void MarkSchemeModified()
        {
            if (!isSchemeModified)
            {
                isSchemeModified = true;
                RaisePropertyChanged(nameof(SchemeTitle));
            }
        }

        private void ClearSchemeModified()
        {
            if (isSchemeModified)
            {
                isSchemeModified = false;
                RaisePropertyChanged(nameof(SchemeTitle));
            }
        }

        // ==================== 代理属性：指向 ActiveWorkflow ====================

        /// <summary>画布上的运行节点集合。</summary>
        public ObservableCollection<IHandleRunModel> Nodes => ActiveWorkflow?.Nodes;

        /// <summary>画布上的可见连线集合。</summary>
        public ObservableCollection<WorkflowConnection> Connections => ActiveWorkflow?.Connections;

        /// <summary>右侧"模块结果"表格的数据源。</summary>
        public ObservableCollection<WorkflowExecutionRecord> ExecutionRecords => ActiveWorkflow?.ExecutionRecords;

        private WorkflowExecutionRecord selectedExecutionRecord;

        /// <summary>模块结果表格中当前选中的执行记录。</summary>
        public WorkflowExecutionRecord SelectedExecutionRecord
        {
            get => selectedExecutionRecord;
            set
            {
                if (SetProperty(ref selectedExecutionRecord, value))
                {
                    // 在非流程连续运行期间，若用户在表格中直接点击某行记录，同步在上方显示该记录结果
                    if (value != null && !isWorkflowExec)
                    {
                        var msg = $"[{value.Module}] 状态：{value.StatusText}  耗时：{value.ElapsedText}";
                        if (!string.IsNullOrWhiteSpace(value.ResultData))
                        {
                            msg += $"{Environment.NewLine}{value.ResultData}";
                        }
                        ResultMessage = msg;
                    }
                }
            }
        }

        public ObservableCollection<DisplaySourceItem> DisplaySources => ActiveWorkflow?.DisplaySources;

        public ObservableCollection<WorkflowOutputOption> SelectedNodeDisplayOutputs => selectedNodeDisplayOutputs;

        public ObservableCollection<WorkflowOutputOption> AllWorkflowDisplayOutputs => allWorkflowDisplayOutputs;

        /// <summary>当前选中节点的输入来源编辑项。</summary>
        public ObservableCollection<WorkflowInputBindingEditorItem> SelectedInputBindings => ActiveWorkflow?.SelectedInputBindings;

        /// <summary>当前选中的节点。</summary>
        public IHandleRunModel SelectedNode
        {
            get => ActiveWorkflow?.SelectedNode;
            internal set
            {
                if (ActiveWorkflow != null && ActiveWorkflow.SelectedNode != value)
                {
                    ActiveWorkflow.SelectedNode = value;
                    RaisePropertyChanged();
                    RefreshSelectedInputBindings();
                    RefreshSelectedNodeDisplayOutputs();
                    
                    // 显示选中节点的输出图像
                    DisplaySelectedNodeOutput(value);

                    // 联动显示选中节点的执行结果，并定位表格对应记录
                    UpdateExecutionResultForNode(value);
                }
            }
        }

        /// <summary>当前选中的连线。</summary>
        public WorkflowConnection SelectedConnection
        {
            get => ActiveWorkflow?.SelectedConnection;
            private set
            {
                if (ActiveWorkflow != null)
                {
                    if (ActiveWorkflow.SelectedConnection != null)
                        ActiveWorkflow.SelectedConnection.IsSelected = false;
                    ActiveWorkflow.SelectedConnection = value;
                    if (value != null)
                        value.IsSelected = true;
                    RaisePropertyChanged();
                }
            }
        }

        public DisplaySourceItem SelectedDisplaySource
        {
            get => ActiveWorkflow?.SelectedDisplaySource;
            set
            {
                if (ActiveWorkflow != null && ActiveWorkflow.SelectedDisplaySource != value)
                {
                    ActiveWorkflow.SelectedDisplaySource = value;
                    RaisePropertyChanged();
                }
            }
        }

        public WorkflowOutputOption SelectedDisplayOutput
        {
            get => selectedDisplayOutput;
            set => SetProperty(ref selectedDisplayOutput, value);
        }

        /// <summary>流程画布缩放比例。</summary>
        public double WorkflowScale { get => workflowScale; set => SetProperty(ref workflowScale, value); }

        /// <summary>顶部状态栏消息。</summary>
        public string ResultMessage { get => field; internal set => SetProperty(ref field, value); }

        public ICollectionView HandleInstanceView => CollectionViewSource.GetDefaultView(Nodes);
        public HSmartWindowControlWPF HWindow { get; private set; }

        // ==================== 显示叠加层 ====================

        public string DisplayOverlayText
        {
            get => displayOverlayText;
            private set
            {
                if (SetProperty(ref displayOverlayText, value))
                    RaisePropertyChanged(nameof(HasDisplayOverlay));
            }
        }

        public string DisplayOverlayForeground { get => displayOverlayForeground; private set => SetProperty(ref displayOverlayForeground, value); }
        public string DisplayOverlayBackground { get => displayOverlayBackground; private set => SetProperty(ref displayOverlayBackground, value); }
        public bool HasDisplayOverlay => !string.IsNullOrWhiteSpace(DisplayOverlayText);

        // ==================== 命令声明 ====================

        public DelegateCommand<IHandleRunModel> DeleteNodeCommand { get; }
        public DelegateCommand<IHandleRunModel> HandleEditCommand { get; }
        public DelegateCommand<IHandleRunModel> HandleExecCommand { get; }
        public DelegateCommand<object> RunHandleClickCommand { get; }
        public DelegateCommand<WorkflowNodeCreateRequest> AddNodeCommand { get; }
        public DelegateCommand<WorkflowNodeMoveRequest> MoveNodeCommand { get; }
        public DelegateCommand<WorkflowConnectionRequest> AddConnectionCommand { get; }
        public DelegateCommand<WorkflowConnection> SelectConnectionCommand { get; }
        public DelegateCommand<IHandleRunModel> SelectNodeCommand { get; }
        public DelegateCommand<WorkflowConnection> DeleteConnectionCommand { get; }
        public DelegateCommand DeleteSelectedConnectionCommand { get; }
        public DelegateCommand<WorkflowOutputOption> AddDisplaySourceCommand { get; }
        public DelegateCommand<DisplaySourceItem> RemoveDisplaySourceCommand { get; }
        public DelegateCommand<DisplaySourceItem> MoveDisplaySourceUpCommand { get; }
        public DelegateCommand<DisplaySourceItem> MoveDisplaySourceDownCommand { get; }
        public DelegateCommand<WorkflowInputBindingEditorItem> ClearInputBindingCommand { get; }
        public DelegateCommand<WorkflowInputBindingRequest> SetInputBindingCommand { get; }
        public DelegateCommand ZoomInCommand { get; }
        public DelegateCommand ZoomOutCommand { get; }
        public DelegateCommand ZoomResetCommand { get; }
        public DelegateCommand<object> HandleSectionChangedCommand { get; }
        public DelegateCommand StartProcessCommand { get; set; }
        public DelegateCommand StopExecutionCommand { get; }
        public DelegateCommand ExecuteToSelectedNodeCommand { get; }
        public DelegateCommand ToggleContinuousRunCommand { get; }
        public DelegateCommand ToggleSingleLoopCommand { get; }
        public DelegateCommand AutoLayoutCommand { get; }
        public DelegateCommand UndoCommand { get; }
        public DelegateCommand RedoCommand { get; }
        public DelegateCommand NewSchemeCommand { get; }
        public DelegateCommand OpenSchemeCommand { get; }
        public DelegateCommand<string> OpenSchemePathCommand { get; }
        public DelegateCommand SaveSchemeCommand { get; }
        public DelegateCommand SaveSchemeAsCommand { get; }
        public DelegateCommand DeleteSchemeCommand { get; }
        public DelegateCommand RenameSchemeCommand { get; }
        public DelegateCommand ManageSchemesCommand { get; }
        public DelegateCommand AddWorkflowCommand { get; }
        public DelegateCommand RemoveWorkflowCommand { get; }
        public DelegateCommand<WorkflowViewModel> SwitchWorkflowCommand { get; }
        public DelegateCommand<WorkflowViewModel> RenameWorkflowCommand { get; }
        public DelegateCommand<IHandleRunModel> NodeRenameCommand { get; }

        // ==================== 方案管理 ====================

        /// <summary>新建一个空白方案。</summary>
        private async void NewScheme()
        {
            if (isSchemeModified)
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                var result = owner == null
                    ? System.Windows.MessageBox.Show("当前方案尚未保存，是否保存？", "新建方案",
                        System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question)
                    : System.Windows.MessageBox.Show(owner, "当前方案尚未保存，是否保存？", "新建方案",
                        System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Cancel) return;
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await SaveSchemeAsAsync();
                    if (isSchemeModified) return;
                }
            }

            // 清空所有流程数据。
            foreach (var wf in Workflows)
                DisposeWorkflow(wf);
            Workflows.Clear();
            activeWorkflowIndex = -1;

            schemeName = "未命名方案";
            schemeFilePath = null;
            RaisePropertyChanged(nameof(SchemeName));
            RaisePropertyChanged(nameof(SchemeTitle));

            AddWorkflowInternal("主流程");
            ActiveWorkflowIndex = 0;
            ClearSchemeModified();

            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow == null)
            {
                System.Windows.MessageBox.Show(
                    "已新建空白方案，可从左侧工具箱添加算子。",
                    "新建方案",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    mainWindow,
                    "已新建空白方案，可从左侧工具箱添加算子。",
                    "新建方案",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>从 JSON 文件打开方案。</summary>
        private async void OpenScheme()
        {
            if (isSchemeModified)
            {
                var result = System.Windows.MessageBox.Show("当前方案尚未保存，是否保存？", "打开方案",
                    System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Cancel) return;
                if (result == System.Windows.MessageBoxResult.Yes) await SaveSchemeAsAsync();
            }

            var dialog = new OpenFileDialog
            {
                Title = "打开方案文件",
                Filter = "方案文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = SchemePersistenceService.DefaultSchemeDirectory
            };

            if (dialog.ShowDialog() != true) return;

            await OpenSchemeFileAsync(dialog.FileName);
        }

        /// <summary>
        /// 旧方案迁移：数据引擎已改为纯显式绑定驱动（连线不再隐式传递上游整包数据），
        /// 加载后为每根没有对应绑定的连线补建默认绑定——上游主输出 → 下游主输入槽。
        /// 已有指向该上游的槽绑定时跳过；无可填槽（多连线汇合且槽已占用）时留待用户显式绑定。
        /// </summary>
        private void MigrateConnectionBindingsToExplicit()
        {
            foreach (var wf in Workflows)
            {
                foreach (var conn in wf.Connections)
                {
                    if (conn.From == null || conn.To == null) continue;
                    if (conn.To.InputBindings == null || conn.To.InputBindings.Count == 0) continue;
                    if (conn.To.InputBindings.Any(b => !b.IsParameterBinding && ReferenceEquals(b.SourceNode, conn.From)))
                        continue;

                    var freeSlots = conn.To.InputBindings
                        .Where(b => !b.IsParameterBinding && b.SourceNode == null)
                        .ToList();
                    if (freeSlots.Count == 0) continue;

                    var sourceSlots = UI.Controls.WorkflowBindingHelper.GetOutputSlots(conn.From).ToList();

                    // 逐槽匹配：同名键优先（如 shape_model.id ← shape_model.id），其次类型兼容；主输入回退 output 键
                    foreach (var slot in freeSlots)
                    {
                        var matched = sourceSlots.FirstOrDefault(x =>
                            string.Equals(x.Key, slot.TargetInputKey, StringComparison.OrdinalIgnoreCase) &&
                            UI.Controls.WorkflowBindingHelper.IsCompatible(slot.TargetInputType, x.ValueType))
                            ?? sourceSlots.FirstOrDefault(x =>
                                string.Equals(x.Key, RunOutputKeys.Output, StringComparison.OrdinalIgnoreCase) &&
                                UI.Controls.WorkflowBindingHelper.IsCompatible(slot.TargetInputType, x.ValueType))
                            ?? sourceSlots.FirstOrDefault(x =>
                                UI.Controls.WorkflowBindingHelper.IsCompatible(slot.TargetInputType, x.ValueType));

                        if (matched == null) continue;
                        slot.SourceNode = conn.From;
                        slot.SourceOutputKey = matched.Key;
                    }
                }
            }
        }

        /// <summary>刷新「最近打开」与「示例方案」菜单集合。</summary>
        private void RefreshSchemeMenus()
        {
            RecentSchemes.Clear();
            foreach (var path in RecentSchemeService.Load())
            {
                RecentSchemes.Add(new SchemeMenuItem
                {
                    DisplayName = Path.GetFileNameWithoutExtension(path),
                    FullPath = path,
                    Description = path
                });
            }
            RaisePropertyChanged(nameof(RecentSchemes));

            DemoSchemes.Clear();
            foreach (var demo in DemoSchemeService.ListDemos())
                DemoSchemes.Add(demo);
            RaisePropertyChanged(nameof(DemoSchemes));
        }

        /// <summary>首次启动生成示例方案并初始化快速打开菜单（构造函数调用）。</summary>
        private void InitializeSchemeManagement()
        {
            try
            {
                DemoSchemeService.EnsureCreated();
            }
            catch (Exception ex)
            {
                ExecutionLogService.App($"示例方案生成失败：{ex.Message}", ExecLogLevel.Warn);
            }
            RefreshSchemeMenus();
        }

        /// <summary>
        /// 打开指定路径的方案文件（菜单打开/最近打开/示例方案共用入口）。
        /// </summary>
        private async Task OpenSchemeFileAsync(string selectedFile)
        {
            if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile))
            {
                System.Windows.MessageBox.Show($"方案文件不存在：{selectedFile}", "打开方案",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            Exception loadError = null;
            var loadingTimer = Stopwatch.StartNew();
            SchemeLoadingWindowHost loadingWindow = null;
            try
            {
                SetSchemeLoadingMessage("正在准备加载方案...", loadingWindow);
                IsSchemeLoading = true;
                loadingWindow = await SchemeLoadingWindowHost.ShowAsync(System.Windows.Application.Current?.MainWindow, SchemeLoadingMessage);

                SetSchemeLoadingMessage("正在读取方案文件...", loadingWindow);

                var data = await SchemePersistenceService.LoadAsync(selectedFile);
                SetSchemeLoadingMessage("正在恢复流程节点...", loadingWindow);

                var loadReport = await LoadSchemeFromDataAsync(data, selectedFile, loadingWindow);
                if (!string.IsNullOrWhiteSpace(loadReport))
                {
                    System.Windows.MessageBox.Show($"方案已加载，但存在以下兼容性问题：\n\n{loadReport}",
                        "方案加载报告", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    ResultMessage = loadReport;
                }
            }
            catch (Exception ex)
            {
                loadError = ex;
            }
            finally
            {
                IsSchemeLoading = false;
                loadingWindow?.Dispose();
                // 方案加载完成 → 扫描 ReadImage 节点生成缩略图面板
                RefreshThumbnails();
            }

            if (loadError != null)
            {
                System.Windows.MessageBox.Show($"打开方案失败：{loadError.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private static async Task ShowSchemeLoadingOverlayAsync()
        {
            await Task.Yield();
            await Task.Delay(16);
        }

        private void SetSchemeLoadingMessage(string message, SchemeLoadingWindowHost loadingWindow = null)
        {
            SchemeLoadingMessage = message;
            loadingWindow?.UpdateMessage(message);
        }

        /// <summary>保存方案到当前路径。</summary>
        private async void SaveScheme()
        {
            if (string.IsNullOrWhiteSpace(schemeFilePath))
            {
                await SaveSchemeAsAsync();
                return;
            }

            try
            {
                var data = BuildSchemeData();
                await SchemePersistenceService.SaveAsync(schemeFilePath, data);
                ClearSchemeModified();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存方案失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>另存方案到新路径。</summary>
        private async Task SaveSchemeAsAsync()
        {
            EnsureSchemeDirectory();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"{schemeName}_{timestamp}.json";
            var dialog = new SaveFileDialog
            {
                Title = "保存方案",
                Filter = "方案文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = defaultFileName,
                RestoreDirectory = true,
                InitialDirectory = SchemePersistenceService.DefaultSchemeDirectory,
                AddExtension = true,
                OverwritePrompt = true,
                CheckFileExists = false,
                ValidateNames = true
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                schemeFilePath = dialog.FileName;
                schemeName = Path.GetFileNameWithoutExtension(dialog.FileName);
                RaisePropertyChanged(nameof(SchemeName));
                RaisePropertyChanged(nameof(SchemeTitle));
                RaisePropertyChanged(nameof(SchemeFilePath));

                var data = BuildSchemeData();
                await SchemePersistenceService.SaveAsync(schemeFilePath, data);
                ClearSchemeModified();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存方案失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void EnsureSchemeDirectory()
        {
            try
            {
                Directory.CreateDirectory(SchemePersistenceService.DefaultSchemeDirectory);
            }
            catch
            {
                // 保持现有行为，让后续保存时再统一提示。
            }
        }

        /// <summary>删除当前方案文件。</summary>
        private void DeleteScheme()
        {
            if (string.IsNullOrWhiteSpace(schemeFilePath) || !File.Exists(schemeFilePath))
            {
                System.Windows.MessageBox.Show("当前方案尚未保存到文件，无需删除。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"确定要删除方案文件吗？\n\n{schemeFilePath}\n\n此操作不可撤销。", "删除方案",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                File.Delete(schemeFilePath);
                schemeFilePath = null;
                RaisePropertyChanged(nameof(SchemeFilePath));
                System.Windows.MessageBox.Show("方案文件已删除，当前内存中的方案仍可继续编辑。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"删除方案文件失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>重命名当前方案（更新内存中的方案名，已保存文件时同步重命名文件）。</summary>
        private void RenameScheme()
        {
            var currentName = schemeName;
            var result = ZeroCodeVisionStudio.Shared.Dialogs.InputDialogWindow.Show(
                "重命名方案", "输入新的方案名称：", currentName);
            if (string.IsNullOrWhiteSpace(result) || result == currentName) return;

            var newName = result.Trim();
            if (newName.Length > 64) newName = newName.Substring(0, 64);

            // 如果已保存到文件，重命名文件
            if (!string.IsNullOrWhiteSpace(schemeFilePath) && File.Exists(schemeFilePath))
            {
                try
                {
                    var newDirectory = Path.GetDirectoryName(schemeFilePath);
                    var newFilePath = Path.Combine(newDirectory, newName + ".json");
                    if (File.Exists(newFilePath))
                    {
                        newFilePath = Path.Combine(newDirectory, newName + "_" + DateTime.Now.ToString("HHmmss") + ".json");
                    }
                    File.Move(schemeFilePath, newFilePath);
                    schemeFilePath = newFilePath;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"重命名方案文件失败：{ex.Message}\n已更新内存中的方案名。", "提示",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }

            schemeName = newName;
            RaisePropertyChanged(nameof(SchemeName));
            RaisePropertyChanged(nameof(SchemeTitle));
            RaisePropertyChanged(nameof(SchemeFilePath));
            MarkSchemeModified();
        }

        /// <summary>管理方案：弹出对话框列出默认方案目录下所有 .json 方案文件。</summary>
        private void ManageSchemes()
        {
            try
            {
                var dir = SchemePersistenceService.DefaultSchemeDirectory;
                Directory.CreateDirectory(dir);
                var files = Directory.GetFiles(dir, "*.json")
                    .Select(f => new { Path = f, Name = Path.GetFileNameWithoutExtension(f), Modified = File.GetLastWriteTime(f) })
                    .OrderByDescending(x => x.Modified)
                    .ToList();

                if (files.Count == 0)
                {
                    System.Windows.MessageBox.Show($"方案目录为空：\n{dir}", "方案管理",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"方案目录：{dir}");
                sb.AppendLine($"共 {files.Count} 个方案文件\n");
                foreach (var f in files)
                {
                    var marker = f.Path.Equals(schemeFilePath, StringComparison.OrdinalIgnoreCase) ? " ◀ (当前)" : "";
                    sb.AppendLine($"• {f.Name}{marker}");
                    sb.AppendLine($"  {f.Modified:yyyy-MM-dd HH:mm}  {f.Path}");
                }
                sb.AppendLine("\n提示：使用 [文件 → 打开方案...] 打开任意方案。");

                System.Windows.MessageBox.Show(sb.ToString(), "方案管理 - 全部方案文件",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"列出方案失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>重命名画布上的节点（弹出输入框）。</summary>
        private void RenameNode(IHandleRunModel node)
        {
            if (node == null) return;
            var result = ZeroCodeVisionStudio.Shared.Dialogs.InputDialogWindow.Show(
                "重命名节点", "输入新的节点名称：", node.DisplayName);
            if (string.IsNullOrWhiteSpace(result) || result == node.DisplayName) return;
            node.DisplayName = result.Trim();
            MarkSchemeModified();
        }

        /// <summary>重命名指定流程。</summary>
        private void RenameWorkflow(WorkflowViewModel wf)
        {
            if (wf == null) wf = ActiveWorkflow;
            if (wf == null) return;
            var result = ZeroCodeVisionStudio.Shared.Dialogs.InputDialogWindow.Show(
                "重命名流程", "输入新的流程名称：", wf.WorkflowName);
            if (string.IsNullOrWhiteSpace(result) || result == wf.WorkflowName) return;
            wf.WorkflowName = result.Trim();
            MarkSchemeModified();
        }

        // ==================== 方案数据序列化/反序列化 ====================

        private SchemeData BuildSchemeData()
        {
            var data = new SchemeData
            {
                Name = schemeName,
                ActiveWorkflowIndex = activeWorkflowIndex,
                Workflows = new List<WorkflowData>()
            };

            foreach (var wf in Workflows)
            {
                var wfData = new WorkflowData { Name = wf.WorkflowName };
                var nodeIndexMap = new Dictionary<IHandleRunModel, int>();

                for (int i = 0; i < wf.Nodes.Count; i++)
                {
                    var node = wf.Nodes[i];
                    nodeIndexMap[node] = i;

                    var nodeData = new NodeData
                    {
                        RegisterKey = node.RegisterModel?.Key,
                        DisplayName = node.DisplayName,
                        X = node.X,
                        Y = node.Y,
                        ExecutionCondition = node.ExecutionCondition
                    };

                    // 保存参数值。
                    var parameter = node.Handle?.HandleParameter;
                    if (parameter != null)
                    {
                        foreach (var prop in parameter.GetType().GetProperties()
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                        {
                            try
                            {
                                var val = prop.GetValue(parameter);
                                if (val != null)
                                    nodeData.ParameterValues[prop.Name] = val;
                            }
                            catch { }
                        }
                    }

                    // 保存输入绑定（含变量源绑定：SourceNode 为空但 SourceOutputKey 以 var: 开头）。
                    foreach (var binding in node.InputBindings.Where(b => b.SourceNode != null || b.IsVariableSource))
                    {
                        nodeData.InputBindings.Add(new InputBindingData
                        {
                            TargetInputKey = binding.TargetInputKey,
                            SourceNodeIndex = binding.SourceNode != null && nodeIndexMap.TryGetValue(binding.SourceNode, out var idx) ? idx : null,
                            SourceOutputKey = binding.SourceOutputKey
                        });
                    }

                    wfData.Nodes.Add(nodeData);
                }

                // 保存连线。
                foreach (var conn in wf.Connections)
                {
                    if (nodeIndexMap.TryGetValue(conn.From, out var fi) && nodeIndexMap.TryGetValue(conn.To, out var ti))
                    {
                        wfData.Connections.Add(new ConnectionData { FromIndex = fi, ToIndex = ti });
                    }
                }

                // 保存显示源顺序。
                for (int i = 0; i < wf.DisplaySources.Count; i++)
                {
                    var displaySource = wf.DisplaySources[i];
                    if (displaySource?.Node == null)
                    {
                        continue;
                    }

                    if (!nodeIndexMap.TryGetValue(displaySource.Node, out var nodeIndex))
                    {
                        continue;
                    }

                    wfData.DisplaySources.Add(new DisplaySourceData
                    {
                        NodeIndex = nodeIndex,
                        OutputKey = displaySource.Output?.OutputKey,
                        Order = displaySource.Order,
                        IsVisible = displaySource.IsVisible
                    });
                }

                data.Workflows.Add(wfData);
            }

            return data;
        }

        /// <summary>
        /// 从方案数据恢复画布。
        /// 返回加载报告：缺失算子、恢复失败的参数等兼容性问题；无问题时返回 null。
        /// </summary>
        private async Task<string> LoadSchemeFromDataAsync(SchemeData data, string filePath, SchemeLoadingWindowHost loadingWindow = null)
        {
            SetSchemeLoadingMessage("正在清理当前方案...", loadingWindow);

            foreach (var wf in Workflows)
                DisposeWorkflow(wf);
            Workflows.Clear();
            ClearUndoHistory();
            activeWorkflowIndex = -1;
            RaiseWorkflowProxyProperties();
            await Task.Yield();

            schemeName = data.Name ?? "未命名方案";
            schemeFilePath = filePath;
            RaisePropertyChanged(nameof(SchemeName));
            RaisePropertyChanged(nameof(SchemeTitle));
            RaisePropertyChanged(nameof(SchemeFilePath));

            // 加载兼容性报告：未知算子的节点会被跳过、个别参数可能恢复失败，必须显式告知用户而非静默丢弃。
            var missingOperators = new List<string>();
            var failedParameterCount = 0;

            if (data.Workflows == null || data.Workflows.Count == 0)
            {
                Workflows.Add(new WorkflowViewModel("主流程", SeqGenerator));
                activeWorkflowIndex = -1;
                ActiveWorkflowIndex = 0;
                ClearSchemeModified();
                return null;
            }

            var loadedWorkflows = new List<WorkflowViewModel>();
            var restoredNodeCount = 0;
            foreach (var wfData in data.Workflows)
            {
                var wf = new WorkflowViewModel(wfData.Name ?? "流程", SeqGenerator);
                wf.ParallelExecution = wfData.ParallelExecution;

                if (wfData.Nodes != null)
                {
                    var nodes = new List<IHandleRunModel>();
                    var nodeIndexMap = new Dictionary<int, IHandleRunModel>();
                    for (var nodeIndex = 0; nodeIndex < wfData.Nodes.Count; nodeIndex++)
                    {
                        var nodeData = wfData.Nodes[nodeIndex];
                        if (!HandleControl.IsRegister(nodeData.RegisterKey ?? ""))
                        {
                            // 记录缺失算子，加载完成后统一报告（不再静默跳过）。
                            var displayName = nodeData.DisplayName ?? nodeData.RegisterKey ?? "未知节点";
                            if (!missingOperators.Contains(displayName))
                                missingOperators.Add(displayName);
                            continue;
                        }

                        var registerModel = HandleControl.GetHandle(nodeData.RegisterKey);
                        var runModel = new HandleRunModel(registerModel, nodeData.DisplayName ?? registerModel.HandleName)
                        {
                            X = nodeData.X,
                            Y = nodeData.Y,
                            ExecutionCondition = nodeData.ExecutionCondition ?? ""
                        };
                        runModel.Handle.ContextRunModel = runModel;

                        // 恢复参数值。
                        if (nodeData.ParameterValues != null)
                        {
                            var parameter = runModel.Handle?.HandleParameter;
                            if (parameter != null)
                            {
                                foreach (var kv in nodeData.ParameterValues)
                                {
                                    try
                                    {
                                        var prop = parameter.GetType().GetProperty(kv.Key);
                                        if (prop == null)
                                        {
                                            continue;
                                        }

                                        if (prop.CanWrite)
                                        {
                                            var convertedValue = Services.WorkflowExecutionEngine.ConvertParameterForLoad(kv.Value, prop.PropertyType);
                                            prop.SetValue(parameter, convertedValue);
                                        }
                                        else
                                        {
                                            PopulateReadOnlyCollection(parameter, prop, kv.Value);
                                        }
                                    }
                                    catch
                                    {
                                        // 单个参数恢复失败不再静默：计入加载报告。
                                        failedParameterCount++;
                                    }
                                }
                            }

                            if (parameter is Plugin.Handle.CompareCondition.CompareConditionParameter compareParameter)
                            {
                                compareParameter.EnsureBindingKeys();
                            }
                        }

                        runModel.RefreshInputBindings();
                        runModel.Handle.OnImageDisplay += OnHandleDisplayEvent;
                        wf.Nodes.Add(runModel);
                        nodes.Add(runModel);
                        nodeIndexMap[nodeIndex] = runModel;

                        restoredNodeCount++;
                        if (restoredNodeCount % 2 == 0)
                        {
                            await Task.Yield();
                        }
                    }

                    // 恢复输入绑定。
                    foreach (var item in nodeIndexMap)
                    {
                        var nodeData = wfData.Nodes[item.Key];
                        var node = item.Value;
                        if (nodeData.InputBindings == null) continue;

                        foreach (var bindData in nodeData.InputBindings)
                        {
                            var binding = node.InputBindings.FirstOrDefault(b => b.TargetInputKey == bindData.TargetInputKey);
                            if (binding == null) continue;

                            if (bindData.SourceNodeIndex.HasValue &&
                                nodeIndexMap.TryGetValue(bindData.SourceNodeIndex.Value, out var sourceNode))
                            {
                                binding.SourceNode = sourceNode;
                                binding.SourceOutputKey = bindData.SourceOutputKey;
                            }
                            else if (!string.IsNullOrWhiteSpace(bindData.SourceOutputKey))
                            {
                                // 变量源绑定（var:g:名 / var:w:名）：无来源节点索引，仅恢复来源键。
                                binding.SourceNode = null;
                                binding.SourceOutputKey = bindData.SourceOutputKey;
                            }
                        }
                    }

                    // 恢复连线。
                    if (wfData.Connections != null)
                    {
                        foreach (var connData in wfData.Connections)
                        {
                            if (nodeIndexMap.TryGetValue(connData.FromIndex, out var fromNode) &&
                                nodeIndexMap.TryGetValue(connData.ToIndex, out var toNode))
                            {
                                wf.Connections.Add(new WorkflowConnection(fromNode, toNode));
                            }
                        }
                    }

                    if (wfData.DisplaySources != null)
                    {
                        foreach (var displayData in wfData.DisplaySources.OrderBy(x => x.Order))
                        {
                            if (!nodeIndexMap.TryGetValue(displayData.NodeIndex, out var sourceNode))
                            {
                                continue;
                            }

                            var outputName = GetDisplayOutputName(sourceNode, displayData.OutputKey);
                            var source = new DisplaySourceItem(
                                sourceNode,
                                new DisplayOutputItem(sourceNode, displayData.OutputKey, outputName),
                                displayData.Order)
                            {
                                IsVisible = displayData.IsVisible
                            };
                            source.StateChanged += (_, __) => RenderDisplaySources(append: false);
                            wf.DisplaySources.Add(source);
                        }
                    }
                }

                loadedWorkflows.Add(wf);
                await Task.Yield();
            }

            SetSchemeLoadingMessage("正在刷新流程画布...", loadingWindow);

            foreach (var wf in loadedWorkflows)
            {
                Workflows.Add(wf);
            }

            activeWorkflowIndex = -1;
            ActiveWorkflowIndex = Math.Clamp(data.ActiveWorkflowIndex, 0, Workflows.Count - 1);
            ClearSchemeModified();

            // 生成加载报告（缺失算子 / 参数恢复失败 / 旧版本格式提示）。
            var issues = new List<string>();
            if (data.Version > 0 && data.Version < Shared.Data.SchemeData.CurrentVersion)
            {
                issues.Add($"方案格式版本 v{data.Version} 低于当前 v{Shared.Data.SchemeData.CurrentVersion}，已按兼容模式加载。");
            }
            if (missingOperators.Count > 0)
            {
                issues.Add($"有 {missingOperators.Count} 个节点的算子未安装或已被移除，已跳过：{string.Join("、", missingOperators)}。");
            }
            if (failedParameterCount > 0)
            {
                issues.Add($"有 {failedParameterCount} 个参数值恢复失败（类型不兼容或参数已更名），已保留默认值。");
            }

            // 旧方案迁移：为没有显式绑定的连线补建默认主输入绑定（纯绑定引擎下数据的唯一来源）
            MigrateConnectionBindingsToExplicit();

            // 成功加载：登记最近打开
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                RecentSchemeService.Record(filePath);
                RefreshSchemeMenus();
            }

            return issues.Count > 0 ? string.Join(Environment.NewLine, issues) : null;
        }

        private void RaiseWorkflowProxyProperties()
        {
            RaisePropertyChanged(nameof(ActiveWorkflow));
            RaisePropertyChanged(nameof(Nodes));
            RaisePropertyChanged(nameof(Connections));
            RaisePropertyChanged(nameof(ExecutionRecords));
            RaisePropertyChanged(nameof(DisplaySources));
            RaisePropertyChanged(nameof(SelectedNodeDisplayOutputs));
            RaisePropertyChanged(nameof(SelectedInputBindings));
            RaisePropertyChanged(nameof(SelectedNode));
            RaisePropertyChanged(nameof(SelectedConnection));
            RaisePropertyChanged(nameof(SelectedDisplaySource));
            RaisePropertyChanged(nameof(SelectedDisplayOutput));
        }

        // ==================== 流程管理 ====================

        private WorkflowViewModel AddWorkflowInternal(string name)
        {
            var wf = new WorkflowViewModel(name, SeqGenerator);
            Workflows.Add(wf);
            return wf;
        }

        private void AddWorkflow()
        {
            var wf = AddWorkflowInternal($"流程 {Workflows.Count + 1}");
            ActiveWorkflowIndex = Workflows.Count - 1;
            MarkSchemeModified();
        }

        private void RemoveWorkflow()
        {
            if (Workflows.Count <= 1)
            {
                System.Windows.MessageBox.Show("至少需要保留一个流程。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var wf = ActiveWorkflow;
            if (wf == null) return;

            var result = System.Windows.MessageBox.Show(
                $"确定要删除流程 \"{wf.WorkflowName}\" 吗？\n此操作不可恢复。", "删除流程",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            DisposeWorkflow(wf);
            Workflows.Remove(wf);

            if (ActiveWorkflowIndex >= Workflows.Count)
                ActiveWorkflowIndex = Workflows.Count - 1;
            else
            {
                RaisePropertyChanged(nameof(ActiveWorkflow));
                RaisePropertyChanged(nameof(Nodes));
                RaisePropertyChanged(nameof(Connections));
                RaisePropertyChanged(nameof(ExecutionRecords));
                RaisePropertyChanged(nameof(SelectedInputBindings));
            }

            MarkSchemeModified();
        }

        private void SwitchWorkflow(WorkflowViewModel wf)
        {
            if (wf == null) return;
            var idx = Workflows.IndexOf(wf);
            if (idx >= 0)
                ActiveWorkflowIndex = idx;
        }

        private void UnsubscribeWorkflowEvents(WorkflowViewModel wf)
        {
            if (wf?.Nodes == null) return;
            foreach (var node in wf.Nodes)
                node.Handle.OnImageDisplay -= OnHandleDisplayEvent;
        }

        private void DisposeWorkflow(WorkflowViewModel wf)
        {
            if (wf?.Nodes == null) return;

            foreach (var node in wf.Nodes)
            {
                if (node?.Handle != null)
                {
                    node.Handle.OnImageDisplay -= OnHandleDisplayEvent;
                }

                node?.Dispose();
            }
        }

        // ==================== 节点操作 ====================

        private void OnHandleDoubleClick(object obj)
        {
            if (obj is IHandleRegisterModel model)
            {
                var position = ActiveWorkflow?.GetNextNodePosition() ?? (80, 80);
                AddHandleToWorkflow(model, position.X, position.Y);
            }
        }

        public IHandleRunModel AddHandleToWorkflow(IHandleRegisterModel model, double x, double y)
        {
            if (model == null || ActiveWorkflow == null)
                return null;

            var runModel = new HandleRunModel(model, SeqGenerator.Generate(model.HandleName))
            {
                X = Math.Max(0, x),
                Y = Math.Max(0, y)
            };
            ActiveWorkflow.Nodes.Add(runModel);
            SelectedNode = runModel;
            runModel.Handle.OnImageDisplay += OnHandleDisplayEvent;
            RefreshAllWorkflowDisplayOutputs();
            MarkSchemeModified();

            var ownerWorkflow = ActiveWorkflow;
            PushUndo($"添加节点 {runModel.DisplayName}",
                undo: () => RemoveNodeForUndo(ownerWorkflow, runModel),
                redo: () => RestoreNodeForUndo(ownerWorkflow, runModel),
                retainedNodes: new List<IHandleRunModel> { runModel });

            return runModel;
        }

        /// <summary>撤销"添加节点"：仅从所属流程移除，不销毁实例（重做时复用）。</summary>
        private void RemoveNodeForUndo(WorkflowViewModel ownerWorkflow, IHandleRunModel node)
        {
            if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
            if (ReferenceEquals(SelectedNode, node))
                SelectedNode = null;
            foreach (var connection in ownerWorkflow.Connections
                .Where(x => ReferenceEquals(x.From, node) || ReferenceEquals(x.To, node)).ToList())
            {
                ownerWorkflow.Connections.Remove(connection);
            }
            ownerWorkflow.Nodes.Remove(node);
            node.Handle.OnImageDisplay -= OnHandleDisplayEvent;
            ownerWorkflow.RefreshConnections();
            RefreshAllWorkflowDisplayOutputs();
        }

        /// <summary>重做"添加节点"：将暂留的节点实例放回所属流程。</summary>
        private void RestoreNodeForUndo(WorkflowViewModel ownerWorkflow, IHandleRunModel node)
        {
            if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
            if (!ownerWorkflow.Nodes.Contains(node))
                ownerWorkflow.Nodes.Add(node);
            node.Handle.OnImageDisplay += OnHandleDisplayEvent;
            ownerWorkflow.RefreshConnections();
            RefreshAllWorkflowDisplayOutputs();
        }

        private void OnAddNode(WorkflowNodeCreateRequest request)
        {
            if (request == null) return;

            var x = request.X;
            var y = request.Y;
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
            {
                var position = ActiveWorkflow?.GetNextNodePosition() ?? (80, 80);
                x = position.X;
                y = position.Y;
            }

            AddHandleToWorkflow(request.Handle, x, y);
        }

        private void OnMoveNode(WorkflowNodeMoveRequest request)
        {
            if (request?.Node == null) return;

            request.Node.X = Math.Max(0, request.X);
            request.Node.Y = Math.Max(0, request.Y);
            ActiveWorkflow?.RefreshConnections();
            MarkSchemeModified();
        }

        // ==================== 节点执行 ====================

        private async void OnHandleExec(IHandleRunModel obj)
        {
            if (obj == null) return;

            await ExecuteSingleNodeForAutomationAsync(obj);
        }

        /// <summary>
        /// 单节点执行（供 UI 双击节点与 AI 工具共用）。
        /// 等待执行真正完成后返回，返回值为该节点是否执行成功。
        /// </summary>
        internal async Task<bool> ExecuteSingleNodeForAutomationAsync(IHandleRunModel node)
        {
            if (node == null || IsExecuting) return false;

            // AI/MCP 来自后台线程：单节点执行全程（输入装配/状态记录/显示源刷新）都在 UI 线程
            var completion = new TaskCompletionSource<bool>();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(new Func<Task>(async () =>
            {
                try
                {
                    IsExecuting = true;
                    executionCts = new CancellationTokenSource();
                    var ct = executionCts.Token;

                    var inputParameter = Services.WorkflowExecutionEngine.BuildInputParameterForNode(this, node);
                    node.Handle.SetRunParameter(inputParameter ?? new RunParameter());

                    isSingleNodeExec = true;
                    bool success = false;
                    try
                    {
                        success = await ExecutionEngine.ExecuteSingleNodeAsync(node, ct);
                    }
                    finally
                    {
                        isSingleNodeExec = false;
                        IsExecuting = false;
                    }

                    SetupSingleNodeDisplaySources(node);
                    completion.SetResult(success);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }));
            return await completion.Task;
        }

        private void SetupSingleNodeDisplaySources(IHandleRunModel node)
        {
            if (node == null || ActiveWorkflow == null) return;

            // 移除上一次自动添加的显示源
            foreach (var item in DisplaySources.Where(x => x.IsAutoAdded).ToList())
                DisplaySources.Remove(item);

            // 上游输入节点的默认输出（= 当前节点的输入图像/区域）
            var upstream = FindPrimaryUpstreamNode(node);
            if (upstream != null)
                AddAutoDisplaySourceEntry(upstream);

            // 当前节点的默认输出
            AddAutoDisplaySourceEntry(node);

            SyncDisplaySourceOrder();
            RenderDisplaySources(append: false);
        }

        private async void OnStartProcess()
        {
            await ExecutionEngine.ExecuteWorkflowCoreAsync();
        }

        /// <summary>
        /// 执行到流程中当前选定的节点为止（包含该节点，后续节点不执行）。
        /// 选中节点为 null 时无操作。
        /// </summary>
        private async void ExecuteToSelectedNode()
        {
            if (SelectedNode == null || IsExecuting) return;
            await ExecutionEngine.ExecuteWorkflowCoreAsync(stopAfterNode: SelectedNode);
        }

        /// <summary>
        /// 供 AI 工具调用的工作流执行入口：等待执行真正完成后返回真实成败。
        /// </summary>
        internal async Task<bool> ExecuteWorkflowForAutomationAsync()
        {
            if (IsExecuting) return false;

            // AI/MCP 来自后台线程：引擎内部会操作节点/记录等 UI 集合，整体调度到 UI 线程执行
            var completion = new TaskCompletionSource<bool>();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(new Func<Task>(async () =>
            {
                try { completion.SetResult(await ExecutionEngine.ExecuteWorkflowCoreAsync()); }
                catch (Exception ex) { completion.SetException(ex); }
            }));
            return await completion.Task;
        }

        /// <summary>请求停止当前执行（单次/单节点/连续运行/单次循环）。返回是否有正在执行的流程。</summary>
        internal bool RequestStopExecution()
        {
            var hadExecution = IsExecuting;
            executionCts?.Cancel();
            if (IsContinuousRunning)
            {
                IsContinuousRunning = false;
                hadExecution = true;
            }
            if (IsSingleLoopRunning)
            {
                IsSingleLoopRunning = false;
                hadExecution = true;
            }
            return hadExecution;
        }

        /// <summary>
        /// 共享迭代逻辑：遍历缩略图，每张跑一次流程。
        /// </summary>
        /// <param name="loopForever">true=连续运行（无限循环）；false=单次循环（跑完一遍停止）</param>
        /// <param name="stopFlag">停止标志（IsContinuousRunning 或 IsSingleLoopRunning），false 时迭代退出</param>
        private async Task RunThumbnailIterationAsync(bool loopForever, Func<bool> stopFlag)
        {
            if (Thumbnails.Count == 0)
            {
                // 没有缩略图就退化为普通连续运行（旧行为保底）
                while (stopFlag())
                {
                    var app = System.Windows.Application.Current;
                    if (app?.Dispatcher != null && app.Dispatcher.HasShutdownStarted) break;
                    var success = await ExecutionEngine.ExecuteWorkflowCoreAsync(fromContinuousLoop: true);
                    if (!stopFlag()) break;
                    await Task.Delay(success ? 30 : 500);
                }
                return;
            }

            // 无条件从第一张开始（单次循环 / 连续运行 都要求遍历全部缩略图）
            int startIdx = 0;

            do
            {
                // 正向遍历一遍缩略图
                for (int i = startIdx; i < Thumbnails.Count; i++)
                {
                    if (!stopFlag()) return; // 用户点了停止

                    SelectedThumbnail = Thumbnails[i]; // setter 已同步 ReadImage 索引
                    await ExecutionEngine.ExecuteWorkflowCoreAsync(fromContinuousLoop: true);
                }

                // 如果不是无限循环就结束
                if (!loopForever) break;

                // 无限循环 → 从头再来
                startIdx = 0;
                // 短暂停歇避免跑太猛
                await Task.Delay(30);
            }
            while (stopFlag());
        }

        private async Task ToggleContinuousRunAsync()
        {
            // === 停止路径 ===
            if (IsContinuousRunning)
            {
                IsContinuousRunning = false;
                ExecutionEngine.CancelExecution();
                return;
            }

            // === 启动路径 ===
            if (isToggleProcessing) return;
            isToggleProcessing = true;

            try
            {
                // 防并发：先停掉可能正在跑的单次循环
                if (IsSingleLoopRunning)
                {
                    IsSingleLoopRunning = false;
                    ExecutionEngine.CancelExecution();
                    await Task.Delay(100); // 给上一轮一点时间收尸
                }

                // 等 IsExecuting 归位
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (IsExecuting && DateTime.UtcNow < deadline)
                    await Task.Delay(30);

                if (IsExecuting)
                {
                    IsExecuting = false;
                    ExecutionEngine.ForceResetState();
                }

                IsContinuousRunning = true;
                try
                {
                    await RunThumbnailIterationAsync(loopForever: true, stopFlag: () => IsContinuousRunning);
                }
                finally
                {
                    IsContinuousRunning = false;
                }
            }
            finally
            {
                isToggleProcessing = false;
            }
        }

        private async Task ToggleSingleLoopAsync()
        {
            // === 停止路径 ===
            if (IsSingleLoopRunning)
            {
                IsSingleLoopRunning = false;
                ExecutionEngine.CancelExecution();
                return;
            }

            // === 启动路径 ===
            if (isToggleProcessing) return;
            isToggleProcessing = true;

            try
            {
                // 防并发：先停掉可能正在跑的连续运行
                if (IsContinuousRunning)
                {
                    IsContinuousRunning = false;
                    ExecutionEngine.CancelExecution();
                    await Task.Delay(100);
                }

                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (IsExecuting && DateTime.UtcNow < deadline)
                    await Task.Delay(30);

                if (IsExecuting)
                {
                    IsExecuting = false;
                    ExecutionEngine.ForceResetState();
                }

                IsSingleLoopRunning = true;
                try
                {
                    await RunThumbnailIterationAsync(loopForever: false, stopFlag: () => IsSingleLoopRunning);
                }
                finally
                {
                    IsSingleLoopRunning = false;
                }
            }
            finally
            {
                isToggleProcessing = false;
            }
        }

        /// <summary>
        /// 工作流执行核心：重置节点 → 拓扑排序 → 逐节点构建输入并执行（后台线程 + 可取消 + 单节点超时）。
        /// 返回整轮执行是否成功（无失败节点且未被取消）。
        /// </summary>
        /// <param name="fromContinuousLoop">仅限连续运行循环内部传 true（循环自身串行调用，绕过重入守卫）。</param>
        

        /// <summary>
        /// 执行单个节点。
        /// 算子本体在后台线程执行（避免长算子冻结 UI）；节点级状态、执行记录与显示更新在 UI 线程完成。
        /// 单节点超时视为失败并中止整个流程：HALCON 算子无法协作取消，继续执行会与僵尸任务竞争资源。
        /// </summary>
        

        private WorkflowExecutionRecord AddExecutionRecord(IHandleRunModel node, HandleStatus status, string resultData)
        {
            if (node == null || ActiveWorkflow == null) return null;

            var record = new WorkflowExecutionRecord
            {
                Sequence = ActiveWorkflow.NextSequence(),
                ExecuteTime = DateTime.Now,
                Module = node.DisplayName,
                Status = status,
                Elapsed = node.LastElapsed,
                ResultData = resultData
            };

            ActiveWorkflow.ExecutionRecords.Add(record);

            if (ActiveWorkflow.ExecutionRecords.Count > 500)
                ActiveWorkflow.ExecutionRecords.RemoveAt(0);

            return record;
        }

        private static string BuildSingleNodeElapsedMessage(IHandleRunModel node, string message)
        {
            var elapsedText = node?.LastElapsedText ?? ExecutionTimeFormatter.Format(TimeSpan.Zero);
            var elapsedMessage = $"算子耗时：{elapsedText}";
            return string.IsNullOrWhiteSpace(message) ? elapsedMessage : $"{elapsedMessage}{Environment.NewLine}{message}";
        }

        private static string BuildWorkflowElapsedMessage(
            TimeSpan elapsed,
            int executedCount,
            int totalCount,
            IHandleRunModel failedNode,
            string lastMessage)
        {
            var statusText = failedNode == null
                ? $"流程耗时：{ExecutionTimeFormatter.Format(elapsed)}；完成：{executedCount}/{totalCount}"
                : $"流程耗时：{ExecutionTimeFormatter.Format(elapsed)}；完成：{executedCount}/{totalCount}；失败节点：{failedNode.DisplayName}";

            return string.IsNullOrWhiteSpace(lastMessage) ? statusText : $"{statusText}{Environment.NewLine}{lastMessage}";
        }

        // ==================== 连线管理 ====================

        private void OnAddConnection(WorkflowConnectionRequest request)
        {
            if (request == null) return;
            AddConnection(request.From, request.To);
        }

        /// <summary>
        /// 建立节点连线并自动绑定输入槽。
        /// fromPin/toPin 为可选的数据槽键名：指定时按槽精确绑定（如 Region→Region），
        /// 留空时按输出/输入类型自动匹配。
        /// </summary>
        public void AddConnection(IHandleRunModel from, IHandleRunModel to, string fromPin = null, string toPin = null)
        {
            if (from == null || to == null || ReferenceEquals(from, to) || ActiveWorkflow == null)
                return;

            if (ActiveWorkflow.Connections.Any(x => ReferenceEquals(x.From, from) && ReferenceEquals(x.To, to)))
                return;

            // 快照 to 节点的绑定状态，供撤销恢复。
            var bindingsBefore = to.InputBindings
                .Where(x => !x.IsParameterBinding)
                .Select(x => (Binding: x, Source: x.SourceNode, Key: x.SourceOutputKey))
                .ToList();

            var connection = new WorkflowConnection(from, to);
            ActiveWorkflow.Connections.Add(connection);
            EnsureInputBinding(to, from, RunOutputKeys.Output, fromPin, toPin);
            ActiveWorkflow.RefreshConnections();
            RefreshSelectedInputBindings();
            MarkSchemeModified();

            var bindingsAfter = to.InputBindings
                .Where(x => !x.IsParameterBinding)
                .Select(x => (Binding: x, Source: x.SourceNode, Key: x.SourceOutputKey))
                .ToList();

            var ownerWorkflow = ActiveWorkflow;
            PushUndo($"连线 {from.DisplayName} -> {to.DisplayName}",
                undo: () => { RemoveConnectionInternal(ownerWorkflow, connection, bindingsBefore); },
                redo: () => { RestoreConnectionInternal(ownerWorkflow, connection, bindingsAfter); });
        }

        /// <summary>撤销/重做连线时作用于其所属流程（ownerWorkflow），与当前活动流程无关。</summary>
        private void RemoveConnectionInternal(WorkflowViewModel ownerWorkflow, WorkflowConnection connection,
            List<(WorkflowInputBinding Binding, IHandleRunModel Source, string Key)> bindingsToRestore)
        {
            if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
            if (ReferenceEquals(ownerWorkflow.SelectedConnection, connection))
                SelectConnection(null);
            ownerWorkflow.Connections.Remove(connection);
            foreach (var item in bindingsToRestore)
            {
                item.Binding.SourceNode = item.Source;
                item.Binding.SourceOutputKey = item.Key;
            }
            ownerWorkflow.RefreshConnections();
            RefreshSelectedInputBindings();
            MarkSchemeModified();
        }

        /// <summary>撤销/重做连线时作用于其所属流程（ownerWorkflow），与当前活动流程无关。</summary>
        private void RestoreConnectionInternal(WorkflowViewModel ownerWorkflow, WorkflowConnection connection,
            List<(WorkflowInputBinding Binding, IHandleRunModel Source, string Key)> bindingsToRestore)
        {
            if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
            if (!ownerWorkflow.Connections.Contains(connection))
                ownerWorkflow.Connections.Add(connection);
            foreach (var item in bindingsToRestore)
            {
                item.Binding.SourceNode = item.Source;
                item.Binding.SourceOutputKey = item.Key;
            }
            ownerWorkflow.RefreshConnections();
            RefreshSelectedInputBindings();
            MarkSchemeModified();
        }

        public void SelectConnection(WorkflowConnection connection)
        {
            if (SelectedConnection != null)
                SelectedConnection.IsSelected = false;

            SelectedConnection = connection;
            if (connection != null && ActiveWorkflow != null)
            {
                SelectedNode = null;
                foreach (var node in ActiveWorkflow.Nodes)
                {
                    node.IsSelected = false;
                }
            }
        }

        public void SelectNode(IHandleRunModel node)
        {
            if (ActiveWorkflow != null && ReferenceEquals(ActiveWorkflow.SelectedNode, node))
            {
                // 重复点击同一个节点时，重新刷新结果显示和输出图像
                DisplaySelectedNodeOutput(node);
                UpdateExecutionResultForNode(node);
            }
            else
            {
                SelectedNode = node;
            }

            if (node != null)
                SelectConnection(null);
        }

        /// <summary>
        /// 联动显示选中节点的执行结果，并在模块结果列表中选中对应记录。
        /// </summary>
        private void UpdateExecutionResultForNode(IHandleRunModel node)
        {
            if (node == null)
            {
                SelectedExecutionRecord = null;
                return;
            }

            // 1. 查找此节点最近的一条执行记录并定位选中
            var record = ActiveWorkflow?.ExecutionRecords?.LastOrDefault(r => r.Module == node.DisplayName);
            SelectedExecutionRecord = record;

            // 2. 格式化并显示结果文本
            if (node.Status == HandleStatus.Pending || (node.LastElapsed == TimeSpan.Zero && record == null))
            {
                ResultMessage = $"[{node.DisplayName}] 尚未执行或无输出结果";
            }
            else
            {
                var elapsedText = node.LastElapsedText;
                var statusText = node.Status == HandleStatus.Success ? "已执行" : (node.Status == HandleStatus.Failed ? "失败" : "执行中");
                var detail = !string.IsNullOrWhiteSpace(node.Handle?.RunParameter?.ResultMessage)
                    ? node.Handle.RunParameter.ResultMessage
                    : record?.ResultData ?? node.StatusMessage;

                var msg = $"[{node.DisplayName}] 状态：{statusText}  耗时：{elapsedText}";
                if (!string.IsNullOrWhiteSpace(detail) && detail != node.StatusMessage)
                {
                    msg += $"{Environment.NewLine}{detail}";
                }
                else if (!string.IsNullOrWhiteSpace(detail))
                {
                    msg += $"{Environment.NewLine}结果：{detail}";
                }
                ResultMessage = msg;
            }
        }

        public void DeleteSelectedConnection()
        {
            if (ActiveWorkflow == null) return;
            var connection = ActiveWorkflow.SelectedConnection;
            if (connection == null) return;

            DeleteConnection(connection);
        }

        public void DeleteConnection(WorkflowConnection connection)
        {
            if (connection == null || ActiveWorkflow == null) return;

            // 快照 to 节点由该连线自动绑定的输入，供撤销恢复。
            var bindingsBefore = connection.To?.InputBindings
                .Where(x => !x.IsParameterBinding)
                .Select(x => (Binding: x, Source: x.SourceNode, Key: x.SourceOutputKey))
                .ToList() ?? new List<(WorkflowInputBinding, IHandleRunModel, string)>();

            if (ReferenceEquals(connection, ActiveWorkflow.SelectedConnection))
                SelectConnection(null);

            ActiveWorkflow.Connections.Remove(connection);
            // 断开连线同时清除仅由此连线建立的自动绑定。
            foreach (var binding in connection.To?.InputBindings.Where(x =>
                ReferenceEquals(x.SourceNode, connection.From) && !x.IsParameterBinding).ToList() ?? new List<WorkflowInputBinding>())
            {
                binding.Clear();
            }
            ActiveWorkflow.RefreshConnections();
            MarkSchemeModified();

            var removedConnection = connection;
            var ownerWorkflow = ActiveWorkflow;
            PushUndo($"删除连线 {connection.From?.DisplayName} -> {connection.To?.DisplayName}",
                undo: () =>
                {
                    if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
                    if (!ownerWorkflow.Connections.Contains(removedConnection))
                        ownerWorkflow.Connections.Add(removedConnection);
                    foreach (var item in bindingsBefore)
                    {
                        item.Binding.SourceNode = item.Source;
                        item.Binding.SourceOutputKey = item.Key;
                    }
                    ownerWorkflow.RefreshConnections();
                    RefreshSelectedInputBindings();
                },
                redo: () =>
                {
                    if (ownerWorkflow == null || !Workflows.Contains(ownerWorkflow)) return;
                    if (ReferenceEquals(ownerWorkflow.SelectedConnection, removedConnection))
                        SelectConnection(null);
                    ownerWorkflow.Connections.Remove(removedConnection);
                    foreach (var binding in removedConnection.To?.InputBindings.Where(x =>
                        ReferenceEquals(x.SourceNode, removedConnection.From) && !x.IsParameterBinding).ToList() ?? new List<WorkflowInputBinding>())
                    {
                        binding.Clear();
                    }
                    ownerWorkflow.RefreshConnections();
                });
        }

        // ==================== 节点删除 ====================

        private void DeleteNode(IHandleRunModel obj)
        {
            if (obj == null || ActiveWorkflow == null) return;

            // 快照连线与其它节点指向该节点的绑定，供撤销恢复。
            var removedConnections = ActiveWorkflow.Connections
                .Where(x => ReferenceEquals(x.From, obj) || ReferenceEquals(x.To, obj))
                .ToList();
            var affectedBindings = ActiveWorkflow.Nodes
                .SelectMany(x => x.InputBindings)
                .Where(x => ReferenceEquals(x.SourceNode, obj) && !x.IsParameterBinding)
                .Select(x => (Binding: x, SourceKey: x.SourceOutputKey))
                .ToList();

            if (ReferenceEquals(obj, SelectedNode))
                SelectedNode = null;

            foreach (var connection in removedConnections)
                ActiveWorkflow.Connections.Remove(connection);
            foreach (var binding in affectedBindings)
                binding.Binding.Clear();
            ActiveWorkflow.Nodes.Remove(obj);
            obj.Handle.OnImageDisplay -= OnHandleDisplayEvent;

            // 节点暂不销毁，保留在撤销栈中；出栈（栈溢出/清空历史）时统一 Dispose。
            // 撤销/重做闭包绑定删除时所属的流程：用户此后切换流程再撤销时不会把节点恢复到别的流程。
            var retainedNode = obj;
            var ownerWorkflow = ActiveWorkflow;
            PushUndo($"删除节点 {obj.DisplayName}",
                undo: () =>
                {
                    if (!Workflows.Contains(ownerWorkflow)) return;
                    ownerWorkflow.Nodes.Add(retainedNode);
                    retainedNode.Handle.OnImageDisplay += OnHandleDisplayEvent;
                    foreach (var connection in removedConnections)
                        ownerWorkflow.Connections.Add(connection);
                    foreach (var binding in affectedBindings)
                    {
                        binding.Binding.SourceNode = retainedNode;
                        binding.Binding.SourceOutputKey = binding.SourceKey;
                    }
                    ownerWorkflow.RefreshConnections();
                    RefreshAllWorkflowDisplayOutputs();
                },
                redo: () =>
                {
                    if (!Workflows.Contains(ownerWorkflow)) return;
                    if (ReferenceEquals(SelectedNode, retainedNode))
                        SelectedNode = null;
                    foreach (var connection in removedConnections)
                        ownerWorkflow.Connections.Remove(connection);
                    foreach (var binding in affectedBindings)
                        binding.Binding.Clear();
                    ownerWorkflow.Nodes.Remove(retainedNode);
                    retainedNode.Handle.OnImageDisplay -= OnHandleDisplayEvent;
                    ownerWorkflow.RefreshConnections();
                    RefreshAllWorkflowDisplayOutputs();
                },
                retainedNodes: new List<IHandleRunModel> { retainedNode });

            ActiveWorkflow.RefreshConnections();
            RefreshAllWorkflowDisplayOutputs();
            MarkSchemeModified();
        }

        private void RemoveConnections(IHandleRunModel node)
        {
            if (ActiveWorkflow == null) return;

            foreach (var connection in ActiveWorkflow.Connections
                .Where(x => ReferenceEquals(x.From, node) || ReferenceEquals(x.To, node)).ToList())
            {
                ActiveWorkflow.Connections.Remove(connection);
            }

            foreach (var binding in ActiveWorkflow.Nodes
                .SelectMany(x => x.InputBindings)
                .Where(x => ReferenceEquals(x.SourceNode, node)).ToList())
            {
                binding.Clear();
            }
        }

        // ==================== 撤销 / 重做 ====================
        // 覆盖结构性操作：节点增删、连线增删。节点移动（拖拽高频事件）与参数修改不入栈。
        // 被撤销删除的节点在栈中暂留（不 Dispose），栈溢出或清空历史时统一释放。

        private sealed class UndoEntry
        {
            public string Description;
            public Action Undo;
            public Action Redo;
            public List<IHandleRunModel> RetainedNodes = new();
        }

        private readonly List<UndoEntry> undoStack = new();
        private readonly List<UndoEntry> redoStack = new();
        private const int MaxUndoEntries = 100;

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        private void PushUndo(string description, Action undo, Action redo, List<IHandleRunModel> retainedNodes = null)
        {
            // 新操作使重做分支失效；其中暂留的节点可以安全销毁。
            foreach (var entry in redoStack)
                DisposeRetained(entry);
            redoStack.Clear();

            undoStack.Add(new UndoEntry
            {
                Description = description,
                Undo = undo,
                Redo = redo,
                RetainedNodes = retainedNodes ?? new List<IHandleRunModel>()
            });

            while (undoStack.Count > MaxUndoEntries)
            {
                DisposeRetained(undoStack[0]);
                undoStack.RemoveAt(0);
            }

            RefreshUndoRedoState();
        }

        private void Undo()
        {
            if (undoStack.Count == 0 || ActiveWorkflow == null) return;
            var entry = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            try
            {
                entry.Undo();
                MarkSchemeModified();
            }
            finally
            {
                redoStack.Add(entry);
                RefreshUndoRedoState();
            }
        }

        private void Redo()
        {
            if (redoStack.Count == 0 || ActiveWorkflow == null) return;
            var entry = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            try
            {
                entry.Redo();
                MarkSchemeModified();
            }
            finally
            {
                undoStack.Add(entry);
                RefreshUndoRedoState();
            }
        }

        private void RefreshUndoRedoState()
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private void DisposeRetained(UndoEntry entry)
        {
            foreach (var node in entry.RetainedNodes)
            {
                if (node == null) continue;

                // 同一节点可能被多个撤销条目暂留（如 删除→撤销→再删除），
                // 仅当它不在画布上、也不被其它条目引用时才真正销毁。
                bool stillReferenced =
                    Workflows.Any(w => w.Nodes != null && w.Nodes.Contains(node)) ||
                    undoStack.Concat(redoStack).Any(e => !ReferenceEquals(e, entry) && e.RetainedNodes.Contains(node));
                if (!stillReferenced)
                    node.Dispose();
            }
            entry.RetainedNodes.Clear();
        }

        /// <summary>清空撤销/重做历史并释放其中暂留的节点（方案加载/新建时调用）。</summary>
        private void ClearUndoHistory()
        {
            foreach (var entry in undoStack) DisposeRetained(entry);
            foreach (var entry in redoStack) DisposeRetained(entry);
            undoStack.Clear();
            redoStack.Clear();
            RefreshUndoRedoState();
        }

        // ==================== AI 自动化辅助入口 ====================

        /// <summary>重命名节点（AI rename_node 工具入口）。新名称在当前流程内必须唯一。</summary>
        internal bool RenameNodeInternal(string oldName, string newName)
        {
            var wf = ActiveWorkflow;
            if (wf == null || string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            var node = wf.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, oldName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            if (wf.Nodes.Any(n => !ReferenceEquals(n, node) &&
                string.Equals(n.DisplayName, newName, StringComparison.OrdinalIgnoreCase)))
                return false;

            var previousName = node.DisplayName;
            node.DisplayName = newName.Trim();
            wf.RefreshConnections();
            RefreshAllWorkflowDisplayOutputs();
            RefreshSelectedInputBindings();
            MarkSchemeModified();

            PushUndo($"重命名节点 {previousName} -> {newName}",
                () =>
                {
                    if (!Workflows.Contains(wf)) return;
                    node.DisplayName = previousName;
                    wf.RefreshConnections();
                    RefreshAllWorkflowDisplayOutputs();
                },
                () =>
                {
                    if (!Workflows.Contains(wf)) return;
                    node.DisplayName = newName.Trim();
                    wf.RefreshConnections();
                    RefreshAllWorkflowDisplayOutputs();
                });
            return true;
        }

        /// <summary>移动节点到指定坐标（AI move_node 工具入口）。</summary>
        internal bool MoveNodeInternal(IHandleRunModel node, double x, double y)
        {
            if (node == null || ActiveWorkflow == null) return false;

            node.X = Math.Max(0, x);
            node.Y = Math.Max(0, y);
            ActiveWorkflow.RefreshConnections();
            MarkSchemeModified();
            return true;
        }

        /// <summary>按名称切换当前活动工作流（AI switch_workflow 工具入口）。</summary>
        internal bool SwitchWorkflowByName(string workflowName)
        {
            var wf = Workflows.FirstOrDefault(w =>
                string.Equals(w.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase));
            if (wf == null) return false;

            SwitchWorkflow(wf);
            return true;
        }

        /// <summary>读取指定节点的输入绑定列表（AI get_node_input_bindings 用）。</summary>
        internal List<WorkflowInputBinding> GetNodeInputBindingsInfo(string nodeName)
        {
            var node = ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            return node?.InputBindings?.ToList() ?? new List<WorkflowInputBinding>();
        }

        /// <summary>
        /// 设置节点的一个输入绑定（AI set_node_input_binding 用）。
        /// targetInputKey 支持数据槽键名（Region/Contour）或 parameter:参数名 / 直接参数名（参数引用）。
        /// sourceNode 为空表示清除该绑定。
        /// </summary>
        internal bool SetNodeInputBindingInternal(string nodeName, string targetInputKey, string sourceNode, string sourceOutputKey)
        {
            var wf = ActiveWorkflow;
            if (wf == null || string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(targetInputKey))
                return false;

            var node = wf.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) return false;

            // 匹配目标绑定：键名相等 → parameter:前缀 → 参数名相等
            var key = targetInputKey.Trim();
            var binding = node.InputBindings.FirstOrDefault(b =>
                string.Equals(b.TargetInputKey, key, StringComparison.OrdinalIgnoreCase));
            binding ??= node.InputBindings.FirstOrDefault(b =>
                string.Equals(b.TargetInputKey, WorkflowInputBinding.ParameterBindingPrefix + key, StringComparison.OrdinalIgnoreCase));
            binding ??= node.InputBindings.FirstOrDefault(b =>
                !b.IsParameterBinding == false &&
                string.Equals(b.TargetParameterName, key, StringComparison.OrdinalIgnoreCase));
            if (binding == null) return false;

            // 清除绑定
            if (string.IsNullOrWhiteSpace(sourceNode))
            {
                binding.Clear();
                RefreshSelectedInputBindings();
                MarkSchemeModified();
                return true;
            }

            var source = wf.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, sourceNode, StringComparison.OrdinalIgnoreCase));
            if (source == null) return false;

            // 解析上游输出槽（键名或显示名宽松匹配；参数绑定允许绑数值/元组输出）
            string resolvedKey = null;
            if (!binding.IsParameterBinding)
            {
                resolvedKey = WorkflowBindingHelper.GetOutputSlots(source)
                    .FirstOrDefault(slot =>
                        (string.Equals(slot.Key, sourceOutputKey, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(slot.DisplayName, sourceOutputKey, StringComparison.OrdinalIgnoreCase)) &&
                        WorkflowBindingHelper.IsCompatible(binding.TargetInputType, slot.ValueType))?.Key;
                resolvedKey ??= WorkflowBindingHelper.GetOutputSlots(source)
                    .FirstOrDefault(slot => WorkflowBindingHelper.IsCompatible(binding.TargetInputType, slot.ValueType))?.Key;
                if (resolvedKey == null) return false;
            }
            else
            {
                resolvedKey = string.IsNullOrWhiteSpace(sourceOutputKey)
                    ? RunOutputKeys.ResultMessage
                    : sourceOutputKey;
            }

            binding.SourceNode = source;
            binding.SourceOutputKey = resolvedKey;
            RefreshSelectedInputBindings();
            MarkSchemeModified();
            return true;
        }

        /// <summary>复制节点（含全部参数），返回新节点（AI duplicate_node 用）。</summary>
        internal IHandleRunModel DuplicateNodeInternal(string nodeName, double offsetX = 40, double offsetY = 40)
        {
            var node = ActiveWorkflow?.Nodes.FirstOrDefault(n =>
                string.Equals(n.DisplayName, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node?.RegisterModel == null) return null;

            var clone = AddHandleToWorkflow(node.RegisterModel, node.X + offsetX, node.Y + offsetY);
            if (clone?.Handle?.HandleParameter == null || node.Handle?.HandleParameter == null)
                return clone;

            // 克隆全部可写参数属性（与方案序列化同一套转换规则）。
            var source = node.Handle.HandleParameter;
            var target = clone.Handle.HandleParameter;
            foreach (var prop in source.GetType().GetProperties().Where(p => p.CanRead))
            {
                try
                {
                    var value = prop.GetValue(source);
                    if (value == null) continue;

                    var targetProp = target.GetType().GetProperty(prop.Name);
                    if (targetProp == null) continue;

                    if (targetProp.CanWrite)
                        targetProp.SetValue(target, Services.WorkflowExecutionEngine.ConvertParameterForLoad(value, targetProp.PropertyType));
                    else
                        PopulateReadOnlyCollection(target, targetProp, value);
                }
                catch
                {
                    // 单个参数克隆失败不影响整体。
                }
            }

            RefreshSelectedNodeDisplayOutputs();
            MarkSchemeModified();
            return clone;
        }

        /// <summary>清空当前活动流程（AI clear_workflow 用；不可撤销，清空后撤销历史一并清除）。</summary>
        internal bool ClearWorkflowInternal()
        {
            var wf = ActiveWorkflow;
            if (wf == null || wf.Nodes.Count == 0) return false;

            foreach (var node in wf.Nodes.ToList())
            {
                node.Handle.OnImageDisplay -= OnHandleDisplayEvent;
                node.Dispose();
            }
            wf.Nodes.Clear();
            wf.Connections.Clear();
            wf.ResetDisplaySources();
            ClearUndoHistory();
            SelectedNode = null;
            RefreshAllWorkflowDisplayOutputs();
            MarkSchemeModified();
            return true;
        }

        /// <summary>按名称删除流程 Tab（AI remove_workflow 用；至少保留一个流程）。</summary>
        internal bool RemoveWorkflowByNameInternal(string workflowName)
        {
            var wf = Workflows.FirstOrDefault(w =>
                string.Equals(w.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase));
            if (wf == null || Workflows.Count <= 1) return false;

            var wasActive = ReferenceEquals(ActiveWorkflow, wf);
            DisposeWorkflow(wf);
            Workflows.Remove(wf);

            if (wasActive)
            {
                activeWorkflowIndex = -1;
                ActiveWorkflowIndex = Math.Clamp(activeWorkflowIndex, 0, Workflows.Count - 1);
            }

            MarkSchemeModified();
            return true;
        }

        /// <summary>重命名流程 Tab（AI rename_workflow 用）。</summary>
        internal bool RenameWorkflowInternal(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;

            var wf = Workflows.FirstOrDefault(w =>
                string.Equals(w.WorkflowName, oldName, StringComparison.OrdinalIgnoreCase));
            if (wf == null) return false;

            if (Workflows.Any(w => !ReferenceEquals(w, wf) &&
                string.Equals(w.WorkflowName, newName, StringComparison.OrdinalIgnoreCase)))
                return false;

            wf.WorkflowName = newName.Trim();
            MarkSchemeModified();
            return true;
        }

        /// <summary>
        /// 通讯接收触发：驱动后台线程收到数据且该驱动启用触发时，
        /// 切换到目标流程（若指定）并执行一轮；正在执行时跳过本次（避免重入）。
        /// </summary>
        private void OnCommTrafficForTrigger(string driverName, bool isRx, int bytes, byte[] data)
        {
            try
            {
                if (!isRx || data == null || data.Length == 0) return;

                var config = Shared.Communication.CommunicationCenter.FindConfig(driverName);
                if (config?.TriggerEnabled != true) return;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        if (IsExecuting)
                        {
                            ResultMessage = $"通讯触发跳过：流程正在执行（驱动 {driverName}）";
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(config.TriggerWorkflowName))
                        {
                            SwitchWorkflowByName(config.TriggerWorkflowName);
                        }

                        await ExecutionEngine.ExecuteWorkflowCoreAsync();
                    }
                    catch (Exception ex)
                    {
                        ResultMessage = $"通讯触发执行异常：{ExceptionFormatter.Describe(ex)}";
                    }
                }));
            }
            catch
            {
            }
        }

        /// <summary>
        /// 自动布局当前流程画布：
        /// 按连线拓扑做最长路径分层（上游在左、下游在右），
        /// 同层节点保持原有纵向顺序，各列围绕整体垂直中心对齐；
        /// 未连通的节点各自作为源参与分层。画布标题栏按钮与 AI 工具共用此实现。
        /// </summary>
        // ==================== 参数对话框绑定编辑 ====================

        /// <summary>
        /// 为节点的绑定编辑生成候选：首项"未绑定"，随后仅上游节点的输出——
        /// 直接父节点在前，沿父级连线追溯的上级节点在后（带 [上级] 前缀）；
        /// 不提供无连线关系的节点（避免误绑全局任意节点），变量候选保持全局/流程级。
        /// </summary>
        private List<ParameterBindingCandidate> BuildBindingCandidates(IHandleRunModel node)
        {
            var result = new List<ParameterBindingCandidate> { ParameterBindingCandidate.Empty() };

            var wf = Workflows.FirstOrDefault(w => w.Nodes.Contains(node)) ?? ActiveWorkflow;
            if (wf != null)
            {
                // 直接父节点：有连线连到本节点的来源。
                var directParents = wf.Connections
                    .Where(c => ReferenceEquals(c.To, node) && c.From != null)
                    .Select(c => c.From)
                    .Distinct()
                    .ToList();

                // 沿父级连线向上追溯的上级节点（BFS 去重，含环路保护）。
                var ancestors = new List<IHandleRunModel>();
                var visited = new HashSet<IHandleRunModel>(directParents) { node };
                var queue = new Queue<IHandleRunModel>(directParents);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var conn in wf.Connections.Where(x => ReferenceEquals(x.To, current)))
                    {
                        if (conn.From == null || !visited.Add(conn.From)) continue;
                        ancestors.Add(conn.From);
                        queue.Enqueue(conn.From);
                    }
                }

                foreach (var source in directParents.Concat(ancestors))
                {
                    var isDirect = directParents.Contains(source);
                    var outputs = WorkflowBindingHelper.GetOutputSlots(source);
                    // 未执行的节点没有声明输出槽时提供默认 Output 项。
                    var items = outputs.Any()
                        ? outputs.Select(s => (key: s.Key, name: string.IsNullOrWhiteSpace(s.DisplayName) ? s.Key : s.DisplayName))
                        : new[] { (key: RunOutputKeys.Output, name: "输出") };

                    foreach (var (key, name) in items)
                    {
                        var prefix = isDirect ? "" : "[上级] ";
                        result.Add(ParameterBindingCandidate.ForNode(source, key, $"{prefix}{source.DisplayName} · {name}"));
                    }
                }
            }

            foreach (var candidate in WorkflowVariableStore.GetCandidates())
            {
                result.Add(ParameterBindingCandidate.ForVariable(candidate.Name, candidate.Scope));
            }

            return result;
        }

        // ==================== 节点级执行条件 ====================

        /// <summary>右键「执行条件…」命令：为节点设置/清除条件表达式。</summary>
        public DelegateCommand<IHandleRunModel> SetNodeConditionCommand { get; private set; }

        private void OnSetNodeCondition(IHandleRunModel node)
        {
            if (node == null) return;

            var input = PromptTextInput(
                "节点执行条件",
                $"为节点 [{node.DisplayName}] 设置执行条件（留空清除）：",
                node.ExecutionCondition,
                "示例：$IsOK$ == true　|　$DefectCount$ < 3　|　!$TrueBranch$\n" +
                "$Key$ 引用上游输出键；支持 == != > >= < <= 与前置 ! 取反；条件为假时该节点整轮跳过。");

            if (input == null) return;

            node.ExecutionCondition = input.Trim();
            MarkSchemeModified();
            ResultMessage = string.IsNullOrEmpty(node.ExecutionCondition)
                ? $"已清除节点 [{node.DisplayName}] 的执行条件。"
                : $"节点 [{node.DisplayName}] 执行条件：{node.ExecutionCondition}";
        }

        /// <summary>
        /// 求值节点执行条件：先做 $Key$ 上游输出插值，再解析单个比较表达式。
        /// 空条件恒真；解析异常按假处理（不中断流程）。
        /// </summary>
        

        /// <summary>解析比较表达式：可选前置 !、两侧比较（== != > >= < <=），裸值按真值判断。</summary>
        

        

        

        /// <summary>极简文本输入对话框（执行条件编辑用，避免为单字段建专用视图）。</summary>
        private static string PromptTextInput(string title, string tip, string current, string help)
        {
            var window = new System.Windows.Window
            {
                Title = title,
                Width = 480,
                SizeToContent = System.Windows.SizeToContent.Height,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current?.MainWindow,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            var box = new System.Windows.Controls.TextBox
            {
                Text = current ?? "",
                FontSize = 13,
                Padding = new System.Windows.Thickness(8, 6, 8, 6)
            };
            var ok = new System.Windows.Controls.Button { Content = "确定", Width = 88, IsDefault = true, Margin = new System.Windows.Thickness(4, 0, 0, 0) };
            var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 88, IsCancel = true, Margin = new System.Windows.Thickness(4, 0, 0, 0) };
            ok.Click += (_, __) => window.DialogResult = true;

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 10, 0, 0)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(16) };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = tip,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(box);
            if (!string.IsNullOrEmpty(help))
            {
                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = help,
                    FontSize = 11,
                    Opacity = 0.75,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 6, 0, 0)
                });
            }
            panel.Children.Add(buttons);

            window.Content = panel;
            box.Focus();
            box.SelectAll();
            return window.ShowDialog() == true ? box.Text : null;
        }

        public void AutoLayoutWorkflowNodes()
        {
            var wf = ActiveWorkflow;
            if (wf == null || wf.Nodes.Count == 0) return;

            var nodes = wf.Nodes.ToList();
            var conns = wf.Connections.ToList();

            var inDegree = nodes.ToDictionary(n => n, _ => 0);
            var adjacency = nodes.ToDictionary(n => n, _ => new List<IHandleRunModel>());
            foreach (var c in conns)
            {
                if (c.From == null || c.To == null) continue;
                if (!inDegree.ContainsKey(c.To) || !adjacency.ContainsKey(c.From)) continue;
                inDegree[c.To]++;
                adjacency[c.From].Add(c.To);
            }

            // Kahn 分层：layer[next] = max(layer[next], layer[cur] + 1)，保证下游永远在下游层。
            var layer = new Dictionary<IHandleRunModel, int>();
            var queue = new Queue<IHandleRunModel>(nodes.Where(n => inDegree[n] == 0));
            foreach (var source in queue) layer[source] = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in adjacency[current])
                {
                    if (!layer.TryGetValue(next, out var existing) || existing < layer[current] + 1)
                        layer[next] = layer[current] + 1;
                    if (--inDegree[next] == 0) queue.Enqueue(next);
                }
            }
            foreach (var n in nodes)
                if (!layer.ContainsKey(n)) layer[n] = 0;

            // 分层成行（自上而下）：层号 = 行（Y 递增），同层多分支横向排布（X 展开）。
            // 连线缩短策略（两阶段）：
            // 1) 以"最宽行居中"的经典布局为底（对称结构零扰动）；
            // 2) 多轮"邻层邻居均值回拉"细化：节点期望 X = 全部前驱+后继的当前 X 平均，
            //    行内按期望排序、最小间距推挤、整行向期望重心平移——非对称子树相互收拢，缩短斜线。
            double spacingX = WorkflowConnection.NodeWidth + 76;
            double spacingY = WorkflowConnection.NodeHeight + 84;

            var predecessorOf = nodes.ToDictionary(n => n, _ => new List<IHandleRunModel>());
            var successorOf = nodes.ToDictionary(n => n, _ => new List<IHandleRunModel>());
            foreach (var c in conns)
            {
                if (c.From == null || c.To == null) continue;
                if (predecessorOf.TryGetValue(c.To, out var preList) && predecessorOf.ContainsKey(c.From))
                    preList.Add(c.From);
                if (successorOf.TryGetValue(c.From, out var sucList) && successorOf.ContainsKey(c.To))
                    sucList.Add(c.To);
            }
            var neighborOf = nodes.ToDictionary(
                n => n, n => predecessorOf[n].Concat(successorOf[n]).ToList());

            // 阶段一：最宽行居中的基础布局
            var rowGroups = nodes.GroupBy(n => layer[n]).OrderBy(g => g.Key).ToList();
            var baseRowCount = rowGroups.Max(g => g.Count());
            double baseCenterX = 60 + (baseRowCount - 1) * spacingX / 2;
            foreach (var row in rowGroups)
            {
                var ordered = row.OrderBy(n => n.X).ThenBy(n => n.Y).ToList();
                var leftX = baseCenterX - (ordered.Count - 1) * spacingX / 2;
                for (var i = 0; i < ordered.Count; i++)
                {
                    ordered[i].X = Math.Max(0, leftX + i * spacingX);
                    ordered[i].Y = 60 + row.Key * spacingY;
                }
            }

            // 阶段二：邻层邻居均值回拉（4 轮；期望排序顺带减少交叉）
            for (var round = 0; round < 4; round++)
            {
                foreach (var row in rowGroups)
                {
                    var desired = new Dictionary<IHandleRunModel, double>();
                    foreach (var n in row)
                    {
                        var neighbors = neighborOf[n];
                        desired[n] = neighbors.Count > 0 ? neighbors.Average(x => x.X) : n.X;
                    }

                    var ordered = row.OrderBy(n => desired[n]).ThenBy(n => n.X).ToList();
                    var prevRight = double.MinValue;
                    foreach (var n in ordered)
                    {
                        var x = Math.Max(desired[n], prevRight == double.MinValue ? 0 : prevRight + spacingX);
                        n.X = Math.Max(0, x);
                        prevRight = n.X;
                        n.Y = 60 + row.Key * spacingY;
                    }

                    // 整行向期望重心平移（保持行内无重叠，消除推挤右漂）
                    var actualAvg = ordered.Average(n => n.X);
                    var desiredAvg = desired.Values.Average();
                    var shift = desiredAvg - actualAvg;
                    if (Math.Abs(shift) > 1 && ordered.Min(n => n.X) + shift >= 0)
                        foreach (var n in ordered) n.X += shift;
                }
            }

            // 全图左边界归一
            var minX = nodes.Min(n => n.X);
            if (Math.Abs(minX - 60) > 0.5)
                foreach (var n in nodes) n.X += 60 - minX;

            wf.RefreshConnections();
            MarkSchemeModified();
        }

        /// <summary>保存方案（AI save_scheme 工具入口）。path 为空时保存到当前路径，无当前路径时存入默认目录。</summary>
        internal async Task<string> SaveSchemeForAutomationAsync(string path)
        {
            var targetPath = path;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = string.IsNullOrWhiteSpace(schemeFilePath)
                    ? Shared.Data.SchemePersistenceService.GetDefaultFilePath(SchemeName.TrimStart('*').Trim())
                    : schemeFilePath;
            }

            var data = BuildSchemeData();
            await Shared.Data.SchemePersistenceService.SaveAsync(targetPath, data);

            if (string.IsNullOrWhiteSpace(schemeFilePath))
            {
                schemeFilePath = targetPath;
                RaisePropertyChanged(nameof(SchemeFilePath));
            }
            ClearSchemeModified();
            return targetPath;
        }

        /// <summary>打开方案文件（AI open_scheme 工具入口）。返回加载报告（缺失算子/失败参数），无问题时为空。</summary>
        internal async Task<string> OpenSchemeForAutomationAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"方案文件未找到：{path}");
            }

            var data = await Shared.Data.SchemePersistenceService.LoadAsync(path);

            // 方案应用会重建 Workflows 集合（触发 CollectionView），
            // AI/MCP 调用来自后台线程时必须调度到 UI 线程执行。
            var completion = new TaskCompletionSource<string>();
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                try { completion.SetResult(await LoadSchemeFromDataAsync(data, path)); }
                catch (Exception ex) { completion.SetException(ex); }
            }));
            return await completion.Task;
        }

        private void OnHandleEdit(IHandleRunModel obj)
        {
            obj.Handle.ContextRunModel = obj;
            WorkflowVariableStore.CurrentWorkflowName = ActiveWorkflow?.WorkflowName ?? "";

            // 模态期间登记当前节点：对话框窗口据此挂载通用「输入与参数绑定」编辑区。
            ParameterBindingAmbient.CurrentEditNode = obj;
            try
            {
                obj.Handle.EditParameters();
            }
            finally
            {
                ParameterBindingAmbient.CurrentEditNode = null;
            }

            obj.RefreshInputBindings();
            RefreshSelectedInputBindings();
            MarkSchemeModified();

            // 如果是 ReadImage 节点，参数（ImageFiles 列表）可能变了 → 刷新缩略图面板
            RefreshThumbnails();
        }

        // ==================== 图像显示 ====================

        private void OnHandleDisplayEvent(object sender, ImageDisplayEventArgs e)
        {
            if (sender is IHandle handle && !string.IsNullOrWhiteSpace(handle.RunParameter?.ResultMessage))
                ResultMessage = handle.RunParameter.ResultMessage;

            if (sender is IHandle displayHandle)
                UpdateDisplayOverlay(displayHandle.RunParameter);

            // 算子在后台线程执行时，事件由此线程触发；
            // HALCON 窗口绘制与 ObservableCollection 更新必须调度回 UI 线程。
            void HandleDisplay()
            {
                // 优先级：BitmapSource（不依赖任何第三方 native）> Mat（不依赖 Halcon）> HObject（依赖 Halcon）
                if (e?.Bitmap != null)
                {
                    DisplayBitmap = e.Bitmap;
                }
                else if (e?.Mat != null && !e.Mat.Empty())
                {
                    DisplayImage(e.Mat);
                }
                else if (e?.Image != null)
                {
                    DisplayImage(e.Image);
                }

                if (!isWorkflowNodeExecuting && sender is IHandle previewHandle && ActiveWorkflow != null)
                {
                    var node = ActiveWorkflow.Nodes.FirstOrDefault(x => ReferenceEquals(x.Handle, previewHandle));
                    if (node != null)
                    {
                        node.Status = HandleStatus.Success;
                        node.StatusMessage = "Preview";
                        AddExecutionRecord(node, HandleStatus.Success, BuildResultData(node));
                    }
                }
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                HandleDisplay();
            }
            else
            {
                dispatcher.BeginInvoke(HandleDisplay);
            }
        }

        private void DisplayNodeResult(IHandleRunModel node)
        {
            var parameter = node?.Handle?.RunParameter;
            UpdateDisplayOverlay(parameter);
            if (node != null)
            {
                CaptureDisplayOutputs(node, append: false);
                RenderDisplaySources(append: false);
            }
        }

        /// <summary>
        /// 显示选中节点的输出图像。
        /// 如果节点有输出图像，则在图像窗口中显示。
        /// </summary>
        private void DisplaySelectedNodeOutput(IHandleRunModel node)
        {
            if (node == null || node.Handle?.RunParameter == null)
            {
                return;
            }

            var parameter = node.Handle.RunParameter;
            
            // 检查节点是否有输出图像
            var outputImage = parameter.HOutput ?? parameter.GrayImage ?? parameter.RawImage;
            if (outputImage == null)
            {
                // 尝试从 Outputs 中获取图像
                outputImage = parameter.Outputs.Values.OfType<HObject>().FirstOrDefault();
            }

            if (outputImage != null)
            {
                // 更新显示叠加层信息
                UpdateDisplayOverlay(parameter);
                
                // 设置自动显示源并渲染
                foreach (var item in DisplaySources.Where(x => x.IsAutoAdded).ToList())
                {
                    DisplaySources.Remove(item);
                }

                var src = AddAutoDisplaySourceEntry(node);
                if (src != null)
                {
                    SelectedDisplaySource = src;
                }

                RenderDisplaySources(append: false);
            }
        }

        private void DisplayImage(HObject image)
        {
            if (image == null) return;

            // 路径 1：Halcon runtime 可用 → HWindow.DispObj
            if (hWindow != null)
            {
                try
                {
                    HOperatorSet.DispObj(image, hWindow);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Halcon DispObj 失败，降级 WPF Image：{ex.Message}");
                }
            }

            // 路径 2：降级 → HObject→Mat→BitmapSource（MatHObjectConverter 本身也依赖 Halcon，
            // 所以只有在 Halcon 部分可用时才走得通；Mat 算子直接走 Mat 版本）
            try
            {
                using var mat = MatHObjectConverter.HObjectToMat(image);
                if (mat != null && !mat.Empty())
                {
                    DisplayBitmap = MatBitmapSourceConverter.MatToBitmapSource(mat);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HObject→Mat→BitmapSource 降级失败：{ex.Message}");
            }
        }

        /// <summary>OpenCV Mat 直接显示 — 不依赖 Halcon runtime。</summary>
        private void DisplayImage(Mat mat)
        {
            if (mat == null || mat.Empty()) return;
            DisplayBitmap = MatBitmapSourceConverter.MatToBitmapSource(mat);
        }

        private void UpdateDisplayOverlay(RunParameter parameter)
        {
            var overlayText = parameter?.GetOutput<string>(RunOutputKeys.DisplayText);
            DisplayOverlayText = string.IsNullOrWhiteSpace(overlayText) ? string.Empty : overlayText;
            DisplayOverlayForeground = parameter?.GetOutput<string>(RunOutputKeys.DisplayTextColor, "#F8FAFC") ?? "#F8FAFC";
            DisplayOverlayBackground = parameter?.GetOutput<string>(RunOutputKeys.DisplayBackgroundColor, "#B30F172A") ?? "#B30F172A";
        }

        private static DisplayOutputItem GetDefaultDisplayOutput(IHandleRunModel node)
        {
            var parameter = node?.Handle?.RunParameter;
            if (parameter == null) return null;

            if (parameter.HOutput != null)
            {
                return new DisplayOutputItem(node, RunOutputKeys.Output, "输出图像/区域");
            }

            if (parameter.GrayImage != null)
            {
                return new DisplayOutputItem(node, RunOutputKeys.GrayImage, "灰度图像");
            }

            if (parameter.RawImage != null)
            {
                return new DisplayOutputItem(node, RunOutputKeys.RawImage, "原始图像");
            }

            var output = parameter.Outputs.FirstOrDefault(x => x.Value is HObject);
            return output.Value is HObject ? new DisplayOutputItem(node, output.Key, output.Key) : null;
        }

        private void FitCurrentImage()
        {
            if (HWindow == null || HWindow.ActualWidth <= 0 || HWindow.ActualHeight <= 0)
                return;

            var focusObject = GetCurrentDisplayObject();
            if (focusObject == null)
            {
                return;
            }

            try
            {
                HWindow.SetFullImagePart();
            }
            catch
            {
            }
        }

        private void RedisplayCurrentImage()
        {
            RenderDisplaySources(append: false);
        }

        private void RenderDisplaySources(bool append)
        {
            if (HWindow == null) return;

            if (hWindow == null)
            {
                EnsureHalconWindowAsync();
                return;
            }

            try
            {
                if (!append)
                {
                    HWindow.Items.Clear();
                }

                foreach (var item in DisplaySources.Where(x => x.IsVisible).OrderBy(x => x.Order))
                {
                    var image = GetDisplayObject(item.Node?.Handle?.RunParameter, item.Output?.OutputKey);
                    if (image != null)
                    {
                        HWindow.Items.Add(image);
                    }
                }

                FitCurrentImage();
            }
            catch
            {
                try
                {
                    hWindow.ClearWindow();
                    foreach (var item in DisplaySources.Where(x => x.IsVisible).OrderBy(x => x.Order))
                    {
                        var image = GetDisplayObject(item.Node?.Handle?.RunParameter, item.Output?.OutputKey);
                        if (image != null)
                        {
                            HOperatorSet.DispObj(image, hWindow);
                        }
                    }
                    FitCurrentImage();
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }
        }

        private void EnsureHalconWindowAsync()
        {
            if (HWindow == null) return;

            HWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                hWindow ??= HWindow.HalconWindow;
                if (hWindow != null) RedisplayCurrentImage();
            }), DispatcherPriority.Loaded);
        }

        private static bool TryGetHImage(HObject source, out HImage image)
        {
            image = null;
            if (source == null) return false;

            try
            {
                HOperatorSet.GetObjClass(source, out var objectClass);
                if (objectClass.S != "image") return false;
                image = source as HImage;
                return true;
            }
            catch { return false; }
        }

        private void CaptureDisplayOutputs(IHandleRunModel node, bool append)
        {
            if (node?.Handle?.RunParameter == null) return;

            var outputs = BuildDisplayOutputs(node).ToList();
            var startOrder = DisplaySources.Count;
            for (var i = 0; i < outputs.Count; i++)
            {
                var output = outputs[i];
                var existing = DisplaySources.FirstOrDefault(x =>
                    ReferenceEquals(x.Node, node) &&
                    x.Output?.OutputKey == output.OutputKey);
                if (existing != null)
                {
                    existing.IsVisible = true;
                    continue;
                }

                var displaySource = new DisplaySourceItem(node, outputs[i], startOrder + i)
                {
                    IsVisible = true
                };
                displaySource.StateChanged += (_, __) => RenderDisplaySources(append: false);
                DisplaySources.Add(displaySource);
                RenderDisplaySourceItem(displaySource);
            }
        }

        private void ShowLastExecutedNodeOutput(IReadOnlyList<IHandleRunModel> executionOrder)
        {
            var lastNode = executionOrder?.LastOrDefault();
            if (lastNode == null) return;

            // 清除上次自动添加的显示源（可能是单节点执行留下的）
            foreach (var item in DisplaySources.Where(x => x.IsAutoAdded).ToList())
                DisplaySources.Remove(item);

            // 添加上游输入节点输出 + 当前最后节点输出
            var upstream = FindPrimaryUpstreamNode(lastNode);
            if (upstream != null)
                AddAutoDisplaySourceEntry(upstream);

            var lastSrc = AddAutoDisplaySourceEntry(lastNode);

            SyncDisplaySourceOrder();

            if (lastSrc != null)
                SelectedDisplaySource = lastSrc;

            RenderDisplaySources(append: false);
        }

        /// <summary>
        /// 查找节点的主上游节点（优先从连线找，其次从显式绑定找）。
        /// </summary>
        private IHandleRunModel FindPrimaryUpstreamNode(IHandleRunModel node)
        {
            if (node == null || ActiveWorkflow == null) return null;

            var fromConn = ActiveWorkflow.Connections
                .Where(x => ReferenceEquals(x.To, node))
                .Select(x => x.From)
                .FirstOrDefault();
            if (fromConn != null) return fromConn;

            return node.InputBindings
                .Where(x => !x.IsParameterBinding && x.SourceNode != null)
                .Select(x => x.SourceNode)
                .FirstOrDefault();
        }

        /// <summary>
        /// 将节点的默认输出添加为自动显示源条目，若已存在则跳过。返回新建的条目（或 null）。
        /// </summary>
        private DisplaySourceItem AddAutoDisplaySourceEntry(IHandleRunModel node)
        {
            if (node == null) return null;
            var output = GetDefaultDisplayOutput(node);
            if (output == null) return null;

            if (DisplaySources.Any(x => ReferenceEquals(x.Node, node) && x.Output?.OutputKey == output.OutputKey))
                return null;

            var src = new DisplaySourceItem(node, output, DisplaySources.Count) { IsVisible = true, IsAutoAdded = true };
            src.StateChanged += (_, __) => RenderDisplaySources(append: false);
            DisplaySources.Add(src);
            return src;
        }

        private void AddDisplaySource(WorkflowOutputOption option)
        {
            if (ActiveWorkflow == null)
            {
                return;
            }

            option ??= SelectedDisplayOutput;
            if (option == null || option.IsEmpty)
            {
                return;
            }

            var targetNode = option.SourceNode;
            var existing = DisplaySources.FirstOrDefault(x =>
                ReferenceEquals(x.Node, targetNode) &&
                x.Output?.OutputKey == option.OutputKey);
            if (existing != null)
            {
                SelectedDisplaySource = existing;
                RenderDisplaySources(append: false);
                return;
            }

            var displaySource = new DisplaySourceItem(
                targetNode,
                new DisplayOutputItem(targetNode, option.OutputKey, option.DisplayName),
                DisplaySources.Count)
            {
                IsVisible = true
            };
            displaySource.StateChanged += (_, __) => RenderDisplaySources(append: false);
            DisplaySources.Add(displaySource);
            SelectedDisplaySource = displaySource;
            RenderDisplaySourceItem(displaySource);
            MarkSchemeModified();
        }

        private void RemoveDisplaySource(DisplaySourceItem source)
        {
            if (ActiveWorkflow == null || source == null)
            {
                return;
            }

            if (DisplaySources.Remove(source))
            {
                if (ReferenceEquals(SelectedDisplaySource, source))
                {
                    SelectedDisplaySource = null;
                }

                SyncDisplaySourceOrder();
                RenderDisplaySources(append: false);
                MarkSchemeModified();
            }
        }

        private void MoveDisplaySourceUp(DisplaySourceItem source)
        {
            if (ActiveWorkflow == null || source == null)
            {
                return;
            }

            var index = DisplaySources.IndexOf(source);
            if (index <= 0)
            {
                return;
            }

            DisplaySources.Move(index, index - 1);
            SyncDisplaySourceOrder();
            RenderDisplaySources(append: false);
            MarkSchemeModified();
        }

        private void MoveDisplaySourceDown(DisplaySourceItem source)
        {
            if (ActiveWorkflow == null || source == null)
            {
                return;
            }

            var index = DisplaySources.IndexOf(source);
            if (index < 0 || index >= DisplaySources.Count - 1)
            {
                return;
            }

            DisplaySources.Move(index, index + 1);
            SyncDisplaySourceOrder();
            RenderDisplaySources(append: false);
            MarkSchemeModified();
        }

        private void RenderDisplaySourceItem(DisplaySourceItem item)
        {
            if (item == null || !item.IsVisible)
            {
                return;
            }

            var image = GetDisplayObject(item.Node?.Handle?.RunParameter, item.Output?.OutputKey);
            if (image == null)
            {
                return;
            }

            DisplayImage(image);
        }

        private void SyncDisplaySourceOrder()
        {
            for (var i = 0; i < DisplaySources.Count; i++)
            {
                DisplaySources[i].Order = i;
            }
        }

        private void RefreshSelectedNodeDisplayOutputs()
        {
            selectedNodeDisplayOutputs.Clear();

            if (SelectedNode == null)
            {
                SelectedDisplayOutput = null;
                return;
            }

            foreach (var slot in WorkflowBindingHelper.GetOutputSlots(SelectedNode))
            {
                if (slot?.ValueType == null || !typeof(HObject).IsAssignableFrom(slot.ValueType))
                {
                    continue;
                }

                selectedNodeDisplayOutputs.Add(new WorkflowOutputOption(SelectedNode, slot.Key, slot.DisplayName, slot.ValueType));
            }

            SelectedDisplayOutput = selectedNodeDisplayOutputs.FirstOrDefault();
        }

        private void RefreshAllWorkflowDisplayOutputs()
        {
            allWorkflowDisplayOutputs.Clear();

            if (ActiveWorkflow == null) return;

            foreach (var node in ActiveWorkflow.Nodes)
            {
                foreach (var slot in WorkflowBindingHelper.GetOutputSlots(node))
                {
                    if (slot?.ValueType == null || !typeof(HObject).IsAssignableFrom(slot.ValueType))
                        continue;

                    allWorkflowDisplayOutputs.Add(new WorkflowOutputOption(node, slot.Key,
                        $"{node.DisplayName} - {slot.DisplayName}", slot.ValueType));
                }
            }

            if (SelectedDisplayOutput == null || !allWorkflowDisplayOutputs.Contains(SelectedDisplayOutput))
                SelectedDisplayOutput = allWorkflowDisplayOutputs.FirstOrDefault();
        }

        private HObject GetCurrentDisplayObject()
        {
            var source = SelectedDisplaySource
                ?? DisplaySources.LastOrDefault(x => x.IsVisible);

            return GetDisplayObject(source?.Node?.Handle?.RunParameter, source?.Output?.OutputKey);
        }

        private static string GetDisplayOutputName(IHandleRunModel node, string outputKey)
        {
            if (node == null || string.IsNullOrWhiteSpace(outputKey))
            {
                return outputKey;
            }

            return WorkflowBindingHelper.GetOutputSlots(node)
                .FirstOrDefault(x => x.Key == outputKey)
                ?.DisplayName ?? outputKey;
        }

        private static IEnumerable<DisplayOutputItem> BuildDisplayOutputs(IHandleRunModel node)
        {
            var parameter = node?.Handle?.RunParameter;
            if (parameter == null)
            {
                yield break;
            }

            if (parameter.HOutput != null)
            {
                yield return new DisplayOutputItem(node, RunOutputKeys.Output, "输出图像/区域");
            }

            if (parameter.RawImage != null)
            {
                yield return new DisplayOutputItem(node, RunOutputKeys.RawImage, "原始图像");
            }

            if (parameter.GrayImage != null)
            {
                yield return new DisplayOutputItem(node, RunOutputKeys.GrayImage, "灰度图像");
            }

            foreach (var kv in parameter.Outputs.Where(x => x.Value is HObject))
            {
                yield return new DisplayOutputItem(node, kv.Key, kv.Key);
            }
        }

        private static HObject GetDisplayObject(RunParameter parameter, string outputKey)
        {
            if (parameter == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputKey) || outputKey == RunOutputKeys.Output)
            {
                return parameter.HOutput ?? parameter.GrayImage ?? parameter.RawImage ?? parameter.Outputs.Values.OfType<HObject>().FirstOrDefault();
            }

            return outputKey switch
            {
                RunOutputKeys.RawImage => parameter.RawImage,
                RunOutputKeys.GrayImage => parameter.GrayImage,
                _ => parameter.GetOutput<object>(outputKey) as HObject
            };
        }

        // ==================== 执行排序与参数绑定 ====================

        private IReadOnlyList<IHandleRunModel> GetExecutionOrder()
        {
            if (ActiveWorkflow == null) return Array.Empty<IHandleRunModel>();

            var nodes = ActiveWorkflow.Nodes.ToList();
            if (ActiveWorkflow.Connections.Count == 0)
            {
                var hasInputBindings = nodes.Any(x => x.InputBindings.Any(b => b.SourceNode != null));
                if (!hasInputBindings) return nodes;
            }

            var dependencies = nodes.ToDictionary(x => x, _ => new List<IHandleRunModel>());
            foreach (var connection in ActiveWorkflow.Connections)
                dependencies[connection.To].Add(connection.From);

            foreach (var node in nodes)
                foreach (var binding in node.InputBindings.Where(x => x.SourceNode != null))
                    dependencies[node].Add(binding.SourceNode);

            foreach (var node in nodes)
                dependencies[node] = dependencies[node]
                    .Where(x => x != null && !ReferenceEquals(x, node)).Distinct().ToList();

            var inDegree = nodes.ToDictionary(x => x, x => dependencies[x].Count);
            var queue = new Queue<IHandleRunModel>(nodes.Where(x => inDegree[x] == 0));
            var result = new List<IHandleRunModel>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                foreach (var dependent in dependencies
                    .Where(x => x.Value.Any(dep => ReferenceEquals(dep, current)))
                    .Select(x => x.Key).ToList())
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0) queue.Enqueue(dependent);
                }
            }

            if (result.Count != nodes.Count)
                throw new InvalidOperationException("流程存在循环连线，请删除或修改连线。");

            return result;
        }

        

        private RunParameter BuildConnectedInputParameter(IHandleRunModel node, Dictionary<object, object> clonedValues)
        {
            if (ActiveWorkflow == null) return null;

            var inputNodes = ActiveWorkflow.Connections
                .Where(x => ReferenceEquals(x.To, node))
                .Select(x => x.From)
                .Where(x => x?.Handle?.RunParameter != null)
                .ToList();

            if (inputNodes.Count == 0) return null;

            // 引用共享（写时复制语义）：HALCON 算子消费输入时不修改上游对象，深拷贝整幅图像
            // 是吞吐瓶颈。共享引用登记进参数的共享集合，其 Reset 不释放上游资源，
            // 算子新生成的输出仍由该参数正常持有与释放。
            if (inputNodes.Count == 1)
            {
                return inputNodes[0].Handle.RunParameter.CloneShared();
            }

            var mergedParameter = new RunParameter();
            foreach (var inputNode in inputNodes)
                mergedParameter.MergeFrom(inputNode.Handle.RunParameter, overwrite: true, deepClone: false);

            return mergedParameter;
        }

        /// <summary>
        /// 解析变量源绑定的当前值：流程级按当前流程解析（流程内执行自然命中），
        /// 并按目标槽/参数类型把字符串值转换成强类型（true/数字解析）。
        /// </summary>
        

        /// <summary>把变量文本按目标类型转换（bool/数值原样返回字符串给字符串槽）。</summary>
        

        

        private static bool TryConvertCollectionValue(object value, Type targetType, out object collectionValue)
        {
            collectionValue = null;
            if (!TryGetCollectionElementType(targetType, out var itemType) || value is string)
            {
                return false;
            }

            if (value is not System.Collections.IEnumerable source)
            {
                return false;
            }

            var collection = CreateCollection(targetType, itemType);
            foreach (var item in source)
            {
                collection.Add(Services.WorkflowExecutionEngine.ConvertParameterForLoad(item, itemType));
            }

            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(itemType, collection.Count);
                collection.CopyTo(array, 0);
                collectionValue = array;
                return true;
            }

            collectionValue = collection;
            return true;
        }

        private static bool TryGetCollectionElementType(Type collectionType, out Type itemType)
        {
            itemType = null;
            if (collectionType == typeof(string))
            {
                return false;
            }

            if (collectionType.IsArray)
            {
                itemType = collectionType.GetElementType();
                return itemType != null;
            }

            if (collectionType.IsGenericType &&
                collectionType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                itemType = collectionType.GetGenericArguments()[0];
                return true;
            }

            var enumerableType = collectionType
                .GetInterfaces()
                .Concat(new[] { collectionType })
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            itemType = enumerableType?.GetGenericArguments()[0];
            return itemType != null && typeof(System.Collections.IList).IsAssignableFrom(collectionType);
        }

        private static System.Collections.IList CreateCollection(Type collectionType, Type itemType)
        {
            if (collectionType.IsArray)
            {
                return new List<object>();
            }

            if (!collectionType.IsAbstract && collectionType.GetConstructor(Type.EmptyTypes) != null)
            {
                return (System.Collections.IList)Activator.CreateInstance(collectionType);
            }

            var observableType = typeof(ObservableCollection<>).MakeGenericType(itemType);
            return (System.Collections.IList)Activator.CreateInstance(observableType);
        }

        

        private static object CloneSlotValue(object value, Dictionary<object, object> clonedValues = null)
        {
            if (value == null)
            {
                return null;
            }

            if (clonedValues != null && clonedValues.TryGetValue(value, out var clonedValue))
            {
                return clonedValue;
            }

            return value switch
            {
                HObject hObject => CacheClone(clonedValues, value, hObject.Clone()),
                HTuple tuple => CacheClone(clonedValues, value, tuple.Clone()),
                _ => value
            };
        }

        private static object CacheClone(Dictionary<object, object> clonedValues, object source, object clone)
        {
            clonedValues?.Add(source, clone);
            return clone;
        }

        // ==================== 输入来源绑定 ====================

        private void ClearInputBinding(WorkflowInputBindingEditorItem item) => item?.Clear();

        private void SetInputBinding(WorkflowInputBindingRequest request)
        {
            if (request?.Binding == null) return;

            var option = request.Option;
            if (option?.IsEmpty != false)
            {
                request.Binding.Clear();
                return;
            }

            request.Binding.SourceNode = option.SourceNode;
            request.Binding.SourceOutputKey = option.OutputKey;
            MarkSchemeModified();
            RefreshSelectedInputBindings();
        }

        private void EnsureInputBinding(IHandleRunModel target, IHandleRunModel source, string outputKey,
            string fromPin = null, string toPin = null)
        {
            if (target?.InputBindings == null || source == null || target.InputBindings.Count == 0) return;

            // 显式指定下游输入槽（toPin）时精确匹配绑定。
            if (!string.IsNullOrWhiteSpace(toPin))
            {
                var exact = target.InputBindings.FirstOrDefault(x => !x.IsParameterBinding &&
                    (string.Equals(x.TargetInputKey, toPin, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.TargetInputName, toPin, StringComparison.OrdinalIgnoreCase)));
                if (exact != null)
                {
                    exact.SourceNode = source;
                    exact.SourceOutputKey = ResolveSourceOutputKey(source, fromPin) ?? outputKey;
                    return;
                }
            }

            var sourceSlots = WorkflowBindingHelper.GetOutputSlots(source).ToList();
            var binding = target.InputBindings
                .Where(x => x.SourceNode == null && !x.IsParameterBinding)
                .FirstOrDefault(x => sourceSlots.Any(slot =>
                    slot.Key == x.TargetInputKey && WorkflowBindingHelper.IsCompatible(x.TargetInputType, slot.ValueType)));
            var sourceOutputKey = binding?.TargetInputKey;

            binding ??= target.InputBindings
                .Where(x => x.SourceNode == null && !x.IsParameterBinding)
                .FirstOrDefault(x => sourceSlots.Any(slot =>
                    WorkflowBindingHelper.IsCompatible(x.TargetInputType, slot.ValueType)));
            sourceOutputKey ??= outputKey;

            binding ??= target.InputBindings.FirstOrDefault(x => !x.IsParameterBinding);
            if (binding == null) return;

            binding.SourceNode = source;
            // 显式指定上游输出槽（fromPin）时优先使用声明的槽键。
            binding.SourceOutputKey = ResolveSourceOutputKey(source, fromPin)
                ?? (string.IsNullOrWhiteSpace(sourceOutputKey) ? RunOutputKeys.Output : sourceOutputKey);
        }

        /// <summary>将 AI/调用方提供的引脚名解析为上游真实存在的输出槽键名（大小写/显示名宽松匹配）。</summary>
        private static string ResolveSourceOutputKey(IHandleRunModel source, string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) return null;

            return WorkflowBindingHelper.GetOutputSlots(source)
                .FirstOrDefault(slot =>
                    string.Equals(slot.Key, pin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(slot.DisplayName, pin, StringComparison.OrdinalIgnoreCase))?.Key
                ?? pin;
        }

        private void RefreshSelectedInputBindings()
        {
            if (ActiveWorkflow == null) return;

            ActiveWorkflow.SelectedInputBindings.Clear();
            if (ActiveWorkflow.SelectedNode?.InputBindings == null ||
                ActiveWorkflow.SelectedNode.InputBindings.Count == 0)
                return;

            // 候选与参数对话框的绑定页同源（BuildBindingCandidates：上游节点 + 全局/流程级变量），
            // 不再使用"遍历全部节点"的 BuildOutputOptions，两处绑定入口保持一致。
            var candidates = BuildBindingCandidates(ActiveWorkflow.SelectedNode);

            foreach (var binding in ActiveWorkflow.SelectedNode.InputBindings)
            {
                var options = new List<UI.Controls.WorkflowOutputOption> { new(null, null, "未绑定") };
                foreach (var candidate in candidates)
                {
                    if (candidate.IsEmpty) continue;

                    if (candidate.IsVariable)
                    {
                        options.Add(new UI.Controls.WorkflowOutputOption(candidate.VariableName, candidate.VariableScope));
                        continue;
                    }

                    // 节点输出：按绑定目标类型做兼容过滤（保留旧面板的类型过滤行为）
                    var slot = WorkflowBindingHelper.GetOutputSlots(candidate.SourceNode)
                        .FirstOrDefault(s => s.Key == candidate.OutputKey);
                    if (slot != null &&
                        !WorkflowBindingHelper.IsCompatible(binding.TargetInputType, slot.ValueType))
                    {
                        continue;
                    }

                    options.Add(new UI.Controls.WorkflowOutputOption(
                        candidate.SourceNode,
                        candidate.OutputKey,
                        candidate.Display,
                        slot?.ValueType));
                }

                ActiveWorkflow.SelectedInputBindings.Add(
                    new WorkflowInputBindingEditorItem(binding, options));
            }
        }

        // ==================== 辅助方法 ====================

        private static string BuildResultData(IHandleRunModel node)
        {
            var parameter = node?.Handle?.RunParameter;
            if (parameter == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(parameter.ResultMessage))
                return parameter.ResultMessage;

            var parts = new List<string>();
            var hasDefectCount = parameter.Outputs.TryGetValue(RunOutputKeys.DefectCount, out var defectCount);
            var hasTotalDefectArea = parameter.Outputs.TryGetValue(RunOutputKeys.TotalDefectArea, out var totalDefectArea);
            if (hasDefectCount || hasTotalDefectArea)
            {
                if (hasDefectCount)
                    if (hasTotalDefectArea)
                        parts.Add($"缺陷数量：{FormatOutputValue(defectCount)}");
                if (hasTotalDefectArea)
                    parts.Add($"总面积：{FormatOutputValue(totalDefectArea)}");
            }
            foreach (var output in parameter.Outputs
                .Where(x => x.Key != RunOutputKeys.Output &&
                    x.Key != RunOutputKeys.DefectCount &&
                    x.Key != RunOutputKeys.TotalDefectArea)
                .Take(8))
                parts.Add($"{output.Key}={FormatOutputValue(output.Value)}");

            return parts.Count == 0 ? node.StatusMessage : string.Join("；", parts);
        }

        private static string FormatOutputValue(object value)
        {
            return value switch
            {
                null => string.Empty,
                HObject => "图像/区域",
                HTuple tuple => FormatTuple(tuple),
                double number => number.ToString("0.###"),
                float number => number.ToString("0.###"),
                _ => value.ToString()
            };
        }

        private static string FormatTuple(HTuple tuple)
        {
            if (tuple == null) return string.Empty;

            var values = new List<string>();
            var length = Math.Min(tuple.Length, 6);
            for (var i = 0; i < length; i++)
                values.Add(tuple[i].ToString());

            var text = string.Join(", ", values);
            return tuple.Length > length ? $"{text}, ..." : text;
        }

        private static void PopulateReadOnlyCollection(object owner, PropertyInfo property, object sourceValue)
        {
            if (owner == null || property == null ||
                !TryGetCollectionElementType(property.PropertyType, out var itemType))
            {
                return;
            }

            if (property.GetValue(owner) is not System.Collections.IList target)
            {
                return;
            }

            target.Clear();

            if (sourceValue is JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (var item in element.EnumerateArray())
                {
                    target.Add(Services.WorkflowExecutionEngine.ConvertJsonElement(item, itemType));
                }

                return;
            }

            if (sourceValue is System.Collections.IEnumerable source && sourceValue is not string)
            {
                foreach (var item in source)
                {
                    target.Add(Services.WorkflowExecutionEngine.ConvertParameterForLoad(item, itemType));
                }
            }
        }

    }
}