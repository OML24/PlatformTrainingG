using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZuLms.Web.Middleware;

/// <summary>
/// Enforces the fixed API contract of the build pack (01 §6.1):
/// success → <c>{ ok: true, data: … }</c>, failure → <c>{ ok: false, code, message, fieldErrors }</c>.
/// Binary responses (file downloads) pass through untouched.
/// </summary>
public sealed class ApiEnvelopeMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        Stream original = context.Response.Body;
        var body = new EnvelopeStream(context.Response, original);
        context.Response.Body = body;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
            await body.FlushAsync(context.RequestAborted);
        }

        if (!body.Wrapped)
        {
            if (body.Buffered) await body.CopyBufferedToAsync(original, context.RequestAborted);
            return;
        }

        int status = context.Response.StatusCode;
        JsonNode? payload = body.ParseJson();
        context.Response.Headers.ContentLength = null;

        if (status < 400)
        {
            if (status is StatusCodes.Status204NoContent or StatusCodes.Status205ResetContent)
                context.Response.StatusCode = StatusCodes.Status200OK;
            var envelope = new JsonObject
            {
                ["ok"] = true,
                ["data"] = payload is null ? null : payload.DeepClone()
            };
            await WriteAsync(context, original, envelope, context.Response.StatusCode);
            return;
        }

        string? code = null;
        string? message = null;
        JsonObject? fieldErrors = null;
        if (payload is JsonObject item)
        {
            code = ReadString(item["code"]);
            message = ReadString(item["message"]);
            if (item["fieldErrors"] is JsonObject supplied) fieldErrors = (JsonObject)supplied.DeepClone();
            else if (item["errors"] is JsonObject modelState) fieldErrors = (JsonObject)modelState.DeepClone();
            if (message is null) message = ReadString(item["title"]);
            if (code is null && fieldErrors is not null) code = "VALIDATION_FAILED";
        }
        code ??= ApiMessages.DefaultCode(status);
        message ??= ApiMessages.Error(code, CultureInfo.CurrentUICulture);

        var error = new JsonObject
        {
            ["ok"] = false,
            ["code"] = code,
            ["message"] = message,
            ["fieldErrors"] = fieldErrors
        };
        await WriteAsync(context, original, error, status);
    }

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out string? text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static async Task WriteAsync(HttpContext context, Stream destination, JsonObject envelope, int status)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(envelope.ToJsonString(JsonOptions));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await destination.WriteAsync(bytes, context.RequestAborted);
    }

    /// <summary>
    /// Buffers JSON payloads so they can be wrapped, and lets binary payloads stream straight through.
    /// </summary>
    private sealed class EnvelopeStream(HttpResponse response, Stream inner) : Stream
    {
        private readonly MemoryStream buffer = new();
        private bool decided;
        private bool wrap = true;

        public bool Wrapped => decided ? wrap : true;
        public bool Buffered => decided ? wrap : true;

        private void Decide()
        {
            if (decided) return;
            decided = true;
            string? contentType = response.ContentType;
            bool isJson = string.IsNullOrEmpty(contentType)
                || contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
            wrap = isJson && !response.Headers.ContainsKey("Content-Disposition");
        }

        public override void Write(ReadOnlySpan<byte> value)
        {
            Decide();
            if (wrap) buffer.Write(value);
            else inner.Write(value);
        }

        public override void Write(byte[] value, int offset, int count)
        {
            Decide();
            if (wrap) buffer.Write(value, offset, count);
            else inner.Write(value, offset, count);
        }

        public override Task WriteAsync(byte[] value, int offset, int count, CancellationToken ct)
        {
            Decide();
            return wrap ? buffer.WriteAsync(value, offset, count, ct) : inner.WriteAsync(value, offset, count, ct);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> value, CancellationToken ct = default)
        {
            Decide();
            return wrap ? buffer.WriteAsync(value, ct) : inner.WriteAsync(value, ct);
        }

        public async Task CopyBufferedToAsync(Stream destination, CancellationToken ct)
        {
            if (buffer.Length == 0) return;
            buffer.Position = 0;
            await buffer.CopyToAsync(destination, ct);
        }

        public JsonNode? ParseJson()
        {
            if (buffer.Length == 0) return null;
            try { return JsonNode.Parse(buffer.ToArray()); }
            catch (JsonException) { return null; }
        }

        public override void Flush()
        {
            if (decided && !wrap) inner.Flush();
        }

        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] value, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
