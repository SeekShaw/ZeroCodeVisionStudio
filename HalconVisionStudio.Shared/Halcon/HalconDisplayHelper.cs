using System;
using HalconDotNet;

namespace HalconVisionStudio.Shared
{
    /// <summary>
    /// HALCON 图元有效性校验与安全窗口渲染辅助类。
    /// 防止空图元（CountObj == 0）、未初始化图元（Key == IntPtr.Zero）或已释放图元
    /// 在调用 DispObj 时触发 HALCON error #4056 (object-ID is NULL)。
    /// </summary>
    public static class HalconDisplayHelper
    {
        /// <summary>
        /// 检查 HObject 是否为有效、已初始化且包含至少一个实际图元的 HALCON 对象。
        /// </summary>
        public static bool IsValidObject(HObject obj)
        {
            if (obj == null || !obj.IsInitialized() || obj.Key == IntPtr.Zero)
                return false;
            try
            {
                HOperatorSet.CountObj(obj, out var count);
                return count.Length > 0 && count.I > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全向 HALCON 窗口渲染对象，遇到无效或空对象自动忽略，绝不抛出 #4056 异常。
        /// </summary>
        public static void SafeDispObj(HObject obj, HWindow hwin)
        {
            if (hwin == null || !IsValidObject(obj))
                return;
            try
            {
                HOperatorSet.DispObj(obj, hwin);
            }
            catch
            {
                // 安全捕获，防止 HALCON 渲染异常打断 UI 流程
            }
        }

        /// <summary>
        /// 检查 HObject 是否为图像对象（而非区域 Region 或轮廓 XLD）。
        /// </summary>
        public static bool IsImageObject(HObject obj)
        {
            if (!IsValidObject(obj)) return false;
            try
            {
                HOperatorSet.GetObjClass(obj, out var objClass);
                return objClass.S == "image";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 为图元对象设置窗口绘制属性：
        /// 区域（Region）使用 margin（空心边缘）绿色绘制，线宽 2；
        /// XLD 轮廓使用红色绘制，线宽 2；
        /// 避免区域以默认白色填充（fill）遮挡背景图像。
        /// </summary>
        public static void SetDefaultDrawModeForObject(HObject obj, HWindow hwin)
        {
            if (hwin == null || !IsValidObject(obj)) return;
            try
            {
                HOperatorSet.GetObjClass(obj, out var objClass);
                var cls = objClass.S;
                if (cls == "region")
                {
                    HOperatorSet.SetDraw(hwin, "margin");
                    HOperatorSet.SetColor(hwin, "green");
                    HOperatorSet.SetLineWidth(hwin, 2);
                }
                else if (cls != null && cls.StartsWith("xld"))
                {
                    HOperatorSet.SetColor(hwin, "red");
                    HOperatorSet.SetLineWidth(hwin, 2);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 沿节点的输入绑定链向上追溯查找真实的图像对象（Image），
        /// 跳过中间区域（Region）或 XLD 节点，以便在参数调试弹窗中提供真实背景图像。
        /// </summary>
        public static HObject FindUpstreamImage(IHandle handle)
        {
            var runModel = handle?.ContextRunModel;
            if (runModel == null) return null;

            var queue = new System.Collections.Generic.Queue<IHandleRunModel>();
            var visited = new System.Collections.Generic.HashSet<IHandleRunModel>();
            queue.Enqueue(runModel);
            visited.Add(runModel);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.InputBindings == null) continue;

                foreach (var binding in current.InputBindings)
                {
                    var srcNode = binding.SourceNode;
                    if (srcNode == null || !visited.Add(srcNode)) continue;

                    var param = srcNode.Handle?.RunParameter;
                    if (param != null)
                    {
                        if (IsImageObject(param.GrayImage)) return param.GrayImage;
                        if (IsImageObject(param.RawImage)) return param.RawImage;
                        if (IsImageObject(param.HOutput)) return param.HOutput;
                    }

                    queue.Enqueue(srcNode);
                }
            }

            return null;
        }
    }
}
