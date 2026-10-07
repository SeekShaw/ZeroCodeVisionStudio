using HalconVisionStudio.Shared;
using HalconDotNet;

namespace Plugin.Handle.GenEmptyRegion
{
    [Handle("生成空区域", "GenEmptyRegion", "生成一个空区域，通常作为区域拼接的初始容器。", Common.HandleType_Geometry)]
    public class GenEmptyRegionHandle : HandleBase
    {
        public GenEmptyRegionHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public override void Handle()
        {
            HOperatorSet.GenEmptyRegion(out var emptyRegion);
            RunParameter.HOutput = emptyRegion;
            RunParameter.SetOutput(RunOutputKeys.Output, emptyRegion);
            RunParameter.SetOutput(RunOutputKeys.Region, emptyRegion);
            RaiseImageDisplay(emptyRegion);
        }
    }
}
