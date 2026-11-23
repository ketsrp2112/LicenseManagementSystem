using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class ApplicationFile
{
    public int Id { get; set; }

    public string TrId { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
