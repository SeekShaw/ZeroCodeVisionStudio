using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>
    /// 通讯接收算子：从通讯驱动中心配置的驱动接收数据（等待超时语义）。
    /// 输出：Data（按格式解码）、Length（字节数）、Hex（十六进制原文）。
    /// </summary>
    public class CommReceiveParameter : IHandleParameter
    {
        [DisplayName("驱动名称")]
        [DriverOptions(CommDriverType.TcpClient, CommDriverType.TcpServer, CommDriverType.Udp, CommDriverType.Serial, CommDriverType.Loopback, CommDriverType.Mqtt, CommDriverType.WebSocket)]
        [Description("通讯驱动中心配置的驱动名称（工具 → 通讯驱动管理）")]
        public string DriverName { get; set; } = "";

        [DisplayName("等待超时(ms)")]
        [Description("等待接收数据的超时时间；超时未收到数据则报错（流程轮询场景建议用分支容错）")]
        public int TimeoutMs { get; set; } = 500;

        public CommDataFormat OutputFormat { get; set; } = CommDataFormat.文本;

        /// <summary>是否超时视为成功（空数据）。轮询场景可开启，避免流程中断。</summary>
        [DisplayName("超时视为成功")]
        [Description("开启后：超时未收到数据不报错，Length 输出 0，流程继续")]
        public bool TimeoutAsSuccess { get; set; } = false;

        [DisplayName("帧提取模式")]
        [Description("流式协议粘包处理：定长帧/结束符(填 0D 0A)/帧头帧尾/长度字段；数据报协议选不提取")]
        public FrameExtractMode ExtractMode { get; set; } = FrameExtractMode.无;

        [DisplayName("定长帧长")]
        [Description("定长模式的帧字节数")]
        public int FixedLength { get; set; } = 8;

        [DisplayName("结束符")]
        [Description("结束符模式的帧尾，十六进制如 0D 0A")]
        public string Terminator { get; set; } = "";

        [DisplayName("帧头(HEX)")]
        [Description("帧头帧尾/长度字段模式的帧头，十六进制如 AA")]
        public string HeadHex { get; set; } = "";

        [DisplayName("帧尾(HEX)")]
        [Description("帧头帧尾模式的帧尾，十六进制如 55")]
        public string TailHex { get; set; } = "";

        [DisplayName("长度字段偏移")]
        [Description("长度字段模式：长度字段在帧头后第几字节")]
        public int LengthFieldOffset { get; set; } = 0;

        [DisplayName("长度字段字节")]
        [Description("长度字段模式：长度值占 1 或 2 字节")]
        public int LengthFieldSize { get; set; } = 2;

        [DisplayName("长度含帧头")]
        [Description("长度字段模式：长度值是否包含帧头与长度字段本身")]
        public bool LengthIncludesHeader { get; set; } = false;

        public IHandleParameter Clone() => (CommReceiveParameter)MemberwiseClone();

        public void Reset()
        {
            DriverName = "";
            TimeoutMs = 500;
            OutputFormat = CommDataFormat.文本;
            TimeoutAsSuccess = false;
            ExtractMode = FrameExtractMode.无;
            FixedLength = 8;
            Terminator = "";
            HeadHex = "";
            TailHex = "";
            LengthFieldOffset = 0;
            LengthFieldSize = 2;
            LengthIncludesHeader = false;
        }
    }

    [Handle("通讯接收", "CommReceive", "通过通讯驱动中心配置的驱动（TCP/UDP/串口）接收数据，等待至超时。可配合输入绑定把接收数据传给下游或驱动参数。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class CommReceiveHandle : ParameterHandleBase<CommReceiveParameter>
    {
        public CommReceiveHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.DriverName))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    "CommReceive 未配置驱动名称。请先在「工具 → 通讯驱动管理」创建驱动，并在此填写其名称。");
            }

            var driver = CommunicationCenter.GetDriver(p.DriverName);
            if (driver == null)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    $"未找到名为 [{p.DriverName}] 的通讯驱动。请在「工具 → 通讯驱动管理」中检查配置。");
            }

byte[] data;
            if (p.ExtractMode == FrameExtractMode.无)
            {
                data = driver.WaitForData(Math.Max(10, p.TimeoutMs));
            }
            else
            {
                data = ExtractFrame(driver, p);
            }

            if (data.Length == 0)
            {
                if (p.TimeoutAsSuccess)
                {
                    RunParameter.SetOutput("Data", "");
                    RunParameter.SetOutput("Length", 0);
                    RunParameter.SetOutput("Hex", "");
                    RunParameter.ResultMessage = $"[{p.DriverName}] 等待 {p.TimeoutMs}ms 未收到数据（按配置视为成功）";
                    return;
                }

                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"[{p.DriverName}] 等待 {p.TimeoutMs}ms 未收到数据（连接状态：{driver.StatusText}）");
            }

            var text = p.OutputFormat == CommDataFormat.十六进制
                ? CommDataHelper.ToHex(data)
                : CommDataHelper.ToText(data);

            RunParameter.SetOutput("Data", text);
            RunParameter.SetOutput("Length", data.Length);
            RunParameter.SetOutput("Hex", CommDataHelper.ToHex(data));
            RunParameter.ResultMessage = $"[{p.DriverName}] 收到 {data.Length} 字节：{Truncate(text, 60)}";
        }

        private static string Truncate(string text, int max) =>
            string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max) + "…";

        /// <summary>
        /// 按配置的帧模式从驱动缓冲提取一帧：
        /// 循环"取缓冲→试切帧→残帧回写"直至凑满一帧或超时；粘包剩余自动留存下次使用。
        /// </summary>
        private static byte[] ExtractFrame(ICommunicationDriver driver, CommReceiveParameter p)
        {
            var deadline = Environment.TickCount64 + Math.Max(10, p.TimeoutMs);
            while (Environment.TickCount64 < deadline)
            {
                var buffered = driver.ReadAvailable();
                if (buffered.Length == 0)
                {
                    System.Threading.Thread.Sleep(10);
                    continue;
                }

                if (FrameExtractor.TryExtract(buffered, p.ExtractMode, p.FixedLength, p.Terminator,
                        p.HeadHex, p.TailHex, p.LengthFieldOffset, p.LengthFieldSize, p.LengthIncludesHeader,
                        out var frame, out var remainder))
                {
                    if (remainder.Length > 0) driver.PushBack(remainder);
                    return frame;
                }

                driver.PushBack(remainder.Length > 0 ? remainder : buffered);
                System.Threading.Thread.Sleep(10);
            }
            return Array.Empty<byte>();
        }
    }
}
