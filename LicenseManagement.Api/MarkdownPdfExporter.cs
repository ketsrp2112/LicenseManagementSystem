// ============================================================
// MarkdownPdfExporter.cs  —  Markdown (.md) -> PDF
//
// NuGet:
//   dotnet add package Markdig
//   dotnet add package PuppeteerSharp
//
// หลักการ: Markdown -> HTML (Markdig) -> พิมพ์เป็น PDF ด้วย Chromium (PuppeteerSharp)
// เหตุผลที่เลือกวิธีนี้ (กันรูปเพี้ยน/แถวตารางเพี้ยน/ภาษาไทยเพี้ยน):
//   1) รูป  : ฝังเป็น base64 + CSS max-width:100%, height:auto  -> สัดส่วนไม่เพี้ยน ไม่ล้นหน้า
//   2) ตาราง: border-collapse + tr{break-inside:avoid} + thead ซ้ำทุกหน้า -> แถวไม่ถูกตัดกลางแถว
//   3) ไทย  : Chromium ใช้ HarfBuzz จัดวรรณยุกต์/สระถูกต้อง (ฝังฟอนต์ได้ถ้าเครื่อง server ไม่มี)
//
// ครั้งแรกที่รัน PuppeteerSharp จะดาวน์โหลด Chromium อัตโนมัติ (ต้องมีเน็ต 1 ครั้ง)
// ============================================================

using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SkiaSharp;

public static class MarkdownPdfExporter
{
    /// <param name="markdownPath">path ของไฟล์ .md (รูปแบบ relative เช่น images/01_index.png จะอ้างอิงจากโฟลเดอร์ของไฟล์นี้)</param>
    /// <param name="thaiFontPath">(ไม่บังคับ) path ของไฟล์ฟอนต์ไทย .ttf เช่น Sarabun-Regular.ttf เพื่อฝังลง PDF</param>
    public static async Task<byte[]> ExportAsync(string markdownPath, string? thaiFontPath = null)
    {
        var fullPath = Path.GetFullPath(markdownPath);
        var baseDir = Path.GetDirectoryName(fullPath)!;
        var markdown = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);

        var html = BuildHtml(markdown, baseDir, thaiFontPath);
        return await HtmlToPdfAsync(html);
    }

    // ---------- 1) Markdown -> HTML ----------
    private static string BuildHtml(string markdown, string baseDir, string? thaiFontPath)
    {
        // UseAdvancedExtensions = pipe tables, task list, autolink ฯลฯ
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var document = Markdown.Parse(markdown, pipeline);

        // แปลงรูปทุกรูปเป็น data URI เพื่อไม่ต้องพึ่ง path ตอนพิมพ์
        foreach (var link in document.Descendants<LinkInline>().Where(l => l.IsImage).ToList())
        {
            var url = link.Url;
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = Uri.UnescapeDataString(url).Replace('/', Path.DirectorySeparatorChar);
            var imgPath = Path.Combine(baseDir, rel);
            if (!File.Exists(imgPath))
            {
                // ไม่เจอตาม path ใน md -> ลองหาด้วยชื่อไฟล์ทั่วโฟลเดอร์ docs (ไม่สนตัวพิมพ์/โฟลเดอร์ย่อย)
                var fileName = Path.GetFileName(imgPath);
                var found = Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                if (found != null) imgPath = found;
            }
            if (!File.Exists(imgPath))
            {
                Console.WriteLine($"[PDF] ไม่พบไฟล์รูป: {url}  (หาที่ {imgPath})");
                continue; // ไม่เจอรูป -> ข้าม ไม่ให้ระบบพัง
            }

            var mime = Path.GetExtension(imgPath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
            // ย่อรูป + แปลงเป็น JPEG พื้นขาว (ไม่มี alpha) เพื่อให้ PDF เล็กและเปิดได้เสถียรทุก viewer
            // ถ้าแปลงไม่ได้ (เช่น svg) ใช้ไฟล์เดิม
            link.Url = (mime == "image/svg+xml" ? null : ToOptimizedDataUri(imgPath))
                       ?? $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(imgPath))}";
        }

        var body = document.ToHtml(pipeline);

        // ฝังฟอนต์ไทย (ถ้าระบุมา)
        var fontFace = "";
        if (!string.IsNullOrEmpty(thaiFontPath) && File.Exists(thaiFontPath))
        {
            var b64 = Convert.ToBase64String(File.ReadAllBytes(thaiFontPath));
            fontFace = $"@font-face{{font-family:'EmbeddedThai';src:url(data:font/ttf;base64,{b64}) format('truetype');}}";
        }

        return $$"""
        <!DOCTYPE html>
        <html lang="th">
        <head>
        <meta charset="utf-8">
        <style>
        {{fontFace}}
        @page { size: A4; }
        * { box-sizing: border-box; }
        body {
            font-family: 'EmbeddedThai','Sarabun','Noto Sans Thai','Tahoma','Segoe UI',sans-serif;
            font-size: 11pt; line-height: 1.6; color: #222;
        }
        h1 { font-size: 24pt; border-bottom: 2px solid #444; padding-bottom: 4px; }
        h2 { font-size: 17pt; border-bottom: 1px solid #ccc; padding-bottom: 3px; margin-top: 1.6em; }
        h3 { font-size: 13.5pt; margin-top: 1.3em; }
        h1, h2, h3, h4 { break-after: avoid; page-break-after: avoid; }

        /* ---------- รูป: คงสัดส่วน ไม่ล้นหน้า ไม่ถูกตัดกลาง ---------- */
        p:has(> img) { break-inside: avoid; text-align: center; margin: 10px 0; }
        img {
            display: block; margin: 8px auto;
            max-width: 100%;
            height: auto;            /* สำคัญ: กันรูปยืด/บีบ */
            max-height: 220mm;       /* กันรูปสูงเกินหนึ่งหน้า */
            border: 1px solid #ddd;
            break-inside: avoid; page-break-inside: avoid;
        }

        /* ---------- ตาราง: แถวไม่ถูกตัด, header ซ้ำทุกหน้า ---------- */
        table {
            width: 100%; border-collapse: collapse; margin: 12px 0;
            table-layout: auto;
            font-size: 10pt;
        }
        thead { display: table-header-group; }
        tfoot { display: table-footer-group; }
        tr { break-inside: avoid; page-break-inside: avoid; }
        th, td {
            border: 1px solid #bbb; padding: 6px 8px;
            vertical-align: top; text-align: left;
            overflow-wrap: anywhere; word-break: break-word;
        }
        th { background: #f0f2f5; font-weight: 600; }

        code {
            font-family: Consolas,'Courier New',monospace; font-size: 0.92em;
            background: #f3f3f3; padding: 1px 4px; border-radius: 3px;
            overflow-wrap: anywhere;
        }
        pre { background:#f6f6f6; padding:10px; border-radius:4px; white-space:pre-wrap; break-inside:avoid; }
        pre code { background: none; padding: 0; }
        blockquote {
            margin: 12px 0; padding: 6px 14px;
            border-left: 4px solid #8aa; background: #f5f9fa; color:#333;
            break-inside: avoid;
        }
        li { margin: 2px 0; }
        hr { border: 0; border-top: 1px solid #ccc; margin: 20px 0; }
        </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """;
    }

    // ---------- ย่อ/แปลงรูปก่อนฝังลง PDF ----------
    // - จำกัดความกว้างไม่เกิน maxWidth px (รูป screenshot 4K ไม่จำเป็นต้องเต็มความละเอียดบนกระดาษ A4)
    // - วาดลงพื้นขาวแล้วเข้ารหัส JPEG: ตัดช่อง alpha (SMask) ที่ทำให้บาง viewer แสดงรูปหาย/ไม่ครบ
    private static string? ToOptimizedDataUri(string path, int maxWidth = 1600, int quality = 90)
    {
        try
        {
            using var original = SKBitmap.Decode(path);
            if (original == null) return null;

            var scale = original.Width > maxWidth ? (float)maxWidth / original.Width : 1f;
            var w = Math.Max(1, (int)Math.Round(original.Width * scale));
            var h = Math.Max(1, (int)Math.Round(original.Height * scale));

            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // SkiaSharp 3.x: ใช้ SKSamplingOptions แทน FilterQuality (ย่อรูปแบบนุ่ม ไม่แตก)
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            using var resized = (w == original.Width && h == original.Height)
                ? original.Copy()
                : original.Resize(new SKImageInfo(w, h, original.ColorType, original.AlphaType), sampling);
            if (resized == null) return null;
            canvas.DrawBitmap(resized, 0, 0);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            return "data:image/jpeg;base64," + Convert.ToBase64String(data.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF] ย่อรูปไม่สำเร็จ ({path}): {ex.Message}");
            return null;
        }
    }

    // ---------- 2) HTML -> PDF ----------
    private static async Task<byte[]> HtmlToPdfAsync(string html)
    {
        await new BrowserFetcher().DownloadAsync(); // โหลด Chromium ครั้งแรกครั้งเดียว

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" } // จำเป็นบน Linux/Docker
        });
        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new PuppeteerSharp.NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });
        await page.EvaluateExpressionAsync("document.fonts.ready"); // รอฟอนต์โหลดครบ

        // รอให้ทุกรูป decode เสร็จก่อนพิมพ์ และรายงานรูปที่โหลดไม่ขึ้น
        var broken = await page.EvaluateFunctionAsync<string[]>(@"async () => {
            const imgs = Array.from(document.images);
            await Promise.all(imgs.map(i => i.decode().catch(() => {})));
            return imgs.filter(i => !i.complete || i.naturalWidth === 0)
                       .map(i => (i.alt || '') + ' | ' + i.src.slice(0, 60));
        }");
        foreach (var b in broken)
            Console.WriteLine($"[PDF] รูปโหลดไม่ขึ้น: {b}");

        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,        // ให้สีพื้นหัวตาราง/โค้ดแสดงออกมา
            PreferCSSPageSize = false,
            MarginOptions = new MarginOptions
            {
                Top = "18mm",
                Bottom = "20mm",
                Left = "16mm",
                Right = "16mm"
            },
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate =
                "<div style='font-size:9px;width:100%;text-align:center;color:#666;'>" +
                "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
        });
    }
}

// ============================================================
// ตัวอย่างการใช้งาน
// ============================================================
//
// 1) Console:
//    var pdf = await MarkdownPdfExporter.ExportAsync("UserManual.md");
//    await File.WriteAllBytesAsync("UserManual.pdf", pdf);
//
// 2) ASP.NET Core (ปุ่ม Export จากระบบ):
//    app.MapGet("/api/manual/pdf", async (IWebHostEnvironment env) =>
//    {
//        var md  = Path.Combine(env.ContentRootPath, "Docs", "UserManual.md");   // รูปอยู่ Docs/images/*.png
//        var pdf = await MarkdownPdfExporter.ExportAsync(md);
//        return Results.File(pdf, "application/pdf", "UserManual.pdf");
//    });
//
// 3) ถ้าเครื่อง server ไม่มีฟอนต์ไทย ให้ฝังเอง:
//    await MarkdownPdfExporter.ExportAsync(md, thaiFontPath: "Fonts/Sarabun-Regular.ttf");