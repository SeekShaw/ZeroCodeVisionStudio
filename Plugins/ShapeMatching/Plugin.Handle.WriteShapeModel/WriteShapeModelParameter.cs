using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.WriteShapeModel
{
    public class WriteShapeModelParameter : BindableBase, IHandleParameter
    {
        private string modelPath = string.Empty;

        [DisplayName("模板路径")]
        [Description("形状模板文件的保存路径，支持 .shm 格式")]
        [FileDialog(FileDialogType.Save, "Shape Model (*.shm)|*.shm|All files (*.*)|*.*", ".shm")]
        public string ModelPath { get => modelPath; set => SetProperty(ref modelPath, value); }

        public IHandleParameter Clone() => new WriteShapeModelParameter { ModelPath = ModelPath };

        public void Reset()
        {
            ModelPath = string.Empty;
        }
    }
}
