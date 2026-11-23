using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class FileVersion
{
    public int Id { get; set; }

    public string TrId { get; set; } = null!;

    public int VersionNo { get; set; }

    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public DateTime? CreateDate { get; set; }
}
