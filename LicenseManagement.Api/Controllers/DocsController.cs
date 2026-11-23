// วางไว้ในโฟลเดอร์ Controllers/DocsController.cs
// เรียกจากหน้าเว็บ:  GET /api/docs/pdf/User_Manual
//                    GET /api/docs/pdf/API_Documentation   (ชื่อไฟล์ .md ใน docs/ ไม่ต้องใส่ .md)

using Microsoft.AspNetCore.Mvc;

namespace LicenseManagement.Api.Controllers;

[ApiController]
[Route("api/docs")]
public class DocsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public DocsController(IWebHostEnvironment env) => _env = env;

    // อนุญาตเฉพาะไฟล์ในรายการนี้ (กัน path traversal เช่น ../appsettings.json)
    private static readonly HashSet<string> AllowedDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        "User_Manual",
        "API_Documentation",
        "Business_Workflow",
        "Database_Design",
        "System_Architecture"
    };

    [HttpGet("pdf/{name}")]
    public async Task<IActionResult> ExportPdf(string name)
    {
        // TODO: ถ้าต้องการให้เฉพาะคนที่ login เรียกได้ ให้ตรวจ X-Session-Token
        //       ด้วยวิธีเดียวกับ controller อื่นของระบบ ก่อนบรรทัดถัดไป

        if (!AllowedDocs.Contains(name))
            return NotFound($"ไม่พบเอกสาร '{name}'");

        // docs/ อยู่ที่รากของโปรเจกต์ ตามโครงสร้างใน Solution Explorer
        var mdPath = Path.Combine(_env.ContentRootPath, "docs", name + ".md");
        if (!System.IO.File.Exists(mdPath))
            return NotFound($"ไม่พบไฟล์ {name}.md");

        // รูปใน md เป็น images/xx.png -> อ่านจาก docs/images/ อัตโนมัติ
        var pdf = await MarkdownPdfExporter.ExportAsync(mdPath);
        return File(pdf, "application/pdf", name + ".pdf");
    }
}