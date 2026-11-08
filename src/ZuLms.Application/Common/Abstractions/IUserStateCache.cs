namespace ZuLms.Application.Common.Abstractions;

public interface IUserStateCache
{
    void Invalidate(long userId);
}
