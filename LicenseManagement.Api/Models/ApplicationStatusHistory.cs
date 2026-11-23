using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class ApplicationStatusHistory
{
    public int Ida { get; set; }

    public int? Statusid { get; set; }

    public string? Statusname { get; set; }

    public DateTime? Dateupdate { get; set; }

    public string? Nameupdate { get; set; }

    public string? Citizenid { get; set; }

    public string? Trid { get; set; }

    public string? Remark { get; set; }
}
