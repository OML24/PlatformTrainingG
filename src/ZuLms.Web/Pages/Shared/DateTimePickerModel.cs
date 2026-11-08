namespace ZuLms.Web.Pages.Shared;

/// <summary>
/// Model for the shared date/time picker. Native <c>type="date"</c> and <c>type="datetime-local"</c>
/// inputs are forbidden by the build pack; every date field renders through this partial instead.
/// </summary>
public sealed record DateTimePickerModel(
    string Id,
    string Label,
    bool WithTime = false,
    bool Required = false,
    string? Value = null,
    string? HintKey = null,
    string ColumnClass = "col-md-6");
