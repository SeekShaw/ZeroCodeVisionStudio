using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HalconVisionStudio.AI.Config;
using HalconVisionStudio.AI.Tools;

namespace HalconVisionStudio.AI.Mcp
{
    /// <summary>
    /// 内嵌 MCP Server（HTTP + SSE，JSON-RPC 2.0）。
    /// 安全模型：
    /// - 仅监听 127.0.0.1/localhost 回环地址；
    /// - 默认强制 Token 鉴权（Bearer / ?token= / X-MCP-Token），令牌经 DPAPI 同机保护，
    ///   防止本机其它进程或浏览器中的任意网页未经授权调用自动化工具；
    /// - 不返回 Access-Control-Allow-Origin 响应头，浏览器页面无法跨域读取响应。
    /// </summary>
    public class McpServer : IDisposable
    {
        private readonly IVisionStudioAutomationService _automationService;
        private HttpListener _listener;
        private CancellationTokenSource _cts;

        /// <summary>是否强制 Token 鉴权（来自 AiSettings.RequireMcpToken）。</summary>
        public bool RequireToken { get; set; } = true;

        /// <summary>鉴权令牌（由 McpAuthTokenStore 提供）。</summary>
        public string AuthToken { get; set; } = "";

        /// <summary>并发请求上限，超过后返回 429，提供最基本的过载保护。</summary>
        private static readonly SemaphoreSlim RequestSlots = new(8, 8);

        private sealed class SseClientSession
        {
            public string SessionId { get; set; }
            public StreamWriter Writer { get; set; }
            public HttpListenerResponse Response { get; set; }
            public SemaphoreSlim Lock { get; } = new(1, 1);

            public async Task SendEventAsync(string eventType, string data)
            {
                await Lock.WaitAsync();
                try
                {
                    if (string.IsNullOrEmpty(eventType))
                    {
                        await Writer.WriteAsync($"data: {data}\r\n\r\n");
                    }
                    else
                    {
                        await Writer.WriteAsync($"event: {eventType}\r\ndata: {data}\r\n\r\n");
                    }
                    await Writer.FlushAsync();
                    await Response.OutputStream.FlushAsync();
                }
                finally
                {
                    Lock.Release();
                }
            }

            public async Task SendPingAsync()
            {
                await Lock.WaitAsync();
                try
                {
                    await Writer.WriteAsync(": ping\r\n\r\n");
                    await Writer.FlushAsync();
                    await Response.OutputStream.FlushAsync();
                }
                finally
                {
                    Lock.Release();
                }
            }
        }

        private readonly ConcurrentDictionary<string, SseClientSession> _sseClients = new();

        public bool IsRunning => _listener != null && _listener.IsListening;
        public int Port { get; private set; } = 8765;
        public int ConnectedClientCount => _sseClients.Count;

        public event Action<bool> ServerStatusChanged;
        public event Action<string> LogMessage;

        public McpServer(IVisionStudioAutomationService automationService)
        {
            _automationService = automationService ?? throw new ArgumentNullException(nameof(automationService));
        }

        public async Task StartAsync(int port = 8765)
        {
            if (IsRunning) return;

            Port = port;
            RequireToken = AiSettingsManager.Current.RequireMcpToken;
            AuthToken = McpAuthTokenStore.LoadOrCreate();
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();

            // 监听本地回环地址
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                Log(RequireToken && !string.IsNullOrEmpty(AuthToken)
                    ? $"MCP Server 已启动，监听 http://127.0.0.1:{Port}/ （Token 鉴权已开启）"
                    : $"MCP Server 已启动，监听 http://127.0.0.1:{Port}/ （警告：Token 鉴权未生效，任何本机进程都可调用）");
                ServerStatusChanged?.Invoke(true);

                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                Log($"启动 MCP Server 异常: {ex.Message}");
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            if (_listener == null) return;

            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
            }
            finally
            {
                _listener = null;
                _sseClients.Clear();
                Log("MCP Server 已停止");
                ServerStatusChanged?.Invoke(false);
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), ct);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"接收连接异常: {ex.Message}");
                }
            }
        }

        /// <summary>校验请求令牌：支持 Authorization: Bearer、?token= 查询参数、X-MCP-Token 头。</summary>
        private bool IsAuthorized(HttpListenerRequest req)
        {
            if (!RequireToken || string.IsNullOrEmpty(AuthToken))
            {
                return true;
            }

            var auth = req.Headers["Authorization"];
            if (!string.IsNullOrEmpty(auth) &&
                auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                CryptographicEquals(auth.Substring(7).Trim(), AuthToken))
            {
                return true;
            }

            var queryToken = req.QueryString["token"];
            if (!string.IsNullOrEmpty(queryToken) && CryptographicEquals(queryToken.Trim(), AuthToken))
            {
                return true;
            }

            var headerToken = req.Headers["X-MCP-Token"];
            return !string.IsNullOrEmpty(headerToken) && CryptographicEquals(headerToken.Trim(), AuthToken);
        }

        /// <summary>常量时间字符串比较，避免时序侧信道。</summary>
        private static bool CryptographicEquals(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            // CORS 预检允许通过（不携带任何 Access-Control-Allow-* 头，浏览器无法读取跨域响应）。
            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            var path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            var accept = req.Headers["Accept"] ?? "";

            try
            {
                // 根路径 HTML 状态页不鉴权（不泄露任何方案数据）；其余全部需要令牌。
                bool isStatusPage = req.HttpMethod == "GET" && (path == "" || path == "/") && accept.Contains("text/html");

                if (!isStatusPage && !IsAuthorized(req))
                {
                    resp.StatusCode = 401;
                    resp.ContentType = "application/json; charset=utf-8";
                    var bytes = Encoding.UTF8.GetBytes(AiJson.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = (object)null,
                        error = new { code = -32001, message = "未授权：缺少或错误的 MCP Token。请在软件 AI 面板中查看令牌，并通过 Authorization: Bearer <token> 头或 ?token= 参数携带。" }
                    }));
                    resp.ContentLength64 = bytes.Length;
                    await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    resp.Close();
                    Log($"已拒绝未授权请求 {req.HttpMethod} {path} 来自 {req.RemoteEndPoint}");
                    return;
                }

                if (!await RequestSlots.WaitAsync(0))
                {
                    resp.StatusCode = 429;
                    resp.Close();
                    return;
                }

                try
                {
                    // 判断是否为 SSE 连接请求
                    bool isSseRequest = path == "/mcp" || path == "/sse" || path == "/events"
                                     || accept.Contains("text/event-stream");

                    if (req.HttpMethod == "GET")
                    {
                        if (isSseRequest || (path == "" && !accept.Contains("text/html")))
                        {
                            await HandleSseConnectAsync(context);
                        }
                        else
                        {
                            // 根路径或其它 GET 请求返回 HTML 服务状态概览
                            resp.StatusCode = 200;
                            resp.ContentType = "text/html; charset=utf-8";
                            using var writer = new StreamWriter(resp.OutputStream, Encoding.UTF8);
                            await writer.WriteAsync($@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>HALCON Vision Studio MCP Server</title></head>
<body style='font-family:Segoe UI, sans-serif; padding: 2rem; background: #1e1e2e; color: #cdd6f4;'>
  <h2>HALCON Vision Studio MCP Server</h2>
  <p><span style='color: #a6e3a1;'>● 状态: 运行中</span> (Port: {Port})</p>
  <p>SSE 端点: <code>http://127.0.0.1:{Port}/mcp</code> 或 <code>http://127.0.0.1:{Port}/sse</code></p>
  <p>鉴权: {(RequireToken ? "<span style='color:#a6e3a1;'>Token 已启用</span>" : "<span style='color:#f38ba8;'>未启用（不安全）</span>")}</p>
  <p>活跃客户端会话数: {ConnectedClientCount}</p>
</body>
</html>");
                            await writer.FlushAsync();
                            resp.Close();
                        }
                    }
                    else if (req.HttpMethod == "POST")
                    {
                        // 接收所有 MCP JSON-RPC 消息请求（/mcp, /message, /messages, /rpc 等）
                        await HandleJsonRpcPostAsync(context);
                    }
                    else
                    {
                        resp.StatusCode = 405;
                        resp.Close();
                    }
                }
                finally
                {
                    RequestSlots.Release();
                }
            }
            catch (Exception ex)
            {
                Log($"处理请求 {path} 异常: {ex.Message}");
                try
                {
                    resp.StatusCode = 500;
                    resp.Close();
                }
                catch { }
            }
        }

        private async Task HandleSseConnectAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            resp.ContentType = "text/event-stream; charset=utf-8";
            resp.Headers.Add("Cache-Control", "no-cache");
            resp.Headers.Add("Connection", "keep-alive");
            resp.KeepAlive = true;
            resp.SendChunked = true;

            var sessionId = Guid.NewGuid().ToString("N");
            var writer = new StreamWriter(resp.OutputStream, new UTF8Encoding(false)) { AutoFlush = true };
            var session = new SseClientSession
            {
                SessionId = sessionId,
                Writer = writer,
                Response = resp
            };
            _sseClients[sessionId] = session;

            Log($"MCP Client 连接 (SSE Session: {sessionId}, 来自 {req.RemoteEndPoint})");

            try
            {
                // 告知客户端用于发送消息的 POST 端点（包含 SessionId）
                var endpoint = $"/message?sessionId={sessionId}";
                await session.SendEventAsync("endpoint", endpoint);
                Log($"[SSE OUT] 已发送 endpoint 事件: {endpoint}");

                // 保活循环：每 15 秒发送一次 SSE ping
                while (_listener != null && _listener.IsListening && !_cts.IsCancellationRequested)
                {
                    await Task.Delay(15000, _cts.Token);
                    await session.SendPingAsync();
                }
            }
            catch (Exception ex)
            {
                // 客户端断开或网络异常
                Log($"MCP Client 会话 {sessionId} 结束: {ex.Message}");
            }
            finally
            {
                _sseClients.TryRemove(sessionId, out _);
                Log($"MCP Client 断开 (SSE Session: {sessionId})");
                try { resp.Close(); } catch { }
            }
        }

        private async Task HandleJsonRpcPostAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            // 提取 SessionId：支持 QueryString（sessionId / session_id）与 Header（Mcp-Session-Id / x-session-id）
            string sessionId = req.QueryString["sessionId"]
                            ?? req.QueryString["session_id"]
                            ?? req.Headers["Mcp-Session-Id"]
                            ?? req.Headers["x-session-id"];

            // 容错：如果未显式提供且恰好只有 1 个活动 SSE 会话，自动绑定
            if (string.IsNullOrEmpty(sessionId) && _sseClients.Count == 1)
            {
                using var it = _sseClients.Keys.GetEnumerator();
                if (it.MoveNext()) sessionId = it.Current;
            }

            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            string responseJson = null;
            bool isNotification = false;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var batchResults = new List<object>();
                        foreach (var item in root.EnumerateArray())
                        {
                            var res = await ProcessRpcItemAsync(item);
                            if (res != null) batchResults.Add(res);
                        }
                        if (batchResults.Count > 0)
                        {
                            responseJson = AiJson.Serialize(batchResults);
                        }
                        else
                        {
                            isNotification = true;
                        }
                    }
                    else
                    {
                        var res = await ProcessRpcItemAsync(root);
                        if (res != null)
                        {
                            responseJson = AiJson.Serialize(res);
                        }
                        else
                        {
                            isNotification = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    responseJson = AiJson.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = (object)null,
                        error = new { code = -32700, message = $"Parse error: {ex.Message}" }
                    });
                }
            }

            // 双通道消息分发：
            // 1. 若存在活跃的 SSE 会话，且有返回消息，将 JSON-RPC 响应推入 SSE 事件流
            if (!string.IsNullOrEmpty(sessionId) && _sseClients.TryGetValue(sessionId, out var sseSession))
            {
                if (!string.IsNullOrEmpty(responseJson))
                {
                    try
                    {
                        await sseSession.SendEventAsync("message", responseJson);
                        Log($"[SSE OUT] 消息已推至 Session {sessionId} (Length: {responseJson.Length})");
                    }
                    catch (Exception sseEx)
                    {
                        Log($"[SSE OUT 异常] 推送至 Session {sessionId} 失败: {sseEx.Message}");
                    }
                }
            }

            // 2. HTTP POST 响应处理
            resp.ContentType = "application/json; charset=utf-8";
            if (isNotification || string.IsNullOrEmpty(responseJson))
            {
                // MCP 标准：通知或无返回内容使用 202 Accepted
                resp.StatusCode = 202;
                var acceptedBytes = Encoding.UTF8.GetBytes("Accepted");
                resp.ContentLength64 = acceptedBytes.Length;
                await resp.OutputStream.WriteAsync(acceptedBytes, 0, acceptedBytes.Length);
            }
            else
            {
                resp.StatusCode = 200;
                var bytes = Encoding.UTF8.GetBytes(responseJson);
                resp.ContentLength64 = bytes.Length;
                await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }

            await resp.OutputStream.FlushAsync();
            resp.Close();
        }

        private async Task<object> ProcessRpcItemAsync(JsonElement element)
        {
            object id = null;
            if (element.TryGetProperty("id", out var idProp))
            {
                if (idProp.ValueKind == JsonValueKind.Number) id = idProp.GetInt64();
                else if (idProp.ValueKind == JsonValueKind.String) id = idProp.GetString();
            }

            if (!element.TryGetProperty("method", out var methodProp))
            {
                return new { jsonrpc = "2.0", id, error = new { code = -32600, message = "Invalid Request" } };
            }

            var method = methodProp.GetString();
            element.TryGetProperty("params", out var paramsProp);

            try
            {
                switch (method)
                {
                    case "initialize":
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    resources = new { subscribe = false, listChanged = true }
                                },
                                serverInfo = new
                                {
                                    name = "halcon-vision-studio",
                                    version = "1.1.0"
                                }
                            }
                        };

                    case "notifications/initialized":
                    case "initialized":
                        return null; // Notification, no response

                    case "ping":
                        return new { jsonrpc = "2.0", id, result = new { } };

                    case "tools/list":
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = new
                            {
                                tools = VisionToolCatalog.ToMcpTools()
                            }
                        };

                    case "tools/call":
                        var callResult = await ExecuteMcpToolCallAsync(paramsProp);
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = callResult
                        };

                    case "resources/list":
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = new
                            {
                                resources = new object[]
                                {
                                    new
                                    {
                                        uri = "halcon://workflow/active",
                                        name = "当前活动工作流拓扑",
                                        mimeType = "application/json",
                                        description = "获取当前画布上所有节点、连线及运行状态"
                                    },
                                    new
                                    {
                                        uri = "halcon://logs/execution",
                                        name = "最近执行记录",
                                        mimeType = "application/json",
                                        description = "获取最近执行记录清单"
                                    },
                                    new
                                    {
                                        uri = "halcon://catalog/operators",
                                        name = "可用算子目录",
                                        mimeType = "application/json",
                                        description = "获取系统所有已注册算子信息"
                                    }
                                }
                            }
                        };

                    case "resources/read":
                        var uri = paramsProp.TryGetProperty("uri", out var u) ? u.GetString() : "";
                        var content = await ReadResourceAsync(uri);
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = new
                            {
                                contents = new object[]
                                {
                                    new
                                    {
                                        uri,
                                        mimeType = "application/json",
                                        text = content
                                    }
                                }
                            }
                        };

                    default:
                        return new
                        {
                            jsonrpc = "2.0",
                            id,
                            error = new { code = -32601, message = $"Method not found: {method}" }
                        };
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    error = new { code = -32000, message = ex.Message }
                };
            }
        }

        private async Task<object> ExecuteMcpToolCallAsync(JsonElement paramsElem)
        {
            if (!paramsElem.TryGetProperty("name", out var nameProp))
            {
                return new
                {
                    isError = true,
                    content = new object[] { new { type = "text", text = "缺少 name 参数" } }
                };
            }

            var toolName = nameProp.GetString();
            var hasArgs = paramsElem.TryGetProperty("arguments", out var argsElem) &&
                          argsElem.ValueKind == JsonValueKind.Object;
            var argumentsJson = hasArgs ? argsElem.GetRawText() : null;

            try
            {
                var result = await VisionToolCatalog.ExecuteAsync(_automationService, toolName, argumentsJson);
                var resultText = AiJson.Serialize(result);

                // 业务失败（Success:false）映射为 MCP isError:true，客户端可据此感知失败。
                bool isError = IsBusinessFailure(resultText);
                return new
                {
                    isError,
                    content = new object[] { new { type = "text", text = resultText } }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    isError = true,
                    content = new object[] { new { type = "text", text = $"执行异常: {ex.Message}" } }
                };
            }
        }

        private static bool IsBusinessFailure(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("Success", out var s) &&
                    s.ValueKind == JsonValueKind.False)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private async Task<string> ReadResourceAsync(string uri)
        {
            switch (uri)
            {
                case "halcon://workflow/active":
                    var state = await _automationService.GetWorkflowStateAsync();
                    return AiJson.Serialize(state);

                case "halcon://logs/execution":
                    var logs = await _automationService.GetExecutionRecordsAsync(50);
                    return AiJson.Serialize(logs);

                case "halcon://catalog/operators":
                    var cat = await _automationService.GetOperatorCatalogAsync();
                    return AiJson.Serialize(cat);

                default:
                    return AiJson.Serialize(new { error = $"未知资源 URI: {uri}" });
            }
        }

        private void Log(string msg)
        {
            LogMessage?.Invoke($"[MCP] {DateTime.Now:HH:mm:ss} {msg}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
