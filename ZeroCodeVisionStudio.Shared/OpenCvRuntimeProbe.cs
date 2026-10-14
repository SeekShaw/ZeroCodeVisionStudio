using System;
using System.Threading;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// OpenCV 运行时可用性探针 —— 和 <see cref="HalconRuntimeProbe"/> 对称。
    /// 程序启动时主动探测一次 OpenCvSharp 能否正常初始化（底层需要 OpenCvSharpExtern native DLL），
    /// 缓存结果供后续算子执行前检查。用户机器上 OpenCV native DLL 不可用时，
    /// OpenCV 算子会被优雅跳过，而非每个节点都抛 TypeInitializationException。
    /// </summary>
    public static class OpenCvRuntimeProbe
    {
        private static int _probed;
        private static bool _isAvailable;
        private static string _probeError;

        /// <summary>OpenCV 运行时是否可用。未探测时会先执行一次探测。</summary>
        public static bool IsAvailable
        {
            get { EnsureProbed(); return _isAvailable; }
        }

        /// <summary>探测失败时的简要错误信息；成功时为 null。</summary>
        public static string ProbeError
        {
            get { EnsureProbed(); return _probeError; }
        }

        /// <summary>
        /// 重置探测状态（允许下次 EnsureProbed 重新探测）。
        /// 用于应用启动早期 probe 可能因 DLL 尚未就绪而失败，
        /// 在应用完全初始化后再重新 probe 一次。
        /// </summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _probed, 0);
            _isAvailable = false;
            _probeError = null;
        }

        /// <summary>
        /// 执行一次探测。成功后幂等（缓存 true 不再重试）；
        /// 失败时不锁死，允许后续再次调用重试。
        /// </summary>
        public static bool EnsureProbed()
        {
            // 已成功探测过 → 直接返回（成功结果幂等）
            if (_probed == 1 && _isAvailable)
                return true;

            try
            {
                // 最小化探测 —— 调一个无副作用的基础方法（GetTickCount），
                // 触发 OpenCvSharpExtern.dll 加载和原生绑定初始化。
                // 如果 native DLL 缺失 / 架构不匹配 / 版本冲突，
                // 这里会抛出 TypeInitializationException / DllNotFoundException / EntryPointNotFoundException。
                var tick = OpenCvSharp.Cv2.GetTickCount();
                _isAvailable = true;
                _probeError = null;
                Interlocked.Exchange(ref _probed, 1); // 成功后锁死
            }
            catch (TypeInitializationException te)
            {
                _isAvailable = false;
                _probeError = $"OpenCV native 库初始化失败（{te.InnerException?.Message ?? te.Message}）。可能缺少 OpenCvSharpExtern.dll 或 VC++ 运行时。";
            }
            catch (DllNotFoundException dn)
            {
                _isAvailable = false;
                _probeError = $"未找到 OpenCV native 库（{dn.Message}）。请确保 OpenCvSharpExtern.dll 在输出目录中。";
            }
            catch (EntryPointNotFoundException ep)
            {
                _isAvailable = false;
                _probeError = $"OpenCV native API 入口缺失（{ep.Message}）。OpenCvSharp 版本与 native DLL 不匹配。";
            }
            catch (BadImageFormatException bi)
            {
                _isAvailable = false;
                _probeError = $"OpenCV native DLL 架构不匹配（{bi.Message}）。可能是 x64/x86 混淆。";
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _probeError = $"OpenCV 运行时不可用（{ex.GetType().Name}：{ex.Message}）。";
            }

            return _isAvailable;
        }

        /// <summary>
        /// OpenCV 算子是否应该被跳过（运行时不可用时跳过）。
        /// </summary>
        public static bool ShouldSkipOperator() => !IsAvailable;

        /// <summary>生成用户友好的跳过原因消息。</summary>
        public static string GetSkipReason(string operatorName)
        {
            EnsureProbed();
            return $"跳过 OpenCV 算子 [{operatorName}]：{(_probeError ?? "OpenCV 运行时不可用")}";
        }
    }
}
