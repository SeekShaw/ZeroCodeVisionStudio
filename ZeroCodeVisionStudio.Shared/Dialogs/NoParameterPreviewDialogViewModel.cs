using HalconDotNet;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 无参数算子的预览对话框 ViewModel。
    /// 对话框打开时自动显示上游背景图像；执行后将结果叠加在背景上，不清除原图。
    /// </summary>
    public class NoParameterPreviewDialogViewModel : BindableBase, IDialogAware
    {
        private IHandle handle;
        private string message;
        private HObject backgroundImage;
        private HWindow halconWindow;

        public NoParameterPreviewDialogViewModel()
        {
            ExecuteCommand = new DelegateCommand(OnExecute);
            CloseCommand = new DelegateCommand(() =>
                RequestClose.Invoke(new Prism.Dialogs.DialogResult(Prism.Dialogs.ButtonResult.OK)));
        }

        public DialogCloseListener RequestClose { get; set; }

        /// <summary>内嵌的 HALCON 智能窗口控件，用于显示算子处理结果图像。</summary>
        public HSmartWindowControlWPF HWindow { get => field; private set => SetProperty(ref field, value); }

        /// <summary>对话框标题：算子显示名称。</summary>
        public string DialogTitle { get => field; private set => SetProperty(ref field, value); } = "参数配置";

        /// <summary>算子分类。</summary>
        public string CategoryText { get => field; private set => SetProperty(ref field, value); }

        /// <summary>算子图标（与工具箱一致）。</summary>
        public System.Windows.Media.Geometry OperatorIcon { get => field; private set => SetProperty(ref field, value); }

        public DelegateCommand ExecuteCommand { get; }

        /// <summary>关闭对话框命令。</summary>
        public DelegateCommand CloseCommand { get; }

        /// <summary>执行结果消息，绑定到对话框底部的状态文本框。</summary>
        public string Message { get => message; private set => SetProperty(ref message, value); }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            if (handle != null)
                handle.OnImageDisplay -= OnImageDisplay;

            backgroundImage = null;
            halconWindow = null;
            HWindow?.Dispose();
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            HWindow = HalconRuntimeProbe.IsAvailable ? new HSmartWindowControlWPF() : null;

            handle = parameters.GetValue<IHandle>(Common.HandleParameter_HandleName);

            // 标题栏：算子名/分类/图标（与通用参数对话框一致）。
            var runModel = handle?.ContextRunModel;
            DialogTitle = runModel?.RegisterModel?.HandleName
                ?? runModel?.DisplayName
                ?? handle?.GetType().Name
                ?? "参数配置";
            CategoryText = runModel?.RegisterModel?.HandleType;
            OperatorIcon = runModel?.RegisterModel?.IconGeometry;

            if (HWindow != null)
            {
                HWindow.HInitWindow += (s, e) =>
                {
                    halconWindow = HWindow.HalconWindow;
                    if (halconWindow == null) return;

                    backgroundImage = GetBackgroundImage();
                    if (HalconDisplayHelper.IsValidObject(backgroundImage))
                    {
                        HalconDisplayHelper.SafeDispObj(backgroundImage, halconWindow);
                        HWindow.SetFullImagePart();
                    }
                };
            }

            if (handle != null)
                handle.OnImageDisplay += OnImageDisplay;

            Message = "此算子无需参数。点击执行查看结果。";
        }

        private void OnExecute()
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

        /// <summary>
        /// 叠加显示执行结果：先清窗口，再显示背景，最后叠加结果。
        /// 直接通过 HWindow.HalconWindow 获取窗口句柄，避免 HInitWindow 时序问题。
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

        private HObject GetBackgroundImage()
        {
            var upstreamImg = HalconDisplayHelper.FindUpstreamImage(handle);
            if (HalconDisplayHelper.IsValidObject(upstreamImg)) return upstreamImg;

            var rp = handle?.RunParameter;
            return rp?.GrayImage ?? rp?.RawImage ?? rp?.HOutput;
        }
    }
}
