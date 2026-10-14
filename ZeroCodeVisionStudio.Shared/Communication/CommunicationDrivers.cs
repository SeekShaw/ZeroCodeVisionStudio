using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroCodeVisionStudio.Shared.Communication
{
    /// <summary>
    /// 通讯驱动接口：统一 TCP客户端/TCP服务端/UDP/串口 的连接、收发与缓冲语义。
    /// 驱动实例由 <see cref="CommunicationCenter"/> 按配置缓存复用（长连接），
    /// 通讯算子只做 Send / WaitForData，不再各自开连接。
    /// </summary>
    public interface ICommunicationDriver : IDisposable
    {
        /// <summary>驱动配置。</summary>
        CommDriverConfig Config { get; }

        /// <summary>是否已连接（串口=已打开）。</summary>
        bool IsConnected { get; }

        /// <summary>状态描述（列表/诊断用）。</summary>
        string StatusText { get; }

        /// <summary>最近一次错误信息。</summary>
        string LastError { get; }

        /// <summary>收发统计摘要（收/发字节、错误次数）。</summary>
        string StatsText { get; }

        /// <summary>收到新数据事件（后台接收线程触发，UI 使用需自行调度）。</summary>
        event Action<byte[]> DataReceived;

        /// <summary>建立连接/打开端口。已连接时直接返回 true。</summary>
        bool Connect();

        /// <summary>断开连接。</summary>
        void Disconnect();

        /// <summary>发送数据（TcpClient 未连接且允许自动重连时先重连）。</summary>
        bool Send(byte[] data);

        /// <summary>接收缓冲区中可读字节数。</summary>
        int AvailableBytes { get; }

        /// <summary>非阻塞读走全部缓冲数据（无数据返回空数组）。</summary>
        byte[] ReadAvailable();

        /// <summary>等待接收数据：阻塞至有数据或超时，返回全部缓冲（超时返回空数组）。</summary>
        byte[] WaitForData(int timeoutMs);

        /// <summary>把未消费的字节写回接收缓冲头部（帧提取残帧留存）。</summary>
        void PushBack(byte[] data);
    }

    /// <summary>
    /// 驱动基类：统一的接收缓冲（字节流累积）、收发锁与状态维护。
    /// </summary>
    public abstract class CommunicationDriverBase : ICommunicationDriver
    {
        public CommDriverConfig Config { get; }

        public abstract bool IsConnected { get; }

        public string StatusText => IsConnected
            ? $"已连接 · 待读 {AvailableBytes} 字节"
            : string.IsNullOrEmpty(LastError) ? "未连接" : $"未连接 · {LastError}";

        public string LastError { get; protected set; } = "";

        public event Action<byte[]> DataReceived;

        private readonly object bufferLock = new();
        private readonly List<byte> receiveBuffer = new();
        protected readonly object ioLock = new();

        // ==================== 收发统计 ====================
        /// <summary>累计接收字节数。</summary>
        public long RxBytes { get; private set; }

        /// <summary>累计发送字节数。</summary>
        public long TxBytes { get; private set; }

        /// <summary>累计收发错误次数。</summary>
        public int ErrorCount { get; private set; }

        /// <summary>最近一次通讯时间（未通讯为 null）。</summary>
        public DateTime? LastActivity { get; private set; }

        /// <summary>统计摘要（管理页显示）。</summary>
        public string StatsText => $"收 {RxBytes} B · 发 {TxBytes} B · 错 {ErrorCount} 次";

        protected void ReportTx(int bytes)
        {
            TxBytes += bytes;
            LastActivity = DateTime.Now;
            CommunicationCenter.ReportTraffic(Config?.Name, bytes, isRx: false);
        }

        /// <summary>发送成功后调用：统计 + 报文上报（监视器显示实际载荷）。</summary>
        protected void ReportSent(byte[] data)
        {
            if (data == null) return;
            ReportTx(data.Length);
            CommunicationCenter.ReportMessage(Config?.Name, false, data);
        }

        protected void ReportError()
        {
            ErrorCount++;
            CommunicationCenter.ReportTraffic(Config?.Name, 0, isRx: false, isError: true);
        }

        protected CommunicationDriverBase(CommDriverConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract bool Connect();
        public abstract void Disconnect();
        public abstract bool Send(byte[] data);

        public int AvailableBytes
        {
            get
            {
                lock (bufferLock) return receiveBuffer.Count;
            }
        }

        public byte[] ReadAvailable()
        {
            lock (bufferLock)
            {
                if (receiveBuffer.Count == 0) return Array.Empty<byte>();
                var data = receiveBuffer.ToArray();
                receiveBuffer.Clear();
                return data;
            }
        }

        public void PushBack(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            lock (bufferLock) receiveBuffer.InsertRange(0, data);
        }

        public byte[] WaitForData(int timeoutMs)
        {
            var deadline = Environment.TickCount64 + Math.Max(0, timeoutMs);
            while (Environment.TickCount64 < deadline)
            {
                lock (bufferLock)
                {
                    if (receiveBuffer.Count > 0)
                    {
                        var data = receiveBuffer.ToArray();
                        receiveBuffer.Clear();
                        return data;
                    }
                }
                Thread.Sleep(10);
            }
            return Array.Empty<byte>();
        }

        /// <summary>后台接收线程调用：写入缓冲并触发事件。</summary>
        protected void OnDataReceived(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            lock (bufferLock) receiveBuffer.AddRange(data);
            RxBytes += data.Length;
            LastActivity = DateTime.Now;
            CommunicationCenter.ReportTraffic(Config?.Name, data.Length, isRx: true);
            CommunicationCenter.ReportMessage(Config?.Name, true, data);
            DataReceived?.Invoke(data);
        }

        public virtual void Dispose() => Disconnect();
    }

    /// <summary>TCP 客户端驱动：长连接 + 后台接收 + 可选自动重连。</summary>
    public sealed class TcpClientDriver : CommunicationDriverBase
    {
        private TcpClient client;
        private CancellationTokenSource readCts;
        private volatile bool connected;

        private new TcpClientDriverConfig Config => (TcpClientDriverConfig)base.Config;

        public TcpClientDriver(TcpClientDriverConfig config) : base(config) { }

        public override bool IsConnected => connected && client?.Connected == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (IsConnected) return true;
                DisconnectCore();

                try
                {
                    var newClient = new TcpClient();
                    var connectTask = newClient.ConnectAsync(Config.Host, Config.Port);
                    if (!connectTask.Wait(Config.ConnectTimeoutMs) || !newClient.Connected)
                    {
                        newClient.Close();
                        LastError = $"连接超时 {Config.Host}:{Config.Port}（{Config.ConnectTimeoutMs}ms）";
                        return false;
                    }

                    client = newClient;
                    client.NoDelay = true;
                    connected = true;
                    LastError = "";

                    readCts = new CancellationTokenSource();
                    var stream = client.GetStream();
                    var buffer = new byte[8192];
                    var loop = Task.Run(async () =>
                    {
                        try
                        {
                            while (!readCts.IsCancellationRequested)
                            {
                                var read = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);
                                if (read == 0) break; // 对端关闭
                                var chunk = new byte[read];
                                Array.Copy(buffer, chunk, read);
                                OnDataReceived(chunk);
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception)
                        {
                            // 连接异常断开：标记断线，缓冲数据保留。
                        }
                        finally
                        {
                            connected = false;
                        }
                    }, readCts.Token);

                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    connected = false;
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
            connected = false;
            try { readCts?.Cancel(); } catch { }
            try { client?.Close(); } catch { }
            client = null;
            readCts = null;
        }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            if (!IsConnected)
            {
                if (!Config.AutoReconnect)
                {
                    LastError = "连接已断开（未启用自动重连）";
                    return false;
                }
                if (!Connect())
                {
                    return false;
                }
            }

            try
            {
                lock (ioLock)
                {
                    client?.GetStream().Write(data, 0, data.Length);
                }
                ReportSent(data);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"发送失败: {ex.Message}";
                connected = false;
                return false;
            }
        }
    }

    /// <summary>TCP 服务端驱动：监听端口接受多客户端；发送广播给全部客户端，接收来自任一客户端。</summary>
    public sealed class TcpServerDriver : CommunicationDriverBase
    {
        private TcpListener listener;
        private readonly List<TcpClient> clients = new();
        private CancellationTokenSource acceptCts;

        private new TcpServerDriverConfig Config => (TcpServerDriverConfig)base.Config;

        public TcpServerDriver(TcpServerDriverConfig config) : base(config) { }

        public override bool IsConnected => listener != null;

        public string ClientCountText => $"{clients.Count} 个客户端";

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (listener != null) return true;
                try
                {
                    listener = new TcpListener(IPAddress.Any, Config.ListenPort);
                    listener.Start(Config.MaxClients);
                    LastError = "";
                    acceptCts = new CancellationTokenSource();
                    var token = acceptCts.Token;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!token.IsCancellationRequested)
                            {
                                var client = await listener.AcceptTcpClientAsync(token);
                                lock (clients)
                                {
                                    if (clients.Count >= Config.MaxClients)
                                    {
                                        client.Close();
                                        continue;
                                    }
                                    clients.Add(client);
                                }
                                StartClientReceive(client, token);
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception) { }
                    }, token);

                    return true;
                }
                catch (Exception ex)
                {
                    LastError = $"监听失败: {ex.Message}";
                    listener = null;
                    return false;
                }
            }
        }

        private void StartClientReceive(TcpClient client, CancellationToken token)
        {
            var buffer = new byte[8192];
            var stream = client.GetStream();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (read == 0) break;
                        var chunk = new byte[read];
                        Array.Copy(buffer, chunk, read);
                        OnDataReceived(chunk);
                    }
                }
                catch { }
                finally
                {
                    lock (clients) clients.Remove(client);
                    try { client.Close(); } catch { }
                }
            }, token);
        }

        public override void Disconnect()
        {
            lock (ioLock)
            {
                try { acceptCts?.Cancel(); } catch { }
                try { listener?.Stop(); } catch { }
                listener = null;
                acceptCts = null;
                lock (clients)
                {
                    foreach (var c in clients) try { c.Close(); } catch { }
                    clients.Clear();
                }
            }
        }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            if (listener == null)
            {
                LastError = "服务端未启动";
                return false;
            }

            try
            {
                TcpClient[] targets;
                lock (clients) targets = clients.ToArray();
                if (targets.Length == 0)
                {
                    LastError = "无已连接客户端";
                    return false;
                }

                foreach (var target in targets)
                {
                    var stream = target.GetStream();
                    stream.Write(data, 0, data.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"广播失败: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>UDP 驱动：绑定本地端口收，向远端发。</summary>
    public sealed class UdpDriver : CommunicationDriverBase
    {
        private UdpClient udp;
        private CancellationTokenSource readCts;

        private new UdpDriverConfig Config => (UdpDriverConfig)base.Config;

        public UdpDriver(UdpDriverConfig config) : base(config) { }

        public override bool IsConnected => udp != null;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (udp != null) return true;
                try
                {
                    udp = new UdpClient(Config.LocalPort);
                    LastError = "";
                    readCts = new CancellationTokenSource();
                    var token = readCts.Token;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!token.IsCancellationRequested)
                            {
                                var result = await udp.ReceiveAsync(token);
                                OnDataReceived(result.Buffer);
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception) { }
                    }, token);
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = $"绑定端口失败: {ex.Message}";
                    udp = null;
                    return false;
                }
            }
        }

        public override void Disconnect()
        {
            lock (ioLock)
            {
                try { readCts?.Cancel(); } catch { }
                try { udp?.Close(); } catch { }
                udp = null;
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

            if (udp == null && !Connect()) return false;

            try
            {
                lock (ioLock)
                {
                    udp?.Send(data, data.Length, Config.RemoteHost, Config.RemotePort);
                }
                ReportSent(data);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"发送失败: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>串口驱动：System.IO.Ports 实现，DataReceived 事件入缓冲。</summary>
    public sealed class SerialDriver : CommunicationDriverBase
    {
        private SerialPort port;

        private new SerialDriverConfig Config => (SerialDriverConfig)base.Config;

        public SerialDriver(SerialDriverConfig config) : base(config) { }

        public override bool IsConnected => port?.IsOpen == true;

        public override bool Connect()
        {
            lock (ioLock)
            {
                if (port?.IsOpen == true) return true;
                DisconnectCore();
                try
                {
                    port = new SerialPort(
                        Config.PortName,
                        Config.BaudRate,
                        (Parity)(int)Config.Parity,
                        Config.DataBits,
                        (StopBits)(int)Config.StopBits)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 1000
                    };
                    port.DataReceived += (_, e) =>
                    {
                        if (e.EventType != SerialData.Chars || !port.IsOpen) return;
                        try
                        {
                            var available = port.BytesToRead;
                            if (available <= 0) return;
                            var buffer = new byte[available];
                            port.Read(buffer, 0, available);
                            OnDataReceived(buffer);
                        }
                        catch { }
                    };
                    port.Open();
                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = $"打开串口失败: {ex.Message}";
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
            try { if (port?.IsOpen == true) port.Close(); } catch { }
            try { port?.Dispose(); } catch { }
            port = null;
        }

        public override bool Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                LastError = "发送内容为空";
                return false;
            }

            if (port?.IsOpen != true && !Connect()) return false;

            try
            {
                lock (ioLock) port.Write(data, 0, data.Length);
                ReportSent(data);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"发送失败: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>通讯数据工具：文本/十六进制互转。</summary>
    public static class CommDataHelper
    {
        public static byte[] ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            var cleaned = hex.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "");
            if (cleaned.Length % 2 != 0) cleaned = "0" + cleaned;
            var result = new byte[cleaned.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
            }
            return result;
        }

        public static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public static string ToText(byte[] data) =>
            data == null || data.Length == 0 ? "" : Encoding.UTF8.GetString(data);
    }
}
