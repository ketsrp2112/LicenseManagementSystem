/* =====================================================================
   03_CreateRelationships.sql
   Database : LicenseManagementDB
   Purpose  : Add foreign keys (via TRID) and indexes. Safe to re-run.
   Run with : sqlcmd -b ...   (-b stops on the first error)
   ===================================================================== */
USE [LicenseManagementDB];
GO

/* ---------------------------------------------------------------------
   STEP 1: Make Applications.TRID a valid parent key
   A FK can only point to a PRIMARY KEY or UNIQUE column, so TRID must be
   NOT NULL and UNIQUE.
   --------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.Applications WHERE TRID IS NULL)
    THROW 50001, 'Applications.TRID contains NULL rows. Fix the data first.', 1;
GO

IF EXISTS (SELECT TRID FROM dbo.Applications GROUP BY TRID HAVING COUNT(*) > 1)
    THROW 50002, 'Applications.TRID contains duplicates. Fix the data first.', 1;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Applications') AND name = N'TRID' AND is_nullable = 1)
    ALTER TABLE dbo.Applications ALTER COLUMN TRID NVARCHAR(50) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Applications_TRID')
    ALTER TABLE dbo.Applications ADD CONSTRAINT UQ_Applications_TRID UNIQUE (TRID);
GO

/* ---------------------------------------------------------------------
   STEP 2: Align data types of child TRID columns
   Parent is NVARCHAR(50); these children are VARCHAR(50) (system_type_id 167).
   A FK requires the same data type, so convert them to NVARCHAR(50).
   --------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ApplicationFiles') AND name = N'TrId' AND system_type_id = 167)
    ALTER TABLE dbo.ApplicationFiles ALTER COLUMN TrId NVARCHAR(50) NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ApplicationVersions') AND name = N'TrId' AND system_type_id = 167)
    ALTER TABLE dbo.ApplicationVersions ALTER COLUMN TrId NVARCHAR(50) NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.FileVersions') AND name = N'TrId' AND system_type_id = 167)
    ALTER TABLE dbo.FileVersions ALTER COLUMN TrId NVARCHAR(50) NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ProductVersions') AND name = N'TrId' AND system_type_id = 167)
    ALTER TABLE dbo.ProductVersions ALTER COLUMN TrId NVARCHAR(50) NOT NULL;
GO

/* ---------------------------------------------------------------------
   STEP 3: Foreign keys  (child.TRID -> Applications.TRID)
   NO ACTION = an application cannot be deleted while it still has
   files, history, or versions (safe default for license records).
   If this step fails with an FK conflict, some child rows have a TRID
   that does not exist in Applications. Find them with:
     SELECT c.* FROM dbo.<ChildTable> c
     LEFT JOIN dbo.Applications a ON a.TRID = c.<TrIdColumn>
     WHERE c.<TrIdColumn> IS NOT NULL AND a.TRID IS NULL;
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ApplicationProduct_Applications')
    ALTER TABLE dbo.ApplicationProduct
    ADD CONSTRAINT FK_ApplicationProduct_Applications
        FOREIGN KEY (TR_ID) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ApplicationFiles_Applications')
    ALTER TABLE dbo.ApplicationFiles
    ADD CONSTRAINT FK_ApplicationFiles_Applications
        FOREIGN KEY (TrId) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ApplicationStatusHistory_Applications')
    ALTER TABLE dbo.ApplicationStatusHistory
    ADD CONSTRAINT FK_ApplicationStatusHistory_Applications
        FOREIGN KEY (TRID) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ApplicationVersions_Applications')
    ALTER TABLE dbo.ApplicationVersions
    ADD CONSTRAINT FK_ApplicationVersions_Applications
        FOREIGN KEY (TrId) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FileVersions_Applications')
    ALTER TABLE dbo.FileVersions
    ADD CONSTRAINT FK_FileVersions_Applications
        FOREIGN KEY (TrId) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductVersions_Applications')
    ALTER TABLE dbo.ProductVersions
    ADD CONSTRAINT FK_ProductVersions_Applications
        FOREIGN KEY (TrId) REFERENCES dbo.Applications (TRID)
        ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

/* ---------------------------------------------------------------------
   STEP 4: Indexes (SQL Server does NOT create indexes for FK columns)
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationProduct_TR_ID')
    CREATE NONCLUSTERED INDEX IX_ApplicationProduct_TR_ID ON dbo.ApplicationProduct (TR_ID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationFiles_TrId')
    CREATE NONCLUSTERED INDEX IX_ApplicationFiles_TrId ON dbo.ApplicationFiles (TrId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationStatusHistory_TRID')
    CREATE NONCLUSTERED INDEX IX_ApplicationStatusHistory_TRID ON dbo.ApplicationStatusHistory (TRID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationVersions_TrId_VersionNo')
    CREATE NONCLUSTERED INDEX IX_ApplicationVersions_TrId_VersionNo ON dbo.ApplicationVersions (TrId, VersionNo);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FileVersions_TrId_VersionNo')
    CREATE NONCLUSTERED INDEX IX_FileVersions_TrId_VersionNo ON dbo.FileVersions (TrId, VersionNo);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductVersions_TrId_VersionNo')
    CREATE NONCLUSTERED INDEX IX_ProductVersions_TrId_VersionNo ON dbo.ProductVersions (TrId, VersionNo);
GO

-- Frequently searched columns on Applications / UserAccount
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Applications_IDENTIFY')
    CREATE NONCLUSTERED INDEX IX_Applications_IDENTIFY ON dbo.Applications (IDENTIFY);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Applications_LICENSENUMBER')
    CREATE NONCLUSTERED INDEX IX_Applications_LICENSENUMBER ON dbo.Applications (LICENSENUMBER);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserAccount_IDENTIFY')
    CREATE NONCLUSTERED INDEX IX_UserAccount_IDENTIFY ON dbo.UserAccount (IDENTIFY);
GO

/* ---------------------------------------------------------------------
   STEP 5 (OPTIONAL, commented out): UserAccount <-> Applications via IDENTIFY
   A direct FK is NOT possible: IDENTIFY is not unique in either table
   (one company has many applications and many user accounts).
   Recommended: add a Traders table that holds each IDENTIFY once, and
   point both tables at it. Uncomment to use.
   --------------------------------------------------------------------- */
/*
IF OBJECT_ID(N'dbo.Traders', N'U') IS NULL
CREATE TABLE dbo.Traders (
    IDENTIFY    NVARCHAR(30)  NOT NULL,
    COMPANYNAME NVARCHAR(255) NULL,
    CONSTRAINT PK_Traders PRIMARY KEY (IDENTIFY)
);
GO

-- Load existing IDs (one row per IDENTIFY)
INSERT INTO dbo.Traders (IDENTIFY, COMPANYNAME)
SELECT IDENTIFY, MAX(COMPANYNAME)
FROM (SELECT IDENTIFY, COMPANYNAME FROM dbo.Applications WHERE IDENTIFY IS NOT NULL
      UNION ALL
      SELECT IDENTIFY, COMPANYNAME FROM dbo.UserAccount  WHERE IDENTIFY IS NOT NULL) x
WHERE NOT EXISTS (SELECT 1 FROM dbo.Traders t WHERE t.IDENTIFY = x.IDENTIFY)
GROUP BY IDENTIFY;
GO

ALTER TABLE dbo.Applications
ADD CONSTRAINT FK_Applications_Traders
    FOREIGN KEY (IDENTIFY) REFERENCES dbo.Traders (IDENTIFY);
GO
ALTER TABLE dbo.UserAccount
ADD CONSTRAINT FK_UserAccount_Traders
    FOREIGN KEY (IDENTIFY) REFERENCES dbo.Traders (IDENTIFY);
GO
*/