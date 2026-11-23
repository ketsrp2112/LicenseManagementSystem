
USE [LicenseManagementDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

------------------------------------------------------------
-- 1. UserAccount (3 rows)
------------------------------------------------------------
INSERT INTO dbo.UserAccount
(
    USERNAME, PASSWORD, CITIZENID, IDENTIFY,
    FULLNAME, USERTYPE, CREATEDATE,
    COMPANYNAME, TEL, EMAIL
)
VALUES
(
    N'trader01', N'DEMO_ONLY_123', N'1100000000001',
    N'0105567000001', N'สมชาย ใจดี',
    N'Trader', '2026-11-01',
    N'ABC Medical Co., Ltd.', N'0812345678',
    N'trader01@example.com'
),
(
    N'trader02', N'DEMO_ONLY_123', N'1100000000002',
    N'0105567000002', N'สมหญิง รักดี',
    N'Trader', '2026-11-02',
    N'Health Plus Co., Ltd.', N'0823456789',
    N'trader02@example.com'
),
(
    N'staff01', N'DEMO_ONLY_123', N'1100000000003',
    N'0105567000003', N'เจ้าหน้าที่ ทดสอบ',
    N'Staff', '2026-11-03',
    NULL, N'0834567890',
    N'staff01@example.com'
);

------------------------------------------------------------
-- 2. Applications (3 rows)
------------------------------------------------------------
INSERT INTO dbo.Applications
(
    TRID, IDENTIFY, COMPANYNAME,
    CITIZENID, CITIZENNAME,
    [TYPE], DATESUBMISSION,
    STATUSID, STATUSNAME, LICENSENUMBER,
    TEL, EMAIL, DATEUPDATE, REMARK,
    DATEAPPV, VersionNo, TYPENM,
    DATEEXP, ReferenceLicenseNumber,
    LicenseStatus, LicenseStatusDate, ReplacedBy
)
VALUES
(
    N'TR202611200009',
    N'0105567000001',
    N'ABC Medical Co., Ltd.',
    N'1100000000001',
    N'สมชาย ใจดี',
    1, '2026-11-20',
    3, N'Approved',
    N'LIC202611200009',
    N'0812345678',
    N'trader01@example.com',
    '2026-11-20',
    N'New License - Approved',
    '2026-11-20',
    1, N'New License',
    '2028-11-20',
    NULL,
    N'Active',
    '2026-11-20',
    NULL
),
(
    N'TR202611200010',
    N'0105567000002',
    N'Health Plus Co., Ltd.',
    N'1100000000002',
    N'สมหญิง รักดี',
    2, '2026-11-20',
    2, N'Under Review',
    N'LIC202611200010',
    N'0823456789',
    N'trader02@example.com',
    '2026-11-20',
    N'Amendment - Under Review',
    NULL,
    1, N'Amendment',
    '2028-12-31',
    N'LIC202611200001',
    N'Active',
    '2026-01-01',
    NULL
),
(
    N'TR202611200011',
    N'0105567000001',
    N'ABC Medical Co., Ltd.',
    N'1100000000001',
    N'สมชาย ใจดี',
    3, '2026-11-20',
    1, N'Submitted',
    NULL,
    N'0812345678',
    N'trader01@example.com',
    '2026-11-20',
    N'Renew - Waiting for review',
    NULL,
    1, N'Renew',
    NULL,
    N'LIC202611200001',
    N'Active',
    '2026-11-20',
    NULL
);

------------------------------------------------------------
-- 3. ApplicationProduct (3 rows)
------------------------------------------------------------
INSERT INTO dbo.ApplicationProduct
(
    NEWCODE, PRODUCTNAME, RID,
    MODELNAME, DESCRIPTION,
    DATEINSERT, DATEUPDATE,
    STATUSPRODUCT, ACTIVE, TR_ID
)
VALUES
(
    N'PRD001', N'Digital Blood Pressure Monitor',
    NULL, N'BP-100',
    N'เครื่องวัดความดันโลหิตแบบดิจิทัล',
    '2026-11-20', '2026-11-20',
    N'Active', 1, N'TR202611200009'
),
(
    N'PRD002', N'Patient Monitor',
    NULL, N'PM-200',
    N'เครื่องติดตามสัญญาณชีพผู้ป่วย',
    '2026-11-20', '2026-11-20',
    N'Active', 1, N'TR202611200010'
),
(
    N'PRD003', N'Infrared Thermometer',
    NULL, N'TH-300',
    N'เครื่องวัดอุณหภูมิอินฟราเรด',
    '2026-11-20', '2026-11-20',
    N'Pending', 1, N'TR202611200011'
);

------------------------------------------------------------
-- 4. ApplicationFiles (3 rows)
------------------------------------------------------------
INSERT INTO dbo.ApplicationFiles
(
    TrId, FileName, FilePath, CreatedDate
)
VALUES
(
    'TR202611200009',
    'Company_Registration.pdf',
    '/uploads/TR202611200009/Company_Registration.pdf',
    '2026-11-20'
),
(
    'TR202611200010',
    'Product_Details.pdf',
    '/uploads/TR202611200010/Product_Details.pdf',
    '2026-11-20'
),
(
    'TR202611200011',
    'Renewal_Document.pdf',
    '/uploads/TR202611200011/Renewal_Document.pdf',
    '2026-11-20'
);

------------------------------------------------------------
-- 5. ApplicationStatusHistory (3 rows)
------------------------------------------------------------
INSERT INTO dbo.ApplicationStatusHistory
(
    STATUSID, STATUSNAME, DATEUPDATE,
    NAMEUPDATE, CITIZENID, TRID, Remark
)
VALUES
(
    3, N'Approved', '2026-11-20 15:00:00',
    N'เจ้าหน้าที่ ทดสอบ', N'1100000000003',
    N'TR202611200009',
    N'Application approved'
),
(
    2, N'Under Review', '2026-11-20 14:00:00',
    N'เจ้าหน้าที่ ทดสอบ', N'1100000000003',
    N'TR202611200010',
    N'Document verification in progress'
),
(
    1, N'Submitted', '2026-11-20 10:00:00',
    N'สมชาย ใจดี', N'1100000000001',
    N'TR202611200011',
    N'Application submitted successfully'
);

------------------------------------------------------------
-- 6. ApplicationVersions (3 rows)
------------------------------------------------------------
INSERT INTO dbo.ApplicationVersions
(
    TrId, VersionNo, Email, Tel,
    Remark, CreateDate, CreateBy
)
VALUES
(
    'TR202611200009', 1,
    'trader01@example.com', '0812345678',
    N'Initial application',
    '2026-11-20', 'trader01'
),
(
    'TR202611200010', 1,
    'trader02@example.com', '0823456789',
    N'Initial amendment application',
    '2026-11-20', 'trader02'
),
(
    'TR202611200011', 1,
    'trader01@example.com', '0812345678',
    N'Initial renewal application',
    '2026-11-20', 'trader01'
);

------------------------------------------------------------
-- 7. FileVersions (3 rows)
------------------------------------------------------------
INSERT INTO dbo.FileVersions
(
    TrId, VersionNo, FileName, FilePath, CreateDate
)
VALUES
(
    'TR202611200009', 1,
    'Company_Registration.pdf',
    '/uploads/TR202611200009/Company_Registration.pdf',
    '2026-11-20'
),
(
    'TR202611200010', 1,
    'Product_Details.pdf',
    '/uploads/TR202611200010/Product_Details.pdf',
    '2026-11-20'
),
(
    'TR202611200011', 1,
    'Renewal_Document.pdf',
    '/uploads/TR202611200011/Renewal_Document.pdf',
    '2026-11-20'
);

------------------------------------------------------------
-- 8. ProductVersions (3 rows)
------------------------------------------------------------
INSERT INTO dbo.ProductVersions
(
    TrId, VersionNo,
    ProductName, ModelName, Description
)
VALUES
(
    'TR202611200009', 1,
    N'Digital Blood Pressure Monitor',
    N'BP-100',
    N'เครื่องวัดความดันโลหิตแบบดิจิทัล'
),
(
    'TR202611200010', 1,
    N'Patient Monitor',
    N'PM-200',
    N'เครื่องติดตามสัญญาณชีพผู้ป่วย'
),
(
    'TR202611200011', 1,
    N'Infrared Thermometer',
    N'TH-300',
    N'เครื่องวัดอุณหภูมิอินฟราเรด'
);

------------------------------------------------------------
-- 9. RunningNumbers (3 rows)
------------------------------------------------------------
INSERT INTO dbo.RunningNumbers
(
    Prefix, LastNumber, Dategen, Typenumber
)
VALUES
(
    N'TR202611200009', 9, '2026-11-20', N'TR'
),
(
    N'LIC02611200009', 9, '2026-11-20', N'LIC'
),
(
    N'TR202611200010', 10, '2026-11-20', N'TR'
);

COMMIT TRANSACTION;
GO

------------------------------------------------------------
-- 10. Verify sample data
------------------------------------------------------------
SELECT 'Applications' AS TableName, COUNT(*) AS Total
FROM dbo.Applications
UNION ALL
SELECT 'ApplicationProduct', COUNT(*)
FROM dbo.ApplicationProduct
UNION ALL
SELECT 'ApplicationFiles', COUNT(*)
FROM dbo.ApplicationFiles
UNION ALL
SELECT 'ApplicationStatusHistory', COUNT(*)
FROM dbo.ApplicationStatusHistory
UNION ALL
SELECT 'ApplicationVersions', COUNT(*)
FROM dbo.ApplicationVersions
UNION ALL
SELECT 'FileVersions', COUNT(*)
FROM dbo.FileVersions
UNION ALL
SELECT 'ProductVersions', COUNT(*)
FROM dbo.ProductVersions
UNION ALL
SELECT 'RunningNumbers', COUNT(*)
FROM dbo.RunningNumbers
UNION ALL
SELECT 'UserAccount', COUNT(*)
FROM dbo.UserAccount;
GO
