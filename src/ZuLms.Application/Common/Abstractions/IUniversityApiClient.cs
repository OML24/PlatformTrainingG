using System.Text.Json;

namespace ZuLms.Application.Common.Abstractions;

public interface IUniversityApiClient
{
    Task<IReadOnlyList<JsonElement>> FetchAsync(UniversitySource source,
        CancellationToken cancellationToken);
}

public enum UniversitySource { Centers, Departments, Employees, Emails, Programs }
