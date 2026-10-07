using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using HalconDotNet;

namespace HalconVisionStudio.Shared
{
    public abstract class HandleBase : BindableBase, IHandle
    {
        protected readonly IDialogService dialogService;

        protected abstract string DialogName { get; }
        private bool disposed;

        private static readonly Geometry DefaultIcon = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z");

        protected HandleBase(IDialogService dialogService)
        {
            this.dialogService = dialogService;
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


        public abstract void Handle();


        public virtual void SetRunParameter(RunParameter parameter)
        {
            // parameter == null 的语义是"重置为全新实例"。首次调用时属性本身也是 null，
            // ReferenceEquals(null, null) 为真会提前返回，导致 RunParameter 永远不会被创建——
            // 未执行过流程的节点直接在参数对话框点"执行"就会在 Handle() 内空引用。
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
}
