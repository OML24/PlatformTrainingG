using System.Globalization;
using System.Resources;
using System.Text.Encodings.Web;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Localization;

public sealed class EmailContentBuilder : IEmailContentBuilder
{
    private static readonly ResourceManager Resources = new("ZuLms.Web.SharedResource", typeof(SharedResource).Assembly);

    public (string Subject, string BodyHtml) ForgotPassword(string name, string password, UiLanguage language)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(language == UiLanguage.En ? "en" : "ar");
        string subject = Resources.GetString("Auth.Forgot.EmailSubject", culture)
            ?? throw new InvalidOperationException("Missing forgot-password email subject.");
        string template = Resources.GetString("Auth.Forgot.EmailBody", culture)
            ?? throw new InvalidOperationException("Missing forgot-password email body.");
        string body = string.Format(culture, template,
            HtmlEncoder.Default.Encode(name), HtmlEncoder.Default.Encode(password));
        return (subject, body);
    }

    public (string Subject, string BodyHtml) InitialCredentials(string name, string login,
        string password, UiLanguage language)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(language == UiLanguage.En ? "en" : "ar");
        string subject = Resources.GetString("Credentials.Initial.EmailSubject", culture)
            ?? throw new InvalidOperationException("Missing initial-credentials email subject.");
        string template = Resources.GetString("Credentials.Initial.EmailBody", culture)
            ?? throw new InvalidOperationException("Missing initial-credentials email body.");
        string baseUrl = Environment.GetEnvironmentVariable("ZU_PUBLIC_BASE_URL")
            ?? throw new InvalidOperationException("ZU_PUBLIC_BASE_URL is required.");
        string body = string.Format(culture, template,
            HtmlEncoder.Default.Encode(name), HtmlEncoder.Default.Encode(login),
            HtmlEncoder.Default.Encode(password), HtmlEncoder.Default.Encode(baseUrl));
        return (subject, body);
    }

    public (string Subject, string BodyHtml) ExternalInstructorAccount(string name,
        string login, string password, UiLanguage language)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(language == UiLanguage.En ? "en" : "ar");
        string subject = Resources.GetString("ExternalInstructor.EmailSubject", culture)
            ?? throw new InvalidOperationException("Missing external-instructor email subject.");
        string template = Resources.GetString("ExternalInstructor.EmailBody", culture)
            ?? throw new InvalidOperationException("Missing external-instructor email body.");
        string baseUrl = Environment.GetEnvironmentVariable("ZU_PUBLIC_BASE_URL")
            ?? throw new InvalidOperationException("ZU_PUBLIC_BASE_URL is required.");
        string body = string.Format(culture, template,
            HtmlEncoder.Default.Encode(name), HtmlEncoder.Default.Encode(login),
            HtmlEncoder.Default.Encode(password), HtmlEncoder.Default.Encode(baseUrl));
        return (subject, body);
    }
}
