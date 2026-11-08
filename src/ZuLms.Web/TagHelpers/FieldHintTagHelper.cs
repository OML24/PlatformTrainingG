using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ZuLms.Web.TagHelpers;

/// <summary>
/// Renders the field-hint affordance (01 §6.4). <c>&lt;input zu-hint="course.title_ar" /&gt;</c> becomes
/// a hint-enabled control plus a <c>?</c> icon; <c>hints.js</c> fills the text from
/// <c>GET /api/v1/hints?context=…</c> so administrator edits appear without redeploying.
/// </summary>
[HtmlTargetElement("input", Attributes = HintAttributeName)]
[HtmlTargetElement("textarea", Attributes = HintAttributeName)]
[HtmlTargetElement("select", Attributes = HintAttributeName)]
public sealed class FieldHintTagHelper : TagHelper
{
    public const string HintAttributeName = "zu-hint";

    [HtmlAttributeName(HintAttributeName)]
    public string? Key { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll(HintAttributeName);
        if (string.IsNullOrWhiteSpace(Key)) return;

        string key = Key.Trim();
        string contextKey = key.Contains('.') ? key[..key.IndexOf('.')] : key;
        output.Attributes.SetAttribute("data-hint-key", key);
        output.Attributes.SetAttribute("data-hint-context", contextKey);
        output.PostElement.AppendHtml(
            $"<span class=\"zu-hint-icon\" data-hint-key=\"{key}\" data-hint-context=\"{contextKey}\" tabindex=\"0\" role=\"note\">?</span>");
    }
}
