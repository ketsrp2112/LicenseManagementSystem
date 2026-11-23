using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class ApplicationProduct
{
    public int Ida { get; set; }

    public string? Newcode { get; set; }

    public string? Productname { get; set; }

    public int? Rid { get; set; }

    public string? Modelname { get; set; }

    public string? Description { get; set; }

    public DateTime? Dateinsert { get; set; }

    public DateTime? Dateupdate { get; set; }

    public string? Statusproduct { get; set; }

    public int? Active { get; set; }

    public string? TrId { get; set; }
}
