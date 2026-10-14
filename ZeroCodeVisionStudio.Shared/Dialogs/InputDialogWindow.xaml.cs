using System.Windows;

namespace ZeroCodeVisionStudio.Shared.Dialogs
{
    /// <summary>
    /// 简单的文本输入对话框（替代 MahApps.Metro.ShowInputAsync，兼容定制版 Metro）。
    /// </summary>
    public partial class InputDialogWindow : Window
    {
        // ========== DependencyProperty — XAML Binding 可观测 ==========

        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(InputDialogWindow),
                new PropertyMetadata("输入"));

        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }

        public static readonly DependencyProperty PromptProperty =
            DependencyProperty.Register(nameof(Prompt), typeof(string), typeof(InputDialogWindow),
                new PropertyMetadata("请输入："));

        public string Prompt
        {
            get => (string)GetValue(PromptProperty);
            set => SetValue(PromptProperty, value);
        }

        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register(nameof(InputText), typeof(string), typeof(InputDialogWindow),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string InputText
        {
            get => (string)GetValue(InputTextProperty);
            set => SetValue(InputTextProperty, value);
        }

        // ========== 构造 + 路由事件 ==========

        public InputDialogWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (s, e) =>
            {
                InputBox.Focus();
                InputBox.Dispatcher.BeginInvoke(new Action(() => InputBox.SelectAll()),
                    System.Windows.Threading.DispatcherPriority.Input);
            };
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 静态快捷方法：弹出输入框，返回用户输入的字符串（取消或空输入返回 null）。
        /// </summary>
        public static string? Show(string title, string prompt, string initialValue = "", Window? owner = null)
        {
            var dlg = new InputDialogWindow
            {
                TitleText = title,
                Prompt = prompt,
                InputText = initialValue,
                Owner = owner ?? System.Windows.Application.Current?.MainWindow
            };
            var result = dlg.ShowDialog();
            return result == true ? dlg.InputText?.Trim() : null;
        }
    }
}
