using System.Windows.Controls;

namespace HalconVisionStudio.UI.Dialogs
{
    public partial class ParameterDialogView : UserControl
    {
        public ParameterDialogView()
        {
            InitializeComponent();
            Loaded += (_, __) => BindingHost.BindToCurrent();
        }
    }
}
