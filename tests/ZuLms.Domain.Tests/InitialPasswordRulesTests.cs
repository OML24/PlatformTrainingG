using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Rules;

namespace ZuLms.Domain.Tests;

public sealed class InitialPasswordRulesTests
{
    [Fact]
    public void GeneratesPolicyCompliantPersonalPassword()
    {
        var policy = new PasswordPolicy
        {
            MinLength = 15, MaxLength = 128, BlockCommon = true, BlockContext = true
        };
        string password = InitialPasswordRules.Generate(policy, [], ["موظف تجريبي", "1234"]);
        Assert.Equal(16, password.Length);
        Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#%*\\-_+=?]{16}$", password);
        Assert.Empty(PasswordPolicyRules.Validate(password, policy, [], ["موظف تجريبي", "1234"]));
    }

    [Fact]
    public void IncludesEveryRequiredClassAndClampsLength()
    {
        var policy = new PasswordPolicy { MinLength = 20, MaxLength = 24,
            RequireUpper = true, RequireLower = true, RequireDigit = true, RequireSymbol = true };
        string password = InitialPasswordRules.Generate(policy, [], []);
        Assert.Equal(20, password.Length);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => "!@#%*-_+=?".Contains(c));
        Assert.Empty(PasswordPolicyRules.Validate(password, policy, [], []));
    }

    [Fact]
    public void ClampsDownToMaximum()
    {
        var policy = new PasswordPolicy { MinLength = 8, MaxLength = 12 };
        Assert.Equal(12, InitialPasswordRules.Generate(policy, [], []).Length);
    }

    [Fact]
    public void ImpossibleRequiredClassesGiveClearError()
    {
        var policy = new PasswordPolicy { MinLength = 1, MaxLength = 2,
            RequireUpper = true, RequireLower = true, RequireDigit = true };
        var error = Assert.Throws<InvalidOperationException>(() =>
            InitialPasswordRules.Generate(policy, [], []));
        Assert.Contains("No initial password satisfies", error.Message);
    }
}
