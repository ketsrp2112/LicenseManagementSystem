using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LicenseManagement.Api.Models;

public partial class LicenseManagementContext : DbContext
{
    public LicenseManagementContext()
    {
    }

    public LicenseManagementContext(DbContextOptions<LicenseManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Application> Applications { get; set; }

    public virtual DbSet<ApplicationFile> ApplicationFiles { get; set; }

    public virtual DbSet<ApplicationProduct> ApplicationProducts { get; set; }

    public virtual DbSet<ApplicationStatusHistory> ApplicationStatusHistories { get; set; }

    public virtual DbSet<ApplicationVersion> ApplicationVersions { get; set; }

    public virtual DbSet<FileVersion> FileVersions { get; set; }

    public virtual DbSet<ProductVersion> ProductVersions { get; set; }

    public virtual DbSet<RunningNumber> RunningNumbers { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=LicenseManagementDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Ida);

            entity.HasIndex(e => e.Licensenumber, "IX_App_License");

            entity.HasIndex(e => new { e.Citizenid, e.Identify, e.Datesubmission }, "IX_App_Owner").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.Statusid, e.Type, e.Datesubmission }, "IX_App_Status_Type").IsDescending(false, false, true);

            entity.HasIndex(e => e.Trid, "IX_App_Trid");

            entity.HasIndex(e => new { e.Citizenid, e.Identify }, "IX_Applications_Citizen");

            entity.HasIndex(e => new { e.Citizenid, e.Statusid }, "IX_Applications_Citizen_Status");

            entity.HasIndex(e => e.Dateappv, "IX_Applications_DateAppv");

            entity.HasIndex(e => e.Datesubmission, "IX_Applications_DateSubmission");

            entity.HasIndex(e => e.ReferenceLicenseNumber, "IX_Applications_ReferenceLicense");

            entity.HasIndex(e => new { e.Statusid, e.Datesubmission, e.Ida }, "IX_Applications_Sort").IsDescending(false, true, true);

            entity.HasIndex(e => e.Statusid, "IX_Applications_StatusId");

            entity.HasIndex(e => new { e.Statusid, e.Dateappv }, "IX_Applications_Status_Appv");

            entity.HasIndex(e => new { e.Statusid, e.Datesubmission }, "IX_Applications_Status_Submit");

            entity.HasIndex(e => new { e.Citizenid, e.Identify }, "IX_Applications_Trader");

            entity.HasIndex(e => e.Type, "IX_Applications_Type");

            entity.HasIndex(e => e.Licensenumber, "UX_Applications_LICENSENUMBER")
                .IsUnique()
                .HasFilter("([LICENSENUMBER] IS NOT NULL)");

            entity.HasIndex(e => e.Trid, "UX_Applications_TRID")
                .IsUnique()
                .HasFilter("([TRID] IS NOT NULL)");

            entity.Property(e => e.Ida).HasColumnName("IDA");
            entity.Property(e => e.Citizenid)
                .HasMaxLength(13)
                .HasColumnName("CITIZENID");
            entity.Property(e => e.Citizenname)
                .HasMaxLength(255)
                .HasColumnName("CITIZENNAME");
            entity.Property(e => e.Companyname)
                .HasMaxLength(255)
                .HasColumnName("COMPANYNAME");
            entity.Property(e => e.Dateappv)
                .HasColumnType("datetime")
                .HasColumnName("DATEAPPV");
            entity.Property(e => e.Dateexp)
                .HasColumnType("datetime")
                .HasColumnName("DATEEXP");
            entity.Property(e => e.Datesubmission)
                .HasColumnType("datetime")
                .HasColumnName("DATESUBMISSION");
            entity.Property(e => e.Dateupdate)
                .HasColumnType("datetime")
                .HasColumnName("DATEUPDATE");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .HasColumnName("EMAIL");
            entity.Property(e => e.Identify)
                .HasMaxLength(13)
                .HasColumnName("IDENTIFY");
            entity.Property(e => e.LicenseStatus).HasMaxLength(20);
            entity.Property(e => e.LicenseStatusDate).HasColumnType("datetime");
            entity.Property(e => e.Licensenumber)
                .HasMaxLength(50)
                .HasColumnName("LICENSENUMBER");
            entity.Property(e => e.ReferenceLicenseNumber).HasMaxLength(50);
            entity.Property(e => e.Remark).HasColumnName("REMARK");
            entity.Property(e => e.ReplacedBy).HasMaxLength(50);
            entity.Property(e => e.Statusid).HasColumnName("STATUSID");
            entity.Property(e => e.Statusname)
                .HasMaxLength(50)
                .HasColumnName("STATUSNAME");
            entity.Property(e => e.Tel)
                .HasMaxLength(11)
                .HasColumnName("TEL");
            entity.Property(e => e.Trid)
                .HasMaxLength(50)
                .HasColumnName("TRID");
            entity.Property(e => e.Type).HasColumnName("TYPE");
            entity.Property(e => e.Typenm)
                .HasMaxLength(255)
                .HasColumnName("TYPENM");
            entity.Property(e => e.VersionNo).HasDefaultValue(1);
        });

        modelBuilder.Entity<ApplicationFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Applicat__3214EC07F10F781B");

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.TrId).HasMaxLength(50);
        });

        modelBuilder.Entity<ApplicationProduct>(entity =>
        {
            entity.HasKey(e => e.Ida);

            entity.ToTable("ApplicationProduct");

            entity.HasIndex(e => e.TrId, "IX_ApplicationProduct_TRID");

            entity.Property(e => e.Ida).HasColumnName("IDA");
            entity.Property(e => e.Active).HasColumnName("ACTIVE");
            entity.Property(e => e.Dateinsert)
                .HasColumnType("datetime")
                .HasColumnName("DATEINSERT");
            entity.Property(e => e.Dateupdate)
                .HasColumnType("datetime")
                .HasColumnName("DATEUPDATE");
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION");
            entity.Property(e => e.Modelname)
                .HasMaxLength(255)
                .HasColumnName("MODELNAME");
            entity.Property(e => e.Newcode)
                .HasMaxLength(50)
                .HasColumnName("NEWCODE");
            entity.Property(e => e.Productname).HasColumnName("PRODUCTNAME");
            entity.Property(e => e.Rid).HasColumnName("RID");
            entity.Property(e => e.Statusproduct)
                .HasMaxLength(50)
                .HasColumnName("STATUSPRODUCT");
            entity.Property(e => e.TrId)
                .HasMaxLength(50)
                .HasColumnName("TR_ID");
        });

        modelBuilder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Ida);

            entity.ToTable("ApplicationStatusHistory");

            entity.HasIndex(e => e.Trid, "IX_ApplicationStatusHistory_TRID");

            entity.Property(e => e.Ida).HasColumnName("IDA");
            entity.Property(e => e.Citizenid)
                .HasMaxLength(50)
                .HasColumnName("CITIZENID");
            entity.Property(e => e.Dateupdate)
                .HasColumnType("datetime")
                .HasColumnName("DATEUPDATE");
            entity.Property(e => e.Nameupdate)
                .HasMaxLength(255)
                .HasColumnName("NAMEUPDATE");
            entity.Property(e => e.Statusid).HasColumnName("STATUSID");
            entity.Property(e => e.Statusname)
                .HasMaxLength(50)
                .HasColumnName("STATUSNAME");
            entity.Property(e => e.Trid)
                .HasMaxLength(50)
                .HasColumnName("TRID");
        });

        modelBuilder.Entity<ApplicationVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Applicat__3214EC07C79CDFEB");

            entity.Property(e => e.CreateBy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Tel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TrId).HasMaxLength(50);
        });

        modelBuilder.Entity<FileVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FileVers__3214EC076ECAD71F");

            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.TrId).HasMaxLength(50);
        });

        modelBuilder.Entity<ProductVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductV__3214EC079DEABC9C");

            entity.Property(e => e.ModelName).HasMaxLength(255);
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.TrId).HasMaxLength(50);
        });

        modelBuilder.Entity<RunningNumber>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RunningN__3214EC27E68E2AAA");

            entity.HasIndex(e => e.Prefix, "UQ_RunningNumbers_Prefix").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Dategen).HasColumnType("datetime");
            entity.Property(e => e.Prefix).HasMaxLength(20);
            entity.Property(e => e.Typenumber).HasMaxLength(20);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.Ida);

            entity.ToTable("UserAccount");

            entity.Property(e => e.Ida).HasColumnName("IDA");
            entity.Property(e => e.Citizenid)
                .HasMaxLength(30)
                .HasColumnName("CITIZENID");
            entity.Property(e => e.Companyname)
                .HasMaxLength(255)
                .HasColumnName("COMPANYNAME");
            entity.Property(e => e.Createdate)
                .HasColumnType("datetime")
                .HasColumnName("CREATEDATE");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("EMAIL");
            entity.Property(e => e.Fullname)
                .HasMaxLength(50)
                .HasColumnName("FULLNAME");
            entity.Property(e => e.Identify)
                .HasMaxLength(30)
                .HasColumnName("IDENTIFY");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("PASSWORD");
            entity.Property(e => e.Tel)
                .HasMaxLength(11)
                .HasColumnName("TEL");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("USERNAME");
            entity.Property(e => e.Usertype)
                .HasMaxLength(50)
                .HasComment("Trader, Staff")
                .HasColumnName("USERTYPE");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserSessions_UserId");

            entity.HasIndex(e => e.SessionToken, "UX_UserSessions_SessionToken").IsUnique();

            entity.Property(e => e.SessionToken).HasMaxLength(64);

            entity.HasOne(d => d.User).WithMany(p => p.UserSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSessions_UserAccount");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
