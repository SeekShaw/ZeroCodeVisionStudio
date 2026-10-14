using System;
using System.Text.Json.Serialization;

namespace ZeroCodeVisionStudio.Shared.Communication
{
    /// <summary>
    /// 西门子 S7 驱动配置（基于 S7netplus，ISO-on-TCP，默认端口 102）。
    /// 地址格式与 TIA/STEP7 一致：DB10.DBW0（DB 字）、DB10.DBX0.1（DB 位）、MW100 / MD100 / M100.3 / I50 / Q80。
    /// </summary>
    public class S7DriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "192.168.0.1";
        public int Port { get; set; } = 102;

        /// <summary>CPU 类型索引（下拉顺序见 <see cref="CpuNames"/>，默认 4 = S7-1200）。</summary>
        public int CpuIndex { get; set; } = 4;

        /// <summary>机架号（Rack），S7-1200/1500 为 0。</summary>
        public int Rack { get; set; } = 0;

        /// <summary>槽号（Slot）：S7-1200/1500 通常 0 或 1；S7-300 视 CPU 所在槽（常为 2）。</summary>
        public int Slot { get; set; } = 1;

        /// <summary>连接超时（毫秒）。</summary>
        public int ConnectTimeoutMs { get; set; } = 5000;

        /// <summary>CPU 类型显示名（与 <see cref="CpuIndex"/> 对应）。</summary>
        [JsonIgnore]
        public static readonly string[] CpuNames = { "S7-200", "S7-200 Smart", "S7-300", "S7-400", "S7-1200", "S7-1500" };

        /// <summary>钳制后的 CPU 索引（容错）。</summary>
        [JsonIgnore]
        public int CpuIndexClamped => Math.Clamp(CpuIndex, 0, CpuNames.Length - 1);

        public override CommDriverType Type => CommDriverType.S7;
        public override string Summary => $"{Host}:{Port} {CpuNames[CpuIndexClamped]} 机架{Rack} 槽{Slot}";
    }

    /// <summary>
    /// 三菱 MELSEC MC 协议驱动配置（4E 二进制帧，以太网）。
    /// 地址格式：D100 / W100 / R100（字器件，十进制）、M100（位，十进制）、X1A / Y10 / B0（位，十六进制编号）。
    /// <para>注：自实现协议帧，无第三方库依赖；建议首用前现场联机验证。</para>
    /// </summary>
    public class McDriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "192.168.0.10";

        /// <summary>MC 协议 TCP 端口：Q/L 系列以太网模块与 FX5U 默认 5007（模块可改为 5000~5009 等）。</summary>
        public int Port { get; set; } = 5007;

        /// <summary>网络号（0 = 本站网络）。</summary>
        public int NetworkNo { get; set; } = 0;

        /// <summary>PLC 号（0xFF = 站内 PLC，默认）。</summary>
        public int PlcNo { get; set; } = 0xFF;

        /// <summary>请求目标模块 IO 号（0x03FF = 本站 CPU 模块，默认）。</summary>
        public int ModuleIo { get; set; } = 0x03FF;

        /// <summary>请求目标站号（0 = 本站）。</summary>
        public int StationNo { get; set; } = 0;

        /// <summary>应答监视定时器与 IO 超时（毫秒）。</summary>
        public int TimeoutMs { get; set; } = 3000;

        public override CommDriverType Type => CommDriverType.MelsecMc;
        public override string Summary => $"{Host}:{Port} (MC 4E)";
    }

    /// <summary>
    /// 欧姆龙 FINS/TCP 驱动配置（PLC 默认 TCP 端口 9600，连接时自动节点握手）。
    /// 地址格式：D100（DM）、W100（WR）、H100（HR）、CIO100 / CIO100.05（位用 .位号）。
    /// <para>注：自实现协议帧，无第三方库依赖；建议首用前现场联机验证。</para>
    /// </summary>
    public class FinsDriverConfig : CommDriverConfig
    {
        public string Host { get; set; } = "192.168.0.10";
        public int Port { get; set; } = 9600;

        /// <summary>本站（上位机）FINS 节点号：-1 = 连接时由 PLC 自动分配（推荐，默认）。</summary>
        public int LocalNode { get; set; } = -1;

        /// <summary>目标 PLC FINS 节点号：-1 = 使用节点握手返回的 PLC 节点（推荐，默认）；0~255 手动指定。</summary>
        public int DestNode { get; set; } = -1;

        /// <summary>目标网络号（0 = 本地网络，默认）。</summary>
        public int DestNetwork { get; set; } = 0;

        /// <summary>IO 超时（毫秒）。</summary>
        public int TimeoutMs { get; set; } = 3000;

        public override CommDriverType Type => CommDriverType.Fins;
        public override string Summary => $"{Host}:{Port} (FINS/TCP)";
    }
}
