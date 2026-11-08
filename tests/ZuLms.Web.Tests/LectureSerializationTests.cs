using System.Text.Json;
using System.Text.Json.Serialization;
using ZuLms.Application.Common.Serialization;
using ZuLms.Application.Features.Courses;

namespace ZuLms.Web.Tests;

public sealed class LectureSerializationTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void SpaceSeparatedPickerDateCannotBeReadAsLectureDateTime()
    {
        const string json = """
            {"mode":"InPerson","startsAt":"2026-11-01 09:33","endsAt":"2026-11-01 09:43"}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LectureInput>(json, Options));
    }

    [Theory]
    [InlineData("2026-11-01T09:33:00", "2026-11-01T09:43:00", 1, 9, 33)]
    [InlineData("2026-11-01T23:59:00", "2026-11-02T00:00:00", 1, 23, 59)]
    [InlineData("2026-11-02T00:00:00", "2026-11-02T00:01:00", 2, 0, 0)]
    public void CreateAndEditAcceptIsoLocalTimeWithoutTimezoneConversion(
        string start, string end, int day, int hour, int minute)
    {
        string json = $$"""
            {"mode":"InPerson","startsAt":"{{start}}","endsAt":"{{end}}","locationAr":"Test room"}
            """;

        var create = JsonSerializer.Deserialize<LectureInput>(json, Options)!;
        var edit = JsonSerializer.Deserialize<LectureScheduleInput>(json, Options)!;

        Assert.Equal(new DateTime(2026, 11, day, hour, minute, 0), create.StartsAt);
        Assert.Equal(DateTimeKind.Unspecified, create.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Unspecified, create.EndsAt.Kind);
        Assert.Equal(create.StartsAt, edit.StartsAt);
        Assert.Equal(create.EndsAt, edit.EndsAt);
        Assert.Equal(DateTimeKind.Unspecified, edit.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Unspecified, edit.EndsAt.Kind);
        Assert.True(create.EndsAt > create.StartsAt);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new AmmanDateTimeOffsetConverter());
        return options;
    }
}
