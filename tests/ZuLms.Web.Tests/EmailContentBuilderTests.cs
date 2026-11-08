using ZuLms.Domain.Enums;
using ZuLms.Web.Localization;

namespace ZuLms.Web.Tests;

public sealed class EmailContentBuilderTests
{
    [Fact]
    public void ForgotEmailEscapesDynamicValues()
    {
        var builder = new EmailContentBuilder();
        var (subject, body) = builder.ForgotPassword("<script>", "a<&b", UiLanguage.En);
        Assert.Contains("Password recovery", subject);
        Assert.DoesNotContain("<script>", body);
        Assert.DoesNotContain("a<&b", body);
        Assert.Contains("&lt;script&gt;", body);
        Assert.Contains("a&lt;&amp;b", body);
    }

    [Fact]
    public void ExternalInstructorEmailEscapesCredentials()
    {
        string? previous = Environment.GetEnvironmentVariable("ZU_PUBLIC_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("ZU_PUBLIC_BASE_URL", "https://example.test");
            var builder = new EmailContentBuilder();
            var (subject, body) = builder.ExternalInstructorAccount(
                "<trainer>", "8000", "a<&b", UiLanguage.Ar);
            Assert.Contains("المدرب الخارجي", subject);
            Assert.DoesNotContain("<trainer>", body);
            Assert.DoesNotContain("a<&b", body);
            Assert.Contains("&lt;trainer&gt;", body);
            Assert.Contains("a&lt;&amp;b", body);
        }
        finally { Environment.SetEnvironmentVariable("ZU_PUBLIC_BASE_URL", previous); }
    }
}
