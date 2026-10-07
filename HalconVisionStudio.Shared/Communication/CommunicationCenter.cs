using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HalconVisionStudio.Shared.Communication
{
    /// <summary>
    /// 通讯驱动中心：集中管理全部通讯驱动配置与运行实例。
    /// <para>
    /// - 配置持久化到 %APPDATA%\HalconVisionStudio\communication_drivers.json，改动即保存；
    /// - 驱动实例按配置名缓存（长连接复用），配置修订（Revision 变化）时自动重建；
    /// - 通讯算子（CommSend/CommReceive 等）通过 <see cref="GetDriver"/> 按名引用，
    ///   与具体协议解耦——同一流程换 TCP 为串口只改驱动配置，不改流程。
    /// </para>
    /// </summary>
    public static class CommunicationCenter
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HalconVisionStudio");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "communication_drivers.json");

        private static readonly object gate = new();
        private static ObservableCollection<CommDriverConfig> configs;
        private static readonly Dictionary<string, (int Revision, ICommunicationDriver Driver)> driverCache = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>全部驱动配置（首次访问时从磁盘加载；供管理界面绑定）。</summary>
        public static ObservableCollection<CommDriverConfig> Configs
        {
            get
            {
                lock (gate)
                {
                    configs ??= Load();
                    return configs;
                }
            }
        }

        /// <summary>
        /// 流量/报文事件（驱动名、方向、字节数、可选原始数据）：
        /// 报文监视器、接收触发流程都订阅此事件。
        /// 注意：由驱动后台线程触发，UI/触发方需自行调度到目标线程。
        /// </summary>
        public static event Action<string, bool, int, byte[]> TrafficReported;

        /// <summary>驱动调用：上报收/发流量与错误（供监视器/统计/审计消费）。</summary>
        public static void ReportTraffic(string driverName, int bytes, bool isRx, bool isError = false)
        {
            try
            {
                TrafficReported?.Invoke(driverName, isRx, bytes, isError ? null : Array.Empty<byte>());
            }
            catch { }
            WriteAudit(isError
                ? $"{DateTime.Now:HH:mm:ss.fff} [错误] {driverName}"
                : null);
        }

        /// <summary>报文日志：驱动收发时由监视器拉取（避免事件载荷过大）。</summary>
        public static void ReportMessage(string driverName, bool isRx, byte[] data)
        {
            try
            {
                TrafficReported?.Invoke(driverName, isRx, data?.Length ?? 0, data);
            }
            catch { }
        }

        /// <summary>启动时自动连接全部标记 AutoConnect 的驱动。</summary>
        public static void AutoConnectAll()
        {
            foreach (var config in Configs.Where(c => c.AutoConnect))
            {
                try
                {
                    GetDriver(config.Name)?.Connect();
                }
                catch (Exception ex)
                {
                    WriteAudit($"{DateTime.Now:HH:mm:ss.fff} [自动连接失败] {config.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>导出全部驱动配置到指定 JSON 文件（产线工位间迁移）。</summary>
        public static void ExportConfigs(string filePath)
        {
            lock (gate)
            {
                File.WriteAllText(filePath, JsonSerializer.Serialize(Configs.ToList(), JsonOptions));
            }
        }

        /// <summary>从 JSON 文件导入驱动配置（同名跳过，返回导入数量）。</summary>
        public static int ImportConfigs(string filePath)
        {
            var list = JsonSerializer.Deserialize<List<CommDriverConfig>>(File.ReadAllText(filePath), JsonOptions) ?? new List<CommDriverConfig>();
            var imported = 0;
            lock (gate)
            {
                foreach (var config in list)
                {
                    if (FindConfig(config.Name) != null) continue;
                    config.Revision++;
                    Configs.Add(config);
                    imported++;
                }
                if (imported > 0) Save();
            }
            return imported;
        }

        private static readonly object auditLock = new();

        /// <summary>轻量审计日志：%APPDATA% 下 communication_audit.log（滚动截断到 512KB）。</summary>
        public static void WriteAudit(string line)
        {
            if (line == null) return;
            try
            {
                lock (auditLock)
                {
                    var path = Path.Combine(Diagnostics.ExecutionLogService.LogDirectory, "communication_audit.log");
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > 512 * 1024)
                    {
                        // 简单滚动：超限重命名 .old 覆盖
                        File.Delete(path + ".old");
                        File.Move(path, path + ".old");
                    }
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch { }
        }

        /// <summary>按名称查找驱动配置（忽略大小写）。</summary>
        public static CommDriverConfig FindConfig(string name) =>
            Configs.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>通讯算子入口：按配置名获取（或重建）驱动实例；配置不存在返回 null。</summary>
        public static ICommunicationDriver GetDriver(string name)
        {
            lock (gate)
            {
                var config = FindConfig(name);
                if (config == null) return null;

                if (driverCache.TryGetValue(config.Id, out var cached) &&
                    cached.Revision == config.Revision)
                {
                    return cached.Driver;
                }

                // 配置修订过：释放旧实例，按新配置重建。
                cached.Driver?.Dispose();
                var driver = CreateDriver(config);
                driverCache[config.Id] = (config.Revision, driver);
                return driver;
            }
        }

        /// <summary>获取已存在的驱动实例（状态轮询用；不创建新连接）。</summary>
        public static ICommunicationDriver GetExistingDriver(string name)
        {
            lock (gate)
            {
                var config = FindConfig(name);
                if (config == null) return null;
                return driverCache.TryGetValue(config.Id, out var cached) && cached.Revision == config.Revision
                    ? cached.Driver
                    : null;
            }
        }

        /// <summary>创建指定配置类型的驱动实例。</summary>
        public static ICommunicationDriver CreateDriver(CommDriverConfig config) => config switch
        {
            TcpClientDriverConfig tcp => new TcpClientDriver(tcp),
            TcpServerDriverConfig server => new TcpServerDriver(server),
            UdpDriverConfig udp => new UdpDriver(udp),
            SerialDriverConfig serial => new SerialDriver(serial),
            LoopbackDriverConfig loop => new LoopbackDriver(loop),
            ModbusTcpDriverConfig mtcp => new ModbusTcpDriver(mtcp),
            ModbusRtuDriverConfig mrtu => new ModbusRtuDriver(mrtu),
            MqttDriverConfig mqtt => new MqttDriver(mqtt),
            WebSocketDriverConfig ws => new WebSocketDriver(ws),
            S7DriverConfig s7 => new S7Driver(s7),
            McDriverConfig mc => new McDriver(mc),
            FinsDriverConfig fins => new FinsDriver(fins),
            _ => throw new NotSupportedException($"未知驱动类型: {config?.GetType().Name}")
        };

        /// <summary>配置改动后保存（管理界面在增删改后调用；Revision 由调用方递增）。</summary>
        public static void Save()
        {
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(SettingsDir);
                    File.WriteAllText(SettingsFile, JsonSerializer.Serialize(Configs.ToList(), JsonOptions));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommCenter] 保存驱动配置失败: {ex.Message}");
                }
            }
        }

        /// <summary>断开并释放全部驱动实例（应用退出时调用）。</summary>
        public static void ShutdownAll()
        {
            lock (gate)
            {
                foreach (var entry in driverCache.Values)
                {
                    try { entry.Driver.Dispose(); } catch { }
                }
                driverCache.Clear();
            }
        }

        private static ObservableCollection<CommDriverConfig> Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var list = JsonSerializer.Deserialize<List<CommDriverConfig>>(File.ReadAllText(SettingsFile), JsonOptions);
                    return new ObservableCollection<CommDriverConfig>(list ?? new List<CommDriverConfig>());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommCenter] 加载驱动配置失败: {ex.Message}");
            }
            return new ObservableCollection<CommDriverConfig>();
        }
    }
}
