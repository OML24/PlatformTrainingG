using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Entities.Org;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Sync;

public sealed class UniversityMergeService(IAppDbContext db, IClock clock, ISettingsService settings,
    IUserStateCache userState)
{
    public Task<SourceMergeStats> ApplyCentersAsync(IReadOnlyList<CenterSource> source,
        long runId, CancellationToken ct) => db.InTransactionAsync(async () =>
    {
        var sides = await LoadSidesAsync(source.Select(x => x.SideCode), ct);
        var existing = await db.Centers.IgnoreQueryFilters().ToListAsync(ct);
        var byKey = existing.ToDictionary(x => (x.SideCode, x.CenterCode));
        if (source.Count == 0 && existing.Any(x => sides[x.SideCode].IsImported && x.IsActive))
            throw new InvalidDataException("SUSPICIOUS_EMPTY_CENTERS");
        string codes = await settings.GetAsync<string>("org.faculty_unit_codes", ct);
        var facultyUnits = ParseCodes(codes);
        int added = 0, updated = 0, unchanged = 0, missing = 0;
        var seen = new HashSet<(int, int)>();
        foreach (var row in source)
        {
            var key = (row.SideCode, row.CenterCode);
            if (!seen.Add(key)) throw new InvalidDataException("DUPLICATE_CENTER_SOURCE_KEY");
            if (!sides[row.SideCode].IsImported) continue;
            string hash = Hash(row);
            if (!byKey.TryGetValue(key, out var center))
            {
                center = new Center
                {
                    SideCode = row.SideCode, CenterCode = row.CenterCode,
                    NameAr = row.NameAr, UnitCode = row.UnitCode,
                    HeadEmployeeCode = row.HeadEmployeeCode,
                    RegistrarEmployeeCode = row.RegistrarEmployeeCode,
                    IsFaculty = row.UnitCode is int unit && facultyUnits.Contains(unit) &&
                        !(row.SideCode == 320 && row.CenterCode is 61 or 62 or 102 or 121),
                    SourceHash = hash, LastSeenAt = clock.UtcNow,
                    CreatedAt = clock.UtcNow, IsActive = true
                };
                db.Centers.Add(center);
                added++;
            }
            else
            {
                if (center.SourceHash != hash)
                {
                    center.NameAr = row.NameAr;
                    center.UnitCode = row.UnitCode;
                    center.HeadEmployeeCode = row.HeadEmployeeCode;
                    center.RegistrarEmployeeCode = row.RegistrarEmployeeCode;
                    center.SourceHash = hash;
                    updated++;
                }
                else unchanged++;
                center.LastSeenAt = clock.UtcNow;
                if (center.SourceMissing)
                {
                    center.SourceMissing = false;
                    center.SourceMissingSince = null;
                    await CloseReviewAsync("Center", Key(row.SideCode, row.CenterCode),
                        SyncReviewReason.MissingFromSource, ct);
                }
            }
        }
        foreach (var center in existing.Where(x => sides[x.SideCode].IsImported &&
            !seen.Contains((x.SideCode, x.CenterCode))))
        {
            if (center.SourceMissing) continue;
            center.SourceMissing = true;
            center.SourceMissingSince = clock.UtcNow;
            await AddReviewIfAbsentAsync("Center", Key(center.SideCode, center.CenterCode),
                SyncReviewReason.MissingFromSource, runId, null, ct);
            missing++;
        }
        await db.SaveChangesAsync(ct);
        return new SourceMergeStats(source.Count, added, updated, unchanged, missing);
    }, ct);

    public Task<SourceMergeStats> ApplyDepartmentsAsync(IReadOnlyList<DepartmentSource> source,
        long runId, CancellationToken ct) => db.InTransactionAsync(async () =>
    {
        var sides = await LoadSidesAsync(source.Select(x => x.SideCode), ct);
        var existing = await db.Departments.IgnoreQueryFilters().ToListAsync(ct);
        if (source.Count == 0 && existing.Any(x => sides[x.SideCode].IsImported && x.IsActive))
            throw new InvalidDataException("SUSPICIOUS_EMPTY_DEPARTMENTS");
        var byKey = existing.ToDictionary(x => (x.SideCode, x.DepartmentCode));
        var centerIds = await db.Centers.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => new { x.SideCode, x.CenterCode }, x => x.Id, ct);
        int added = 0, updated = 0, unchanged = 0, missing = 0;
        var seen = new HashSet<(int, int)>();
        foreach (var row in source)
        {
            var key = (row.SideCode, row.DepartmentCode);
            if (!seen.Add(key)) throw new InvalidDataException("DUPLICATE_DEPARTMENT_SOURCE_KEY");
            if (!sides[row.SideCode].IsImported) continue;
            long? centerId = row.CenterCode is int code &&
                centerIds.TryGetValue(new { row.SideCode, CenterCode = code }, out long id) ? id : null;
            if (row.CenterCode is not null && centerId is null)
                await AddReviewIfAbsentAsync("Department", Key(row.SideCode, row.DepartmentCode),
                    SyncReviewReason.UnresolvedReference, runId,
                    JsonSerializer.Serialize(new { row.CenterCode }), ct);
            else if (centerId is not null)
                await CloseReviewAsync("Department", Key(row.SideCode, row.DepartmentCode),
                    SyncReviewReason.UnresolvedReference, ct);
            string hash = Hash(row);
            if (!byKey.TryGetValue(key, out var department))
            {
                department = new Department
                {
                    SideCode = row.SideCode, DepartmentCode = row.DepartmentCode,
                    NameAr = row.NameAr, CenterCode = row.CenterCode, CenterId = centerId,
                    RegCode = row.RegCode, HeadEmployeeCode = row.HeadEmployeeCode,
                    SourceHash = hash, LastSeenAt = clock.UtcNow,
                    CreatedAt = clock.UtcNow, IsActive = true
                };
                db.Departments.Add(department);
                added++;
            }
            else
            {
                if (department.SourceHash != hash || department.CenterId != centerId)
                {
                    department.NameAr = row.NameAr;
                    department.CenterCode = row.CenterCode;
                    department.CenterId = centerId;
                    if (department.RegCode != row.RegCode) department.NameEn = null;
                    department.RegCode = row.RegCode;
                    department.HeadEmployeeCode = row.HeadEmployeeCode;
                    department.SourceHash = hash;
                    updated++;
                }
                else unchanged++;
                department.LastSeenAt = clock.UtcNow;
                if (department.SourceMissing)
                {
                    department.SourceMissing = false;
                    department.SourceMissingSince = null;
                    await CloseReviewAsync("Department", Key(row.SideCode, row.DepartmentCode),
                        SyncReviewReason.MissingFromSource, ct);
                }
            }
        }
        foreach (var department in existing.Where(x => sides[x.SideCode].IsImported &&
            !seen.Contains((x.SideCode, x.DepartmentCode))))
        {
            if (department.SourceMissing) continue;
            department.SourceMissing = true;
            department.SourceMissingSince = clock.UtcNow;
            await AddReviewIfAbsentAsync("Department", Key(department.SideCode, department.DepartmentCode),
                SyncReviewReason.MissingFromSource, runId, null, ct);
            missing++;
        }
        await db.SaveChangesAsync(ct);
        return new SourceMergeStats(source.Count, added, updated, unchanged, missing);
    }, ct);

    // Organization-only maintenance: re-resolve existing raw HR codes without
    // fetching employees, provisioning accounts, touching credentials or sending mail.
    public Task<int> ReconcileEmployeeOrganizationAsync(long runId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var imported = await db.UniversitySides.Where(x => x.IsImported)
                .Select(x => x.SideCode).ToListAsync(ct);
            var employees = await db.Employees.Where(x => imported.Contains(x.SideCode)).ToListAsync(ct);
            var centers = await db.Centers.IgnoreQueryFilters().AsNoTracking()
                .ToDictionaryAsync(x => new { x.SideCode, x.CenterCode }, x => x.Id, ct);
            var departments = await db.Departments.IgnoreQueryFilters().AsNoTracking()
                .ToDictionaryAsync(x => new { x.SideCode, x.DepartmentCode }, ct);
            var academic = ParseCodes(await settings.GetAsync<string>("directory.academic_main_job_codes", ct));
            int updated = 0;
            foreach (var employee in employees)
            {
                long? centerId = employee.CenterCode is int centerCode &&
                    centers.TryGetValue(new { employee.SideCode, CenterCode = centerCode }, out long cid) ? cid : null;
                Department? department = employee.DepartmentCode is int departmentCode &&
                    departments.TryGetValue(new { employee.SideCode, DepartmentCode = departmentCode }, out var found) ? found : null;
                bool mismatch = department is not null &&
                    !UniversityOrganizationRules.DepartmentMatchesCenter(employee.CenterCode, department.CenterCode);
                long? departmentId = mismatch ? null : department?.Id;
                string key = Key(employee.SideCode, employee.EmployeeCode);
                if (employee.CenterCode.HasValue && centerId is null || employee.DepartmentCode.HasValue && departmentId is null)
                    await AddReviewIfAbsentAsync("Employee", key, SyncReviewReason.UnresolvedReference,
                        runId, JsonSerializer.Serialize(new { employee.CenterCode, employee.DepartmentCode,
                            DepartmentCenterCode = department?.CenterCode, CenterMismatch = mismatch }), ct);
                else await CloseReviewAsync("Employee", key, SyncReviewReason.UnresolvedReference, ct);
                string? statusName = UniversityOrganizationRules.EmployeeStatusName(employee.EmpStatus, employee.EmpStatusDesc);
                bool isAcademic = academic.Contains(employee.MainJobCode ?? int.MinValue);
                if (employee.CenterId == centerId && employee.DepartmentId == departmentId &&
                    employee.EmpStatusDesc == statusName && employee.IsAcademic == isAcademic) continue;
                employee.CenterId = centerId;
                employee.DepartmentId = departmentId;
                employee.EmpStatusDesc = statusName;
                employee.IsAcademic = isAcademic;
                updated++;
            }
            await db.SaveChangesAsync(ct);
            return updated;
        }, ct);

    public Task<SourceMergeStats> ApplyProgramsAsync(IReadOnlyList<AcademicProgramSource> source,
        long runId, CancellationToken ct) => db.InTransactionAsync(async () =>
    {
        if (source.Select(x => x.ProgramCode).Distinct(StringComparer.Ordinal).Count() != source.Count)
            throw new InvalidDataException("DUPLICATE_PROGRAM_SOURCE_KEY");
        // This catalog has no side field and describes ZU academic programs only.
        // It enriches HR departments through their exact textual reg_code; facultyid
        // is an academic faculty code, NOT an HR center_id. Administrative units,
        // other sides, local activation flags and employee eligibility are untouched.
        bool imported = await db.UniversitySides.AnyAsync(x => x.SideCode == 320 && x.IsImported, ct);
        if (!imported) return new SourceMergeStats(source.Count, 0, 0, 0, 0);
        var departments = await db.Departments.IgnoreQueryFilters()
            .Where(x => x.SideCode == 320 && x.RegCode != null).ToListAsync(ct);
        if (source.Count == 0 && departments.Count > 0)
            throw new InvalidDataException("SUSPICIOUS_EMPTY_PROGRAMS");
        var byCode = source.ToDictionary(x => x.ProgramCode, StringComparer.Ordinal);
        int updated = 0, unchanged = 0;
        foreach (var department in departments)
        {
            if (!byCode.TryGetValue(department.RegCode!, out var program) ||
                string.IsNullOrWhiteSpace(program.NameEn)) continue;
            if (department.NameEn == program.NameEn) { unchanged++; continue; }
            department.NameEn = program.NameEn;
            updated++;
        }
        await db.SaveChangesAsync(ct);
        return new SourceMergeStats(source.Count, 0, updated, unchanged, 0);
    }, ct);

    public Task<SourceMergeStats> ApplyEmployeesAsync(IReadOnlyList<EmployeeSource> source,
        long runId, CancellationToken ct) => db.InTransactionAsync(async () =>
    {
        var sides = await LoadSidesAsync(source.Select(x => x.SideCode), ct);
        foreach (var row in source.Where(x => !string.IsNullOrWhiteSpace(x.SideNameAr)))
            sides[row.SideCode].NameAr = UniversityOrganizationRules.SideName(row.SideCode, row.SideNameAr);
        var existing = await db.Employees.ToListAsync(ct);
        if (source.Count == 0 && existing.Any(x => sides[x.SideCode].IsImported))
            throw new InvalidDataException("SUSPICIOUS_EMPTY_EMPLOYEES");
        var byKey = existing.ToDictionary(x => (x.SideCode, x.EmployeeCode));
        var centerIds = await db.Centers.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => new { x.SideCode, x.CenterCode }, x => x.Id, ct);
        var departments = await db.Departments.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => new { x.SideCode, x.DepartmentCode }, ct);
        string codes = await settings.GetAsync<string>("directory.academic_main_job_codes", ct);
        var academicCodes = ParseCodes(codes);
        int added = 0, updated = 0, unchanged = 0, missing = 0;
        var seen = new HashSet<(int, int)>();
        var statusChangedEmployeeIds = new List<long>();
        foreach (var row in source)
        {
            var key = (row.SideCode, row.EmployeeCode);
            if (!seen.Add(key)) throw new InvalidDataException("DUPLICATE_EMPLOYEE_SOURCE_KEY");
            if (!sides[row.SideCode].IsImported) continue;
            long? centerId = row.CenterCode is int centerCode &&
                centerIds.TryGetValue(new { row.SideCode, CenterCode = centerCode }, out long cid) ? cid : null;
            Department? department = row.DepartmentCode is int departmentCode &&
                departments.TryGetValue(new { row.SideCode, DepartmentCode = departmentCode }, out var found) ? found : null;
            bool centerMismatch = department is not null &&
                !UniversityOrganizationRules.DepartmentMatchesCenter(row.CenterCode, department.CenterCode);
            long? departmentId = centerMismatch ? null : department?.Id;
            bool isAcademic = academicCodes.Contains(row.MainJobCode ?? int.MinValue);
            bool unresolved = row.CenterCode is not null && centerId is null ||
                row.DepartmentCode is not null && departmentId is null;
            string entityKey = Key(row.SideCode, row.EmployeeCode);
            if (unresolved)
                await AddReviewIfAbsentAsync("Employee", entityKey,
                    SyncReviewReason.UnresolvedReference, runId,
                    JsonSerializer.Serialize(new { row.CenterCode, row.DepartmentCode,
                        DepartmentCenterCode = department?.CenterCode, CenterMismatch = centerMismatch }), ct);
            else await CloseReviewAsync("Employee", entityKey, SyncReviewReason.UnresolvedReference, ct);
            string hash = Hash(row);
            if (!byKey.TryGetValue(key, out var employee))
            {
                employee = new Employee
                {
                    SideCode = row.SideCode, EmployeeCode = row.EmployeeCode,
                    CreatedAt = clock.UtcNow
                };
                ApplyEmployeeFields(employee, row, centerId, departmentId,
                    isAcademic, hash);
                employee.LastSeenAt = clock.UtcNow;
                db.Employees.Add(employee);
                added++;
            }
            else
            {
                if (employee.SourceHash != hash || employee.CenterId != centerId ||
                    employee.DepartmentId != departmentId || employee.IsAcademic != isAcademic)
                {
                    if (employee.EmpStatus != row.EmpStatus)
                    {
                        statusChangedEmployeeIds.Add(employee.Id);
                        await AddReviewIfAbsentAsync("Employee", entityKey,
                            SyncReviewReason.StatusChanged, runId,
                            JsonSerializer.Serialize(new { previousStatus = employee.EmpStatus,
                                currentStatus = row.EmpStatus }), ct);
                    }
                    ApplyEmployeeFields(employee, row, centerId, departmentId,
                        isAcademic, hash);
                    updated++;
                }
                else unchanged++;
                employee.LastSeenAt = clock.UtcNow;
                if (employee.SourceMissing)
                {
                    employee.SourceMissing = false;
                    employee.SourceMissingSince = null;
                    await CloseReviewAsync("Employee", entityKey,
                        SyncReviewReason.MissingFromSource, ct);
                }
            }
        }
        foreach (var employee in existing.Where(x => sides[x.SideCode].IsImported &&
            !seen.Contains((x.SideCode, x.EmployeeCode))))
        {
            if (employee.SourceMissing) continue;
            employee.SourceMissing = true;
            employee.SourceMissingSince = clock.UtcNow;
            await AddReviewIfAbsentAsync("Employee", Key(employee.SideCode, employee.EmployeeCode),
                SyncReviewReason.MissingFromSource, runId, null, ct);
            missing++;
        }
        await db.SaveChangesAsync(ct);
        if (statusChangedEmployeeIds.Count > 0)
        {
            var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.EmployeeId.HasValue && statusChangedEmployeeIds.Contains(x.EmployeeId.Value))
                .Select(x => x.Id).ToListAsync(ct);
            foreach (long userId in users) userState.Invalidate(userId);
        }
        return new SourceMergeStats(source.Count, added, updated, unchanged, missing);
    }, ct);

    public Task<SourceMergeStats> ApplyEmailsAsync(IReadOnlyList<EmailSource> source,
        long runId, CancellationToken ct) => db.InTransactionAsync(async () =>
    {
        var sides = await LoadSidesAsync(source.Select(x => x.SideCode), ct);
        var employees = await db.Employees.ToListAsync(ct);
        if (source.Count == 0 && employees.Any(x => sides[x.SideCode].IsImported &&
            !string.IsNullOrWhiteSpace(x.SourceEmail)))
            throw new InvalidDataException("SUSPICIOUS_EMPTY_EMAILS");
        var byKey = employees.ToDictionary(x => (x.SideCode, x.EmployeeCode));
        int updated = 0, unchanged = 0;
        foreach (var group in source.GroupBy(x => (x.SideCode, x.EmployeeCode)))
        {
            if (!sides[group.Key.SideCode].IsImported) continue;
            if (group.Count() > 1)
                await AddReviewIfAbsentAsync("Employee", Key(group.Key.SideCode, group.Key.EmployeeCode),
                    SyncReviewReason.DuplicateSourceKey, runId, null, ct);
            if (!byKey.TryGetValue(group.Key, out var employee)) continue;
            string? email = group.Select(x => x.Email?.Trim().ToLowerInvariant())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (employee.SourceEmail == email) unchanged++;
            else { employee.SourceEmail = email; updated++; }
        }
        await db.SaveChangesAsync(ct);
        return new SourceMergeStats(source.Count, 0, updated, unchanged, 0);
    }, ct);

    public Task<int> DetectSharedEmailsAsync(long runId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var addresses = await db.Users.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.EmailOverride ?? x.Employee!.SourceEmail)
                .Where(x => x != null && x != "")
                .ToListAsync(ct);
            var shared = addresses.Select(x => x!.Trim().ToLowerInvariant())
                .Where(x => x.Length > 0)
                .GroupBy(x => x, StringComparer.Ordinal)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            int opened = 0;
            foreach (string email in shared)
            {
                bool exists = await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Email" &&
                    x.EntityKey == email && x.Reason == SyncReviewReason.SharedEmail &&
                    x.ResolvedAt == null, ct);
                if (exists) continue;
                db.SyncReviewItems.Add(new SyncReviewItem
                {
                    EntityType = "Email", EntityKey = email,
                    Reason = SyncReviewReason.SharedEmail, Origin = SyncReviewOrigin.Sync,
                    FirstSeenRunId = runId
                });
                opened++;
            }
            var old = await db.SyncReviewItems.Where(x => x.EntityType == "Email" &&
                x.Reason == SyncReviewReason.SharedEmail && x.ResolvedAt == null)
                .ToListAsync(ct);
            foreach (var item in old.Where(x => !shared.Contains(x.EntityKey)))
            {
                item.ResolvedAt = clock.UtcNow;
                item.Resolution = SyncReviewResolution.Ignore;
                item.Notes = "لم يعد البريد مشتركاً";
            }
            await db.SaveChangesAsync(ct);
            return opened;
        }, ct);

    private static void ApplyEmployeeFields(Employee target, EmployeeSource row,
        long? centerId, long? departmentId, bool isAcademic, string hash)
    {
        target.FirstName = row.FirstName;
        target.SecondName = row.SecondName;
        target.ThirdName = row.ThirdName;
        target.LastName = row.LastName;
        target.FullNameAr = row.FullNameAr;
        target.FullNameEn = row.FullNameEn;
        target.CenterCode = row.CenterCode;
        target.CenterId = centerId;
        target.DepartmentCode = row.DepartmentCode;
        target.DepartmentId = departmentId;
        target.MainJobCode = row.MainJobCode;
        target.MainJobDesc = row.MainJobDesc;
        target.SubJobCode = row.SubJobCode;
        target.SubJobDesc = row.SubJobDesc;
        target.EmpStatus = row.EmpStatus;
        target.EmpStatusDesc = row.EmpStatusDesc;
        target.ChkEmpStat = row.ChkEmpStat;
        target.StartWorkDate = row.StartWorkDate;
        target.StopWorkDate = row.StopWorkDate;
        target.IsAcademic = isAcademic;
        target.SearchText = ArabicSearchNormalizer.Normalize(
            $"{row.FullNameAr} {row.FullNameEn} {row.EmployeeCode}");
        target.SourceHash = hash;
    }

    private async Task<Dictionary<int, UniversitySide>> LoadSidesAsync(IEnumerable<int> sourceCodes,
        CancellationToken ct)
    {
        var sides = await db.UniversitySides.ToDictionaryAsync(x => x.SideCode, ct);
        foreach (int code in sourceCodes.Distinct())
        {
            if (sides.TryGetValue(code, out var existing))
            {
                existing.LastSeenAt = clock.UtcNow;
                continue;
            }
            var side = new UniversitySide { SideCode = code, NameAr = UniversityOrganizationRules.SideName(code),
                IsImported = false, LastSeenAt = clock.UtcNow };
            db.UniversitySides.Add(side);
            sides.Add(code, side);
        }
        return sides;
    }

    private async Task AddReviewIfAbsentAsync(string entityType, string entityKey,
        SyncReviewReason reason, long runId, string? details, CancellationToken ct)
    {
        bool exists = await db.SyncReviewItems.AnyAsync(x => x.EntityType == entityType &&
            x.EntityKey == entityKey && x.Reason == reason && x.ResolvedAt == null, ct);
        if (!exists) db.SyncReviewItems.Add(new SyncReviewItem
        {
            EntityType = entityType, EntityKey = entityKey, Reason = reason,
            Details = details, FirstSeenRunId = runId, Origin = SyncReviewOrigin.Sync
        });
    }

    private async Task CloseReviewAsync(string entityType, string entityKey,
        SyncReviewReason reason, CancellationToken ct)
    {
        var item = await db.SyncReviewItems.SingleOrDefaultAsync(x => x.EntityType == entityType &&
            x.EntityKey == entityKey && x.Reason == reason &&
            x.ResolvedAt == null, ct);
        if (item is null) return;
        item.ResolvedAt = clock.UtcNow;
        item.Resolution = SyncReviewResolution.Ignore;
        item.Notes = "عاد للظهور";
    }

    private static HashSet<int> ParseCodes(string raw) => raw.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(x => int.TryParse(x, out int code) ? (int?)code : null)
        .Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();

    private static string Key(int side, int code) => $"{side}:{code}";
}

public sealed record SourceMergeStats(int Fetched, int Added, int Updated, int Unchanged, int Missing);
