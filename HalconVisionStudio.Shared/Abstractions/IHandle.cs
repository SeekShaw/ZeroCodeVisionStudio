using System.Windows;
using System.Windows.Media;

namespace HalconVisionStudio.Shared
{

    public interface IHandle : IDisposable
    {

        RunParameter RunParameter { get; }

        IHandleParameter HandleParameter { get; }


        IHandleRunModel ContextRunModel { get; set; }

        IReadOnlyList<object> ContextInputBindings { get; set; }

        Geometry IconGeometry { get; }

        void SetRunParameter(RunParameter parameter);

        void Handle();

        void EditParameters();

        void ReleaseResources();
         
        event EventHandler<ImageDisplayEventArgs> OnImageDisplay;
    }
}
