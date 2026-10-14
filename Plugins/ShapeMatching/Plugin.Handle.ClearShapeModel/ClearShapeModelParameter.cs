using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ClearShapeModel
{
    public class ClearShapeModelParameter : BindableBase, IHandleParameter
    {
        private bool clearHalconModel;

        [DisplayName("清除 HALCON 模型")]
        [Description("是否同时调用 HALCON 释放底层形状模板句柄。关闭时仅清理流程侧引用。")]
        public bool ClearHalconModel { get => clearHalconModel; set => SetProperty(ref clearHalconModel, value); }

        public IHandleParameter Clone() => new ClearShapeModelParameter { ClearHalconModel = ClearHalconModel };

        public void Reset()
        {
            ClearHalconModel = false;
        }
    }
}
