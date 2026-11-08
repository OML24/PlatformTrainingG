using ZuLms.Domain.Rules;

namespace ZuLms.Domain.Tests;

public sealed class UniversityOrganizationRulesTests
{
    [Theory]
    [InlineData(320, "جامعة الزرقاء")]
    [InlineData(366, "شركة الضوء الساطع")]
    [InlineData(369, "كلية الزرقاء التقنية المتوسطة")]
    [InlineData(345, "أخرى")]
    public void UsesConfirmedSideNamesFromUserReferences(int side, string name) =>
        Assert.Equal(name, UniversityOrganizationRules.SideName(side));

    [Theory]
    [InlineData(13, "على رأس عمله")]
    [InlineData(224, "موظف جامعة")]
    [InlineData(152, "موظف مدارس الجامعة")]
    [InlineData(365, "موظف ضوء ساطع")]
    [InlineData(424, "فصل عن العمل")]
    public void ResolvesEmploymentStatusWithoutGrantingEligibility(int status, string name) =>
        Assert.Equal(name, UniversityOrganizationRules.EmployeeStatusName(status));

    [Fact]
    public void UnknownReferenceValuesRetainSourceText()
    {
        Assert.Equal("جهة جديدة", UniversityOrganizationRules.SideName(999, " جهة جديدة "));
        Assert.Equal("حالة جديدة", UniversityOrganizationRules.EmployeeStatusName(999, "حالة جديدة"));
        Assert.Equal(17, UniversityOrganizationRules.EmployeeStatusNames.Count);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(null, 2, true)]
    [InlineData(1, null, true)]
    public void DetectsContradictoryHrCenterReference(int? employeeCenter, int? departmentCenter, bool matches) =>
        Assert.Equal(matches, UniversityOrganizationRules.DepartmentMatchesCenter(employeeCenter, departmentCenter));
}
