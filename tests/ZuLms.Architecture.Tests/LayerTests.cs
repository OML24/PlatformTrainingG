using System.Reflection;
using NetArchTest.Rules;

namespace ZuLms.Architecture.Tests;

public sealed class LayerTests
{
    [Theory]
    [InlineData("ZuLms.Domain", "ZuLms.Application")]
    [InlineData("ZuLms.Domain", "ZuLms.Infrastructure")]
    [InlineData("ZuLms.Domain", "ZuLms.Web")]
    [InlineData("ZuLms.Application", "ZuLms.Infrastructure")]
    [InlineData("ZuLms.Application", "ZuLms.Web")]
    [InlineData("ZuLms.Infrastructure", "ZuLms.Web")]
    public void LayersDoNotDependOutward(string source, string forbidden)
    {
        var assembly = Assembly.Load(source);
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn(forbidden).GetResult();
        Assert.True(result.IsSuccessful, $"{source} references {forbidden}");
    }
}
