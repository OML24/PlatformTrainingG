using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using ZuLms.Web.Security;

namespace ZuLms.Web.TagHelpers;

[HtmlTargetElement("zu-if-permission")]
public sealed class PermissionTagHelper(IAuthorizationService authorization) : TagHelper
{
    [HtmlAttributeName("key")]
    public string Key { get; set; } = string.Empty;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var result = await authorization.AuthorizeAsync(ViewContext.HttpContext.User,
            null, new PermissionRequirement(Key));
        if (!result.Succeeded)
        {
            output.SuppressOutput();
            return;
        }
        output.TagName = null;
    }
}
