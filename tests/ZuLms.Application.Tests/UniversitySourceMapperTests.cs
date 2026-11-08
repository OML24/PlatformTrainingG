using System.Text.Json;
using ZuLms.Application.Features.Sync;

namespace ZuLms.Application.Tests;

public sealed class UniversitySourceMapperTests
{
    [Fact]
    public void AcademicProgramCodesRetainLeadingZerosAndStaySeparateFromHrIds()
    {
        using var json = JsonDocument.Parse("""
            {"departmentid":"0101","facultyid":"01","departmentnamee":" Islamic Jurisprudence ","department_status":1}
            """);
        var program = UniversitySourceMapper.Program(json.RootElement);
        Assert.Equal("0101", program.ProgramCode);
        Assert.Equal("01", program.FacultyCode);
        Assert.Equal("Islamic Jurisprudence", program.NameEn);
        Assert.True(program.IsActive);
    }

    [Fact]
    public void InactiveProgramWithMissingEnglishNameIsValidReferenceData()
    {
        using var json = JsonDocument.Parse("""
            {"departmentid":"0507","facultyid":"05","departmentnamee":null,"department_status":0}
            """);
        var program = UniversitySourceMapper.Program(json.RootElement);
        Assert.False(program.IsActive);
        Assert.Null(program.NameEn);
    }

    [Fact]
    public void RejectsUnknownAcademicProgramStatus()
    {
        using var json = JsonDocument.Parse("""
            {"departmentid":"0101","facultyid":"01","department_status":9}
            """);
        Assert.Throws<InvalidDataException>(() => UniversitySourceMapper.Program(json.RootElement));
    }

    [Fact]
    public void AdministrativeEmployeeIsMappedWithoutAcademicProgramOrSuffixAssumptions()
    {
        using var json = JsonDocument.Parse("""
            {"side":320,"employee_id":77,"fname":"موظف","center_id":12,"department_id":7,
             "main_job_code":2,"sub_job_dscp":"سكرتير","emp_status":13,"suffix_id":2}
            """);
        var employee = UniversitySourceMapper.Employee(json.RootElement);
        Assert.Equal(2, employee.MainJobCode);
        Assert.Equal("سكرتير", employee.SubJobDesc);
        Assert.Equal("على رأس عمله", employee.EmpStatusDesc);
        Assert.Equal(12, employee.CenterCode);
        Assert.Equal(7, employee.DepartmentCode);
    }

    [Fact]
    public void MapsOnlyApprovedEmployeeFieldsAndAmmanDates()
    {
        using var json = JsonDocument.Parse("""
            {"side":320,"employee_id":77,"fname":" محمد ","sname":" علي ",
             "lname":" أحمد ","ename":" Mohammad Ali ","side_desc":" جامعة الزرقاء ",
             "center_id":15,"department_id":4,"main_job_code":1,"emp_status":13,
             "start_work_date":"2025-01-01T21:00:00Z", "sid":"sensitive",
             "birth_date":"1980-01-01", "mobile":"private"}
            """);
        EmployeeSource employee = UniversitySourceMapper.Employee(json.RootElement);
        Assert.Equal(320, employee.SideCode);
        Assert.Equal(77, employee.EmployeeCode);
        Assert.Equal("محمد علي أحمد", employee.FullNameAr);
        Assert.Equal("Mohammad Ali", employee.FullNameEn);
        Assert.Equal(new DateOnly(2025, 1, 2), employee.StartWorkDate);
        Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(employee));
        Assert.DoesNotContain("private", JsonSerializer.Serialize(employee));
    }

    [Fact]
    public void UsesSideIdAndNormalizesEmail()
    {
        using var json = JsonDocument.Parse("""
            {"side_id":"321","employee_id":77,"email":" Security@ZUIS.EDU.JO ","extention":"ignored"}
            """);
        EmailSource email = UniversitySourceMapper.Email(json.RootElement);
        Assert.Equal(321, email.SideCode);
        Assert.Equal("security@zuis.edu.jo", email.Email);
    }

    [Theory]
    [InlineData("""{"side_id":null,"employee_id":null,"email":"s.alkhazaleh@zu.edu.jo"}""")]
    [InlineData("""{"side_id":320,"employee_id":null,"email":"x@zu.edu.jo"}""")]
    [InlineData("""{"side_id":"not-a-number","employee_id":7,"email":"x@zu.edu.jo"}""")]
    [InlineData("""{"email":"x@zu.edu.jo"}""")]
    public void SkipsPlaceholderEmailRowsWithoutKeys(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        Assert.False(UniversitySourceMapper.TryEmail(json.RootElement, out _));
    }

    [Fact]
    public void AcceptsEmailRowWithValidKeys()
    {
        using var json = JsonDocument.Parse("""
            {"side_id":320,"employee_id":77,"email":" hr@ZU.EDU.JO "}
            """);
        Assert.True(UniversitySourceMapper.TryEmail(json.RootElement, out EmailSource email));
        Assert.Equal(320, email.SideCode);
        Assert.Equal(77, email.EmployeeCode);
        Assert.Equal("hr@zu.edu.jo", email.Email);
    }
}
