using System.Globalization;
using LicenseManagement.Api.Controllers;   // ProductDto
using LicenseManagement.Api.Models;        // Application
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LicenseManagement.Api.Services
{
    /// <summary>
    /// เรียก CertificateFonts.Register() ครั้งเดียวตอนเริ่มโปรแกรม (Program.cs)
    /// ก่อนสร้าง PDF ครั้งแรก
    /// </summary>
    public static class CertificateFonts
    {
        private static bool _registered;
        private static readonly object _lock = new();

        // Font fallback chain
        public static readonly string[] Chain =
        {
        "Malgun Gothic",     // Korean + English
        "Leelawadee UI",     // Thai
        "Arial"             // Fallback
    };

        public static void Register()
        {
            lock (_lock)
            {
                if (_registered)
                    return;

                QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

                var windowsFontDir =
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

                var fonts = new[]
                {
            "arial.ttf",
            "arialbd.ttf",
            "LeelawUI.ttf",
            "LeelaUIb.ttf",
            "malgun.ttf",
            "malgunbd.ttf"
        };

                foreach (var font in fonts)
                {
                    try
                    {
                        var path = Path.Combine(windowsFontDir, font);

                        if (!File.Exists(path))
                        {
                            Console.WriteLine($"Font not found: {font}");
                            continue;
                        }

                        using var stream = File.OpenRead(path);
                        FontManager.RegisterFont(stream);

                        Console.WriteLine($"Registered: {font}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to register {font}: {ex.Message}");
                    }
                }

                _registered = true;
            }
        }
    }

    public static class CertificateBuilder
    {
        // Color palette
        private const string Navy = "#1F3A5F";
        private const string Gold = "#B08D3C";
        private const string Muted = "#64748B";
        private const string Line = "#E2E8F0";
        private const string Soft = "#F8FAFC";

        public static byte[] Build(Application app, List<ProductDto> products)
        {
            CertificateFonts.Register();

            var status = string.IsNullOrWhiteSpace(app.LicenseStatus) ? "Active" : app.LicenseStatus!;
            var isActive = status == "Active";
            var (statusFg, statusBg) = StatusColors(status);

            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x
                        .FontFamily(CertificateFonts.Chain)
                        .FontSize(10.5f)
                        .FontColor("#1E293B"));

                    // ---------- Double border (navy outer, gold inner) ----------
                    page.Background()
                        .Padding(18).Border(2.5f).BorderColor(Navy)
                        .Padding(4).Border(0.75f).BorderColor(Gold);

                    // ---------- Watermark for Replaced / Cancelled licenses ----------
                    if (!isActive)
                    {
                        page.Foreground().AlignCenter().AlignMiddle()
                            .Rotate(-35)
                            .Text(status.ToUpperInvariant())
                            .FontSize(90).Bold().FontColor("#22DC2626");
                    }

                    // ---------- Content ----------
                    page.Content().PaddingHorizontal(52).PaddingTop(48).Column(col =>
                    {
                        // Header
                        col.Item().AlignCenter().Text("LICENSE MANAGEMENT SYSTEM")
                            .FontSize(9).Bold().LetterSpacing(0.2f).FontColor(Gold);

                        col.Item().PaddingTop(10).AlignCenter().Text("CERTIFICATE OF LICENSE")
                            .FontSize(28).Bold().LetterSpacing(0.08f).FontColor(Navy);

                        col.Item().PaddingTop(10).AlignCenter().Width(90)
                            .LineHorizontal(2).LineColor(Gold);

                        // Holder
                        col.Item().PaddingTop(26).AlignCenter().Text("This is to certify that")
                            .FontSize(11).Italic().FontColor(Muted);

                        col.Item().PaddingTop(8).AlignCenter()
                            .Element(c => Dir(c, app.Companyname))
                            .Text(app.Companyname ?? "-")
                            .FontSize(22).Bold().FontColor(Navy);

                        col.Item().PaddingTop(4).AlignCenter()
                            .Element(c => Dir(c, app.Citizenname))
                            .Text($"Represented by {app.Citizenname ?? "-"}")
                            .FontSize(11).FontColor(Muted);

                        col.Item().PaddingTop(10).Text(t =>
                        {
                            t.AlignCenter();
                            t.Span("is hereby granted the license identified below, subject to the " +
                                   "conditions set by the licensing authority.")
                                .FontSize(10.5f).FontColor(Muted);
                        });

                        // License number box
                        col.Item().PaddingTop(24).AlignCenter()
                            .Border(1).BorderColor(Gold).Background(Soft)
                            .PaddingVertical(14).PaddingHorizontal(46)
                            .Column(c =>
                            {
                                c.Item().AlignCenter().Text("LICENSE NUMBER")
                                    .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(Muted);

                                c.Item().PaddingTop(4).AlignCenter().Text(app.Licensenumber ?? "-")
                                    .FontSize(24).Bold().FontColor(Navy);
                            });

                        // Key information
                        col.Item().PaddingTop(22).Row(r =>
                        {
                            r.Spacing(14);
                            r.RelativeItem().Element(c => Info(c, "License Type", app.Typenm));
                            r.RelativeItem().Element(c => Info(c, "Date of Approval", Fmt(app.Dateappv)));
                            r.RelativeItem().Element(c => Info(c, "Date of Expiry", Fmt(app.Dateexp)));
                            r.RelativeItem().BorderLeft(2).BorderColor(Gold).PaddingLeft(8).Column(c =>
                            {
                                c.Item().Text("Status").FontSize(8.5f).Bold()
                                    .LetterSpacing(0.08f).FontColor(Muted);

                                c.Item().PaddingTop(3).AlignLeft().Background(statusBg)
                                    .PaddingHorizontal(8).PaddingVertical(2)
                                    .Text(status.ToUpperInvariant())
                                    .FontSize(10).Bold().FontColor(statusFg);
                            });
                        });

                        // Supersedes / Replaced by
                        if (!string.IsNullOrWhiteSpace(app.ReferenceLicenseNumber))
                        {
                            col.Item().PaddingTop(10)
                                .Text($"Supersedes license no. {app.ReferenceLicenseNumber}")
                                .FontSize(9.5f).Italic().FontColor(Muted);
                        }

                        if (!string.IsNullOrWhiteSpace(app.ReplacedBy))
                        {
                            col.Item().PaddingTop(4)
                                .Text($"Replaced by license no. {app.ReplacedBy}")
                                .FontSize(9.5f).Italic().FontColor("#B45309");
                        }

                        // Products
                        col.Item().PaddingTop(26).Text("AUTHORIZED PRODUCTS")
                            .FontSize(11).Bold().LetterSpacing(0.12f).FontColor(Navy);

                        col.Item().PaddingTop(6).LineHorizontal(0.75f).LineColor(Gold);

                        col.Item().PaddingTop(10).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.3f); // RID
                                c.RelativeColumn(1.7f); // NEWCODE
                                c.RelativeColumn(3.0f); // PRODUCTNAME
                                c.RelativeColumn(2.0f); // MODELNAME
                                c.RelativeColumn(4.0f); // DESCRIPTION
                            });

                            // Header
                            t.Header(h =>
                            {
                                foreach (var title in new[] { "RID", "NEWCODE", "PRODUCTNAME", "MODELNAME", "DESCRIPTION" })
                                {
                                    h.Cell().Element(HCell)
                                        .Text(title).FontColor(Colors.White).Bold().FontSize(8);
                                }
                            });

                            int i = 0;

                            foreach (var p in products)
                            {
                                int idx = i;

                                t.Cell().Element(c => BCell(c, idx))
                                    .Text(p.Rid?.ToString() ?? "").FontSize(8);

                                t.Cell().Element(c => BCell(c, idx))
                                    .Element(c => Dir(c, p.Newcode))
                                    .Text(p.Newcode ?? "").FontSize(8);

                                t.Cell().Element(c => BCell(c, idx))
                                    .Element(c => Dir(c, p.Productname))
                                    .Text(p.Productname ?? "").FontSize(8).SemiBold();

                                t.Cell().Element(c => BCell(c, idx))
                                    .Element(c => Dir(c, p.Modelname))
                                    .Text(p.Modelname ?? "").FontSize(8);

                                t.Cell().Element(c => BCell(c, idx))
                                    .Element(c => Dir(c, p.Description))
                                    .Text(p.Description ?? "").FontSize(8);

                                i++;
                            }
                        });

                        // Signature + seal
                        col.Item().PaddingTop(34).ShowEntire().Row(r =>
                        {
                            r.RelativeItem().AlignBottom().Column(c =>
                            {
                                c.Item().Text(Fmt(app.Dateappv)).Bold().FontColor(Navy);
                                c.Item().PaddingTop(3).LineHorizontal(0.75f).LineColor(Muted);
                                c.Item().PaddingTop(3).Text("Date of Issue").FontSize(9).FontColor(Muted);
                            });

                            r.ConstantItem(110).AlignCenter().Element(Seal);

                            r.RelativeItem().AlignBottom().Column(c =>
                            {
                                c.Item().Height(20);
                                c.Item().LineHorizontal(0.75f).LineColor(Muted);
                                c.Item().PaddingTop(3).Text("Authorized Officer").Bold().FontColor(Navy);
                                c.Item().Text("Licensing Authority").FontSize(9).FontColor(Muted);
                            });
                        });
                    });

                    // ---------- Footer ----------
                    page.Footer().PaddingHorizontal(52).PaddingBottom(32).Column(f =>
                    {
                        f.Item().LineHorizontal(0.5f).LineColor(Line);

                        f.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem()
                                .Element(c => Dir(c, app.Trid))
                                .Text($"Reference No. {app.Trid}")
                                .FontSize(8.5f).FontColor(Muted);

                            r.RelativeItem().AlignRight().Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Muted));
                                t.Span($"Generated {DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)}   |   Page ");
                                t.CurrentPageNumber();
                                t.Span(" / ");
                                t.TotalPages();
                            });
                        });

                        f.Item().PaddingTop(2).Text(
                                "This certificate is computer-generated. Verify its authenticity " +
                                "with the licensing authority using the license number above.")
                            .FontSize(7.5f).Italic().FontColor(Muted);
                    });
                });
            }).GeneratePdf();
        }

        // =====================================
        // Helpers
        // =====================================

        private static string Fmt(DateTime? d) =>
            d?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";

        private static (string fg, string bg) StatusColors(string status) => status switch
        {
            "Active" => ("#15803D", "#DCFCE7"),
            "Replaced" => ("#B45309", "#FEF3C7"),
            "Cancelled" => ("#B91C1C", "#FEE2E2"),
            _ => ("#475569", "#E2E8F0")
        };

        /// <summary>
        /// ตั้งทิศทางข้อความตามภาษาของข้อมูล: อาหรับ/ฮีบรู/ฯลฯ = RTL, อื่น ๆ = LTR
        /// </summary>
        private static IContainer Dir(IContainer c, string? text) =>
            IsRtl(text) ? c.ContentFromRightToLeft() : c.ContentFromLeftToRight();

        private static bool IsRtl(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (var ch in text)
            {
                // Hebrew, Arabic, Syriac, Thaana, NKo, Arabic Presentation Forms
                if ((ch >= 0x0590 && ch <= 0x08FF) ||
                    (ch >= 0xFB1D && ch <= 0xFDFF) ||
                    (ch >= 0xFE70 && ch <= 0xFEFF))
                    return true;
            }

            return false;
        }

        private static void Info(IContainer c, string label, string? value)
        {
            c.BorderLeft(2).BorderColor(Gold).PaddingLeft(8).Column(col =>
            {
                col.Item().Text(label).FontSize(8.5f).Bold()
                    .LetterSpacing(0.08f).FontColor(Muted);

                col.Item().PaddingTop(3)
                    .Element(x => Dir(x, value))
                    .Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                    .FontSize(11).Bold().FontColor(Navy);
            });
        }

        private static void Seal(IContainer c)
        {
            c.Width(84).Height(84)
                .Border(2).BorderColor(Gold)
                .CornerRadius(42)
                .AlignCenter().AlignMiddle()
                .Text(t =>
                {
                    t.AlignCenter();
                    t.Span("OFFICIAL\nSEAL").FontSize(10).Bold()
                        .LetterSpacing(0.1f).FontColor(Gold);
                });
        }

        private static IContainer HCell(IContainer c) =>
            c.Background(Navy).PaddingVertical(6).PaddingHorizontal(8);

        private static IContainer BCell(IContainer c, int index) =>
            c.Background(index % 2 == 0 ? Soft : Colors.White)
             .BorderBottom(0.5f).BorderColor(Line)
             .PaddingVertical(6).PaddingHorizontal(8);
    }
}