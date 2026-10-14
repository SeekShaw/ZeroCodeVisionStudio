using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace ZeroCodeVisionStudio.Shared.Communication
{
    /// <summary>
    /// MQTT 客户端驱动（MQTTnet v4）：
    /// Send = 发布到 PublishTopic；订阅 Topic 的消息负载进入接收缓冲（可被 CommReceive 消费或触发流程）。
    /// </summary>
    public sealed class MqttDriver : CommunicationDriverBase
    {
        private IMqttClient client;
        private CancellationTokenSource readCts;

        private new MqttDriverConfig Config => (MqttDriverConfig)base.Config;

        public MqttDriver(MqttDriverConfig config) : base(config) { }

        public override bool IsConnected => client?.IsConnected == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (IsConnected) return true;
                try
                {
                    var factory = new MqttFactory();
                    client = factory.CreateMqttClient();

                    var builder = new MqttClientOptionsBuilder()
                        .WithTcpServer(Config.Host, Config.Port)
                        .WithClientId(string.IsNullOrWhiteSpace(Config.ClientId)
                            ? $"hvs-{Guid.NewGuid():N}".Substring(0, 20)
                            : Config.ClientId);
                    if (!string.IsNullOrWhiteSpace(Config.Username))
                    {
                        builder = builder.WithCredentials(Config.Username, Config.Password);
                    }

                    client.ApplicationMessageReceivedAsync += e =>
                    {
                        var payload = e.ApplicationMessage.PayloadSegment.ToArray();
                        OnDataReceived(payload);
                        return Task.CompletedTask;
                    };

                    if (!client.ConnectAsync(builder.Build()).Wait(8000))
                    {
                        LastError = "连接 MQTT Broker 超时";
                        ReportError();
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(Config.SubscribeTopic))
                    {
                        client.SubscribeAsync(new MqttTopicFilterBuilder()
                            .WithTopic(Config.SubscribeTopic)
                            .Build()).Wait(3000);
                    }

                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    client = null;
                    return false;
                }
            }
        }

        public override void Disconnect()
        {
            lock (ioLock)
            {
                try { readCts?.Cancel(); } catch { }
                try { client?.DisconnectAsync().Wait(2000); } catch { }
                try { client?.Dispose(); } catch { }
                client = null;
                readCts = null;
            }
        }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            if (!IsConnected && !Connect()) return false;

            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(Config.PublishTopic)
                    .WithPayload(data)
                    .Build();
                client.PublishAsync(message).Wait(3000);
                ReportSent(data);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"MQTT 发布失败: {ex.Message}";
                ReportError();
                return false;
            }
        }
    }

    /// <summary>WebSocket 客户端驱动：收发二进制/文本帧。</summary>
    public sealed class WebSocketDriver : CommunicationDriverBase
    {
        private ClientWebSocket socket;
        private CancellationTokenSource receiveCts;

        private new WebSocketDriverConfig Config => (WebSocketDriverConfig)base.Config;

        public WebSocketDriver(WebSocketDriverConfig config) : base(config) { }

        public override bool IsConnected => socket?.State == WebSocketState.Open;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (IsConnected) return true;
                DisconnectCore();
                try
                {
                    socket = new ClientWebSocket();
                    var cts = new CancellationTokenSource(8000);
                    socket.ConnectAsync(new Uri(Config.Url), cts.Token).Wait();

                    receiveCts = new CancellationTokenSource();
                    var token = receiveCts.Token;
                    _ = Task.Run(async () =>
                    {
                        var buffer = new byte[8192];
                        try
                        {
                            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                            {
                                using var ms = new System.IO.MemoryStream();
                                WebSocketReceiveResult result;
                                do
                                {
                                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                                    if (result.MessageType == WebSocketMessageType.Close)
                                    {
                                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", token);
                                        return;
                                    }
                                    ms.Write(buffer, 0, result.Count);
                                } while (!result.EndOfMessage);

                                OnDataReceived(ms.ToArray());
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception) { }
                    }, token);

                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    ReportError();
                    DisconnectCore();
                    return false;
                }
            }
        }

        public override void Disconnect()
        {
            lock (ioLock) DisconnectCore();
        }

        private void DisconnectCore()
        {
            try { receiveCts?.Cancel(); } catch { }
            try { socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "client close", CancellationToken.None).Wait(1500); } catch { }
            try { socket?.Dispose(); } catch { }
            socket = null;
            receiveCts = null;
        }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            if (!IsConnected && !Connect()) return false;

            try
            {
                lock (ioLock)
                {
                    socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None).Wait(3000);
                }
                ReportSent(data);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"发送失败: {ex.Message}";
                ReportError();
                return false;
            }
        }
    }
}
