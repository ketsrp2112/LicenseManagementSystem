using LicenseManagement.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LicenseManagement.Api.Controllers
{
    /// <summary>
    /// ตรวจ SessionToken จาก header "X-Session-Token"
    /// [SessionAuthorize]                  = ล็อกอินแล้วทุก role
    /// [SessionAuthorize("Staff")]         = เฉพาะ Staff
    /// [SessionAuthorize("Staff","Trader")] = ระบุหลาย role
    /// role ดึงจากฐานข้อมูลตาม session เสมอ ไม่รับจาก Frontend
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SessionAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string[] _roles;

        public SessionAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;

            if (!http.Request.Headers.TryGetValue("X-Session-Token", out var tokenValues)
                || string.IsNullOrWhiteSpace(tokenValues.ToString()))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var token = tokenValues.ToString();
            var db = http.RequestServices.GetRequiredService<LicenseManagementContext>();

            var session = await db.UserSessions
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.SessionToken == token &&
                    x.ExpireDate > DateTime.Now);

            if (session?.User == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var user = session.User;

            var knownRole = user.Usertype == "Staff" || user.Usertype == "Trader";
            if (!knownRole || (_roles.Length > 0 && !_roles.Contains(user.Usertype)))
            {
                context.Result = new ForbidResult();
                return;
            }

            http.Items["CurrentUser"] = user;
            http.Items["CurrentSessionToken"] = token;

            await next();
        }
    }

    public static class HttpContextUserExtensions
    {
        /// ผู้ใช้ปัจจุบันจาก Session (ใช้ได้เฉพาะ action ที่มี [SessionAuthorize])
        public static UserAccount CurrentUser(this HttpContext http) =>
            (UserAccount)http.Items["CurrentUser"]!;

        public static bool IsStaff(this HttpContext http) =>
            http.CurrentUser().Usertype == "Staff";
    }
}