namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// Allows a handle to release native resources which are not represented by HObject.
    /// </summary>
    public interface IHandleRuntimeResourceOwner
    {
        void ReleaseRuntimeResources();
    }
}
