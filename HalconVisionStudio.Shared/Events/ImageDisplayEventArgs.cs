using System;
using System.Collections.Generic;
using System.Text;
using HalconDotNet;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// 图像显示事件参数。
    /// 当算子执行过程中需要向主窗口推送预览图像时，通过 <see cref="IHandle.OnImageDisplay"/> 事件携带此参数。
    /// </summary>
    public class ImageDisplayEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化图像显示事件参数。
        /// </summary>
        /// <param name="image">需要显示的 HALCON 图像/区域/XLD 对象。</param>
        public ImageDisplayEventArgs(HObject image)
        {
            Image = image;
        }

        /// <summary>
        /// 需要显示的 HALCON 图像对象（HImage、HRegion、HXLD 均可）。
        /// 订阅者负责调用 HOperatorSet.DispObj 将其渲染到目标窗口。
        /// </summary>
        public HObject Image { get; }
    }
}
