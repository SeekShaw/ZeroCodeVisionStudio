using ZeroCodeVisionStudio.Shared.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using HalconDotNet;
using Microsoft.Win32;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 通用参数编辑对话框 ViewModel。
    /// 通过反射自动枚举可编辑属性，用户修改后可点击"执行"预览效果。
    /// 对话框打开时自动显示上游背景图像；执行后将结果叠加在背景图像上，不清除背景。
    /// </summary>
    public class ParameterDialogViewModel : HandleDialogBase
    {
        private IHandle handle;
        private IHandleParameter parameter;
        private string message;

        // 背景图像（对话框打开时从上游获取，整个生命周期内固定）
        private HObject backgroundImage;
        private HWindow halconWindow;

        /// <summary>左侧图像预览窗口，绑定到 View 的 ContentControl。</summary>
        public HSmartWindowControlWPF HWindow { get => field; private set => SetProperty(ref field, value); }

        /// <summary>对话框标题：算子显示名称（如 "阈值分割"）。</summary>
        public string DialogTitle { get => field; private set => SetProperty(ref field, value); } = "参数配置";

        /// <summary>算子分类（如 "图像输入"、"阈值分割"），显示为标题旁的标签。</summary>
        public string CategoryText { get => field; private set => SetProperty(ref field, value); }

        /// <summary>算子图标（与工具箱一致）。</summary>
        public System.Windows.Media.Geometry OperatorIcon { get => field; private set => SetProperty(ref field, value); }

        /// <summary>参数项数量（用于"N 项参数"角标与空状态判断）。</summary>
        public int ParameterCount => Properties.Count;

        /// <summary>关闭对话框命令（触发 Prism 关闭流程，仍会经过 CanCloseDialog 校验）。</summary>
        public DelegateCommand CloseCommand { get; }

        /// <summary>通过反射自动生成的参数属性项列表。</summary>
        public ObservableCollection<ParameterPropertyItem> Properties { get; } = new ObservableCollection<ParameterPropertyItem>();

        /// <summary>操作结果或引导文本，绑定到对话框底部的状态文本框。</summary>
        public string Message
        {
            get => message;
            private set => SetProperty(ref message, value);
        }

        /// <summary>打开文件命令。</summary>
        public DelegateCommand<ParameterPropertyItem> BrowseFileCommand { get; }

        /// <summary>保存文件命令。</summary>
        public DelegateCommand<ParameterPropertyItem> SaveFileCommand { get; }

        /// <summary>是否存在参数校验错误（存在时禁止关闭对话框，避免带错参数退出）。</summary>
        public bool HasValidationErrors => Properties.Any(x => x.HasError);

        /// <inheritdoc/>
        public override bool CanCloseDialog()
        {
            if (Properties.Any(x => x.HasError))
            {
                var firstError = Properties.First(x => x.HasError);
                Message = $"参数校验未通过：{firstError.ErrorMessage}";
                return false;
            }

            return true;
        }

        public ParameterDialogViewModel()
        {
            BrowseFileCommand = new DelegateCommand<ParameterPropertyItem>(BrowseFile);
            SaveFileCommand = new DelegateCommand<ParameterPropertyItem>(SaveFile);
            // Prism 9：DialogCloseListener 为 struct，直接调用 Invoke；
            // 关闭流程仍会经过 CanCloseDialog 校验（存在参数错误时被拦截）。
            CloseCommand = new DelegateCommand(() =>
                RequestClose.Invoke(new Prism.Dialogs.DialogResult(Prism.Dialogs.ButtonResult.OK)));
            ParamBindCommand = new DelegateCommand<ParameterPropertyItem>(OnParamBindIconClick);
        }

        /// <summary>参数行绑定图标命令：弹出候选菜单，选择即绑定/解绑该参数（VM 式直接入口）。</summary>
        public DelegateCommand<ParameterPropertyItem> ParamBindCommand { get; }

        /// <summary>刷新全部参数行的绑定状态图标（对话框打开与绑定变更后调用）。</summary>
        private void RefreshParamBindingStates()
        {
            var node = ParameterBindingAmbient.CurrentEditNode;
            foreach (var prop in Properties)
            {
                var target = WorkflowInputBinding.ParameterBindingPrefix + prop.Name;
                var binding = node?.InputBindings?.FirstOrDefault(x =>
                    string.Equals(x.TargetInputKey, target, StringComparison.Ordinal));
                prop.BindingSourceText = DescribeParamBinding(binding);
            }
        }

        private static string DescribeParamBinding(WorkflowInputBinding binding)
        {
            if (binding == null) return null;
            if (binding.IsVariableSource)
            {
                var parsed = binding.ParseVariableSource();
                return parsed == null ? null
                    : (parsed.Value.Scope == WorkflowVariableScope.Global ? $"[全局] {parsed.Value.Name}" : $"[流程] {parsed.Value.Name}");
            }
            if (binding.SourceNode != null && !string.IsNullOrWhiteSpace(binding.SourceOutputKey))
            {
                return $"{binding.SourceNode.DisplayName} · {binding.SourceOutputKey}";
            }
            return null;
        }

        /// <summary>参数行绑定图标点击：构建候选菜单项并应用到节点绑定。</summary>
        private void OnParamBindIconClick(ParameterPropertyItem prop)
        {
            if (prop == null || handle == null) return;
            var node = ParameterBindingAmbient.CurrentEditNode;
            if (node?.InputBindings == null) return;

            var target = WorkflowInputBinding.ParameterBindingPrefix + prop.Name;
            var binding = node.InputBindings.FirstOrDefault(x =>
                string.Equals(x.TargetInputKey, target, StringComparison.Ordinal));
            if (binding == null) return; // 该参数不可绑定（无预建参数绑定行）

            var menu = new System.Windows.Controls.ContextMenu
            {
                FontSize = 12,
                StaysOpen = false
            };

            void Apply(ParameterBindingCandidate candidate)
            {
                if (candidate.IsEmpty)
                {
                    binding.Clear();
                }
                else if (candidate.IsVariable)
                {
                    binding.SourceNode = null;
                    binding.SourceOutputKey = WorkflowInputBinding.MakeVariableSourceKey(candidate.VariableScope, candidate.VariableName);
                }
                else
                {
                    binding.SourceNode = candidate.SourceNode;
                    binding.SourceOutputKey = candidate.OutputKey;
                }
                node.RefreshInputBindings();
                ParameterBindingAmbient.OnBindingChanged?.Invoke(node);
                RefreshParamBindingStates();
            }

            var candidates = ParameterBindingAmbient.GetCandidates?.Invoke(node) ?? new List<ParameterBindingCandidate>();
            foreach (var candidate in candidates.Take(24))
            {
                var item = new System.Windows.Controls.MenuItem { Header = candidate.Display };
                var captured = candidate;
                item.Click += (s, e) => Apply(captured);
                menu.Items.Add(item);
            }

            if (menu.Items.Count == 0) return;
            menu.IsOpen = true;
        }

        /// <summary>参数项错误状态变化时刷新整体校验标记。</summary>
        private void OnPropertyItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParameterPropertyItem.ErrorMessage) ||
                e.PropertyName == nameof(ParameterPropertyItem.HasError))
            {
                RaisePropertyChanged(nameof(HasValidationErrors));
            }
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            parameter = parameters.GetValue<IHandleParameter>(Common.HandleParameter_ParamName);
            handle    = parameters.GetValue<IHandle>(Common.HandleParameter_HandleName);

            // 标题栏：优先取画布节点注册的算子名/分类/图标，取不到时退回类型名。
            var runModel = handle?.ContextRunModel;
            DialogTitle = runModel?.RegisterModel?.HandleName
                ?? runModel?.DisplayName
                ?? handle?.GetType().Name
                ?? "参数配置";
            CategoryText = runModel?.RegisterModel?.HandleType;
            OperatorIcon = runModel?.RegisterModel?.IconGeometry;

            HWindow = HalconRuntimeProbe.IsAvailable ? new HSmartWindowControlWPF() : null;

            if (HWindow != null)
            {
                HWindow.HInitWindow += (s, e) =>
                {
                    halconWindow = HWindow.HalconWindow;
                    if (halconWindow == null) return;

                    // 对于没有输入的算子（如读取模板），尝试自动执行以显示结果
                    if (ShouldAutoExecute())
                    {
                        AutoExecuteAndDisplay();
                    }
                    else
                    {
                        backgroundImage = GetBackgroundImage();
                        if (HalconDisplayHelper.IsValidObject(backgroundImage))
                        {
                            HalconDisplayHelper.SafeDispObj(backgroundImage, halconWindow);
                            HWindow.SetFullImagePart();
                        }
                    }
                };
            }

            if (handle != null)
                handle.OnImageDisplay += OnImageDisplay;

            LoadProperties();
            RefreshParamBindingStates();
            Message = Properties.Count == 0 ? "此算子无可编辑参数，点击执行查看结果。" : "编辑参数后点击执行预览结果。";
        }

        /// <summary>
        /// 判断是否应该自动执行算子（如读取模板等无输入算子）。
        /// </summary>
        private bool ShouldAutoExecute()
        {
            if (handle == null) return false;
            
            // 检查是否有输入绑定
            var runModel = handle.ContextRunModel;
            if (runModel?.InputBindings != null)
            {
                foreach (var binding in runModel.InputBindings)
                {
                    if (binding.SourceNode != null) return false;
                }
            }
            
            // 使用反射检查是否有 ModelPath 属性且已设置值
            if (parameter != null)
            {
                var modelPathProp = parameter.GetType().GetProperty("ModelPath");
                if (modelPathProp != null && modelPathProp.PropertyType == typeof(string))
                {
                    var path = modelPathProp.GetValue(parameter) as string;
                    return !string.IsNullOrWhiteSpace(path);
                }
            }
            
            return false;
        }

        /// <summary>
        /// 自动执行算子并显示结果。
        /// </summary>
        private void AutoExecuteAndDisplay()
        {
            try
            {
                handle?.SetRunParameter(new RunParameter());
                handle?.Execute();
                
                // 显示执行结果
                var resultImage = handle?.RunParameter?.HOutput;
                if (resultImage != null && halconWindow != null)
                {
                    HOperatorSet.SetSystem("clip_region", "false");
                    HOperatorSet.ClearWindow(halconWindow);
                    HOperatorSet.SetColor(halconWindow, "green");
                    HOperatorSet.SetDraw(halconWindow, "margin");
                    HalconDisplayHelper.SafeDispObj(resultImage, halconWindow);
                    HWindow.SetFullImagePart();
                }
                
                Message = handle?.RunParameter?.ResultMessage ?? "执行完成";
            }
            catch (Exception ex)
            {
                Message = $"自动执行失败: {ExceptionFormatter.Describe(ex)}";
            }
        }

        /// <inheritdoc/>
        public override void OnDialogClosed()
        {
            if (handle != null)
                handle.OnImageDisplay -= OnImageDisplay;

            foreach (var item in Properties)
                item.PropertyChanged -= OnPropertyItemChanged;

            backgroundImage = null;
            halconWindow = null;
            HWindow?.Dispose();
        }

        /// <inheritdoc/>

        protected override void OnExecute()
        {
            Message = DialogExecutionHelper.TryExecuteUpstreamThenSelf(handle);

            if (!HalconDisplayHelper.IsValidObject(backgroundImage))
            {
                backgroundImage = GetBackgroundImage();
            }

            var output = handle?.RunParameter?.HOutput;
            if (HalconDisplayHelper.IsValidObject(output))
            {
                DisplayResult(output);
            }
        }

        /// <inheritdoc/>
        protected override void OnReset()
        {
            parameter?.Reset();
            foreach (var item in Properties)
                item.Refresh();
            Message = "参数已重置。";
        }

        private void LoadProperties()
        {
            foreach (var item in Properties)
                item.PropertyChanged -= OnPropertyItemChanged;
            Properties.Clear();
            if (parameter == null) return;

            var properties = parameter.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(IsEditableProperty);

            foreach (var property in properties)
            {
                var item = new ParameterPropertyItem(parameter, property);
                item.PropertyChanged += OnPropertyItemChanged;
                Properties.Add(item);
            }

            RaisePropertyChanged(nameof(ParameterCount));
        }

        private static bool IsEditableProperty(PropertyInfo property)
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
                return false;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return type.IsEnum ||
                type == typeof(string) ||
                type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(float) ||
                type == typeof(double);
        }

        /// <summary>
        /// 算子执行后叠加显示结果。
        /// 直接通过 HWindow.HalconWindow 获取窗口句柄（不依赖缓存的 halconWindow），
        /// 避免 HInitWindow 时序问题。
        /// </summary>
        private void OnImageDisplay(object sender, ImageDisplayEventArgs e)
        {
            if (HWindow == null || e?.Image == null)
                return;

            DisplayResult(e.Image);
        }

        private void DisplayResult(HObject result)
        {
            if (HWindow == null || !HalconDisplayHelper.IsValidObject(result))
                return;

            HWindow.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow.HalconWindow;
                if (hwin == null) return;

                HOperatorSet.ClearWindow(hwin);
                if (!HalconDisplayHelper.IsValidObject(backgroundImage))
                    backgroundImage = GetBackgroundImage();

                if (HalconDisplayHelper.IsValidObject(backgroundImage))
                    HalconDisplayHelper.SafeDispObj(backgroundImage, hwin);

                if (!ReferenceEquals(result, backgroundImage))
                {
                    HalconDisplayHelper.SetDefaultDrawModeForObject(result, hwin);
                    HalconDisplayHelper.SafeDispObj(result, hwin);
                }

                HWindow.SetFullImagePart();
            });
        }

        /// <summary>
        /// 从上游节点或当前节点自身的输入数据中获取背景图像，用于对话框左侧预览。
        /// </summary>
        private HObject GetBackgroundImage()
        {
            var upstreamImg = HalconDisplayHelper.FindUpstreamImage(handle);
            if (HalconDisplayHelper.IsValidObject(upstreamImg)) return upstreamImg;

            var rp = handle?.RunParameter;
            return rp?.GrayImage ?? rp?.RawImage ?? rp?.HOutput;
        }

        /// <summary>
        /// 打开文件对话框，选择文件路径。
        /// </summary>
        private void BrowseFile(ParameterPropertyItem item)
        {
            if (item == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "选择文件",
                Filter = item.FileFilter ?? "All files (*.*)|*.*",
                CheckFileExists = true,
                RestoreDirectory = true
            };

            if (!string.IsNullOrWhiteSpace(item.Value?.ToString()))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(item.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                    dialog.FileName = item.Value.ToString();
                }
                catch { }
            }

            if (dialog.ShowDialog() == true)
            {
                item.Value = dialog.FileName;
            }
        }

        /// <summary>
        /// 保存文件对话框，选择保存路径。
        /// </summary>
        private void SaveFile(ParameterPropertyItem item)
        {
            if (item == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "保存文件",
                Filter = item.FileFilter ?? "All files (*.*)|*.*",
                DefaultExt = item.FileDefaultExtension,
                AddExtension = true,
                RestoreDirectory = true,
                CheckFileExists = false,
                ValidateNames = true
            };

            if (!string.IsNullOrWhiteSpace(item.Value?.ToString()))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(item.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(item.Value.ToString());
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    dialog.FileName = $"{fileName}_{timestamp}";
                }
                catch { }
            }
            else
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                dialog.FileName = $"model_{timestamp}";
            }

            if (dialog.ShowDialog() == true)
            {
                item.Value = dialog.FileName;
            }
        }
    }


    /// <summary>参数对话框绑定编辑行：一个输入槽或一个可绑定参数的当前源与候选。</summary>
    public class BindingRowItem : BindableBase
    {
        private ParameterBindingCandidate selectedCandidate;
        private string currentSourceText;

        /// <summary>目标节点。</summary>
        public IHandleRunModel Node { get; init; }

        /// <summary>绑定目标键（输入槽键 或 parameter:属性名）。</summary>
        public string TargetKey { get; init; }

        /// <summary>行显示名（"输入：图像" / "参数：最小灰度"）。</summary>
        public string DisplayName { get; init; }

        /// <summary>说明。</summary>
        public string Description { get; init; }

        /// <summary>候选列表（首项为未绑定）。</summary>
        public List<ParameterBindingCandidate> Candidates { get; init; }

        public ParameterBindingCandidate SelectedCandidate
        {
            get => selectedCandidate;
            set
            {
                if (SetProperty(ref selectedCandidate, value) && value != null)
                {
                    Apply(value);
                }
            }
        }

        public string CurrentSourceText
        {
            get => currentSourceText;
            private set => SetProperty(ref currentSourceText, value);
        }

        /// <summary>应用候选到节点绑定并通知主程序。</summary>
        private void Apply(ParameterBindingCandidate candidate)
        {
            var binding = Node?.InputBindings?.FirstOrDefault(x =>
                string.Equals(x.TargetInputKey, TargetKey, StringComparison.Ordinal));
            if (binding == null)
            {
                if (candidate.IsEmpty) return; // 本就未绑定
                binding = new WorkflowInputBinding(Node, TargetKey, DisplayName);
                Node?.InputBindings?.Add(binding);
            }

            if (candidate.IsEmpty)
            {
                binding.Clear();
            }
            else if (candidate.IsVariable)
            {
                binding.SourceNode = null;
                binding.SourceOutputKey = WorkflowInputBinding.MakeVariableSourceKey(candidate.VariableScope, candidate.VariableName);
            }
            else
            {
                binding.SourceNode = candidate.SourceNode;
                binding.SourceOutputKey = candidate.OutputKey;
            }

            CurrentSourceText = Describe();
            Node?.RefreshInputBindings();
            ParameterBindingAmbient.OnBindingChanged?.Invoke(Node);
        }

        /// <summary>当前源描述（用于行内展示）。</summary>
        public string Describe()
        {
            var binding = Node?.InputBindings?.FirstOrDefault(x =>
                string.Equals(x.TargetInputKey, TargetKey, StringComparison.Ordinal));
            if (binding == null || (binding.SourceNode == null && !binding.IsVariableSource))
            {
                return "未绑定";
            }

            if (binding.IsVariableSource)
            {
                var parsed = binding.ParseVariableSource();
                return parsed == null ? "变量" : $"变量 [{(parsed.Value.Scope == WorkflowVariableScope.Global ? "全局" : "流程")}] {parsed.Value.Name}";
            }

            return $"{binding.SourceNode.DisplayName} · {binding.SourceOutputKey}";
        }

        /// <summary>从节点现有绑定恢复选中候选（无匹配则选中"未绑定"）。</summary>
        public void RestoreSelection()
        {
            CurrentSourceText = Describe();
            var binding = Node?.InputBindings?.FirstOrDefault(x =>
                string.Equals(x.TargetInputKey, TargetKey, StringComparison.Ordinal));

            ParameterBindingCandidate match = Candidates[0];
            if (binding != null)
            {
                if (binding.IsVariableSource && binding.ParseVariableSource() is { } parsed)
                {
                    match = Candidates.FirstOrDefault(c =>
                        c.IsVariable && c.VariableName == parsed.Name && c.VariableScope == parsed.Scope) ?? match;
                }
                else if (binding.SourceNode != null)
                {
                    match = Candidates.FirstOrDefault(c =>
                        !c.IsEmpty && !c.IsVariable &&
                        ReferenceEquals(c.SourceNode, binding.SourceNode) &&
                        string.Equals(c.OutputKey, binding.SourceOutputKey, StringComparison.Ordinal)) ?? match;
                }
            }

            // 绕过 setter 的应用逻辑，仅恢复显示。
            selectedCandidate = match;
            RaisePropertyChanged(nameof(SelectedCandidate));
        }
    }
}
