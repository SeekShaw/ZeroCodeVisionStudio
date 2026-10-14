using System;
using System.Windows.Media;
using ZeroCodeVisionStudio.Shared;

namespace ZeroCodeVisionStudio.Core
{
    /// <summary>
    /// 算子实例化失败时的占位实现。
    /// 不调用任何 Halcon API，Handle() 直接标记运行失败。
    /// 用于在 Halcon 运行时缺失或算子类型加载失败时替代真正的算子实例，
    /// 避免 HandleRunModel 构造崩溃导致整个程序闪退。
    /// </summary>
    public class ErrorStubHandle : IHandle
    {
        private readonly IHandleRegisterModel registerModel;
        private readonly Exception creationError;
        private RunParameter runParameter;

        public ErrorStubHandle(IHandleRegisterModel registerModel, Exception creationError)
        {
            this.registerModel = registerModel;
            this.creationError = creationError;
        }

        public RunParameter RunParameter => runParameter ??= new RunParameter();

        public IHandleParameter HandleParameter { get; } = null;

        public IHandleRunModel ContextRunModel { get; set; }

        public System.Collections.Generic.IReadOnlyList<object> ContextInputBindings { get; set; }

        public Geometry IconGeometry => registerModel?.IconGeometry;

        public void SetRunParameter(RunParameter parameter) => runParameter = parameter ?? new RunParameter();

        /// <summary>
        /// 占位算子的 Handle() — 不调用任何 Halcon API。
        /// 直接设置 RunParameter 失败状态并记录错误信息。
        /// </summary>
        public void Handle()
        {
            var message = $"算子 [{registerModel?.HandleName ?? "未知"}] 初始化失败：" +
                          (creationError?.Message ?? "未知错误") +
                          "。请检查 Halcon 运行时是否已正确安装。";
            RunParameter.Success = false;
            RunParameter.ResultMessage = message;
            RunParameter.SetOutput(RunOutputKeys.ExecutionSuccess, false);
            RunParameter.SetOutput(RunOutputKeys.ExecutionStatus, "失败");
        }

        public void EditParameters()
        {
            // 占位算子无法编辑参数
            System.Windows.MessageBox.Show(
                $"无法编辑参数：算子初始化失败。{Environment.NewLine}{creationError?.Message}",
                "算子不可用",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        public void ReleaseResources() { }

        public event EventHandler<ImageDisplayEventArgs> OnImageDisplay;

        public void Dispose() { }
    }
}
