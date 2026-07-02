using EShooting.Application.Common.Interfaces;
using EShooting.Application.StaffMembers;
using EShooting.Web.Extensions;

namespace EShooting.Web.Auth;

/// <summary>
/// Resepsiya sessiyasında icazələri bazadan yeniləyir (profil dəyişəndə çıxış gözləmədən tətbiq olunsun).
/// </summary>
public sealed class ReceptionPermissionRefreshMiddleware(
    RequestDelegate next,
    ILogger<ReceptionPermissionRefreshMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITrainingCenterRepository repository)
    {
        if (context.User.IsReceptionStaff())
        {
            var staffId = context.User.GetStaffMemberId();
            if (staffId is Guid id)
            {
                try
                {
                    var member = await repository.GetStaffMemberByIdAsync(id, context.RequestAborted);
                    var profile = member?.AccessProfile;
                    if (member is not null
                        && !member.IsDeleted
                        && member.IsActive
                        && profile is not null
                        && !profile.IsDeleted
                        && profile.IsActive)
                    {
                        var session = ReceptionStaffSessionMapper.Map(member, profile);
                        await ReceptionStaffSignIn.SignInAsync(context, session);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Resepsiya icazələri yenilənmədi (staffId={StaffId}).", staffId);
                }
            }
        }

        await next(context);
    }
}
