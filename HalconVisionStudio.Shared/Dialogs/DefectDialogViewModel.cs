using System.Windows.Input;
using HalconVisionStudio.Shared.Diagnostics;
using HalconDotNet;
using Prism.Commands;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 缺陷检测算子专用对话框 ViewModel 基类。
    /// <para>
    /// 提供左右分栏布局所需的 HWindow（左侧图像预览）和参数编辑支持。
    /// 对话框打开时自动显示上游背景图像；执行后将结果叠加在背景图像上，不清除背景。
    /// </para>
    /// </summary>
    public abstract class DefectDialogViewModel<THandle, TParameter> : HandleDialogBase
        where THandle : IHandle
        where TParameter : IHandleParameter
    {
        private TParameter parameter;
        private THandle handle;

        // 背景图像（对话框打开时从上游获取，整个对话框生命周期内固定）
        private HObject backgroundImage;
        // HALCON 窗口句柄（HInitWindow 事件触发后才可用）
        private HWindow halconWindow;
        private string message;

        /// <summary>操作结果或引导文本，绑定到对话框底部的状态文本框。</summary>
        public virtual string Message
        {
            get => message;
            set => SetProperty(ref message, value);
        }

        protected DefectDialogViewModel()
        {
            ClearOverlayCommand = new DelegateCommand(ClearOverlay);
            ClearDrawCommand = ClearOverlayCommand;
        }

        /// <summary>清除窗口叠加层命令。</summary>
        public ICommand ClearOverlayCommand { get; }

        /// <summary>清除窗口绘制/叠加层命令（别名）。</summary>
        public ICommand ClearDrawCommand { get; protected set; }

        /// <summary>左侧图像预览窗口，绑定到 View 的 ContentControl。</summary>
        public HSmartWindowControlWPF HWindow { get => field; private set => SetProperty(ref field, value); }

        /// <summary>当前算子的参数实例，绑定到视图后直接编辑原始对象。</summary>
        public TParameter Parameter
        {
            get => parameter;
            private set => SetProperty(ref parameter, value);
        }

        /// <summary>当前对话框关联的算子实例，仅供派生类访问。</summary>
        protected THandle Handle
        {
            get => handle;
            private set => handle = value;
        }

        /// <summary>
        /// 检查 HObject 是否为有效、已初始化且包含至少一个实际图元的 HALCON 对象。
        /// </summary>
        public static bool IsValidHalconObject(HObject obj) => HalconDisplayHelper.IsValidObject(obj);

        /// <summary>
        /// 安全向 HALCON 窗口渲染对象，遇到无效或空对象自动忽略，绝不抛出 #4056 异常。
        /// </summary>
        public static void SafeDispObj(HObject obj, HWindow hwin) => HalconDisplayHelper.SafeDispObj(obj, hwin);

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            Parameter = parameters.GetValue<TParameter>(Common.HandleParameter_ParamName);
            Handle    = parameters.GetValue<THandle>(Common.HandleParameter_HandleName);

            HWindow = new HSmartWindowControlWPF();

            // 等待 HALCON 窗口初始化完成后显示背景图像
            HWindow.HInitWindow += (s, e) =>
            {
                halconWindow = HWindow.HalconWindow;
                if (halconWindow == null) return;

                backgroundImage = GetBackgroundImage();
                if (IsValidHalconObject(backgroundImage))
                {
                    SafeDispObj(backgroundImage, halconWindow);
                    HWindow.SetFullImagePart();
                }
            };

            if (Handle != null)
                Handle.OnImageDisplay += OnImageDisplay;
        }

        /// <inheritdoc/>
        public override void OnDialogClosed()
        {
            if (Handle != null)
                Handle.OnImageDisplay -= OnImageDisplay;

            // backgroundImage 是从上游借用的引用，不由本对话框释放
            backgroundImage = null;
            halconWindow = null;
            HWindow?.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnExecute()
        {
            try
            {
                Message = DialogExecutionHelper.TryExecuteUpstreamThenSelf(Handle);
            }
            catch (Exception ex)
            {
                Message = $"执行异常: {ExceptionFormatter.Describe(ex)}";
            }

            if (!IsValidHalconObject(backgroundImage))
            {
                backgroundImage = GetBackgroundImage();
            }

            // 兜底保障：若算子未触发 OnImageDisplay 但输出了有效图元，主动刷新显示
            var output = Handle?.RunParameter?.HOutput;
            if (IsValidHalconObject(output))
            {
                DisplayResult(output);
            }
        }

        /// <summary>
        /// 清除窗口上的算子结果或绘制叠加层，重新只显示原始背景图像。
        /// </summary>
        public virtual void ClearOverlay()
        {
            if (HWindow == null) return;
            if (!IsValidHalconObject(backgroundImage))
            {
                backgroundImage = GetBackgroundImage();
            }

            HWindow.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow.HalconWindow;
                if (hwin == null) return;

                HOperatorSet.ClearWindow(hwin);
                if (IsValidHalconObject(backgroundImage))
                {
                    SafeDispObj(backgroundImage, hwin);
                }
            });

            if (Handle?.RunParameter != null)
            {
                // 严禁将 borrowed 的 backgroundImage 直接赋值给 HOutput，
                // 否则后续 Handle 执行覆盖 HOutput 时，RunParameter 会自动 DisposeDetachedResource 导致 backgroundImage 被销毁！
                if (IsValidHalconObject(backgroundImage))
                {
                    try
                    {
                        HOperatorSet.CopyObj(backgroundImage, out var copy, 1, -1);
                        Handle.RunParameter.HOutput = copy;
                    }
                    catch
                    {
                        Handle.RunParameter.HOutput = null;
                    }
                }
                else
                {
                    Handle.RunParameter.HOutput = null;
                }
                Handle.RunParameter.ResultMessage = null;
            }
        }

        /// <inheritdoc/>
        protected override void OnReset()
        {
            Parameter?.Reset();
            ClearOverlay();
        }

        /// <summary>
        /// 算子执行后触发的图像显示回调。
        /// 直接通过 HWindow.HalconWindow 获取窗口句柄（不依赖缓存），
        /// 避免 HInitWindow 时序问题导致 halconWindow 为 null 时无法显示。
        /// 显示顺序：背景图像 → 叠加算子结果。
        /// </summary>
        private void OnImageDisplay(object sender, ImageDisplayEventArgs e)
        {
            if (HWindow == null)
                return;

            HWindow.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow.HalconWindow;
                if (hwin == null) return;

                HOperatorSet.ClearWindow(hwin);
                if (!IsValidHalconObject(backgroundImage))
                {
                    backgroundImage = GetBackgroundImage();
                }
                if (IsValidHalconObject(backgroundImage))
                {
                    SafeDispObj(backgroundImage, hwin);
                }
                if (IsValidHalconObject(e?.Image) && !ReferenceEquals(e.Image, backgroundImage))
                {
                    HalconDisplayHelper.SetDefaultDrawModeForObject(e.Image, hwin);
                    SafeDispObj(e.Image, hwin);
                }
            });
        }

        private void DisplayResult(HObject result)
        {
            if (HWindow == null || !IsValidHalconObject(result)) return;
            HWindow.Dispatcher.Invoke(() =>
            {
                var hwin = HWindow.HalconWindow;
                if (hwin == null) return;

                HOperatorSet.ClearWindow(hwin);
                if (!IsValidHalconObject(backgroundImage))
                {
                    backgroundImage = GetBackgroundImage();
                }
                if (IsValidHalconObject(backgroundImage))
                {
                    SafeDispObj(backgroundImage, hwin);
                }
                if (!ReferenceEquals(result, backgroundImage))
                {
                    HalconDisplayHelper.SetDefaultDrawModeForObject(result, hwin);
                    SafeDispObj(result, hwin);
                }
            });
        }

        /// <summary>
        /// 从上游节点或当前 Handle 的 RunParameter 中获取背景图像。
        /// 优先使用最近的上游输出；若无上游则使用当前节点已有的图像。
        /// </summary>
        private HObject GetBackgroundImage()
        {
            var upstreamImg = HalconDisplayHelper.FindUpstreamImage(Handle);
            if (IsValidHalconObject(upstreamImg)) return upstreamImg;

            var rp = Handle?.RunParameter;
            var local = rp?.RawImage ?? rp?.GrayImage ?? rp?.HOutput;
            if (IsValidHalconObject(local)) return local;

            return null;
        }
    }
}
