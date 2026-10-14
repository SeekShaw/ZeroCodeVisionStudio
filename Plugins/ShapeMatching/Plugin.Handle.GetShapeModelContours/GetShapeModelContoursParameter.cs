using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.GetShapeModelContours
{
    public class GetShapeModelContoursParameter : BindableBase, IHandleParameter
    {
        private int level = 1;

        [DisplayName("金字塔层级")]
        [Description("提取模板轮廓使用的金字塔层级，通常取 1（最精细层）。")]
        public int Level { get => level; set => SetProperty(ref level, value); }

        public IHandleParameter Clone() => new GetShapeModelContoursParameter { Level = Level };

        public void Reset()
        {
            Level = 1;
        }
    }
}
