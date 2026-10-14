using System;
using System.Threading.Tasks;
using HalconDotNet;
using Prism.Commands;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// 测量类算子专用参数对话框 ViewModel 基类。
    /// 继承自 DefectDialogViewModel，增强了：
    /// 1. 交互式在 HALCON 窗口中手动绘制点/线的安全异步机制，绘制期间挂起控件平移缩放，防止右键冲突；
    /// 2. 测量结果与物理换算的实时响应与绑定；
    /// 3. 执行后自动从 RunParameter 提取并更新测量面板状态，且不重置用户缩放视口；
    /// 4. 恢复默认（Reset）与清除绘制（ClearDraw）功能，一键还原洁净背景与标准参数。
    /// </summary>
    public abstract class MeasureDialogViewModelBase<THandle, TParameter> : DefectDialogViewModel<THandle, TParameter>
        where THandle : IHandle
        where TParameter : BaseMeasureParameter
    {
        private string measureResultText = "尚未执行测量";
        private bool isDrawing;
        private string drawHint = "点击「绘制」按钮可在图像上交互式采集几何特征";

        protected MeasureDialogViewModelBase()
        {
            DrawCommand = new DelegateCommand(OnDrawAsync, () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);

            ClearDrawCommand = new DelegateCommand(OnClearDraw, () => !IsDrawing)
                .ObservesProperty(() => IsDrawing);
        }

        /// <summary>
        /// 测量结果提示信息（显示实测值与物理单位）。
        /// </summary>
        public string MeasureResultText
        {
            get => measureResultText;
            set => SetProperty(ref measureResultText, value);
        }

        /// <summary>
        /// 是否正在进行交互式绘制。
        /// </summary>
        public bool IsDrawing
        {
            get => isDrawing;
            set => SetProperty(ref isDrawing, value);
        }

        /// <summary>
        /// 交互式绘制提示语。
        /// </summary>
        public string DrawHint
        {
            get => drawHint;
            set => SetProperty(ref drawHint, value);
        }

        /// <summary>
        /// 交互绘制命令。
        /// </summary>
        public DelegateCommand DrawCommand { get; }

        /// <summary>
        /// 清除绘制标注图元命令（保留参数，仅清屏）。
        /// </summary>
        public new DelegateCommand ClearDrawCommand { get; }

        /// <summary>
        /// 执行算子并在完成后更新测量结果与 UI 状态。
        /// </summary>
        protected override void OnExecute()
        {
            base.OnExecute();
            UpdateResultState();
        }

        /// <summary>
        /// 恢复默认参数并清除已绘制内容。
        /// </summary>
        protected override void OnReset()
        {
            base.OnReset();
            MeasureResultText = "已恢复默认参数并清除绘制";
            DrawHint = "已恢复默认。点击「绘制」可在图像上交互式采集几何特征";
        }

        /// <summary>
        /// 清除当前窗口上的绘制内容与标注，恢复纯净背景图像。
        /// </summary>
        protected virtual void OnClearDraw()
        {
            ClearOverlay();
            MeasureResultText = "已清除绘制标注";
            DrawHint = "已清除标注。点击「绘制」可在图像上交互式采集几何特征";
        }

        /// <summary>
        /// 从算子输出中同步结果到界面。
        /// </summary>
        public virtual void UpdateResultState()
        {
            if (Handle?.RunParameter == null) return;
            MeasureResultText = Handle.RunParameter.ResultMessage ?? "测量完成";
        }

        /// <summary>
        /// 执行安全异步 HALCON 绘制动作。
        /// 绘制期间挂起控件自身的缩放与平移，防止与 HALCON 原生绘制的右键结束事件冲突。
        /// </summary>
        public async Task ExecuteDrawActionAsync(Action<HWindow> drawAction, string successMsg = "采集完成，已更新参数")
        {
            var hwin = HWindow?.HalconWindow;
            if (hwin == null)
            {
                DrawHint = "图像窗口尚未初始化，请稍后再试";
                return;
            }

            IsDrawing = true;
            DrawHint = "正在采集中... 在左侧窗口左键点击/拖动，右键确定/结束";

            var prevZoom = HSmartWindowControlWPF.ZoomContent.WheelForwardZoomsIn;
            var prevMove = true;
            if (HWindow != null)
            {
                prevZoom = HWindow.HZoomContent;
                prevMove = HWindow.HMoveContent;
                HWindow.HZoomContent = HSmartWindowControlWPF.ZoomContent.Off;
                HWindow.HMoveContent = false;
            }

            try
            {
                await Task.Run(() => drawAction(hwin));
                DrawHint = successMsg;
            }
            catch (HOperatorException ex)
            {
                DrawHint = $"操作已取消或出错：{ex.Message}";
                return;
            }
            catch (Exception ex)
            {
                DrawHint = $"采集出错：{ex.Message}";
                return;
            }
            finally
            {
                if (HWindow != null)
                {
                    HWindow.HZoomContent = prevZoom;
                    HWindow.HMoveContent = prevMove;
                }
                IsDrawing = false;
            }

            try
            {
                // 采集完成后自动尝试执行一次测量计算与预览
                OnExecute();
            }
            catch (Exception ex)
            {
                DrawHint = $"{successMsg}（提示：{ex.Message}）";
            }
        }

        /// <summary>
        /// 默认点击绘制按钮时的异步操作。
        /// </summary>
        private async void OnDrawAsync()
        {
            await ExecuteDrawActionAsync(PerformDraw);
        }

        /// <summary>
        /// 具体的交互式 Halcon 绘制逻辑（在后台线程执行）。
        /// </summary>
        protected abstract void PerformDraw(HWindow hwin);
    }
}
