using Microsoft.Extensions.Caching.Memory;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Web.Security;

public sealed class UserStateCache(IMemoryCache cache) : IUserStateCache
{
    public void Invalidate(long userId) => AuthHttp.InvalidateUser(cache, userId);
}
