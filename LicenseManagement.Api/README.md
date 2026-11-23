# License Management System

A web-based license application management system built with **C#**, **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core**, and **Microsoft SQL Server**. The frontend uses **HTML, CSS, and vanilla JavaScript** and is served from the API project through `wwwroot`.

The system supports the application lifecycle for medical-device establishment licenses: applicants create and submit applications, staff officers review them, and approved requests update license records and can produce PDF certificates. The application also includes role-based data access, database-backed sessions, document uploads, status history, and version snapshots.

> **Project status:** Under active development. This README describes the implementation represented by the current `AccountController` and `ApplicationsController` code. Some setup details and database scripts may need to be aligned with the latest local project files before publishing.

## 1. Key Features

| Area | Current implementation |
|---|---|
| User roles | `Trader` (applicant) and `Staff` (officer) |
| Account management | Registration, login, logout, user lookup, password change, and session validation |
| Password security | ASP.NET Core `PasswordHasher<UserAccount>`; legacy plain-text passwords are upgraded to hashes after a successful login |
| Session management | Random session tokens stored in `UserSessions`; sessions expire after 8 hours |
| Server-side authorization | The API resolves the current user from the session token and checks the user's role and record ownership |
| Application types | New License, Amendment, Renew, and Cancel |
| Application workflow | Draft, submission, officer review, revision, applicant cancellation, rejection, and approval |
| Search and listing | Keyword search, status/type/date filters, sorting, pagination, and staff summary statistics |
| Product management | One or more products per application, including product name, model, description, and generated product code |
| Document management | PDF/JPG/JPEG/PNG uploads; server-side extension and size validation; maximum 10 files per upload request |
| Version history | Snapshots of application details, products, and file metadata when a non-draft application is edited |
| License management | Active-license lookup, eligibility checks for Amendment/Renew/Cancel, replacement/cancellation handling, and renewal validity rules |
| Number generation | Server-generated application and license numbers using a database running-number table |
| PDF certificate | Generates a PDF certificate for an approved application |

### Application types

| Type | Name | Behavior |
|---|---|---|
| 1 | New License | Applicant enters product information. On approval, the system generates a new license number and sets a 5-year validity period. |
| 2 | Amendment | References an active license. Product information can be edited. On approval, a new license number is generated and the previous license is marked `Replaced`. |
| 3 | Renew | References an eligible active license expiring within the next 3 months. Products are copied from the original license and the applicant can edit attachments only. On approval, a new license number is generated and the expiry date is extended by 5 years from the previous expiry date. |
| 4 | Cancel | References an active license. Products are copied from the original license and the applicant can edit attachments only. On approval, the original license's `LicenseStatus` becomes `Cancelled`; no new license number is generated for the cancellation request. |

The API also prevents a second pending Amendment, Renew, or Cancel request for the same license while an earlier related request is in statuses 1–5.

### Application status workflow

| ID | Status | Set by / meaning |
|---|---|---|
| 1 | Draft | System creates the application |
| 2 | Submitted | Trader submits the application |
| 3 | Officer Review | Staff starts or continues review |
| 4 | Revision Required | Staff requests corrections; a remark is required |
| 5 | Applicant Edited | Trader submits an edited version after a revision request |
| 6 | Applicant Cancel | Trader cancels the application; a remark is required |
| 7 | Officer Rejected | Staff rejects the application; a remark is required |
| 8 | Approved | Staff approves the request |
| 9 | License Cancel | Status name defined in the controller |

**Cancellation behavior:** When a Cancel-type request is approved, the request itself is saved with status `Approved` (ID 8), while the original license's `LicenseStatus` is set to `Cancelled`. Status ID 9 is defined in the status mapping, but the current approval code does not assign ID 9 to the cancellation request.

## 2. Technology Stack

| Category | Technology |
|---|---|
| Language | C# |
| Backend | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core |
| Database | Microsoft SQL Server |
| Frontend | HTML5, CSS3, vanilla JavaScript |
| API communication | REST-style HTTP endpoints with JSON; multipart form data for file uploads |
| Authentication | Database-backed session token |
| Password hashing | ASP.NET Core Identity `PasswordHasher<TUser>` |
| PDF generation | `CertificateBuilder` |
| Development tools | Visual Studio, SQL Server Management Studio, Git / GitHub |

## 3. System Architecture

The frontend pages and JavaScript files are served from `wwwroot`. JavaScript calls the ASP.NET Core Web API. Entity Framework Core handles database access, while uploaded documents are stored in `wwwroot/uploads` and their metadata is stored in SQL Server.

```text
+-----------------------------------------+
|              Web Frontend               |
|       HTML / CSS / Vanilla JavaScript   |
|                 wwwroot                 |
+-----------------------------------------+
                     |
                     | HTTP / JSON
                     | X-Session-Token
                     v
+-----------------------------------------+
|           ASP.NET Core Web API           |
|                 .NET 10                 |
|                                         |
|  +-----------------------------------+  |
|  | Controllers                       |  |
|  | - AccountController               |  |
|  | - ApplicationsController          |  |
|  +-----------------------------------+  |
|                                         |
|  +-----------------------------------+  |
|  | Entity Framework Core             |  |
|  | LicenseManagementContext          |  |
|  +-----------------------------------+  |
|                                         |
|  +-----------------------------------+  |
|  | CertificateBuilder                |  |
|  | PDF certificate generation        |  |
|  +-----------------------------------+  |
+-----------------------------------------+
              |                 |
              v                 v
+------------------------+  +---------------------------+
| File Storage           |  | Microsoft SQL Server      |
| wwwroot/uploads        |  |                           |
| PDF / JPG / JPEG / PNG |  | Applications              |
|                        |  | ApplicationProduct        |
+------------------------+  | ApplicationFiles          |
                            | ApplicationStatusHistory  |
                            | ApplicationVersions       |
                            | ProductVersions           |
                            | FileVersions              |
                            | UserAccount               |
                            | UserSessions              |
                            | RunningNumbers            |
                            +---------------------------+
```

### Authentication and authorization flow

1. A user registers or signs in through `AccountController`.
2. After successful login, the API creates a random session token and stores it with the user ID and an expiry time in `UserSessions`.
3. The frontend sends the token in the `X-Session-Token` request header when calling protected application endpoints.
4. The API looks up the session, checks that it has not expired, and loads the associated user from `UserAccount`.
5. The API checks the role and data ownership on the server. A Trader can access their own applications; Staff can access submitted applications.
6. Logout removes the current session. Changing a password removes all sessions belonging to that account.

The role shown in the frontend is for presentation only. The API uses the user record associated with the database session to determine permissions.

## 4. Project Structure

The following is the project structure represented in the current README/source snapshot. Update filenames if the local project has changed.

```text
LicenseManagement/
│
├── LicenseManagement.Api/
│   ├── Controllers/
│   │   ├── AccountController.cs
│   │   └── ApplicationsController.cs
│   │
│   ├── Models/
│   │   ├── Application.cs
│   │   ├── ApplicationFile.cs
│   │   ├── ApplicationProduct.cs
│   │   ├── ApplicationStatusHistory.cs
│   │   ├── ApplicationVersion.cs
│   │   ├── FileVersion.cs
│   │   ├── LicenseManagementContext.cs
│   │   ├── ProductVersion.cs
│   │   ├── RunningNumber.cs
│   │   ├── UserAccount.cs
│   │   └── UserSession.cs
│   │
│   ├── Services/
│   │   └── Session authorization helpers
│   │
│   ├── wwwroot/
│   │   ├── CSS/
│   │   ├── js/
│   │   ├── partials/
│   │   ├── uploads/                 # uploaded documents; do not commit
│   │   ├── application-create.html
│   │   ├── application-detail.html
│   │   ├── application-edit.html
│   │   ├── application-version.html
│   │   ├── applications.html
│   │   ├── index.html
│   │   ├── login.html
│   │   └── register.html
│   │
│   ├── Properties/
│   ├── appsettings.json
│   ├── CertificateBuilder.cs
│   ├── Program.cs
│   └── LicenseManagement.Api.http
│
├── Database/                        # SQL scripts, if included in the repository
├── README.md
└── .gitignore
```

## 5. Database Design

The following schema is based on the supplied SQL Server DDL for `LicenseManagementDB`. Table and column names below follow the database definition. The application uses Entity Framework Core to access these tables.

### Tables and key columns

| Table | Key columns / purpose |
|---|---|
| `Applications` | `IDA` (identity primary key), `TRID`, applicant/company details, `TYPE`, `STATUSID`, `STATUSNAME`, `LICENSENUMBER`, `DATESUBMISSION`, `DATEAPPV`, `DATEEXP`, `VersionNo`, `ReferenceLicenseNumber`, `LicenseStatus`, `LicenseStatusDate`, and `ReplacedBy` |
| `ApplicationProduct` | `IDA` (identity primary key), `TR_ID` (application reference value), `NEWCODE`, `PRODUCTNAME`, `MODELNAME`, `DESCRIPTION`, `STATUSPRODUCT`, and `ACTIVE` |
| `ApplicationFiles` | `Id` (identity primary key), `TrId`, `FileName`, `FilePath`, and `CreatedDate` |
| `ApplicationStatusHistory` | `IDA` (identity primary key), `TRID`, `STATUSID`, `STATUSNAME`, `DATEUPDATE`, `NAMEUPDATE`, `CITIZENID`, and `Remark` |
| `ApplicationVersions` | `Id` (identity primary key), `TrId`, `VersionNo`, `Email`, `Tel`, `Remark`, `CreateDate`, and `CreateBy` |
| `ProductVersions` | `Id` (identity primary key), `TrId`, `VersionNo`, `ProductName`, `ModelName`, and `Description` |
| `FileVersions` | `Id` (identity primary key), `TrId`, `VersionNo`, `FileName`, `FilePath`, and `CreateDate` |
| `RunningNumbers` | `ID` (identity primary key), `Prefix` (unique), `LastNumber`, `Dategen`, and `Typenumber` |
| `UserAccount` | `IDA` (identity primary key), `USERNAME`, `PASSWORD`, `CITIZENID`, `IDENTIFY`, `FULLNAME`, `USERTYPE`, `CREATEDATE`, `COMPANYNAME`, `TEL`, and `EMAIL` |
| `UserSessions` | `Id` (identity primary key), `UserId`, `SessionToken`, and `ExpireDate` |

### Relationships and constraints in the supplied DDL

- `UserSessions.UserId` has a foreign key to `UserAccount.IDA`.
- `UserSessions.SessionToken` has a unique index; `UserSessions.UserId` also has a non-unique index.
- `RunningNumbers.Prefix` has a unique constraint.
- The supplied DDL does **not** define foreign-key constraints from `ApplicationProduct`, `ApplicationFiles`, `ApplicationStatusHistory`, `ApplicationVersions`, `ProductVersions`, or `FileVersions` to `Applications`. These tables use `TRID` / `TrId` as logical application references in the supplied schema.
- The supplied DDL does **not** define a unique constraint or index on `Applications.TRID`.
- `Applications.VersionNo` has a default value of `1`; `ApplicationFiles.CreatedDate`, `ApplicationVersions.CreateDate`, and `FileVersions.CreateDate` default to `GETDATE()`.

**Important table-name detail:** the account table is named `dbo.UserAccount` (singular), not `dbo.UserAccounts`. The session foreign key must reference the actual table and primary-key column:

```sql
CONSTRAINT FK_UserSessions_UserAccount
    FOREIGN KEY (UserId) REFERENCES dbo.UserAccount (IDA)
```

Use `IDA` with the same spelling as the column in the table definition. This avoids the earlier invalid-table reference to `dbo.UserAccounts` and also keeps the identifier consistent if the database uses a case-sensitive collation.

### Application versioning

- New applications start with `VersionNo = 1` through the column default.
- Editing a Draft does not create a version snapshot.
- When a Trader edits an application in a non-draft editable status, the current application data, products, and file metadata are copied to the version tables.
- After the snapshot is saved, the application version is incremented and its status becomes `Applicant Edited`.
- Historical file metadata retains the stored file path so that files from earlier versions can be retrieved through the version-file download endpoint.

### Number generation

The API generates numbers on the server and stores counters in `RunningNumbers`.

| Number | Format | Example |
|---|---|---|
| Application number (`TRID`) | `TRyyyyMMddNNNN` | `TR202611220001` |
| License number | `LICyyyyMMddNNNN` | `LIC202611220001` |

The supplied DDL enforces uniqueness on `RunningNumbers.Prefix`. The number generator should also handle concurrent requests safely when reading and updating `LastNumber`; the exact locking/transaction behavior depends on the current implementation in the API code.

## 6. Account and Session Features

### Account endpoints

All account routes use the prefix `/api/account`.

| Method | Endpoint | Description | Access |
|---|---|---|---|
| POST | `/register` | Register a new account. The server assigns the role as `Trader`. | Public |
| POST | `/login` | Verify credentials, create a session, and return the session token and basic user details. | Public |
| POST | `/logout` | Remove the current session. | Authenticated session |
| GET | `/GetUserId?id={id}` | Get user details by ID. Staff can view any user; Trader can view only their own account. | Staff / Trader |
| PUT | `/ChangePasswordByUsername` | Change password after validating username and current password. | Public endpoint; credentials are validated |
| GET | `/session` | Validate the current session and return the current user's basic identity and role. | Authenticated session |

### Password and session rules

- Passwords are hashed with ASP.NET Core `PasswordHasher<UserAccount>`.
- New passwords must contain at least 8 characters, including uppercase, lowercase, and a number.
- When a user with a legacy plain-text password signs in successfully, the API replaces it with a password hash.
- Session tokens are generated using a cryptographically secure random number generator.
- A session expires 8 hours after login.
- Expired sessions for the signing-in user are removed during login.
- Changing a password invalidates all sessions for that account.
- The demo usernames `trader_demo` and `officer_demo` cannot change their passwords through the change-password endpoint.

## 7. Application API Overview

All application routes use the prefix `/api/applications`. Protected endpoints require the `X-Session-Token` header.

| Method | Endpoint | Description |
|---|---|---|
| GET | `/GetApplications` | Paginated application list with keyword, status, type, date, sorting, and staff card filters |
| GET | `/GetApplicationStats` | Staff dashboard summary and application counts by type |
| GET | `/GetDataAll` | Get all applications visible to the current user |
| GET | `/Search?keyword={keyword}` | Search visible applications |
| GET | `/Detail/{trid}` | Get application details, active products, status history, and file metadata |
| GET | `/LicenseDetail/{licenseNumber}` | Get details of an active license, its products, and files |
| GET | `/EligibleLicenses?type={type}` | Get eligible licenses for Amendment, Renew, or Cancel (Trader only) |
| POST | `/` | Create a Draft application |
| POST | `/upload/{trid}` | Upload supporting files (Trader only) |
| DELETE | `/file/{trid}/{fileId}` | Delete an attached file while the application is a Draft (Trader only) |
| GET | `/files/{trid}` | List files for an accessible application |
| GET | `/file-download/{fileId}` | Download a file after session and application-access checks |
| PUT | `/TraderEdit/{trid}` | Edit an application and create a version snapshot when applicable (Trader only) |
| GET | `/VersionHistory/{trid}` | List saved application versions |
| GET | `/VersionDetail/{trid}/{version}` | Get a historical application snapshot, products, and file metadata |
| GET | `/version-file-download/{trid}/{version}/{fileName}` | Download a file from a historical version |
| POST | `/Submitted/{trid}` | Submit a Draft or resubmitted application (Trader only) |
| POST | `/TraderCancel/{trid}` | Cancel an application with a required remark (Trader only) |
| PUT | `/UpdateStatus/{trid}` | Staff review, request revision, reject, or approve an application |
| GET | `/Certificate/{trid}` | Generate a PDF certificate for an approved application |

### Important application rules

- Trader ownership is determined from the signed-in user's database record, not from applicant identity values supplied by the frontend.
- Staff list access is limited to applications that have been submitted (`Datesubmission` is not null).
- A request must contain at least one valid product before submission.
- New License and Amendment accept product details from the applicant.
- Renew and Cancel copy product details from the referenced license; the applicant cannot edit those products through the application edit endpoint.
- Rejection and revision requests require a staff remark.
- Only Draft applications allow file deletion.
- File upload is allowed while the application is in Draft, Revision Required, or Applicant Edited status.
- The upload endpoint accepts PDF, JPG, JPEG, and PNG files, with a maximum of 10 MB per file and 10 files per request.
- Approved New License, Amendment, and Renew applications receive generated product codes where applicable.

## 8. Installation & Setup

### Prerequisites

- Visual Studio 2022 or later
- .NET 10 SDK
- Microsoft SQL Server or SQL Server LocalDB
- SQL Server Management Studio (recommended)
- Git

### Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/YOUR_USERNAME/LicenseManagementSystem.git
   cd LicenseManagementSystem
   ```

2. Restore dependencies:

   ```bash
   dotnet restore
   ```

3. Open the solution in Visual Studio.

4. Configure the SQL Server connection string in `appsettings.json` or the appropriate development configuration. The connection-string key must match the key read by `Program.cs`.

   Example for SQL Server LocalDB:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=LicenseManagementDB;Trusted_Connection=True;TrustServerCertificate=True"
     }
   }
   ```

5. Make sure the database schema exists and includes the tables required by the current `LicenseManagementContext`, including `UserAccount`, `UserSessions`, and `RunningNumbers`. Run the project's SQL setup scripts if they are included.

6. Run the API project:

   ```bash
   dotnet run
   ```

7. Open the local URL shown in the terminal or configured in Visual Studio.

> **Setup note:** Confirm the actual connection-string key in `Program.cs` and use SQL scripts that match the current EF Core models. The example above is illustrative; it should not be treated as confirmation that the project currently uses `DefaultConnection`.

### API testing

The `LicenseManagement.Api.http` file can be used to test API endpoints from Visual Studio. For protected endpoints, first log in and include the returned token in the `X-Session-Token` request header.

## 9. Security Notes and Current Limitations

The current implementation includes server-side session validation and role/ownership checks. The following considerations remain relevant before production deployment:

- **Session transport:** The frontend sends the session token in a custom `X-Session-Token` header. Protect the application with HTTPS and avoid exposing tokens in logs or URLs.
- **Session lifecycle:** Sessions expire after 8 hours. The current login flow removes expired sessions for the user who signs in; a separate scheduled cleanup process is not documented in the supplied code.
- **Password migration:** The login code supports upgrading legacy plain-text passwords to hashes. Existing legacy credentials should be migrated and handled carefully before production use.
- **Password change flow:** `ChangePasswordByUsername` is callable without an existing session and validates the username and old password in the request. Consider rate limiting and additional abuse protections before production use.
- **File storage:** Uploaded files are stored under `wwwroot/uploads`. Downloads through the API check the session and application access, but the storage location should be reviewed to ensure files cannot be served directly as public static content. For production, consider moving files outside the web root.
- **File/database consistency:** File upload writes files to disk before saving their metadata to the database. A failure during the process can leave an unreferenced file; cleanup and transactional coordination can be improved.
- **Transactions:** Application creation and staff approval use serializable database transactions. Other multi-step operations, such as file upload and some status/history updates, are not all wrapped in a single transaction.
- **Input validation:** The API validates key account fields, product fields, file extensions, file sizes, and selected workflow rules. Continue reviewing validation and error handling as features evolve.
- **Automated tests:** An automated unit/integration test project is not represented in the supplied project structure.
- Never commit real passwords, connection credentials, personal data, or confidential uploaded documents. Keep `wwwroot/uploads/` excluded from source control.

## 10. Potential Future Improvements

- Add automated unit and integration tests for authentication, role access, application workflows, and number generation.
- Add rate limiting and additional protection for login and password-change endpoints.
- Review session revocation, expiration cleanup, and secure token storage on the frontend.
- Move uploaded files outside `wwwroot` and retain authenticated file access.
- Improve file/database consistency and add cleanup for orphaned files.
- Add Swagger / OpenAPI documentation and example request/response payloads.
- Add database setup scripts and sanitized demo data that match the current EF Core models.
- Add CI/CD and deployment configuration.

## 11. Project Status

**Under active development.**

The current implementation includes account registration and login, database-backed sessions, server-side role checks, application creation and processing, license lifecycle rules, file handling, version history, and PDF certificate generation.

---

**Developer:** Puengphuk Ketsaraporn

**Project:** License Management System

**Technology:** C# | ASP.NET Core Web API | .NET 10 | Entity Framework Core | SQL Server | HTML | CSS | JavaScript
