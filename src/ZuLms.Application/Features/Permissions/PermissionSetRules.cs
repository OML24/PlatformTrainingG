using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Permissions;

public static class PermissionSetRules
{
    private static readonly HashSet<string> MemberOnlyKeys = new(StringComparer.Ordinal)
    {
        "participants.self_register", "attendance.enter_code", "evaluation.submit", "impact.submit",
        "certificates.view.own", "certificates.download.own", "procert.submit", "procert.withdraw",
        "procert.download.own"
    };

    // Stable ownership snapshot of the complete permissions.json catalog. Role grants are
    // mutable at runtime; they must never redefine which workspace owns an existing key.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<RoleCode>> CatalogOwners = BuildOwners();

    public static HashSet<string> ForMode(RoleCode mode, IEnumerable<string> rolePermissions,
        IEnumerable<(string Key, OverrideMode Mode)> overrides, AccountType accountType)
    {
        var keys = rolePermissions.Where(key => IsOwnedByModeOrShared(mode, key))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in overrides.Where(x => IsOwnedByModeOrShared(mode, x.Key)))
        {
            if (item.Mode == OverrideMode.Allow) keys.Add(item.Key);
            else keys.Remove(item.Key);
        }
        ApplyAccountType(accountType, keys);
        ApplyMode(mode, keys);
        return keys;
    }

    public static void ApplyMode(RoleCode mode, HashSet<string> keys)
    {
        keys.RemoveWhere(key => !IsOwnedByModeOrShared(mode, key));
    }

    public static bool IsOwnedByModeOrShared(RoleCode mode, string key)
    {
        if (key.StartsWith("page.shared.", StringComparison.Ordinal) || key == "profile.edit.own") return true;
        if (key.StartsWith("page.member.", StringComparison.Ordinal) || MemberOnlyKeys.Contains(key))
            return mode == RoleCode.Member;
        if (CatalogOwners.TryGetValue(key, out var owners)) return owners.Contains(mode);
        // New/unclassified keys can be granted by the active role or an active-mode Allow,
        // but cannot use a key already owned by another mode.
        return true;
    }

    public static bool IsKnownCatalogKey(string key) => CatalogOwners.ContainsKey(key);

    private static IReadOnlyDictionary<string, IReadOnlySet<RoleCode>> BuildOwners()
    {
        var result = new Dictionary<string, HashSet<RoleCode>>(StringComparer.Ordinal);
        void Add(RoleCode role, string keys)
        {
            foreach (string key in keys.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!result.TryGetValue(key, out var owners)) result[key] = owners = [];
                owners.Add(role);
            }
        }
        Add(RoleCode.Admin, """
            page.admin.dashboard page.admin.users page.admin.credentials page.admin.courses page.admin.settings
            page.admin.password_policy page.admin.hints page.admin.roles page.admin.centers page.admin.sync
            page.admin.cert_template page.admin.audit page.admin.live_feed page.admin.stats page.admin.needs
            page.vault page.quality.dashboard page.quality.proposals page.quality.cancellations page.quality.courses
            page.quality.tracks page.quality.external_instructors page.quality.needs page.quality.evaluations
            page.quality.eval_form page.quality.certificates page.instructor.courses page.instructor.course
            page.instructor.lectures page.instructor.attendance page.instructor.participants page.instructor.exam
            page.instructor.impact page.instructor.certificates page.instructor.evaluation page.member.dashboard
            page.member.available page.member.enrolled page.member.certificates page.member.impact page.member.teaching
            page.member.pro_certs page.quality.pro_certs page.quality.pro_cert_stats page.shared.directory page.shared.profile
            user.create user.edit user.deactivate user.restore user.roles.assign user.permissions.override
            credentials.reveal credentials.change credentials.send_initial credentials.set_shared_initial settings.edit
            password_policy.edit hints.manage roles.edit_permissions centers.edit sync.run sync.review.resolve
            cert_template.upload certificates.revoke_manual certificates.issue_pending audit.export vault.upload
            vault.download vault.deactivate tracks.manage course.create_direct course.create_historical course.edit
            course.deactivate course.restore course.archive course.unarchive course.reopen course.force_status
            proposal.decide cancellation.decide need.review extinstr.select_existing extinstr.request_account
            extinstr.create_account participants.view.all participants.add participants.remove participants.self_register
            lecture.manage material.manage course.finish attendance.activate_code attendance.edit_manual
            attendance.enter_code exam.manage exam.requirement.edit exam.results.disable impact.submit impact.review
            evaluation.submit evalform.edit evaluation.extend_period evaluation.results.all evaluation.results.course
            certificates.view.all certificates.view.own certificates.download.own stats.university stats.faculty
            stats.course stats.member procert.submit procert.withdraw procert.download.own procert.review procert.edit
            procert.download.any procert.deactivate procert.categories.manage procert.stats profile.edit.own
            """);
        Add(RoleCode.Quality, """
            page.vault page.quality.dashboard page.quality.proposals page.quality.cancellations page.quality.courses
            page.quality.tracks page.quality.external_instructors page.quality.needs page.quality.evaluations
            page.quality.eval_form page.quality.certificates page.instructor.courses page.instructor.course
            page.instructor.lectures page.instructor.attendance page.instructor.participants page.instructor.exam
            page.instructor.impact page.instructor.certificates page.instructor.evaluation page.member.dashboard
            page.member.available page.member.enrolled page.member.certificates page.member.impact page.member.teaching
            page.member.pro_certs page.quality.pro_certs page.quality.pro_cert_stats page.shared.directory page.shared.profile
            certificates.issue_pending vault.upload vault.download tracks.manage course.create_direct course.create_historical
            course.edit course.cancel_direct proposal.decide cancellation.decide need.review extinstr.select_existing
            extinstr.request_account participants.view.all participants.add participants.remove participants.self_register
            lecture.manage material.manage course.finish attendance.activate_code attendance.edit_manual attendance.enter_code
            exam.manage exam.requirement.edit impact.submit impact.review evaluation.submit evalform.edit
            evaluation.extend_period evaluation.results.all evaluation.results.course certificates.view.all
            certificates.view.own certificates.download.own stats.university stats.faculty stats.course stats.member
            procert.submit procert.withdraw procert.download.own procert.review procert.edit procert.download.any
            procert.categories.manage procert.stats profile.edit.own
            """);
        Add(RoleCode.Dean, """
            page.dean.dashboard page.dean.courses page.dean.propose page.dean.members page.dean.needs page.dean.stats
            page.instructor.impact page.instructor.certificates page.member.dashboard page.member.available
            page.member.enrolled page.member.certificates page.member.impact page.member.teaching page.member.pro_certs
            page.shared.directory page.shared.profile course.propose proposal.resubmit cancellation.request need.submit
            need.resubmit participants.view.faculty participants.self_register attendance.enter_code impact.submit impact.review
            evaluation.submit certificates.view.faculty certificates.view.own certificates.download.own stats.faculty
            procert.submit procert.withdraw procert.download.own profile.edit.own faculty.members.view
            """);
        // Center/department managers act like a dean over their (non-faculty) center.
        Add(RoleCode.CenterManager, """
            page.dean.dashboard page.dean.courses page.dean.propose page.dean.members page.dean.needs page.dean.stats
            page.instructor.impact page.instructor.certificates page.member.dashboard page.member.available
            page.member.enrolled page.member.certificates page.member.impact page.member.teaching page.member.pro_certs
            page.shared.directory page.shared.profile course.propose proposal.resubmit cancellation.request need.submit
            need.resubmit participants.view.faculty participants.self_register attendance.enter_code impact.submit impact.review
            evaluation.submit certificates.view.faculty certificates.view.own certificates.download.own stats.faculty
            procert.submit procert.withdraw procert.download.own profile.edit.own faculty.members.view
            """);
        Add(RoleCode.Instructor, """
            page.instructor.courses page.instructor.course page.instructor.lectures page.instructor.attendance
            page.instructor.participants page.instructor.exam page.instructor.evaluation page.member.teaching
            page.shared.directory page.shared.profile lecture.manage material.manage course.finish
            attendance.activate_code attendance.edit_manual exam.manage evaluation.results.course stats.course
            """);
        Add(RoleCode.ExternalInstructor, """
            page.instructor.courses page.instructor.course page.instructor.lectures page.instructor.attendance
            page.instructor.participants page.instructor.exam page.instructor.evaluation lecture.manage material.manage
            course.finish attendance.activate_code attendance.edit_manual exam.manage evaluation.results.course stats.course
            """);
        Add(RoleCode.Member, """
            page.member.dashboard page.member.available page.member.enrolled page.member.certificates page.member.impact
            page.member.teaching page.member.pro_certs page.shared.directory page.shared.profile
            participants.self_register attendance.enter_code impact.submit evaluation.submit certificates.view.own
            certificates.download.own procert.submit procert.withdraw procert.download.own profile.edit.own
            """);
        return result.ToDictionary(x => x.Key, x => (IReadOnlySet<RoleCode>)x.Value, StringComparer.Ordinal);
    }

    public static void ApplyAccountType(AccountType accountType, HashSet<string> keys)
    {
        if (accountType != AccountType.ExternalInstructor) return;
        keys.RemoveWhere(key => key.StartsWith("page.member.", StringComparison.Ordinal) ||
            key.StartsWith("page.shared.", StringComparison.Ordinal) || MemberOnlyKeys.Contains(key));
    }
}
