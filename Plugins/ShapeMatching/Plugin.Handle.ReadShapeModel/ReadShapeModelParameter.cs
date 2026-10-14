using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ReadShapeModel
{
    public class ReadShapeModelParameter : BindableBase, IHandleParameter
    {
        private string modelPath = string.Empty;

        [DisplayName("模板路径")]
        [Description("形状模板文件的加载路径，支持 .shm 格式")]
        [FileDialog(FileDialogType.Open, "Shape Model (*.shm)|*.shm|All files (*.*)|*.*")]
        public string ModelPath { get => modelPath; set => SetProperty(ref modelPath, value); }

        public IHandleParameter Clone() => new ReadShapeModelParameter { ModelPath = ModelPath };

        public void Reset()
        {
            ModelPath = string.Empty;
        }
    }
}
