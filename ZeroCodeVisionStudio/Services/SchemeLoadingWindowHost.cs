using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroCodeVisionStudio.Views;

namespace ZeroCodeVisionStudio.Services
{
    internal sealed class SchemeLoadingWindowHost : IDisposable
    {
        private readonly TaskCompletionSource<bool> readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly double left;
        private readonly double top;
        private readonly double width;
        private readonly double height;
        private readonly Thread uiThread;

        private Dispatcher dispatcher;
        private SchemeLoadingWindow window;
        private bool disposed;

        private SchemeLoadingWindowHost(Window owner, string message)
        {
            CaptureOwnerBounds(owner, out left, out top, out width, out height);
            Message = message;
            uiThread = new Thread(RunWindowThread)
            {
                IsBackground = true,
                Name = "SchemeLoadingWindow"
            };
            uiThread.SetApartmentState(ApartmentState.STA);
        }

        private string Message { get; set; }

        public static async Task<SchemeLoadingWindowHost> ShowAsync(Window owner, string message)
        {
            var host = new SchemeLoadingWindowHost(owner, message);
            host.uiThread.Start();
            await host.readySource.Task.ConfigureAwait(true);
            return host;
        }

        public void UpdateMessage(string message)
        {
            if (disposed)
            {
                return;
            }

            Message = message;
            var currentDispatcher = dispatcher;
            if (currentDispatcher == null)
            {
                return;
            }

            currentDispatcher.BeginInvoke(new Action(() =>
            {
                window?.UpdateMessage(message);
            }), DispatcherPriority.Normal);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            var currentDispatcher = dispatcher;
            if (currentDispatcher == null)
            {
                return;
            }

            currentDispatcher.BeginInvoke(new Action(() =>
            {
                window?.Close();
            }), DispatcherPriority.Send);
        }

        private void RunWindowThread()
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                window = new SchemeLoadingWindow(Message)
                {
                    Left = left + Math.Max(0, width - 360) / 2,
                    Top = top + Math.Max(0, height - 210) / 2
                };
                window.Closed += (_, __) =>
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                };
                window.Show();
                readySource.TrySetResult(true);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                readySource.TrySetResult(false);
            }
        }

        private static void CaptureOwnerBounds(Window owner, out double ownerLeft, out double ownerTop, out double ownerWidth, out double ownerHeight)
        {
            if (owner != null)
            {
                ownerLeft = owner.Left;
                ownerTop = owner.Top;
                ownerWidth = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
                ownerHeight = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
                if (!double.IsNaN(ownerLeft) &&
                    !double.IsNaN(ownerTop) &&
                    ownerWidth > 0 &&
                    ownerHeight > 0 &&
                    owner.WindowState != WindowState.Minimized)
                {
                    return;
                }
            }

            var workArea = SystemParameters.WorkArea;
            ownerLeft = workArea.Left;
            ownerTop = workArea.Top;
            ownerWidth = workArea.Width;
            ownerHeight = workArea.Height;
        }
    }
}
