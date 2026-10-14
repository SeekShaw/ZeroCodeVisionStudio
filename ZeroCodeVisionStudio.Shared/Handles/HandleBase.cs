using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using HalconDotNet;
using OpenCvSharp;

namespace ZeroCodeVisionStudio.Shared
{
    public abstract class HandleBase : BindableBase, IHandle
    {
        protected readonly IDialogService dialogService;

        protected abstract string DialogName { get; }
        private bool disposed;

        private static readonly Geometry DefaultIcon = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z");

        /// <summary>算子后端类型（从 HandleAttribute 反射读取）。</summary>
        public OperatorBackend Backend { get; }

        protected HandleBase(IDialogService dialogService)
        {
            this.dialogService = dialogService;
            Backend = ReadBackendFromAttribute();
        }

        private OperatorBackend ReadBackendFromAttribute()
        {
            var attr = GetType().GetCustomAttribute<HandleAttribute>();
            return attr?.OperatorBackend ?? OperatorBackend.Unknown;
        }

        public RunParameter RunParameter { get; protected set; }

        public IHandleParameter HandleParameter { get; protected set; }

        public virtual Geometry IconGeometry => DefaultIcon;

        public IHandleRunModel ContextRunModel { get; set; }

        public IReadOnlyList<object> ContextInputBindings { get; set; }

        public event EventHandler<ImageDisplayEventArgs> OnImageDisplay;

        public virtual void EditParameters()
        {
            DialogParameters param = new DialogParameters
            {
                { Common.HandleParameter_ParamName, HandleParameter },
                { Common.HandleParameter_HandleName, this },
                { Common.HandleParameter_RunModel, ContextRunModel }
            };
            dialogService.ShowDialog(DialogName, param);
        }

        protected void RaiseImageDisplay(HObject image)
        {
            OnImageDisplay?.Invoke(this, new ImageDisplayEventArgs(image));
        }

        /// <summary>
        /// OpenCV Mat 版本 —— 同时携带 Mat（不依赖 Halcon runtime），Halcon runtime 可用时也尝试转 HObject。
        /// 主窗口 OnHandleDisplayEvent 优先走 Mat 路径（不丢任何图像）。
        /// </summary>
        protected void RaiseImageDisplay(Mat mat)
        {
            if (mat == null || mat.Empty()) return;

            // 直接带 Mat —— 主窗口优先用这条路径，Halcon runtime 缺失时也能显示
            OnImageDisplay?.Invoke(this, new ImageDisplayEventArgs(mat));

            // Halcon runtime 可用时也转发一份 HObject（兼顾 Halcon 原生窗口订阅者）
            if (HalconRuntimeProbe.IsAvailable)
            {
                var hobj = MatHObjectConverter.MatToHObject(mat);
                if (hobj != null)
                {
                    try { OnImageDisplay?.Invoke(this, new ImageDisplayEventArgs(hobj)); }
                    finally { hobj.Dispose(); }
                }
            }
        }

        /// <summary>
        /// WPF BitmapSource 版本 —— 终极兜底（Halcon + OpenCV 原生 DLL 都不可用时用 Windows WIC 解码）。
        /// </summary>
        protected void RaiseImageDisplay(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            if (bitmap == null) return;
            OnImageDisplay?.Invoke(this, new ImageDisplayEventArgs(bitmap));
        }

        /// <summary>
        /// 统一执行入口 —— 先做运行时探测，再执行 Handle()。
        /// 调用方（工作流引擎 / 参数对话框等）应优先调用此方法，而非直接调用 <see cref="Handle"/>。
        /// 所有异常都会被捕获并写入 RunParameter.ResultMessage，不再冒泡。
        /// </summary>
        public void Execute()
        {
            // 1) Halcon Runtime 探测保护（Halcon / Hybrid 后端在未安装 Halcon 时跳过）
            if (HalconRuntimeProbe.ShouldSkipOperator(Backend))
            {
                RunParameter.ResultMessage = HalconRuntimeProbe.GetSkipReason(GetType().Name);
                RunParameter.Success = false;
                return;
            }

            // 2) OpenCV Runtime 探测保护（OpenCV / Hybrid 后端在 OpenCvSharpExtern 不可用时跳过）
            if ((Backend == OperatorBackend.OpenCV || Backend == OperatorBackend.Hybrid) && OpenCvRuntimeProbe.ShouldSkipOperator())
            {
                RunParameter.ResultMessage = OpenCvRuntimeProbe.GetSkipReason(GetType().Name);
                RunParameter.Success = false;
                return;
            }

            // 3) 执行算子本体 —— catch 所有异常，保证不冒泡
            try
            {
                Handle();
            }
            catch (Exception ex)
            {
                var friendly = ex switch
                {
                    DllNotFoundException => $"原生库缺失：{ex.Message}",
                    BadImageFormatException => $"DLL 架构不匹配（x64/x86）：{ex.Message}",
                    EntryPointNotFoundException => $"API 入口缺失（版本不匹配）：{ex.Message}",
                    TypeInitializationException => $"原生库初始化失败：{ex.InnerException?.Message ?? ex.Message}",
                    System.IO.FileNotFoundException => $"文件不存在：{ex.Message}",
                    OpenCvSharp.OpenCVException => $"OpenCV 执行失败：{ex.Message}",
                    HandleException he => he.Message,
                    _ => $"{ex.GetType().Name}：{ex.Message}"
                };
                RunParameter.ResultMessage = $"算子 [{GetType().Name}] 执行失败：{friendly}";
                RunParameter.Success = false;
            }
        }

        public abstract void Handle();

        public virtual void SetRunParameter(RunParameter parameter)
        {
            if (parameter != null && ReferenceEquals(parameter, RunParameter))
            {
                return;
            }

            ReleaseRunParameter();
            this.RunParameter = parameter ?? new RunParameter();
        }

        public void ReleaseResources()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                ReleaseRunParameter();
                OnImageDisplay = null;
                ContextRunModel = null;
                ContextInputBindings = null;
            }

            disposed = true;
        }

        protected void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private void ReleaseRunParameter()
        {
            RunParameter?.Reset();
            RunParameter = null;
        }
    }

    /// <summary>
    /// IHandle 扩展方法：统一执行入口，自动路由到 HandleBase.Execute() 或降级直调 Handle()。
    /// </summary>
    public static class HandleExecuteExtensions
    {
        public static void Execute(this IHandle handle)
        {
            if (handle is HandleBase hb)
                hb.Execute();
            else
                handle.Handle();
        }
    }
}
