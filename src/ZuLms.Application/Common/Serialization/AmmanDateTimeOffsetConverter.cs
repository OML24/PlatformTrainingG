using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZuLms.Application.Common.Serialization;

/// <summary>
/// Accepts and emits timestamps in the platform contract (01 §6.1): the client sends local Amman
/// wall-clock text (<c>yyyy-MM-dd HH:mm</c>, <c>yyyy-MM-dd</c>, or ISO without an offset) and the
/// server converts it to UTC. Values that already carry an offset are honoured as-is.
/// </summary>
public sealed class AmmanDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private static readonly TimeZoneInfo Amman = ResolveAmman();
    private static readonly string[] LocalFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd"
    ];

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a date/time string.");

        string text = reader.GetString()?.Trim() ?? string.Empty;
        if (text.Length == 0) throw new JsonException("Expected a date/time value.");

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var absolute)
            && HasOffset(text))
            return absolute.ToUniversalTime();

        if (DateTime.TryParseExact(text, LocalFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            var wallClock = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return new DateTimeOffset(wallClock, Amman.GetUtcOffset(wallClock)).ToUniversalTime();
        }

        throw new JsonException($"Invalid date/time value '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var local = TimeZoneInfo.ConvertTime(value, Amman);
        writer.WriteStringValue(local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
    }

    private static bool HasOffset(string text) =>
        text.EndsWith('Z') || text.EndsWith('z')
        || (text.Length > 6 && (text[^6] == '+' || text[^6] == '-'));

    private static TimeZoneInfo ResolveAmman()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
