using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Abstractions;

public interface IEmailContentBuilder
{
    (string Subject, string BodyHtml) ForgotPassword(string name, string password, UiLanguage language);
    (string Subject, string BodyHtml) InitialCredentials(string name, string login,
        string password, UiLanguage language);
    (string Subject, string BodyHtml) ExternalInstructorAccount(string name,
        string login, string password, UiLanguage language);
}
