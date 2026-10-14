using HalconDotNet;

namespace ZeroCodeVisionStudio.Shared
{
    public static class HalconShapeModelResource
    {
        public static void TryClear(HTuple modelId)
        {
            if (modelId == null || modelId.Length == 0)
            {
                return;
            }

            try
            {
                HOperatorSet.ClearShapeModel(modelId);
            }
            catch (HOperatorException)
            {
                // The model may have been explicitly cleared by another node.
            }
        }
    }
}
