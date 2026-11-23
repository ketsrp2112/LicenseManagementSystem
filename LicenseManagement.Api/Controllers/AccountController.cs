using LicenseManagement.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LicenseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly LicenseManagementContext _context;

        public AccountController(LicenseManagementContext context)
        {
            _context = context;
        }

        private static readonly PasswordHasher<UserAccount> Hasher = new();

        private static readonly string[] DemoUsers = { "trader_demo", "officer_demo" };

        private static bool IsDemo(string? username) =>
            username != null && DemoUsers.Contains(username, StringComparer.OrdinalIgnoreCase);

        // ---------- Password helpers ----------

        // รหัสที่ผ่าน PasswordHasher (V3) จะขึ้นต้นด้วย "AQAAAA"
        private static bool LooksHashed(string stored) => stored.StartsWith("AQAAAA");

        /// ตรวจรหัสผ่าน ถ้าเป็นข้อมูลเก่าที่ยังเป็น plain text จะเทียบแบบปลอดภัย
        /// แล้วบอกให้ hash ใหม่ (migrate อัตโนมัติตอน login สำเร็จ)
        private static (bool ok, bool needsRehash) CheckPassword(UserAccount user, string password)
        {
            if (string.IsNullOrEmpty(user.Password) || string.IsNullOrEmpty(password))
                return (false, false);

            if (!LooksHashed(user.Password))
            {
                var a = Encoding.UTF8.GetBytes(user.Password);
                var b = Encoding.UTF8.GetBytes(password);
                var equal = CryptographicOperations.FixedTimeEquals(a, b);
                return (equal, equal);
            }

            var result = Hasher.VerifyHashedPassword(user, user.Password, password);
            return (
                result != PasswordVerificationResult.Failed,
                result == PasswordVerificationResult.SuccessRehashNeeded);
        }

        private static string NewSessionToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(); // 32 ตัวอักษร เท่าเดิม

        private static string? ValidateRegister(RegisterRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Fullname) || !Regex.IsMatch(r.Fullname, @"^[A-Za-zก-๙\s.'-]+$"))
                return "Please enter a valid full name.";
            if (string.IsNullOrWhiteSpace(r.Username))
                return "Please enter a username.";
            if (string.IsNullOrEmpty(r.Password) || !Regex.IsMatch(r.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$"))
                return "Password must be at least 8 characters with uppercase, lowercase and a number.";
            if (r.CitizenId == null || !Regex.IsMatch(r.CitizenId, @"^\d{13}$"))
                return "Citizen ID must be exactly 13 digits.";
            if (r.Identify == null || !Regex.IsMatch(r.Identify, @"^\d{13}$"))
                return "Identify must be exactly 13 digits.";
            if (string.IsNullOrWhiteSpace(r.Companyname))
                return "Please enter a company name.";
            if (r.Tel == null || !Regex.IsMatch(r.Tel, @"^0\d{8,11}$"))
                return "Telephone must be 9-11 digits and start with 0.";
            if (r.Email == null || !Regex.IsMatch(r.Email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return "Please enter a valid email address.";
            return null;
        }

        #region Register

        [HttpPost("register")] // POST /api/account/register
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var error = ValidateRegister(request);
            if (error != null)
                return BadRequest(new { Message = error });

            var username = request.Username!.Trim();
            var citizenId = request.CitizenId!.Trim();

            var exists = await _context.UserAccounts
                .AnyAsync(x => x.Username == username || x.Citizenid == citizenId);

            if (exists)
                return BadRequest(new { Message = "Username or Citizen ID already exists." });

            var model = new UserAccount
            {
                Username = username,
                Citizenid = citizenId,
                Identify = request.Identify!.Trim(),
                Fullname = request.Fullname!.Trim(),
                Usertype = "Trader",   // กำหนดที่ server เสมอ ไม่เชื่อ request.Usertype จากหน้าเว็บ
                Tel = request.Tel!.Trim(),
                Email = request.Email!.Trim(),
                Companyname = request.Companyname!.Trim(),
                Createdate = DateTime.Now
            };

            // hash หลังสร้าง model แล้ว (โค้ดเดิมอ้าง model ในตอนประกาศ model เอง จะ compile ไม่ผ่าน)
            model.Password = Hasher.HashPassword(model, request.Password!);

            try
            {
                _context.UserAccounts.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Register Success", UserId = model.Ida });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Unable to register. Please try again." });
            }
        }

        #endregion

        #region Login

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
                    return Unauthorized("Username or Password incorrect.");

                var user = await _context.UserAccounts
                    .FirstOrDefaultAsync(x => x.Username == request.Username.Trim());

                // ข้อความเดียวกันทุกกรณี ไม่บอกว่าผิดที่ username หรือ password
                if (user == null)
                    return Unauthorized("Username or Password incorrect.");

                var (ok, needsRehash) = CheckPassword(user, request.Password);
                if (!ok)
                    return Unauthorized("Username or Password incorrect.");

                // ย้ายรหัสเก่า (plain text) เป็น hash อัตโนมัติ
                if (needsRehash)
                    user.Password = Hasher.HashPassword(user, request.Password);

                // ลบ session ที่หมดอายุของ user นี้
                var expired = _context.UserSessions
                    .Where(s => s.UserId == user.Ida && s.ExpireDate <= DateTime.Now);
                _context.UserSessions.RemoveRange(expired);

                var sessionToken = NewSessionToken();

                _context.UserSessions.Add(new UserSession
                {
                    UserId = user.Ida,
                    SessionToken = sessionToken,
                    ExpireDate = DateTime.Now.AddHours(8)
                });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    sessionToken,
                    user.Ida,
                    user.Username,
                    user.Fullname,
                    user.Usertype,   // ใช้แสดงผลบนหน้าเว็บเท่านั้น สิทธิ์จริงตรวจที่ server จาก session
                    user.Tel,
                    user.Email,
                    user.Companyname
                });
            }
            catch (Exception)
            {
                // ไม่ส่ง ex.Message กลับไป
                return StatusCode(500, "Unable to sign in. Please try again.");
            }
        }

        #endregion

        #region Logout

        [HttpPost("logout")]
        [SessionAuthorize]
        public async Task<IActionResult> Logout()
        {
            var token = (string)HttpContext.Items["CurrentSessionToken"]!;

            var session = await _context.UserSessions
                .FirstOrDefaultAsync(x => x.SessionToken == token);

            if (session != null)
            {
                _context.UserSessions.Remove(session);
                await _context.SaveChangesAsync();
            }

            return Ok("Signed out.");
        }

        #endregion

        #region Get User By Id

        // Staff ดูได้ทุกคน / Trader ดูได้เฉพาะของตัวเอง
        [HttpGet("GetUserId")]
        [SessionAuthorize("Staff", "Trader")]
        public async Task<IActionResult> GetUser(int id)
        {
            var current = HttpContext.CurrentUser();

            if (!HttpContext.IsStaff() && current.Ida != id)
                return Forbid();

            var user = await _context.UserAccounts
                .Where(x => x.Ida == id)
                .Select(x => new   // ไม่ส่ง entity ตรง ๆ จะได้ไม่หลุดฟิลด์ Password
                {
                    x.Ida,
                    x.Username,
                    x.Fullname,
                    x.Citizenid,
                    x.Identify,
                    x.Usertype,
                    x.Tel,
                    x.Email,
                    x.Companyname
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        #endregion

        #region Change Password By Username (ใช้จากหน้า login)

        [HttpPut("ChangePasswordByUsername")] // PUT /api/account/ChangePasswordByUsername
        public async Task<IActionResult> ChangePasswordByUsername(ChangePasswordByUsernameRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.OldPassword) ||
                string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest("Please fill in all fields.");
            }

            if (IsDemo(request.Username))
                return BadRequest("Demo accounts can't be changed.");

            if (!Regex.IsMatch(request.NewPassword, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$"))
                return BadRequest("New password must be at least 8 characters with uppercase, lowercase and a number.");

            if (request.NewPassword == request.OldPassword)
                return BadRequest("New password must be different from the current password.");

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(x => x.Username == request.Username.Trim());

            // ข้อความเดียวกันทั้งกรณีไม่มี user และรหัสผิด
            if (user == null || !CheckPassword(user, request.OldPassword).ok)
                return Unauthorized("Username or current password is incorrect.");

            if (IsDemo(user.Username))
                return BadRequest("Demo accounts can't be changed.");

            user.Password = Hasher.HashPassword(user, request.NewPassword);

            // เปลี่ยนรหัสแล้ว ให้ session เดิมทั้งหมดใช้ไม่ได้
            var sessions = _context.UserSessions.Where(s => s.UserId == user.Ida);
            _context.UserSessions.RemoveRange(sessions);

            await _context.SaveChangesAsync();

            return Ok("Password changed.");
        }

        #endregion

        #region Validate Session

        [HttpGet("session")]
        [SessionAuthorize("Staff", "Trader")]
        public IActionResult ValidateSession()
        {
            var user = HttpContext.CurrentUser();

            return Ok(new
            {
                user.Ida,
                user.Username,
                user.Fullname,
                user.Usertype,
                user.Identify,
                user.Citizenid
            });
        }

        #endregion
    }

    #region DTO Classes
    public class ChangePasswordByUsernameRequest
    {
        public string Username { get; set; } = "";
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class RegisterRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? CitizenId { get; set; }
        public string? Identify { get; set; }
        public string? Fullname { get; set; }
        public string? Usertype { get; set; }   // รับมาแต่ไม่ใช้ (เซิร์ฟเวอร์กำหนดเอง)
        public string? Tel { get; set; }
        public string? Email { get; set; }
        public string? Companyname { get; set; }
    }


    #endregion
}