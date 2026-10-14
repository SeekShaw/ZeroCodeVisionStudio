using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ZeroCodeVisionStudio.AI.Copilot
{
    public enum MessageRole
    {
        System,
        User,
        Assistant,
        Tool
    }

    public enum ToolExecutionStatus
    {
        Running,
        Success,
        Failed
    }

    public class CopilotToolCall : INotifyPropertyChanged
    {
        private ToolExecutionStatus _status = ToolExecutionStatus.Running;
        private string _result;

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; }

        public ToolExecutionStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string StatusText => Status switch
        {
            ToolExecutionStatus.Running => "执行中...",
            ToolExecutionStatus.Success => "执行成功",
            ToolExecutionStatus.Failed => "执行失败",
            _ => "就绪"
        };

        public string Result
        {
            get => _result;
            set
            {
                if (_result != value)
                {
                    _result = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CopilotMessage : INotifyPropertyChanged
    {
        private string _content;
        private string _reasoningContent;
        private bool _isStreaming;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public MessageRole Role { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 附加的本地图片文件路径列表（多模态输入）。
        /// 发送请求时会转成 data:image/png;base64 格式随消息提交，供视觉大模型直接查看图像。
        /// </summary>
        public List<string> Images { get; set; } = new List<string>();

        /// <summary>是否有附加图片。</summary>
        public bool HasImages => Images != null && Images.Count > 0;

        /// <summary>
        /// 推理模型的思考过程（GLM reasoning_content / DeepSeek reasoning_content）。
        /// 仅用于界面展示，不回传 API（服务商要求不能把思考内容放回 messages）。
        /// </summary>
        public string ReasoningContent
        {
            get => _reasoningContent;
            set
            {
                if (_reasoningContent != value)
                {
                    _reasoningContent = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set
            {
                if (_isStreaming != value)
                {
                    _isStreaming = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ToolCallId { get; set; }
        public string ToolName { get; set; }

        public ObservableCollection<CopilotToolCall> ToolCalls { get; set; } = new ObservableCollection<CopilotToolCall>();

        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
