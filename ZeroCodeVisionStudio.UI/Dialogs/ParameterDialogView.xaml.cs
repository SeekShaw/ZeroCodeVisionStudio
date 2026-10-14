using System.Windows.Controls;

namespace ZeroCodeVisionStudio.UI.Dialogs
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
