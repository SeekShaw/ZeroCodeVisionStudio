using System.ComponentModel;
using System.Net.Http;
using System.Text;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.DeviceCommunication
{
    /// <summary>
    /// HTTP 请求算子：GET/POST/PUT 调用企业接口（MES 上报/工单拉取）。
    /// 请求体支持 $Key$ 模板插值（上游测量值自动填充 JSON 字段）。
    /// </summary>
    public class HttpRequestParam : IHandleParameter
    {
        [DisplayName("请求方式")]
        [Description("GET / POST / PUT")]
        public HttpMethodKind Method { get; set; } = HttpMethodKind.GET;

        [DisplayName("URL")]
        [Description("完整地址，如 http://mes.local/api/report")]
        public string Url { get; set; } = "http://127.0.0.1:8080/api";

        [DisplayName("请求体")]
        [Description("POST/PUT 的请求体；支持 $Key$ 插值，如 {\"distance\": $Distance$}")]
        public string Body { get; set; } = "";

        [DisplayName("Content-Type")]
        [Description("POST/PUT 请求体类型，默认 application/json")]
        public string ContentType { get; set; } = "application/json";

        [DisplayName("超时(ms)")]
        public int TimeoutMs { get; set; } = 10000;

        /// <summary>自定义请求头（每行一个 "Key: Value"）。</summary>
        [DisplayName("请求头")]
        [Description("自定义请求头，每行一个，格式 Key: Value")]
        public string Headers { get; set; } = "";

        public IHandleParameter Clone() => (HttpRequestParam)MemberwiseClone();

        public void Reset()
        {
            Method = HttpMethodKind.GET;
            Url = "http://127.0.0.1:8080/api";
            Body = "";
            ContentType = "application/json";
            TimeoutMs = 10000;
            Headers = "";
        }
    }

    public enum HttpMethodKind
    {
        [Description("GET")] GET = 0,
        [Description("POST")] POST = 1,
        [Description("PUT")] PUT = 2
    }

    [Handle("HTTP请求", "HttpRequest", "调用 HTTP 接口（GET/POST/PUT），对接 MES/企业系统。请求体支持 $Key$ 上游值插值；响应状态码与正文输出到 HTTP 状态/HTTP 响应。", "设备通讯")]
    public class HttpHandle : ParameterHandleBase<HttpRequestParam>
    {
        public HttpHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public override void Handle()
        {
            var p = Parameter;
            if (string.IsNullOrWhiteSpace(p?.Url))
            {
                throw new HandleException(ExceptionType.E_PARAMETER_INVALID, "HttpRequest 未配置 URL。");
            }

            var url = Interpolate(p.Url);
            var body = Interpolate(p.Body ?? "");

            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, p.TimeoutMs)) };
            using var request = new HttpRequestMessage(
                p.Method switch
                {
                    HttpMethodKind.POST => System.Net.Http.HttpMethod.Post,
                    HttpMethodKind.PUT => System.Net.Http.HttpMethod.Put,
                    _ => System.Net.Http.HttpMethod.Get
                }, url);

            if (p.Method != HttpMethodKind.GET && !string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8,
                    string.IsNullOrWhiteSpace(p.ContentType) ? "application/json" : p.ContentType);
            }

            foreach (var line in (p.Headers ?? "").Split('\n'))
            {
                var sep = line.IndexOf(':');
                if (sep > 0)
                {
                    request.Headers.TryAddWithoutValidation(line[..sep].Trim(), line[(sep + 1)..].Trim());
                }
            }

            var response = http.SendAsync(request).GetAwaiter().GetResult();
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            RunParameter.SetOutput("HttpStatus", (int)response.StatusCode);
            RunParameter.SetOutput("HttpBody", responseBody);
            RunParameter.ResultMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}：{Truncate(responseBody, 60)}";

            if (!response.IsSuccessStatusCode)
            {
                throw new HandleException(ExceptionType.E_NO_VALID_INPUT,
                    $"HTTP 请求失败：{(int)response.StatusCode} {response.ReasonPhrase}（URL: {url}）");
            }
        }

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

        private static string Truncate(string text, int max) =>
            string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
    }
}
