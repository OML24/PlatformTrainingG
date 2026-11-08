namespace ZuLms.Application.Features.Courses;

public interface IEvaluationPinningService
{
    Task<int> PinCompletedAsync(CancellationToken ct);
}
