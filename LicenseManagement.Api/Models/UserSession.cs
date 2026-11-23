using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class UserSession
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string SessionToken { get; set; } = null!;

    public DateTime ExpireDate { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
