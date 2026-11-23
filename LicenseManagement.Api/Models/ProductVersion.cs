using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class ProductVersion
{
    public int Id { get; set; }

    public string TrId { get; set; } = null!;

    public int VersionNo { get; set; }

    public string? ProductName { get; set; }

    public string? ModelName { get; set; }

    public string? Description { get; set; }
}
