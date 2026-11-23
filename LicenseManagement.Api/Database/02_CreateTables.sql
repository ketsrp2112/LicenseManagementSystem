USE [LicenseManagementDB]
GO


CREATE TABLE [dbo].[Applications](
	[IDA] [int] IDENTITY(1,1) NOT NULL,
	[TRID] [nvarchar](50) NULL,
	[IDENTIFY] [nvarchar](13) NULL,
	[COMPANYNAME] [nvarchar](255) NULL,
	[CITIZENID] [nvarchar](13) NULL,
	[CITIZENNAME] [nvarchar](255) NULL,
	[TYPE] [int] NULL,
	[DATESUBMISSION] [datetime] NULL,
	[STATUSID] [int] NULL,
	[STATUSNAME] [nvarchar](50) NULL,
	[LICENSENUMBER] [nvarchar](50) NULL,
	[TEL] [nvarchar](11) NULL,
	[EMAIL] [nvarchar](50) NULL,
	[DATEUPDATE] [datetime] NULL,
	[REMARK] [nvarchar](max) NULL,
	[DATEAPPV] [datetime] NULL,
	[VersionNo] [int] NOT NULL,
	[TYPENM] [nvarchar](255) NULL,
	[DATEEXP] [datetime] NULL,
	[ReferenceLicenseNumber] [nvarchar](50) NULL,
	[LicenseStatus] [nvarchar](20) NULL,
	[LicenseStatusDate] [datetime] NULL,
	[ReplacedBy] [nvarchar](50) NULL,
 CONSTRAINT [PK_Applications] PRIMARY KEY CLUSTERED 
(
	[IDA] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[Applications] ADD  DEFAULT ((1)) FOR [VersionNo]
GO


CREATE TABLE [dbo].[ApplicationProduct](
	[IDA] [int] IDENTITY(1,1) NOT NULL,
	[NEWCODE] [nvarchar](50) NULL,
	[PRODUCTNAME] [nvarchar](max) NULL,
	[RID] [int] NULL,
	[MODELNAME] [nvarchar](255) NULL,
	[DESCRIPTION] [nvarchar](max) NULL,
	[DATEINSERT] [datetime] NULL,
	[DATEUPDATE] [datetime] NULL,
	[STATUSPRODUCT] [nvarchar](50) NULL,
	[ACTIVE] [int] NULL,
	[TR_ID] [nvarchar](50) NULL,
 CONSTRAINT [PK_ApplicationProduct] PRIMARY KEY CLUSTERED 
(
	[IDA] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[ApplicationFiles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TrId] [varchar](50) NOT NULL,
	[FileName] [varchar](255) NOT NULL,
	[FilePath] [varchar](500) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ApplicationFiles] ADD  DEFAULT (getdate()) FOR [CreatedDate]
GO


CREATE TABLE [dbo].[ApplicationStatusHistory](
	[IDA] [int] IDENTITY(1,1) NOT NULL,
	[STATUSID] [int] NULL,
	[STATUSNAME] [nvarchar](50) NULL,
	[DATEUPDATE] [datetime] NULL,
	[NAMEUPDATE] [nvarchar](255) NULL,
	[CITIZENID] [nvarchar](50) NULL,
	[TRID] [nvarchar](50) NULL,
	[Remark] [nvarchar](max) NULL,
 CONSTRAINT [PK_ApplicationStatusHistory] PRIMARY KEY CLUSTERED 
(
	[IDA] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


CREATE TABLE [dbo].[ApplicationVersions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TrId] [varchar](50) NOT NULL,
	[VersionNo] [int] NOT NULL,
	[Email] [varchar](255) NULL,
	[Tel] [varchar](50) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreateDate] [datetime] NULL,
	[CreateBy] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[ApplicationVersions] ADD  DEFAULT (getdate()) FOR [CreateDate]
GO


CREATE TABLE [dbo].[FileVersions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TrId] [varchar](50) NOT NULL,
	[VersionNo] [int] NOT NULL,
	[FileName] [varchar](255) NULL,
	[FilePath] [varchar](500) NULL,
	[CreateDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[FileVersions] ADD  DEFAULT (getdate()) FOR [CreateDate]
GO

CREATE TABLE [dbo].[ProductVersions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TrId] [varchar](50) NOT NULL,
	[VersionNo] [int] NOT NULL,
	[ProductName] [nvarchar](255) NULL,
	[ModelName] [nvarchar](255) NULL,
	[Description] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


CREATE TABLE [dbo].[RunningNumbers](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Prefix] [nvarchar](20) NOT NULL,
	[LastNumber] [int] NOT NULL,
	[Dategen] [datetime] NOT NULL,
	[Typenumber] [nvarchar](20) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_RunningNumbers_Prefix] UNIQUE NONCLUSTERED 
(
	[Prefix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


CREATE TABLE [dbo].[UserAccount](
	[IDA] [int] IDENTITY(1,1) NOT NULL,
	[USERNAME] [nvarchar](255) NULL,
	[PASSWORD] [nvarchar](255) NULL,
	[CITIZENID] [nvarchar](30) NULL,
	[IDENTIFY] [nvarchar](30) NULL,
	[FULLNAME] [nvarchar](50) NULL,
	[USERTYPE] [nvarchar](50) NULL,
	[CREATEDATE] [datetime] NULL,
	[COMPANYNAME] [nvarchar](255) NULL,
	[TEL] [nvarchar](11) NULL,
	[EMAIL] [nvarchar](255) NULL,
 CONSTRAINT [PK_UserAccount] PRIMARY KEY CLUSTERED 
(
	[IDA] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Trader, Staff' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'UserAccount', @level2type=N'COLUMN',@level2name=N'USERTYPE'
GO



CREATE TABLE dbo.UserSessions
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSessions PRIMARY KEY,
    UserId       INT           NOT NULL,
    SessionToken NVARCHAR(64)  NOT NULL,
    ExpireDate   DATETIME2     NOT NULL,
    CONSTRAINT FK_UserSessions_UserAccount
        FOREIGN KEY (UserId) REFERENCES dbo.UserAccount (Ida)
);

CREATE UNIQUE INDEX UX_UserSessions_SessionToken ON dbo.UserSessions (SessionToken);
CREATE INDEX IX_UserSessions_UserId ON dbo.UserSessions (UserId);

