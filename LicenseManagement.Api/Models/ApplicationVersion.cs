using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class ApplicationVersion
{
    public int Id { get; set; }

    public string TrId { get; set; } = null!;

    public int VersionNo { get; set; }

    public string? Email { get; set; }

    public string? Tel { get; set; }

    public string? Remark { get; set; }

    public DateTime? CreateDate { get; set; }

    public string? CreateBy { get; set; }
}
