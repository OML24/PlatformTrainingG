using System.Collections.Frozen;

namespace ZuLms.Domain.Rules;

public static class UniversityOrganizationRules
{
    public const int ActiveEmployeeStatus = 13;
    // Confirmed organization identifiers; unrelated to suffix IDs or job/status codes.
    public static IReadOnlyDictionary<int, string> SideNames { get; } = new Dictionary<int, string>
    {
        [320] = "جامعة الزرقاء",
        [321] = "مدارس الجامعة",
        [366] = "شركة الضوء الساطع",
        [369] = "كلية الزرقاء التقنية المتوسطة",
        [345] = "أخرى"
    }.ToFrozenDictionary();

    public static IReadOnlyDictionary<int, string> EmployeeStatusNames { get; } = new Dictionary<int, string>
    {
        [13] = "على رأس عمله", [14] = "مستقيل", [15] = "منقطع عـن العمل",
        [41] = "اجازة بدون راتب", [42] = "معار", [89] = "موظف الشركة",
        [125] = "مبتعث", [126] = "وفاة", [129] = "ملغي",
        [152] = "موظف مدارس الجامعة", [153] = "استغناء عن خدمات",
        [223] = "مدارس/مستقيل", [224] = "موظف جامعة", [365] = "موظف ضوء ساطع",
        [422] = "ايقاف عن العمل", [423] = "انهاء عقد", [424] = "فصل عن العمل"
    }.ToFrozenDictionary();

    public static string SideName(int code, string? sourceName = null) =>
        SideNames.TryGetValue(code, out string? name) ? name :
            string.IsNullOrWhiteSpace(sourceName) ? code.ToString() : sourceName.Trim();

    public static string? EmployeeStatusName(int? code, string? sourceName = null) =>
        code.HasValue && EmployeeStatusNames.TryGetValue(code.Value, out string? name)
            ? name : sourceName;

    public static bool DepartmentMatchesCenter(int? employeeCenterCode, int? departmentCenterCode) =>
        !employeeCenterCode.HasValue || !departmentCenterCode.HasValue ||
        employeeCenterCode.Value == departmentCenterCode.Value;
}
