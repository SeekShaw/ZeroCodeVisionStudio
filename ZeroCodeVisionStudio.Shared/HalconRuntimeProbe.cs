using System;
using System.Runtime.InteropServices;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// Halcon 运行时可用性探针。
    /// 程序启动时通过 <see cref="EnsureProbed"/> 执行一次最小化的 Halcon API 调用探测，
    /// 缓存结果供后续算子执行前检查使用。避免每个算子都重复探测。
    /// </summary>
    public static class HalconRuntimeProbe
    {
        private static int probed;
        private static bool isAvailable;
        private static string probeError;

        /// <summary>Halcon 运行时是否可用。未探测时会先执行一次探测。</summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureProbed();
                return isAvailable;
            }
        }

        /// <summary>探测失败时的简要错误信息；成功时为 null。</summary>
        public static string ProbeError
        {
            get
            {
                EnsureProbed();
                return probeError;
            }
        }

        /// <summary>
        /// 执行一次探测（幂等）。返回 true 表示 Halcon 运行时可用。
        /// 在程序启动阶段主动调用，避免首次使用 Halcon 算子时才触发探测延迟。
        /// </summary>
        public static bool EnsureProbed()
        {
            if (Interlocked.Exchange(ref probed, 1) != 0)
                return isAvailable;

            try
            {
                // 使用 Halcon 12 引入的 HSystem.GetSystem 探测（兼容性好）。
                // 这是一个无副作用的只读查询，不会分配图像或区域资源。
                // 如果 halcon.dll 原生库缺失，这里会抛出 DllNotFoundException / EntryPointNotFoundException。
                var version = HalconDotNet.HSystem.GetSystem("version");
                isAvailable = !string.IsNullOrWhiteSpace(version);
                probeError = isAvailable ? null : "Halcon 运行时未返回有效版本号";
            }
            catch (DllNotFoundException dllEx)
            {
                isAvailable = false;
                probeError = $"未找到 Halcon 原生库（{dllEx.Message}）。请安装 Halcon 运行时。";
            }
            catch (EntryPointNotFoundException epEx)
            {
                isAvailable = false;
                probeError = $"Halcon API 入口缺失（{epEx.Message}）。Halcon 版本与 halcondotnet.dll 不匹配。";
            }
            catch (TypeLoadException tlEx)
            {
                isAvailable = false;
                probeError = $"Halcon 类型加载失败（{tlEx.Message}）。";
            }
            catch (BadImageFormatException bifEx)
            {
                isAvailable = false;
                probeError = $"Halcon DLL 架构不匹配（{bifEx.Message}）。可能是 x64/x86 混淆。";
            }
            catch (Exception ex)
            {
                isAvailable = false;
                probeError = $"Halcon 运行时不可用（{ex.GetType().Name}：{ex.Message}）。";
            }

            return isAvailable;
        }

        /// <summary>
        /// 根据算子后端判断当前执行是否应该被跳过。
        /// 仅 Halcon / Hybrid 后端且运行时不可用时返回 true。
        /// </summary>
        public static bool ShouldSkipOperator(OperatorBackend backend) =>
            (backend == OperatorBackend.Halcon || backend == OperatorBackend.Hybrid) && !IsAvailable;

        /// <summary>
        /// 生成用户友好的跳过原因消息。
        /// </summary>
        public static string GetSkipReason(string operatorName)
        {
            EnsureProbed();
            var reason = string.IsNullOrWhiteSpace(probeError)
                ? "当前未安装 Halcon 运行时"
                : probeError;
            return $"跳过算子 [{operatorName}]：{reason}";
        }
    }
}
