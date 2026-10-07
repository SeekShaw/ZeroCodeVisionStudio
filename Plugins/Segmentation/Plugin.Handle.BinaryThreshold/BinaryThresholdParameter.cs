using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.BinaryThreshold
{
    public class BinaryThresholdParameter : BindableBase, IHandleParameter
    {
        private ELightDark lightDark = ELightDark.dark;
        private EMethod method = EMethod.max_separability;

        [DisplayName("亮暗极性")]
        public ELightDark LightDark
        {
            get => lightDark;
            set => SetProperty(ref lightDark, value);
        }

        [DisplayName("方法")]
        public EMethod Method
        {
            get => method;
            set => SetProperty(ref method, value);
        }

        public IHandleParameter Clone()
        {
            return new BinaryThresholdParameter
            {
                LightDark = LightDark,
                Method = Method
            };
        }

        public void Reset()
        {
            LightDark = ELightDark.dark;
            Method = EMethod.max_separability;
        }
    }

    public enum EMethod
    {
        max_separability,
        smooth_histo
    }
}
