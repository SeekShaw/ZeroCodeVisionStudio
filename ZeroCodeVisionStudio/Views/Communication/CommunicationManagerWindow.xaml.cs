using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ZeroCodeVisionStudio.Shared.Communication;
using Microsoft.Win32;

namespace ZeroCodeVisionStudio.Views.Communication
{
    /// <summary>
    /// 通讯驱动管理窗口：驱动配置 CRUD、连接控制、实时状态/统计、报文监视器、导入导出。
    /// </summary>
    public partial class CommunicationManagerWindow
    {
        /// <summary>驱动列表项：配置 + 实时连接状态与统计（定时器刷新）。</summary>
        public sealed class DriverItem : INotifyPropertyChanged
        {
            public CommDriverConfig Config { get; }

            public DriverItem(CommDriverConfig config) => Config = config;

            public string Name => Config.Name;
            public string Summary => Config.Summary;
            public string TypeIcon => Config.Type switch
            {
                CommDriverType.TcpClient => "TCP",
                CommDriverType.TcpServer => "SRV",
                CommDriverType.Udp => "UDP",
                CommDriverType.Serial => "COM",
                CommDriverType.Loopback => "LOOP",
                CommDriverType.ModbusTcp => "MB-TCP",
                CommDriverType.ModbusRtu => "MB-RTU",
                CommDriverType.Mqtt => "MQTT",
                CommDriverType.WebSocket => "WS",
                CommDriverType.S7 => "S7",
                CommDriverType.MelsecMc => "MC",
                CommDriverType.Fins => "FINS",
                _ => "?"
            };

            private bool isConnected;
            public bool IsConnected
            {
                get => isConnected;
                set { if (isConnected != value) { isConnected = value; OnPropertyChanged(); } }
            }

            private string statusText = "未连接";
            public string StatusText
            {
                get => statusText;
                set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
            }

            private string statsText = "";
            public string StatsText
            {
                get => statsText;
                set { if (statsText != value) { statsText = value; OnPropertyChanged(); } }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly ObservableCollection<DriverItem> items = new();
        private readonly DispatcherTimer statusTimer;
        private readonly ObservableCollection<string> monitorLog = new();
        private DriverItem selected;
        private bool monitorPaused;

        public CommunicationManagerWindow()
        {
            InitializeComponent();

            DriverList.ItemsSource = items;
            MonitorList.ItemsSource = monitorLog;
            ReloadList();

            // 订阅全局流量事件（驱动后台线程触发 → 调度到 UI）。
            CommunicationCenter.TrafficReported += OnTrafficReported;

            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            statusTimer.Tick += (_, __) => RefreshStatus();
            statusTimer.Start();

            Closed += (_, __) =>
            {
                statusTimer.Stop();
                CommunicationCenter.TrafficReported -= OnTrafficReported;
            };
        }

        private void OnTrafficReported(string driverName, bool isRx, int bytes, byte[] data)
        {
            if (monitorPaused || data == null || data.Length == 0) return;
            if (selected != null && !string.Equals(driverName, selected.Name, StringComparison.OrdinalIgnoreCase)) return;

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var text = HexViewCheck.IsChecked == true
                        ? CommDataHelper.ToHex(data)
                        : CommDataHelper.ToText(data);

                    monitorLog.Add($"{DateTime.Now:HH:mm:ss.fff} {(isRx ? "收←" : "发→")} [{bytes}B] {text}");
                    while (monitorLog.Count > 500) monitorLog.RemoveAt(0);
                    if (MonitorList.Items.Count > 0) MonitorList.ScrollIntoView(MonitorList.Items[^1]);
                }
                catch { }
            });
        }

        private void ReloadList()
        {
            var selectedName = selected?.Name;
            items.Clear();
            foreach (var config in CommunicationCenter.Configs)
            {
                items.Add(new DriverItem(config));
            }

            DriverList.SelectedItem = string.IsNullOrEmpty(selectedName)
                ? items.FirstOrDefault()
                : items.FirstOrDefault(i => string.Equals(i.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                  ?? items.FirstOrDefault();
        }

        private void OnDriverSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selected = DriverList.SelectedItem as DriverItem;
            ConfigEditor.Content = selected?.Config;
            DriverNameBox.Text = selected?.Config.Name ?? "";
            DriverDescBox.Text = selected?.Config.Description ?? "";
            AutoConnectCheck.IsChecked = selected?.Config.AutoConnect == true;
            TriggerCheck.IsChecked = selected?.Config.TriggerEnabled == true;
            TriggerWorkflowBox.Text = selected?.Config.TriggerWorkflowName ?? "";
            MonitorDriverName.Text = selected != null ? $"— {selected.Name}" : "（选择左侧驱动后显示）";
            monitorLog.Clear();
            RefreshStatus();
        }

        private void UpdateConnectButton()
        {
            if (selected == null)
            {
                ToggleConnectButton.Content = "连接";
                ToggleConnectButton.IsEnabled = false;
                return;
            }

            ToggleConnectButton.IsEnabled = true;
            ToggleConnectButton.Content = selected.IsConnected ? "断开" : "连接";
        }

        private void RefreshStatus()
        {
            foreach (var item in items)
            {
                var driver = CommunicationCenter.GetExistingDriver(item.Config.Name);
                item.IsConnected = driver?.IsConnected == true;
                item.StatusText = driver?.StatusText ?? "未连接";
                item.StatsText = driver?.StatsText ?? "";
            }
            UpdateConnectButton();
            UpdateConnectStatus();
        }

        private static readonly System.Windows.Media.Brush ConnectedBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));

        /// <summary>编辑器工具行的实时连接状态（圆点 + 文本）。</summary>
        private void UpdateConnectStatus()
        {
            if (selected == null)
            {
                ConnectStatus.Text = "";
                ConnDot.Fill = System.Windows.Media.Brushes.Gray;
                return;
            }

            ConnectStatus.Text = selected.StatusText;
            ConnDot.Fill = selected.IsConnected
                ? ConnectedBrush
                : (System.Windows.Media.Brush)TryFindResource("Token.Text.Muted") ?? System.Windows.Media.Brushes.Gray;
        }

        private void OnAddDriver(object sender, RoutedEventArgs e)
        {
            var nameBase = NewDriverType.SelectedIndex switch
            {
                1 => "TCP服务", 2 => "UDP", 3 => "串口", 4 => "ModbusTCP", 5 => "ModbusRTU",
                6 => "MQTT", 7 => "WebSocket", 8 => "西门子S7", 9 => "三菱MC", 10 => "欧姆龙FINS",
                11 => "回环", _ => "TCP客户端"
            };
            var name = nameBase + (items.Count + 1);
            var index = items.Count + 1;
            while (CommunicationCenter.FindConfig(name) != null)
            {
                index++;
                name = nameBase + index;
            }

            CommDriverConfig config = NewDriverType.SelectedIndex switch
            {
                1 => new TcpServerDriverConfig { Name = name },
                2 => new UdpDriverConfig { Name = name },
                3 => new SerialDriverConfig { Name = name },
                4 => new ModbusTcpDriverConfig { Name = name },
                5 => new ModbusRtuDriverConfig { Name = name },
                6 => new MqttDriverConfig { Name = name },
                7 => new WebSocketDriverConfig { Name = name },
                8 => new S7DriverConfig { Name = name },
                9 => new McDriverConfig { Name = name },
                10 => new FinsDriverConfig { Name = name },
                11 => new LoopbackDriverConfig { Name = name },
                _ => new TcpClientDriverConfig { Name = name }
            };
            config.Description = "";

            CommunicationCenter.Configs.Add(config);
            CommunicationCenter.Save();
            ReloadList();
            StatusBar.Text = $"已新增驱动 [{name}]，在右侧完成参数配置后点击「保存配置」。";
        }

        private void OnDeleteDriver(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Tag: DriverItem item })
            {
                CommunicationCenter.GetExistingDriver(item.Config.Name)?.Disconnect();

                CommunicationCenter.Configs.Remove(item.Config);
                CommunicationCenter.Save();
                ReloadList();
                StatusBar.Text = $"已删除驱动 [{item.Name}]。";
            }
        }

        private void OnToggleConnect(object sender, RoutedEventArgs e)
        {
            if (selected == null) return;

            var driver = CommunicationCenter.GetDriver(selected.Config.Name);
            if (driver == null) return;

            if (driver.IsConnected)
            {
                driver.Disconnect();
                StatusBar.Text = $"已断开 [{selected.Name}]。";
            }
            else
            {
                var ok = driver.Connect();
                StatusBar.Text = ok
                    ? $"✓ [{selected.Name}] 连接成功。"
                    : $"✗ [{selected.Name}] 连接失败：{driver.LastError}";
            }
            RefreshStatus();
        }

        private void OnClearBuffer(object sender, RoutedEventArgs e)
        {
            if (selected == null) return;
            CommunicationCenter.GetExistingDriver(selected.Config.Name)?.ReadAvailable();
            StatusBar.Text = $"已清空 [{selected.Name}] 的接收缓冲。";
            RefreshStatus();
        }

        private void OnSaveConfig(object sender, RoutedEventArgs e)
        {
            if (selected == null) return;

            var newName = DriverNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                StatusBar.Text = "✗ 驱动名称不能为空。";
                return;
            }

            if (CommunicationCenter.Configs.Any(c =>
                    !ReferenceEquals(c, selected.Config) &&
                    string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusBar.Text = $"✗ 名称 [{newName}] 已被其它驱动占用。";
                return;
            }

            selected.Config.Name = newName;
            selected.Config.Description = DriverDescBox.Text?.Trim() ?? "";
            selected.Config.AutoConnect = AutoConnectCheck.IsChecked == true;
            selected.Config.TriggerEnabled = TriggerCheck.IsChecked == true;
            selected.Config.TriggerWorkflowName = TriggerWorkflowBox.Text?.Trim() ?? "";
            selected.Config.Revision++;

            CommunicationCenter.Save();
            CommunicationCenter.WriteAudit($"{DateTime.Now:HH:mm:ss.fff} [配置保存] {newName} (自动连接:{selected.Config.AutoConnect} 触发:{selected.Config.TriggerEnabled})");
            ReloadList();
            StatusBar.Text = $"✓ 驱动 [{newName}] 配置已保存。";
        }

        // ==================== 报文监视器 ====================

        private void OnTogglePauseMonitor(object sender, RoutedEventArgs e)
        {
            monitorPaused = !monitorPaused;
            (sender as System.Windows.Controls.Button).Content = monitorPaused ? "继续" : "暂停";
        }

        private void OnClearMonitor(object sender, RoutedEventArgs e) => monitorLog.Clear();

        // ==================== 导入/导出 ====================

        private void OnExportConfigs(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出通讯驱动配置",
                Filter = "驱动配置 (*.json)|*.json",
                FileName = $"communication_drivers_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                CommunicationCenter.ExportConfigs(dialog.FileName);
                StatusBar.Text = $"✓ 已导出 {CommunicationCenter.Configs.Count} 个驱动配置到 {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusBar.Text = $"✗ 导出失败：{ex.Message}";
            }
        }

        private void OnImportConfigs(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入通讯驱动配置",
                Filter = "驱动配置 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var imported = CommunicationCenter.ImportConfigs(dialog.FileName);
                ReloadList();
                StatusBar.Text = imported > 0
                    ? $"✓ 已导入 {imported} 个驱动配置（同名已跳过）。"
                    : "未导入任何配置（可能全部同名或文件为空）。";
            }
            catch (Exception ex)
            {
                StatusBar.Text = $"✗ 导入失败：{ex.Message}";
            }
        }
    }
}
