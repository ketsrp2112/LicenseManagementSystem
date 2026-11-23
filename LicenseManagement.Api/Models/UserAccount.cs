using System;
using System.Collections.Generic;

namespace LicenseManagement.Api.Models;

public partial class UserAccount
{
    public int Ida { get; set; }

    public string? Username { get; set; }

    public string Password { get; set; } = null!;

    public string? Citizenid { get; set; }

    public string? Identify { get; set; }

    public string? Fullname { get; set; }

    /// <summary>
    /// Trader, Staff
    /// </summary>
    public string? Usertype { get; set; }

    public DateTime? Createdate { get; set; }

    public string? Companyname { get; set; }

    public string? Tel { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
}
