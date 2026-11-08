using System.Globalization;
using System.Text.Json;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Sync;

public static class UniversitySourceMapper
{
    private static readonly TimeZoneInfo Amman = TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman");

    public static CenterSource Center(JsonElement row) => new(
        RequiredInt(row, "side"), RequiredInt(row, "center_id"),
        RequiredText(row, "center_dscp"), OptionalInt(row, "unit_id"),
        OptionalInt(row, "centr_head"), OptionalInt(row, "fac_registrar"));

    public static DepartmentSource Department(JsonElement row) => new(
        RequiredInt(row, "side"), RequiredInt(row, "department_id"),
        RequiredText(row, "department_dscp"), OptionalInt(row, "center_id"),
        OptionalText(row, "reg_code"), OptionalInt(row, "gate_dep_head"));

    public static AcademicProgramSource Program(JsonElement row)
    {
        int status = RequiredInt(row, "department_status");
        if (status is not (0 or 1)) throw new InvalidDataException("Invalid academic program status.");
        // Codes are text: "0101" is not employee department 101 or center 1.
        return new AcademicProgramSource(RequiredText(row, "departmentid"),
            RequiredText(row, "facultyid"), OptionalText(row, "departmentnamee"), status == 1);
    }

    public static EmployeeSource Employee(JsonElement row)
    {
        string? first = OptionalText(row, "fname");
        string? second = OptionalText(row, "sname");
        string? third = OptionalText(row, "tname");
        string? last = OptionalText(row, "lname");
        int? status = OptionalInt(row, "emp_status");
        return new EmployeeSource(
            RequiredInt(row, "side"), RequiredInt(row, "employee_id"),
            first, second, third, last, NameRules.Join(first, second, third, last),
            OptionalText(row, "ename"), OptionalText(row, "side_desc"),
            OptionalInt(row, "center_id"), OptionalInt(row, "department_id"),
            OptionalInt(row, "main_job_code"), OptionalText(row, "main_job_dscp"),
            OptionalInt(row, "sub_job_id"), OptionalText(row, "sub_job_dscp"),
            status, UniversityOrganizationRules.EmployeeStatusName(status, OptionalText(row, "emp_status_desc")),
            OptionalInt(row, "chk_emp_stat"), OptionalDate(row, "start_work_date"),
            OptionalDate(row, "stop_work_date"));
    }

    public static EmailSource Email(JsonElement row) => new(
        RequiredInt(row, "side_id"), RequiredInt(row, "employee_id"),
        OptionalText(row, "email")?.ToLowerInvariant());

    // The email source contains occasional placeholder rows without side_id/employee_id.
    // Those rows target no employee, so they are skipped instead of failing the whole source.
    public static bool TryEmail(JsonElement row, out EmailSource email)
    {
        email = null!;
        try
        {
            if (OptionalInt(row, "side_id") is not int side ||
                OptionalInt(row, "employee_id") is not int employee)
                return false;
            email = new EmailSource(side, employee, OptionalText(row, "email")?.ToLowerInvariant());
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static int RequiredInt(JsonElement row, string key) =>
        OptionalInt(row, key) ?? throw new InvalidDataException($"Missing required source field: {key}");

    private static int? OptionalInt(JsonElement row, string key)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        throw new InvalidDataException($"Invalid source number: {key}");
    }

    private static string RequiredText(JsonElement row, string key) =>
        OptionalText(row, key) ?? throw new InvalidDataException($"Missing required source field: {key}");

    private static string? OptionalText(JsonElement row, string key)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Invalid source text: {key}");
        string text = value.GetString()!.Trim();
        return text.Length == 0 ? null : text;
    }

    private static DateOnly? OptionalDate(JsonElement row, string key)
    {
        string? raw = OptionalText(row, key);
        if (raw is null) return null;
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var timestamp))
            throw new InvalidDataException($"Invalid source date: {key}");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, Amman).DateTime);
    }
}

public sealed record CenterSource(int SideCode, int CenterCode, string NameAr,
    int? UnitCode, int? HeadEmployeeCode, int? RegistrarEmployeeCode);
public sealed record DepartmentSource(int SideCode, int DepartmentCode, string NameAr,
    int? CenterCode, string? RegCode, int? HeadEmployeeCode);
public sealed record AcademicProgramSource(string ProgramCode, string FacultyCode,
    string? NameEn, bool IsActive);
public sealed record EmployeeSource(int SideCode, int EmployeeCode,
    string? FirstName, string? SecondName, string? ThirdName, string? LastName,
    string FullNameAr, string? FullNameEn, string? SideNameAr,
    int? CenterCode, int? DepartmentCode, int? MainJobCode, string? MainJobDesc,
    int? SubJobCode, string? SubJobDesc, int? EmpStatus, string? EmpStatusDesc,
    int? ChkEmpStat, DateOnly? StartWorkDate, DateOnly? StopWorkDate);
public sealed record EmailSource(int SideCode, int EmployeeCode, string? Email);
