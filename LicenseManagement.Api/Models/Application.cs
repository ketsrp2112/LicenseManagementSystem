using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class Application
{
    public int Ida { get; set; }

    public string? Trid { get; set; }

    public string? Identify { get; set; }

    public string? Companyname { get; set; }

    public string? Citizenid { get; set; }

    public string? Citizenname { get; set; }

    public int? Type { get; set; }

    public DateTime? Datesubmission { get; set; }

    public int? Statusid { get; set; }

    public string? Statusname { get; set; }

    public string? Licensenumber { get; set; }

    public string? Tel { get; set; }

    public string? Email { get; set; }

    public DateTime? Dateupdate { get; set; }

    public string? Remark { get; set; }

    public DateTime? Dateappv { get; set; }

    public int VersionNo { get; set; }

    public string? Typenm { get; set; }

    public DateTime? Dateexp { get; set; }

    public string? ReferenceLicenseNumber { get; set; }

    public string? LicenseStatus { get; set; }

    public DateTime? LicenseStatusDate { get; set; }

    public string? ReplacedBy { get; set; }
}
