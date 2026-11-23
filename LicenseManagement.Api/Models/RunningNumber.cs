using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class RunningNumber
{
    public int Id { get; set; }

    public string Prefix { get; set; } = null!;

    public int LastNumber { get; set; }

    public DateTime Dategen { get; set; }

    public string Typenumber { get; set; } = null!;
}
