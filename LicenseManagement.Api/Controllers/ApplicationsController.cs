using LicenseManagement.Api.Models;
using LicenseManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Text.RegularExpressions;

namespace LicenseManagement.Api.Controllers
{
    //บอก .NET ว่านี่คือ API Controller
    //ได้รับคุณสมบัติ Validate Model อัตโนมัติ Binding Body อัตโนมัติ Response เป็น JSON
    [ApiController]

    //กำหนด Route ของ Controller
    [Route("api/[controller]")]

    //สร้าง Controller สืบทอดจาก ControllerBase ซึ่งใช้สำหรับ Web API
    public class ApplicationsController : ControllerBase
    {
        private readonly LicenseManagementContext _context; //ตัวแปรสำหรับติดต่อ Database
        private const long MaxUploadSize = 10 * 1024 * 1024;   // 10 MB ต่อไฟล์
        private const int MaxUploadFiles = 10;

        private const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";
        private const string TelPattern = @"^0\d{8,11}$";

        public ApplicationsController(LicenseManagementContext context) //ASP.NET Core จะส่ง DbContext เข้ามาให้อัตโนมัติ
        {
            _context = context;
        }

        private static string GetStatusName(int status)
        {
            return status switch
            {
                1 => "Draft",
                2 => "Submitted",
                3 => "Officer Review",
                4 => "Revision Required",
                5 => "Applicant Edited",
                6 => "Applicant Cancel",
                7 => "Officer Rejected",
                8 => "Approved",
                9 => "License Cancel",
                _ => "Unknown"
            };
        }

        #region check authentication

        // ผู้ใช้ปัจจุบันมาจาก Session ในฐานข้อมูลเท่านั้น ไม่รับ identity / usertype จาก Frontend
        private async Task<UserAccount?> GetCurrentUser()
        {
            var token = Request.Headers["X-Session-Token"].FirstOrDefault()?.Trim();

            if (string.IsNullOrWhiteSpace(token))
                return null;

            var session = await _context.UserSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.SessionToken == token &&
                    x.ExpireDate > DateTime.Now);

            if (session == null)
                return null;

            return await _context.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Ida == session.UserId);
        }

        private static bool IsTrader(UserAccount user) =>
            string.Equals(user.Usertype, "Trader", StringComparison.OrdinalIgnoreCase);

        private static bool IsStaff(UserAccount user) =>
            string.Equals(user.Usertype, "Staff", StringComparison.OrdinalIgnoreCase);

        // ข้อมูลที่ผู้ใช้เข้าถึงได้: Trader เฉพาะของตัวเอง, Staff เฉพาะคำขอที่ส่งแล้ว
        private IQueryable<Application> GetVisibleApplications(UserAccount user, bool noTracking = false)
        {
            IQueryable<Application> query = _context.Applications;
            if (noTracking) query = query.AsNoTracking();

            if (IsTrader(user))
            {
                return query.Where(x =>
                    x.Identify == user.Identify &&
                    x.Citizenid == user.Citizenid);
            }

            if (IsStaff(user))
                return query.Where(x => x.Datesubmission != null);

            return query.Where(x => false);
        }

        #endregion


        #region 1. ดึงรายการมาแสดง และ ค้นหารายการคำขอ

        [HttpGet("GetApplications")]
        public async Task<IActionResult> GetApplications(
            int page = 1,
            int pageSize = 20,
            string? keyword = null,
            int? statusId = null,
            int? type = null,
            DateTime? submissionFrom = null,
            DateTime? submissionTo = null,
            DateTime? approvalFrom = null,
            DateTime? approvalTo = null,
            string? sortBy = null,
            string? sortDir = null,
            string? card = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // =====================================
            // 1. USER FILTER (จาก Session เท่านั้น)
            // =====================================
            var user = await GetCurrentUser();

            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            IQueryable<Application> query = GetVisibleApplications(user, noTracking: true);

            // card (การ์ดสรุป) เป็นฟีเจอร์ของ Staff เท่านั้น
            if (!IsStaff(user))
                card = null;

            // =====================================
            // 2. SEARCH
            // =====================================

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                //StartsWith กับคอลัมน์ที่คนมักค้นด้วยรหัส เช่น TRID และ License Number
                query = query.Where(x =>
                    (x.Trid != null && x.Trid.StartsWith(keyword)) ||
                    (x.Companyname != null &&
                     x.Companyname.Contains(keyword)) ||
                    (x.Licensenumber != null &&
                     x.Licensenumber.StartsWith(keyword)) ||
                    (x.Citizenname != null &&
                     x.Citizenname.Contains(keyword)));
            }

            // =====================================
            // 3. FILTER
            // =====================================

            if (statusId.HasValue)
            {
                query = query.Where(x =>
                    x.Statusid == statusId.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(x =>
                    x.Type == type.Value);
            }

            if (submissionFrom.HasValue)
            {
                var from = submissionFrom.Value.Date;

                query = query.Where(x =>
                    x.Datesubmission >= from);
            }

            if (submissionTo.HasValue)
            {
                var to = submissionTo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.Datesubmission < to);
            }

            if (approvalFrom.HasValue)
            {
                var from = approvalFrom.Value.Date;

                query = query.Where(x =>
                    x.Dateappv >= from);
            }

            if (approvalTo.HasValue)
            {
                var to = approvalTo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.Dateappv < to);
            }

            // =====================================
            // 4. SUMMARY CARD FILTER (Staff เท่านั้น)
            // summary / typeCounts ย้ายไป GetApplicationStats แล้ว
            // =====================================

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            if (card == "pending")
            {
                query = query.Where(x =>
                    x.Statusid == 2 ||
                    x.Statusid == 3 ||
                    x.Statusid == 5);
            }
            else if (card == "waiting")
            {
                query = query.Where(x => x.Statusid == 4);
            }
            else if (card == "approved")
            {
                query = query.Where(x =>
                    x.Statusid == 8 &&
                    x.Dateappv >= monthStart &&
                    x.Dateappv < nextMonth);
            }
            else if (card == "rejected")
            {
                query = query.Where(x =>
                    x.Statusid == 6 ||
                    x.Statusid == 7 ||
                    x.Statusid == 9);
            }

            // =====================================
            // 5. SORT
            // =====================================

            bool descending = string.Equals(
                sortDir,
                "desc",
                StringComparison.OrdinalIgnoreCase);

            switch (sortBy?.ToLowerInvariant())
            {
                case "trid":
                    query = descending
                        ? query.OrderByDescending(x => x.Trid)
                        : query.OrderBy(x => x.Trid);
                    break;

                case "typenm":
                    query = descending
                        ? query.OrderByDescending(x => x.Typenm)
                        : query.OrderBy(x => x.Typenm);
                    break;

                case "companyname":
                    query = descending
                        ? query.OrderByDescending(x => x.Companyname)
                        : query.OrderBy(x => x.Companyname);
                    break;

                case "statusname":
                    query = descending
                        ? query.OrderByDescending(x => x.Statusname)
                        : query.OrderBy(x => x.Statusname);
                    break;

                case "submitted":
                case "datesubmission":
                    query = descending
                        ? query.OrderByDescending(x => x.Datesubmission)
                        : query.OrderBy(x => x.Datesubmission);
                    break;

                case "aging":
                    query = descending
                        ? query.OrderByDescending(x => x.Datesubmission)
                        : query.OrderBy(x => x.Datesubmission);
                    break;

                case "dateappv":
                    query = descending
                        ? query.OrderByDescending(x => x.Dateappv)
                        : query.OrderBy(x => x.Dateappv);
                    break;

                case "licensenumber":
                    query = descending
                        ? query.OrderByDescending(x => x.Licensenumber)
                        : query.OrderBy(x => x.Licensenumber);
                    break;

                default:
                    query = query
                        .OrderBy(x => x.Statusid)
                        .ThenByDescending(x => x.Datesubmission)
                        .ThenByDescending(x => x.Ida); // ให้ลำดับคงที่ ป้องกันข้อมูลซ้ำข้ามหน้า
                    break;
            }

            // =====================================
            // 6. PAGINATION
            // =====================================

            var totalRecords = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(
                totalRecords / (double)pageSize);

            // ป้องกันการขอหน้าที่เกินจำนวนหน้าจริง
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    ida = x.Ida,
                    trid = x.Trid,
                    typenm = x.Typenm,
                    companyname = x.Companyname,
                    statusid = x.Statusid,
                    statusname = x.Statusname,
                    datesubmission = x.Datesubmission,
                    licensenumber = x.Licensenumber,
                    licenseStatus = x.LicenseStatus,
                    dateappv = x.Dateappv,
                    remark = x.Remark
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalRecords,
                totalPages,
                data
            });
        }


        // คืน summary + typeCounts เฉพาะ Staff (Trader เรียกไม่ได้)
        [HttpGet("GetApplicationStats")]
        public async Task<IActionResult> GetApplicationStats()
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsStaff(user)) return Forbid();

            var query = GetVisibleApplications(user, noTracking: true);

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var byStatus = await query
                .GroupBy(x => new { x.Statusid, x.Type })
                .Select(g => new { g.Key.Statusid, g.Key.Type, Count = g.Count() })
                .ToListAsync();

            var approvedThisMonth = await query.CountAsync(x =>
                x.Statusid == 8 && x.Dateappv >= monthStart && x.Dateappv < nextMonth);

            var summary = new
            {
                pending = byStatus.Where(x => x.Statusid is 2 or 3 or 5).Sum(x => x.Count),
                waitingTrader = byStatus.Where(x => x.Statusid == 4).Sum(x => x.Count),
                approved = approvedThisMonth,
                rejected = byStatus.Where(x => x.Statusid is 6 or 7 or 9).Sum(x => x.Count)
            };

            var typeCounts = byStatus
                .GroupBy(x => x.Type)
                .Select(g => new { type = g.Key, count = g.Sum(x => x.Count) });

            return Ok(new { summary, typeCounts });
        }

        //ดึงรายการมาแสดง

        [HttpGet("GetDataAll")]
        public async Task<IActionResult> GetDataAll()
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var result = await GetVisibleApplications(user)
                .OrderByDescending(x => x.Dateupdate)
                .ToListAsync();

            return Ok(result);
        }


        [HttpGet("Search")]
        public async Task<IActionResult> Search(string? keyword)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            IQueryable<Application> query = GetVisibleApplications(user);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x =>
                    (x.Trid != null && x.Trid.Contains(keyword)) ||
                    (x.Companyname != null && x.Companyname.Contains(keyword)) ||
                    (x.Statusname != null && x.Statusname.Contains(keyword)) ||
                    (x.Licensenumber != null && x.Licensenumber.Contains(keyword)));
            }

            var result = await query
                .OrderByDescending(x => x.Datesubmission)
                .ToListAsync();

            return Ok(result);
        }

        #endregion

        #region 2. ดูรายละเอียด

        [HttpGet("Detail/{trid}")]
        public async Task<IActionResult> GetDetail(string trid)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var app = await GetVisibleApplications(user, noTracking: true)
                .FirstOrDefaultAsync(x => x.Trid == trid);

            if (app == null) return NotFound("Application not found");

            var products = await _context.ApplicationProducts
                .AsNoTracking()
                .Where(x => x.TrId == trid && x.Active == 1)
                .OrderBy(x => x.Rid)
                .Select(x => new ProductDto
                {
                    Ida = x.Ida,
                    Rid = x.Rid,
                    Newcode = x.Newcode,
                    Productname = x.Productname,
                    Modelname = x.Modelname,
                    Description = x.Description
                }).ToListAsync();

            var history = await _context.ApplicationStatusHistories
                .AsNoTracking()
                .Where(x => x.Trid == trid)
                .OrderByDescending(x => x.Dateupdate)
                .Select(x => new { x.Statusid, x.Statusname, x.Nameupdate, x.Remark, x.Dateupdate })
                .ToListAsync();

            var files = await _context.ApplicationFiles
                .AsNoTracking()
                .Where(x => x.TrId == trid)
                .Select(x => new
                {
                    id = x.Id,
                    fileName = x.FileName,
                    filePath = "/api/applications/file-download/" + x.Id,
                    downloadUrl = "/api/applications/file-download/" + x.Id
                })
                .ToListAsync();

            return Ok(new { Application = app, Products = products, History = history, Files = files });
        }



        [HttpGet("LicenseDetail/{licenseNumber}")]
        public async Task<IActionResult> GetLicenseDetail(string licenseNumber)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var app = await GetVisibleApplications(user, noTracking: true)
                .FirstOrDefaultAsync(x => x.Licensenumber == licenseNumber && x.Statusid == 8 &&
                    (x.LicenseStatus == null || x.LicenseStatus == "Active"));

            if (app == null) return NotFound(new { Message = "Active license not found." });

            var products = await _context.ApplicationProducts.AsNoTracking()
                .Where(x => x.TrId == app.Trid && x.Active == 1)
                .OrderBy(x => x.Rid)
                .Select(x => new ProductDto { Ida = x.Ida, Rid = x.Rid, Productname = x.Productname, Modelname = x.Modelname, Description = x.Description })
                .ToListAsync();

            var files = await _context.ApplicationFiles.AsNoTracking()
                .Where(x => x.TrId == app.Trid)
                .Select(x => new { id = x.Id, fileName = x.FileName, filePath = "/api/applications/file-download/" + x.Id, downloadUrl = "/api/applications/file-download/" + x.Id })
                .ToListAsync();

            return Ok(new { licenseNumber = app.Licensenumber, licenseType = app.Typenm, dateappv = app.Dateappv, dateexp = app.Dateexp, products, files });
        }


        [HttpGet("files/{trid}")]
        public async Task<IActionResult> GetFiles(string trid)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var canAccess = await GetVisibleApplications(user, noTracking: true)
                .AnyAsync(x => x.Trid == trid);
            if (!canAccess) return NotFound("Application not found.");

            var files = await _context.ApplicationFiles.AsNoTracking()
                .Where(x => x.TrId == trid)
                .Select(x => new
                {
                    id = x.Id,
                    fileName = x.FileName,
                    filePath = "/api/applications/file-download/" + x.Id,
                    downloadUrl = "/api/applications/file-download/" + x.Id
                })
                .ToListAsync();
            return Ok(files);
        }

        // ดาวน์โหลดไฟล์ผ่าน API ที่ตรวจ Session และสิทธิ์ของคำขอก่อนเสมอ
        [HttpGet("file-download/{fileId:int}")]
        public async Task<IActionResult> DownloadFile(int fileId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var file = await _context.ApplicationFiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == fileId);
            if (file == null) return NotFound("File not found.");

            var canAccess = await GetVisibleApplications(user, noTracking: true)
                .AnyAsync(x => x.Trid == file.TrId);
            if (!canAccess) return NotFound("File not found.");

            var storedName = Path.GetFileName(file.FilePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(storedName))
                return NotFound("File not found.");

            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            var fullPath = Path.Combine(uploadFolder, storedName);
            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found.");

            var extension = Path.GetExtension(storedName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullPath, contentType);
        }


        #region เลือก License สำหรับ Amendment / Renew / Cancel

        [HttpGet("EligibleLicenses")]
        public async Task<IActionResult> GetEligibleLicenses(int type)
        {
            var trader = await GetCurrentUser();
            if (trader == null) return Unauthorized();
            if (!IsTrader(trader)) return Forbid();

            if (type != 2 && type != 3 && type != 4)
                return BadRequest(new { Message = "Invalid application type." });

            var today = DateTime.Today;
            var renewLimit = today.AddMonths(3);

            var query = _context.Applications.AsNoTracking().Where(x =>
                x.Citizenid == trader.Citizenid &&
                x.Identify == trader.Identify &&
                x.Statusid == 8 && x.Licensenumber != null && x.Licensenumber != "" &&
                (x.LicenseStatus == null || x.LicenseStatus == "Active"));

            if (type == 3)
            {
                query = query.Where(x => x.Dateexp != null && x.Dateexp >= today && x.Dateexp <= renewLimit);
            }

            query = query.Where(x => !_context.Applications.Any(p =>
                p.ReferenceLicenseNumber == x.Licensenumber &&
                p.Citizenid == x.Citizenid &&
                (p.Type == 2 || p.Type == 3 || p.Type == 4) &&
                p.Statusid >= 1 && p.Statusid <= 5));

            var licenses = await query.OrderBy(x => x.Dateexp)
                .Select(x => new { licensenumber = x.Licensenumber, dateexp = x.Dateexp, companyname = x.Companyname, trid = x.Trid })
                .ToListAsync();

            return Ok(new { count = licenses.Count, hasEligibleLicense = licenses.Any(), licenses });
        }

        #endregion



        #endregion

        #region 3. เพิ่มคำขอ

        [HttpPost]
        public async Task<IActionResult> Create(ApplicationCreateRequest request)
        {
            if (request.Application == null)
                return BadRequest(new { Message = "Invalid application data." });

            var trader = await GetCurrentUser();
            if (trader == null) return Unauthorized();
            if (!IsTrader(trader)) return Forbid();

            var type = request.Application.Type;

            if (type == null || type < 1 || type > 4)
                return BadRequest(new { Message = "Invalid application type." });

            // ใช้ข้อมูลเจ้าของคำขอจาก Session เท่านั้น ไม่รับ identity จาก Frontend
            var appRequest = request.Application;

            Application? referenceApp = null;
            List<ProductDto> productsToCreate = new();

            // =====================================
            // 1. ตรวจสอบ License ต้นทาง
            // =====================================

            if (type != 1)
            {
                var referenceNumber = appRequest.ReferenceLicenseNumber;

                if (string.IsNullOrWhiteSpace(referenceNumber))
                {
                    return BadRequest(new
                    {
                        Message = "Please select Reference License Number."
                    });
                }

                referenceApp = await _context.Applications
                    .FirstOrDefaultAsync(x =>
                        x.Licensenumber == referenceNumber &&
                        x.Identify == trader.Identify &&
                        x.Citizenid == trader.Citizenid &&
                        x.Statusid == 8 &&
                        (x.LicenseStatus == null ||
                         x.LicenseStatus == "Active"));

                if (referenceApp == null)
                {
                    return BadRequest(new
                    {
                        Message = "License not found or no longer active."
                    });
                }

                // Renew: ต้องอยู่ภายใน 3 เดือนก่อนหมดอายุ
                if (type == 3)
                {
                    var today = DateTime.Today;
                    var renewLimit = today.AddMonths(3);

                    if (referenceApp.Dateexp == null ||
                        referenceApp.Dateexp.Value.Date < today ||
                        referenceApp.Dateexp.Value.Date > renewLimit)
                    {
                        return BadRequest(new
                        {
                            Message = "This license is not eligible for renewal."
                        });
                    }
                }
            }


            // =====================================
            // 2. เตรียมข้อมูลสินค้า
            // =====================================

            if (type == 1 || type == 2)
            {
                // New และ Amendment รับ Products จากหน้าเว็บ

                if (request.Products == null ||
                    !request.Products.Any())
                {
                    return BadRequest(new
                    {
                        Message = "Please add at least one product."
                    });
                }

                // ตรวจสอบข้อมูลสินค้า
                if (request.Products.Any(p =>
                    string.IsNullOrWhiteSpace(p.Productname) ||
                    string.IsNullOrWhiteSpace(p.Modelname) ||
                    string.IsNullOrWhiteSpace(p.Description)))
                {
                    return BadRequest(new
                    {
                        Message = "Product information is incomplete."
                    });
                }

                productsToCreate = request.Products
                    .Select(p => new ProductDto
                    {
                        Productname = p.Productname,
                        Modelname = p.Modelname,
                        Description = p.Description
                    })
                    .ToList();
            }
            else
            {
                // Renew และ Cancel
                // ไม่รับ Products จาก Client
                // ให้ดึงจาก License เดิมเท่านั้น

                var oldProducts = await _context.ApplicationProducts
                    .AsNoTracking()
                    .Where(x =>
                        x.TrId == referenceApp!.Trid &&
                        x.Active == 1)
                    .OrderBy(x => x.Rid)
                    .ToListAsync();

                if (!oldProducts.Any())
                {
                    return BadRequest(new
                    {
                        Message = "Original license has no products."
                    });
                }

                productsToCreate = oldProducts
                    .Select(p => new ProductDto
                    {
                        Productname = p.Productname,
                        Modelname = p.Modelname,
                        Description = p.Description
                    })
                    .ToList();
            }


            // =====================================
            // 3. สร้าง Application ใหม่
            // =====================================

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable
                );


            try
            {
                string trid = await GenerateNumber(1);

                // สร้าง Entity ใหม่และรับเฉพาะค่าที่จำเป็นจาก Request
                // ป้องกัน Client ส่ง Status / License / Owner / Date มาปลอมแปลง
                var app = new Application
                {
                    Type = type,
                    Remark = appRequest.Remark,
                    ReferenceLicenseNumber = type == 1 ? null : referenceApp!.Licensenumber
                };

                app.Ida = 0;
                app.Trid = trid;

                // ข้อมูลเจ้าของคำขอ ใช้จากบัญชีในฐานข้อมูล
                app.Identify = trader.Identify;
                app.Citizenid = trader.Citizenid;
                app.Citizenname = trader.Fullname;
                app.Companyname = trader.Companyname;

                app.Email = trader.Email;
                app.Tel = trader.Tel;

                // สถานะคำขอเริ่มต้น
                app.Statusid = 1;
                app.Statusname = GetStatusName(1);

                app.Dateupdate = DateTime.Now;
                app.Datesubmission = null;

                app.VersionNo = 1;

                // ยังไม่มี License ใหม่จนกว่าเจ้าหน้าที่อนุมัติ
                app.Licensenumber = null;
                app.Dateappv = null;
                app.Dateexp = null;

                app.LicenseStatus = null;
                app.LicenseStatusDate = null;

                // เก็บเลข License ต้นทาง
                app.ReferenceLicenseNumber =
                    type == 1
                        ? null
                        : referenceApp!.Licensenumber;

                // ชื่อประเภทคำขอ
                app.Typenm = type switch
                {
                    1 => "New License",
                    2 => "Amendment",
                    3 => "Renew",
                    4 => "Cancel",
                    _ => "Unknown"
                };

                _context.Applications.Add(app);

                // =====================================
                // 4. เพิ่มสินค้าในคำขอใหม่
                // =====================================

                int ridCounter = 1;

                foreach (var product in productsToCreate)
                {
                    var newProduct = new ApplicationProduct
                    {
                        Ida = 0,
                        TrId = trid,

                        Productname = product.Productname,
                        Modelname = product.Modelname,
                        Description = product.Description,

                        Statusproduct = type == 2 ? "edit" : "add",

                        Rid = ridCounter,

                        Dateinsert = DateTime.Now,
                        Dateupdate = DateTime.Now,

                        Active = 1
                    };

                    _context.ApplicationProducts.Add(newProduct);

                    ridCounter++;
                }

                await _context.SaveChangesAsync();

                // บันทึกประวัติ
                await History(trid, app, trader);

                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Create Success",
                    Trid = trid,
                    Type = app.Type,
                    ReferenceLicenseNumber = app.ReferenceLicenseNumber
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                // ไม่ส่ง ex.Message กลับไปหา Client (อาจเผยชื่อตาราง/คอลัมน์)
                return StatusCode(500, new
                {
                    Message = "Create Application Failed."
                });
            }
        }


        [HttpPost("upload/{trid}")]
        public async Task<IActionResult> UploadFile(
            string trid,
            [FromForm] List<IFormFile> files,
            [FromForm] List<string>? names)          // ชื่อที่ผู้ใช้ตั้ง เรียงตามลำดับเดียวกับ files (ไม่ส่งมาก็ได้)
        {
            var trader = await GetCurrentUser();
            if (trader == null) return Unauthorized();
            if (!IsTrader(trader)) return Forbid();

            var app = await _context.Applications
                .FirstOrDefaultAsync(x => x.Trid == trid &&
                    x.Identify == trader.Identify && x.Citizenid == trader.Citizenid);

            if (app == null)
                return NotFound("Application not found.");

            // แนบไฟล์ได้เมื่อผู้ใช้ยังแก้ไขได้ (Draft / Revision / Edited)
            if (app.Statusid != 1 && app.Statusid != 4 && app.Statusid != 5)
                return BadRequest("Cannot upload files in the current status.");

            if (files == null || files.Count == 0)
                return BadRequest("No file uploaded.");

            if (files.Count > MaxUploadFiles)
                return BadRequest($"You can upload up to {MaxUploadFiles} files at a time.");

            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png" };

            // ตรวจทุกไฟล์ก่อน จะได้ไม่บันทึกไปครึ่งเดียว
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                    return BadRequest($"File type {ext} is not allowed.");

                if (file.Length == 0)
                    return BadRequest($"{file.FileName} is empty.");

                if (file.Length > MaxUploadSize)
                    return BadRequest($"{file.FileName} is larger than 10 MB.");
            }

            string uploadFolder = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            Directory.CreateDirectory(uploadFolder);

            // ชื่อที่มีอยู่แล้วในคำขอนี้ (ไม่สนตัวพิมพ์เล็ก/ใหญ่)
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingNames = await _context.ApplicationFiles
                .AsNoTracking()
                .Where(x => x.TrId == trid)
                .Select(x => x.FileName)
                .ToListAsync();

            foreach (var n in existingNames)
                if (!string.IsNullOrEmpty(n)) usedNames.Add(n);

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                string? requested = (names != null && i < names.Count) ? names[i] : null;

                // ชื่อที่แสดง: ใช้ชื่อที่ผู้ใช้ตั้ง ถ้าซ้ำกับของเดิมจะต่อ _V1, _V2, ...
                string displayName = UniqueDisplayName(requested, file.FileName, ext, usedNames);
                usedNames.Add(displayName);

                // ชื่อไฟล์จริงบนดิสก์ ไม่ใช้ค่าจากผู้ใช้ (กัน path traversal / ชื่อชน)
                string storedName =
                    $"{trid}_V{app.VersionNo}_{Guid.NewGuid():N}{ext}";

                string fullPath = Path.Combine(uploadFolder, storedName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _context.ApplicationFiles.Add(new ApplicationFile
                {
                    TrId = trid,
                    FileName = displayName,
                    FilePath = storedName
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Upload Success" });
        }


        //   อัปโหลดชื่อเดิมครั้งที่ 1 -> "Certificate_V1.pdf"
        //   อัปโหลดชื่อเดิมครั้งที่ 2 -> "Certificate_V2.pdf"
        private static string UniqueDisplayName(
            string? requested, string originalFileName, string ext, HashSet<string> used)
        {
            string name = string.IsNullOrWhiteSpace(requested)
                ? originalFileName
                : requested.Trim();

            // ตัดนามสกุลออกก่อน แล้วต่อกลับตามไฟล์จริง
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                name = name[..^ext.Length];

            // ตัดอักขระที่ใช้ในชื่อไฟล์ไม่ได้ และจำกัดความยาว
            name = Regex.Replace(name, @"[\\/:*?""<>|]", "").Trim();
            if (name.Length > 100) name = name[..100].Trim();
            if (name.Length == 0) name = "file";

            string candidate = name + ext;
            int version = 1;

            while (used.Contains(candidate))
            {
                candidate = $"{name}_V{version}{ext}";
                version++;
            }

            return candidate;
        }

        //สร้างหมายเลขคำขอใหม่ ใบอนุญาติใหม่ (TrId) โดยใช้รูปแบบ 
        private async Task<string> GenerateNumber(int type)
        {
            var now = DateTime.Now;
            string typenb = ""; // 1=TrId, 2=LicenseNumber 

            string prefix = "";
            if (type == 1)
            {
                typenb = "TrId";
                prefix = $"TR{now.Year}{now:MMdd}";

            }
            else if (type == 2)
            {
                typenb = "LIC";
                prefix = $"LIC{now.Year}{now:MMdd}";
            }



            IDbContextTransaction? ownTransaction = null;

            // ถ้ายังไม่มี Transaction ให้สร้างใหม่
            if (_context.Database.CurrentTransaction == null)
            {
                ownTransaction = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);
            }

            try
            {
                // Lock แถวหรือช่วงข้อมูล เพื่อป้องกันการจองเลขซ้ำ
                var runningNumber = await _context.RunningNumbers
                    .FromSqlInterpolated($@"
                SELECT *
                FROM dbo.RunningNumbers WITH (UPDLOCK, HOLDLOCK)
                WHERE Prefix = {prefix}
            ")
                    .SingleOrDefaultAsync();

                int nextNumber;

                if (runningNumber == null)
                {
                    nextNumber = 1;

                    runningNumber = new RunningNumber
                    {
                        Prefix = prefix,
                        LastNumber = nextNumber,
                        Dategen = now,
                        Typenumber = typenb
                    };

                    _context.RunningNumbers.Add(runningNumber);
                }
                else
                {
                    runningNumber.LastNumber++;

                    runningNumber.Dategen = now;
                    runningNumber.Typenumber = "TRID";

                    nextNumber = runningNumber.LastNumber;
                }

                await _context.SaveChangesAsync();

                // Commit เฉพาะกรณีที่ Method นี้สร้าง Transaction เอง
                if (ownTransaction != null)
                {
                    await ownTransaction.CommitAsync();
                }

                return $"{prefix}{nextNumber:D4}";
            }
            catch
            {
                if (ownTransaction != null)
                {
                    await ownTransaction.RollbackAsync();
                }

                throw;
            }
            finally
            {
                if (ownTransaction != null)
                {
                    await ownTransaction.DisposeAsync();
                }
            }
        }

        [HttpDelete("file/{trid}/{fileId:int}")]
        public async Task<IActionResult> DeleteFile(
            string trid,
            int fileId)
        {
            // ตรวจสิทธิ์เจ้าของคำขอ (เหมือน TraderEdit)
            var trader = await GetCurrentUser();

            if (trader == null)
                return Unauthorized();
            if (!IsTrader(trader))
                return Forbid();

            var app = await _context.Applications
                .FirstOrDefaultAsync(x =>
                    x.Trid == trid &&
                    x.Identify == trader.Identify &&
                    x.Citizenid == trader.Citizenid);

            if (app == null)
                return NotFound("Application not found.");

            // ลบได้เฉพาะ Draft เท่านั้น (ส่งคำขอแล้วต้องเก็บประวัติ)
            if (app.Statusid != 1)
                return BadRequest("Files can be deleted only while the application is a draft.");

            var file = await _context.ApplicationFiles
                .FirstOrDefaultAsync(x => x.Id == fileId && x.TrId == trid);

            if (file == null)
                return NotFound("File not found.");

            // ลบไฟล์จริงบนดิสก์ (ใช้ GetFileName กัน path traversal)
            string storedName = Path.GetFileName(file.FilePath ?? "");

            if (!string.IsNullOrEmpty(storedName))
            {
                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot", "uploads", storedName);

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            _context.ApplicationFiles.Remove(file);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Deleted" });
        }

        #endregion

        #region 4. แก้ไขคำขอ
        [HttpPut("TraderEdit/{trid}")]
        public async Task<IActionResult> TraderEdit(
            string trid, TraderEditRequest request)
        {
            var trader = await GetCurrentUser();

            if (trader == null)
                return Unauthorized();

            if (!IsTrader(trader))
                return Forbid();

            var app = await _context.Applications
                .FirstOrDefaultAsync(x =>
                    x.Trid == trid &&
                    x.Identify == trader.Identify &&
                    x.Citizenid == trader.Citizenid);   // ต้องเป็นเจ้าของคำขอ

            if (app == null)
                return NotFound();

            // แก้ไขได้เฉพาะ Draft / Revision Required / Applicant Edited
            if (app.Statusid != 1 && app.Statusid != 4 && app.Statusid != 5)
                return BadRequest("Cannot edit this application");

            bool isDraft = app.Statusid == 1;

            // Type 3, 4 แก้ได้เฉพาะไฟล์แนบ
            bool filesOnly = (app.Type == 3 || app.Type == 4);

            if (!filesOnly)
            {
                if (request.Products == null || !request.Products.Any())
                    return BadRequest("Please add at least one product.");

                if (!string.IsNullOrWhiteSpace(request.Email) &&
                    !Regex.IsMatch(request.Email, EmailPattern))
                    return BadRequest("Please enter a valid email address.");

                if (!string.IsNullOrWhiteSpace(request.Tel) &&
                    !Regex.IsMatch(request.Tel, TelPattern))
                    return BadRequest("Telephone must be 9-11 digits and start with 0.");

                if (request.Products.Any(p =>
                    string.IsNullOrWhiteSpace(p.Productname) ||
                    string.IsNullOrWhiteSpace(p.Modelname) ||
                    string.IsNullOrWhiteSpace(p.Description)))
                    return BadRequest("Product information is incomplete.");
            }

            var oldProducts = await _context.ApplicationProducts
                .Where(x => x.TrId == trid)
                .ToListAsync();

            // ===== เก็บประวัติ: เฉพาะที่ไม่ใช่ Draft =====
            if (!isDraft)
            {
                var currentVersion = app.VersionNo;

                _context.ApplicationVersions.Add(new ApplicationVersion
                {
                    TrId = trid,
                    VersionNo = currentVersion,
                    Email = app.Email,
                    Tel = app.Tel,
                    Remark = app.Remark,
                    CreateDate = DateTime.Now
                });

                foreach (var p in oldProducts)
                {
                    _context.ProductVersions.Add(new ProductVersion
                    {
                        TrId = trid,
                        VersionNo = currentVersion,
                        ProductName = p.Productname,
                        ModelName = p.Modelname,
                        Description = p.Description
                    });
                }

                var oldFiles = await _context.ApplicationFiles
                    .Where(x => x.TrId == trid)
                    .ToListAsync();

                foreach (var f in oldFiles)
                {
                    _context.FileVersions.Add(new FileVersion
                    {
                        TrId = trid,
                        VersionNo = currentVersion,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        CreateDate = DateTime.Now
                    });
                }
            }

            // Type 1, 2: แก้ Email / Tel / Product ได้
            // Type 3, 4: ข้ามส่วนนี้ทั้งหมด ข้อมูลเดิมคงอยู่
            if (!filesOnly)
            {
                app.Email = request.Email;
                app.Tel = request.Tel;
                app.Remark = request.Remark;

                _context.ApplicationProducts.RemoveRange(oldProducts);

                int ridCounter = 1;
                foreach (var p in request.Products)
                {
                    _context.ApplicationProducts.Add(new ApplicationProduct
                    {
                        TrId = trid,
                        Productname = p.Productname,
                        Modelname = p.Modelname,
                        Description = p.Description,
                        Statusproduct = isDraft ? "draft" : "edit",
                        Rid = ridCounter++,
                        Dateinsert = DateTime.Now,
                        Dateupdate = DateTime.Now,
                        Active = 1
                    });
                }
            }

            if (!isDraft)
            {
                // Revision / Applicant Edited: บวกเวอร์ชันและเปลี่ยนสถานะ
                app.VersionNo++;
                app.Statusid = 5;
                app.Statusname = "Applicant Edited";
            }
            // Draft: คง VersionNo และคงสถานะ = 1 ไว้เหมือนเดิม

            app.Dateupdate = DateTime.Now;

            await _context.SaveChangesAsync();

            // บันทึก History เฉพาะที่ไม่ใช่ Draft (Draft ยังไม่ได้ส่งคำขอ)
            if (!isDraft)
                await History(trid, app, trader);

            return Ok();
        }


        [HttpGet("VersionHistory/{trid}")]
        public async Task<IActionResult> VersionHistory(string trid)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var canAccess = await GetVisibleApplications(user, noTracking: true).AnyAsync(x => x.Trid == trid);
            if (!canAccess) return NotFound("Application not found.");

            var versions = await _context.ApplicationVersions.AsNoTracking()
                .Where(x => x.TrId == trid).OrderByDescending(x => x.VersionNo).ToListAsync();
            return Ok(versions);
        }

        [HttpGet("version-file-download/{trid}/{version}/{fileName}")]
        public async Task<IActionResult> DownloadVersionFile(string trid, int version, string fileName)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var canAccess = await GetVisibleApplications(user, noTracking: true)
                .AnyAsync(x => x.Trid == trid);
            if (!canAccess) return NotFound("File not found.");

            var versionFile = await _context.FileVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TrId == trid && x.VersionNo == version && x.FileName == fileName);
            if (versionFile == null) return NotFound("File not found.");

            var storedName = Path.GetFileName(versionFile.FilePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(storedName)) return NotFound("File not found.");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", storedName);
            if (!System.IO.File.Exists(fullPath)) return NotFound("File not found.");

            var extension = Path.GetExtension(storedName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullPath, contentType);
        }

        [HttpGet("VersionDetail/{trid}/{version}")]
        public async Task<IActionResult> VersionDetail(string trid, int version)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var canAccess = await GetVisibleApplications(user, noTracking: true).AnyAsync(x => x.Trid == trid);
            if (!canAccess) return NotFound("Application not found.");

            var app = await _context.ApplicationVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TrId == trid && x.VersionNo == version);
            if (app == null) return NotFound("Version not found.");

            var products = await _context.ProductVersions.AsNoTracking()
                .Where(x => x.TrId == trid && x.VersionNo == version).ToListAsync();
            var files = await _context.FileVersions.AsNoTracking()
                .Where(x => x.TrId == trid && x.VersionNo == version)
                .Select(x => new
                {
                    fileName = x.FileName,
                    createDate = x.CreateDate,
                    filePath = "/api/applications/version-file-download/" + trid + "/" + version + "/" + x.FileName,
                    downloadUrl = "/api/applications/version-file-download/" + trid + "/" + version + "/" + x.FileName
                })
                .ToListAsync();

            return Ok(new { application = app, products, files });
        }
        #endregion

        #region 5. ยกเลิกคำขอ

        [HttpPost("TraderCancel/{trid}")]
        public async Task<IActionResult> TraderCancel(
            string trid,
            [FromBody] CancelDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Remark))
                return BadRequest("Please enter cancel remark");

            var trader = await GetCurrentUser();

            if (trader == null)
                return Unauthorized();
            if (!IsTrader(trader))
                return Forbid();

            var app = await _context.Applications
                .FirstOrDefaultAsync(x =>
                    x.Trid == trid &&
                    x.Identify == trader.Identify &&
                    x.Citizenid == trader.Citizenid);

            if (app == null)
                return NotFound();

            // 6 = ยกเลิกแล้ว, 7 = ถูกปฏิเสธ, 8 = อนุมัติแล้ว, 9 = ยกเลิก License
            if (app.Statusid is 6 or 7 or 8 or 9)
                return BadRequest("Cannot cancel this application");

            app.Statusid = 6;
            app.Statusname = GetStatusName(6);
            app.Dateupdate = DateTime.Now;



            await _context.SaveChangesAsync();
            await History(trid, app, trader); //บันทึกประวัติ

            return Ok(new { Message = "Cancel Success" });
        }
        #endregion

        #region 6. เปลี่ยนสถานะ

        //1 = Draft
        //2 = Submitted
        //3 = Officer Review
        //4 = Revision Required
        //5 = Applicant Edited
        //6 = Applicant cancel
        //7 = Officer Rejected
        //8 = Approved
        //9 = Cancel License
        [HttpPost("Submitted/{trid}")]
        public async Task<IActionResult> Submitted(string trid)
        {
            var trader = await GetCurrentUser();

            if (trader == null)
                return Unauthorized();
            if (!IsTrader(trader))
                return Forbid();

            var app = await _context.Applications
                .FirstOrDefaultAsync(x =>
                    x.Trid == trid &&
                    x.Identify == trader.Identify &&
                    x.Citizenid == trader.Citizenid);

            if (app == null)
                return NotFound();

            if (app.Statusid != 1 && app.Statusid != 4 && app.Statusid != 5)
                return BadRequest("Cannot submit this application");

            // ห้ามส่งคำขอที่ไม่มีสินค้า
            var hasProducts = await _context.ApplicationProducts
                .AnyAsync(x => x.TrId == trid && x.Active == 1);

            if (!hasProducts)
                return BadRequest("Please add at least one product before submitting.");

            app.Statusid = 2;
            app.Statusname = GetStatusName(2);
            app.Dateupdate = DateTime.Now;
            app.Datesubmission = DateTime.Now;

            await _context.SaveChangesAsync();
            await History(trid, app, trader); //บันทึกประวัติ


            return Ok(new { Message = "Submitted Success" });
        }


        [HttpPut("UpdateStatus/{trid}")]
        public async Task<IActionResult> UpdateStatus(
            string trid,
            [FromBody] StatusUpdateDto request)
        {
            var officer = await GetCurrentUser();

            if (officer == null)
                return Unauthorized();

            // เฉพาะ Staff ตรวจจาก Session เท่านั้น
            if (!IsStaff(officer))
                return Forbid();

            if (!new[] { 3, 4, 7, 8 }.Contains(request.Statusid))
                return BadRequest("Invalid status");

            if ((request.Statusid == 4 || request.Statusid == 7) &&
                string.IsNullOrWhiteSpace(request.Remark))
            {
                return BadRequest("Please enter remark");
            }

            var app = await GetVisibleApplications(officer)
                .FirstOrDefaultAsync(x => x.Trid == trid);

            if (app == null)
                return NotFound();

            // รีวิวได้เฉพาะ Submitted / Officer Review / Applicant Edited
            // (สถานะ 4, 6, 7, 8, 9 จะถูกกันไว้ที่เงื่อนไขนี้ทั้งหมด)
            if (app.Statusid != 2 && app.Statusid != 5 && app.Statusid != 3)
                return BadRequest("Application cannot be reviewed");

            // กันเขียน History ซ้ำเมื่อส่งสถานะเดิม
            if (app.Statusid == request.Statusid)
                return BadRequest("Application is already in this status.");

            // ทุกอย่างที่เกี่ยวกับการอนุมัติ (เลข License, แก้ License เดิม, Newcode, History)
            // อยู่ใน Transaction เดียว ถ้าล้มเหลวจะย้อนกลับทั้งหมด
            await using var transaction =
                await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                if (request.Statusid == 8)
                {
                    var now = DateTime.Now;

                    if (app.Type == 1)
                    {
                        // New License

                        app.Licensenumber = await GenerateNumber(2);
                        app.Dateappv = now;
                        app.Dateexp = now.AddYears(5);

                        app.LicenseStatus = "Active";
                        app.LicenseStatusDate = now;

                    }
                    else
                    {
                        var old = await _context.Applications
                            .FirstOrDefaultAsync(x =>
                                x.Licensenumber == app.ReferenceLicenseNumber &&
                                x.Statusid == 8 &&
                                (x.LicenseStatus == null ||
                                 x.LicenseStatus == "Active"));

                        if (old == null)
                        {
                            return BadRequest(
                                "Original license not found or no longer active.");
                        }

                        switch (app.Type)
                        {
                            case 2: // Amendment

                                app.Licensenumber = await GenerateNumber(2);
                                app.Dateappv = now;
                                app.Dateexp = old.Dateexp;

                                app.LicenseStatus = "Active";
                                app.LicenseStatusDate = now;

                                old.LicenseStatus = "Replaced";
                                old.LicenseStatusDate = now;
                                old.ReplacedBy = app.Licensenumber;

                                break;

                            case 3: // Renew

                                app.Licensenumber = await GenerateNumber(2);
                                app.Dateappv = now;
                                app.Dateexp = old.Dateexp.Value.AddYears(5);

                                app.LicenseStatus = "Active";
                                app.LicenseStatusDate = now;

                                old.LicenseStatus = "Replaced";
                                old.LicenseStatusDate = now;
                                old.ReplacedBy = app.Licensenumber;

                                break;

                            case 4: // Cancel

                                // ยกเลิก License เดิม
                                //old.Statusid = 9;
                                //old.Statusname = GetStatusName(9);
                                old.LicenseStatus = "Cancelled";
                                old.LicenseStatusDate = now;
                                old.ReplacedBy = app.Licensenumber;
                                // คำขอ Cancel ไม่สร้าง License ใหม่
                                app.Licensenumber = null;
                                app.Dateappv = now;
                                app.Dateexp = null;
                                app.LicenseStatus = null;
                                app.LicenseStatusDate = null;

                                break;
                        }
                    }
                }

                app.Statusid = request.Statusid;
                app.Statusname = GetStatusName(request.Statusid);
                app.Dateupdate = DateTime.Now;
                app.Remark = request.Remark;

                await _context.SaveChangesAsync();

                if ((request.Statusid == 8) && (app.Type is 1 or 2 or 3))
                {
                    var hasProducts = await _context.ApplicationProducts
                        .AnyAsync(x => x.TrId == trid && x.Active == 1);

                    if (hasProducts)
                    {
                        await GenerateNewcode(trid);
                    }
                }

                await History(trid, app, officer); //บันทึกประวัติ

                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Update Status Success",
                    Statusid = app.Statusid,
                    Statusname = app.Statusname
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    Message = "Update Status Failed."
                });
            }
        }



        //สร้างNEWCODE
        private async Task GenerateNewcode(string trId)
        {
            var products = await _context.ApplicationProducts
                .Where(x => x.TrId == trId)
                .ToListAsync();

            if (!products.Any())
                throw new InvalidOperationException($"ไม่พบ Product ที่ TrId = {trId}");

            foreach (var product in products)
            {
                // ข้ามรายการที่มี Newcode แล้ว
                if (!string.IsNullOrEmpty(product.Newcode))
                    continue;

                product.Newcode = "NC-" + trId
                    + Guid.NewGuid().ToString("N").Substring(0, 4)
                    + product.Rid;
            }

            await _context.SaveChangesAsync();
        }

        //เก็บประวัติ
        private async Task History(string Trid, Application app, UserAccount user)
        {
            var history = new ApplicationStatusHistory
            {
                Trid = Trid,
                Statusid = app.Statusid,
                Statusname = app.Statusname,
                Dateupdate = DateTime.Now,
                Nameupdate = user.Fullname,
                Citizenid = user.Citizenid,
                Remark = app.Remark

            };

            _context.ApplicationStatusHistories.Add(history);
            await _context.SaveChangesAsync();
        }
        #endregion

        #region 7. Certificate 

        [HttpGet("Certificate/{trid}")]
        public async Task<IActionResult> Certificate(string trid)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();
            if (!IsTrader(user) && !IsStaff(user)) return Forbid();

            var app = await GetVisibleApplications(user, noTracking: true)
                .FirstOrDefaultAsync(x => x.Trid == trid);
            if (app == null) return NotFound("Application not found.");

            if (app.Statusid != 8 || string.IsNullOrWhiteSpace(app.Licensenumber))
                return BadRequest("Certificate is available only for approved licenses.");

            var products = await _context.ApplicationProducts.AsNoTracking()
                .Where(x => x.TrId == trid && x.Active == 1)
                .OrderBy(x => x.Rid)
                .Select(x => new ProductDto { Rid = x.Rid, Newcode = x.Newcode, Productname = x.Productname, Modelname = x.Modelname, Description = x.Description })
                .ToListAsync();

            var pdf = CertificateBuilder.Build(app, products);
            return File(pdf, "application/pdf");
        }
        #endregion
    }

    #region DTO Classes
    public class ApplicationCreateRequest
    {
        public Application Application { get; set; } = null!;
        public List<ProductDto> Products { get; set; } = new();
    }


    public class ProductDto
    {
        public int? Ida { get; set; }
        public int? Rid { get; set; }
        public string? Newcode { get; set; }
        public string? Productname { get; set; }
        public string? Modelname { get; set; }
        public string? Description { get; set; }
    }

    public class ApplicationUpdateDto
    {
        public int Type { get; set; }
        public string? Tel { get; set; }
        public string? Email { get; set; }
        public string? Remark { get; set; }
        public List<ProductDto>? Products { get; set; }
    }

    // ไม่มี Identify / CitizenId แล้ว: ตัวตนมาจาก Session เท่านั้น
    public class StatusUpdateDto
    {
        public int Statusid { get; set; }
        public string? Remark { get; set; }
    }

    public class CancelDto
    {
        public string? Remark { get; set; }
    }

    public class TraderEditRequest
    {
        public string? Email { get; set; }

        public string? Tel { get; set; }

        public string? Remark { get; set; }

        public List<ProductDto> Products { get; set; }
            = new();
    }
    #endregion


}