using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ZeroCodeVisionStudio.Shared;
using ZeroCodeVisionStudio.Shared.Communication;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>
    /// 通讯发送算子：引用通讯驱动中心配置的任意驱动（TCP客户端/TCP服务端/UDP/串口）发送数据。
    /// 与驱动中心配合使用——先在「工具 → 通讯驱动管理」配置并保存驱动。
    /// </summary>
    public class CommSendParameter : IHandleParameter
    {
        [DisplayName("驱动名称")]
        [DriverOptions(CommDriverType.TcpClient, CommDriverType.TcpServer, CommDriverType.Udp, CommDriverType.Serial, CommDriverType.Loopback, CommDriverType.Mqtt, CommDriverType.WebSocket)]
        [Description("通讯驱动中心配置的驱动名称（工具 → 通讯驱动管理）")]
        public string DriverName { get; set; } = "";

        [DisplayName("发送内容")]
        [Description("文本模式直接发送；十六进制模式填十六进制串（如 01 03 00 00 00 0A）")]
        public string Content { get; set; } = "";

        public CommDataFormat Format { get; set; } = CommDataFormat.文本;

        [DisplayName("追加换行")]
        [Description("文本模式发送内容末尾追加 \\r\\n")]
        public bool AppendNewline { get; set; } = false;

        public IHandleParameter Clone() => (CommSendParameter)MemberwiseClone();

        public void Reset()
        {
            DriverName = "";
            Content = "";
            Format = CommDataFormat.文本;
            AppendNewline = false;
        }
    }

    /// <summary>数据格式：文本（UTF-8）或 十六进制字节串。</summary>
    public enum CommDataFormat
    {
        [Description("文本")] 文本 = 0,
        [Description("十六进制")] 十六进制 = 1
    }

    [Handle("通讯发送", "CommSend", "通过通讯驱动中心配置的驱动（TCP客户端/TCP服务端/UDP/串口）发送文本或十六进制数据。长连接复用，无需每次重建连接。", "设备通讯", OperatorBackend = OperatorBackend.Hybrid)]
    public class CommSendHandle : ParameterHandleBase<CommSendParameter>
    {
        public CommSendHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.DriverName))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    "CommSend 未配置驱动名称。请先在「工具 → 通讯驱动管理」创建驱动，并在此填写其名称。");
            }

            var driver = CommunicationCenter.GetDriver(p.DriverName);
            if (driver == null)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID,
                    $"未找到名为 [{p.DriverName}] 的通讯驱动。请在「工具 → 通讯驱动管理」中检查配置。");
            }

            // 模板插值：$Key$ 用上游节点输出值替换（如 $Distance$、$DefectCount$）。
            var content = Interpolate(p.Content);
            byte[] payload = p.Format == CommDataFormat.十六进制
                ? CommDataHelper.ParseHex(content)
                : System.Text.Encoding.UTF8.GetBytes(p.AppendNewline ? p.Content + "\r\n" : p.Content);

            if (payload.Length == 0)
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "CommSend 发送内容为空。");
            }

            if (!driver.Send(payload))
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"通讯发送失败（驱动 [{p.DriverName}]）：{driver.LastError}");
            }

            var preview = p.Format == CommDataFormat.十六进制
                ? CommDataHelper.ToHex(payload)
                : p.Content;

            RunParameter.SetOutput("SentBytes", payload.Length);
            RunParameter.ResultMessage = $"已通过 [{p.DriverName}] 发送 {payload.Length} 字节：{Truncate(preview, 60)}";
        }

        private static string Truncate(string text, int max) =>
            string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max) + "…";

        /// <summary>
        /// $Key$ 模板插值：从上游 RunParameter.Outputs/Inputs 查值替换；未找到的占位符原样保留。
        /// </summary>
        private string Interpolate(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains('$') || RunParameter == null)
                return template;

            foreach (var kv in RunParameter.Outputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            foreach (var kv in RunParameter.Inputs)
                template = template.Replace("$" + kv.Key + "$", kv.Value?.ToString() ?? "");
            return template;
        }
    }
}
