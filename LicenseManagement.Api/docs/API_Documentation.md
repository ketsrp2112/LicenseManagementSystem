# API Documentation

## 1. Overview

This document summarizes the API routes documented for the **License Management System**.

| Item | Value |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| Language | C# |
| Data access | Entity Framework Core |
| Database | Microsoft SQL Server |
| Account controller | `AccountController` |
| Application controller | `ApplicationsController` |
| Application route prefix | `/api/applications` |
| Account route prefix | `/api/account` |
| Protected application request header | `X-Session-Token` |

> **Documentation scope:** The supplied README lists endpoint methods, paths, purposes, and access notes, but does not provide complete request DTOs, response schemas, HTTP status-code contracts, or sample JSON bodies. Those details are intentionally not invented here. Confirm exact payload fields against the current controller action signatures and models.

## 2. Base URL

Use the HTTPS or HTTP local URL printed by `dotnet run` or configured in Visual Studio. The base URL varies by development and deployment environment.

Example placeholder:

```text
https://localhost:{port}
```

Replace `{port}` with the actual port configured for the running API.

## 3. Authentication

### 3.1 Login

1. Call `POST /api/account/login` with the login credentials expected by the current controller.
2. On successful login, the API creates a cryptographically secure random session token.
3. The token is stored in `UserSessions` with the user ID and expiration time.
4. The session expires 8 hours after login.

### 3.2 Authenticated requests

Include the returned token in the following header for protected application endpoints:

```http
X-Session-Token: {session-token}
```

The API resolves the current user from the session, checks expiration, and enforces role and record-ownership rules. The role displayed by the frontend is not the authority for API permissions.

### 3.3 Logout and password change

- `POST /api/account/logout` removes the current session.
- `PUT /api/account/ChangePasswordByUsername` validates the username and current password; a successful password change invalidates all sessions for that account.
- The password-change endpoint is documented as callable without an existing session. Rate limiting and additional abuse protections are noted as production considerations.

### 3.4 Password rules

The documented new-password rule requires at least 8 characters, including uppercase, lowercase, and a number. Passwords use ASP.NET Core `PasswordHasher<UserAccount>`. Legacy plain-text passwords are upgraded to hashes after a successful login.

## 4. Account Endpoints

All account routes use `/api/account`.

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/register` | Public | Register a new account. The server assigns the `Trader` role. |
| POST | `/login` | Public | Verify credentials, create a session, and return the session token and basic user details. |
| POST | `/logout` | Authenticated session | Remove the current session. |
| GET | `/GetUserId?id={id}` | Staff / Trader | Get user details by ID. Staff can view any user; Trader can view only their own account. |
| PUT | `/ChangePasswordByUsername` | Public endpoint; credentials validated | Change password after validating username and current password. |
| GET | `/session` | Authenticated session | Validate the current session and return the current user's basic identity and role. |

### 4.1 Account endpoint notes

- Registration creates a Trader account; the README does not document public Staff registration.
- Login returns a session token and basic user details.
- Session expiry is 8 hours.
- Changing a password invalidates all sessions for that account.
- Demo usernames `trader_demo` and `officer_demo` cannot change their passwords through the documented password-change endpoint.

## 5. Application Endpoints

All application routes use `/api/applications`. Protected endpoints require `X-Session-Token`.

### 5.1 Application listing and lookup

| Method | Endpoint | Access / notes | Description |
|---|---|---|---|
| GET | `/GetApplications` | Role and ownership rules apply | Paginated application list with keyword, status, type, date, sorting, and Staff card filters. |
| GET | `/GetApplicationStats` | Staff dashboard function | Staff summary and application counts by type. |
| GET | `/GetDataAll` | Role and ownership rules apply | Get all applications visible to the current user. |
| GET | `/Search?keyword={keyword}` | Role and ownership rules apply | Search applications visible to the current user. |
| GET | `/Detail/{trid}` | Accessible application | Get application details, active products, status history, and file metadata. |
| GET | `/LicenseDetail/{licenseNumber}` | Accessible license | Get details of an active license, its products, and files. |
| GET | `/EligibleLicenses?type={type}` | Trader only | Get licenses eligible for Amendment, Renew, or Cancel. |

### 5.2 Create, edit, and submit

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/` | Trader workflow | Create a Draft application. |
| PUT | `/TraderEdit/{trid}` | Trader only | Edit an application and create a version snapshot when applicable. |
| POST | `/Submitted/{trid}` | Trader only | Submit a Draft or resubmitted application. |
| POST | `/TraderCancel/{trid}` | Trader only | Cancel an application with a required remark. |
| PUT | `/UpdateStatus/{trid}` | Staff workflow | Staff review, request revision, reject, or approve an application. |

### 5.3 File operations

| Method | Endpoint | Access / conditions | Description |
|---|---|---|---|
| POST | `/upload/{trid}` | Trader only; allowed statuses: Draft, Revision Required, Applicant Edited | Upload supporting files. |
| DELETE | `/file/{trid}/{fileId}` | Trader only; Draft only | Delete an attached file. |
| GET | `/files/{trid}` | Accessible application | List files for an application. |
| GET | `/file-download/{fileId}` | Session and application access checked | Download an application file. |
| GET | `/version-file-download/{trid}/{version}/{fileName}` | Historical version access | Download a file associated with a historical version. |

Document upload constraints:

- Allowed extensions: PDF, JPG, JPEG, PNG.
- Maximum file size: 10 MB per file.
- Maximum 10 files per upload request.
- File deletion is allowed only while the application is a Draft.

### 5.4 Version history

| Method | Endpoint | Description |
|---|---|---|
| GET | `/VersionHistory/{trid}` | List saved application versions. |
| GET | `/VersionDetail/{trid}/{version}` | Get a historical application snapshot, products, and file metadata. |
| GET | `/version-file-download/{trid}/{version}/{fileName}` | Download a file from a historical version. |

Versioning behavior:

- Editing a Draft does not create a version snapshot.
- Editing a non-Draft application in an editable status captures the current application data, products, and file metadata.
- After the snapshot is saved, the version is incremented and the application status becomes `Applicant Edited`.

### 5.5 Certificate

| Method | Endpoint | Description |
|---|---|---|
| GET | `/Certificate/{trid}` | Generate a PDF certificate for an approved application. |

PDF generation is handled by `CertificateBuilder`.

## 6. Application Types and API Business Rules

| Type | Name | API behavior |
|---:|---|---|
| 1 | New License | Applicant enters product information. Approval generates a new license number and a 5-year validity period. |
| 2 | Amendment | References an active license; product information can be edited. Approval generates a new license number and marks the previous license `Replaced`. |
| 3 | Renew | References an eligible active license expiring within the next 3 months. Products are copied from the original license; applicant can edit attachments only. Approval generates a new license number and extends expiry by 5 years from the previous expiry date. |
| 4 | Cancel | References an active license. Products are copied from the original license; applicant can edit attachments only. Approval marks the original license `LicenseStatus = Cancelled`; no new license number is generated for the cancellation request. |

Additional rules:

- A request must contain at least one valid product before submission.
- Trader ownership is determined from the signed-in user's database record, not identity values supplied by the frontend.
- Staff list access is limited to applications with a non-null `Datesubmission` value.
- A second pending Amendment, Renew, or Cancel request for the same license is prevented while an earlier related request is in statuses 1–5.
- Renew and Cancel copy products from the referenced license; the applicant cannot edit those products through the application edit endpoint.
- Approved New License, Amendment, and Renew applications receive generated product codes where applicable.
- Revision requests and rejection require a Staff remark.

## 7. Application Status Reference

| ID | Status | Meaning / action |
|---:|---|---|
| 1 | Draft | Created by the system. |
| 2 | Submitted | Submitted by Trader. |
| 3 | Officer Review | Staff starts or continues review. |
| 4 | Revision Required | Staff requests corrections; remark required. |
| 5 | Applicant Edited | Trader submits an edited version after revision. |
| 6 | Applicant Cancel | Trader cancels the application; remark required. |
| 7 | Officer Rejected | Staff rejects the application; remark required. |
| 8 | Approved | Staff approves the request. |
| 9 | License Cancel | Status name is defined in the controller mapping. |

**Cancellation distinction:** When a Cancel-type request is approved, the request itself is saved as status ID 8 (`Approved`), while the original license's `LicenseStatus` becomes `Cancelled`. Status ID 9 is defined in the mapping, but the current approval code described by the README does not assign ID 9 to the cancellation request.

## 8. Number Generation

The API generates numbers on the server and stores counters in `RunningNumbers`.

| Number | Format | Example |
|---|---|---|
| Application number (`TRID`) | `TRyyyyMMddNNNN` | `TR202611220001` |
| License number | `LICyyyyMMddNNNN` | `LIC202611220001` |

`RunningNumbers.Prefix` has a unique constraint in the supplied database summary. Confirm the precise concurrency handling for `LastNumber` against the current implementation.

## 9. Request and Response Payloads

The supplied README does not include the exact request DTO definitions or response JSON schemas for these endpoints. Therefore:

- Use the current controller action parameters and request model classes as the authoritative payload contract.
- Do not assume property names, required fields, response wrappers, or error formats based only on endpoint names.
- For multipart upload, use the form field names and binding defined by the current upload action.
- The included `LicenseManagement.Api.http` file can be used to test endpoints from Visual Studio.

Swagger / OpenAPI documentation and example request/response payloads are listed as potential future improvements in the README, not as a confirmed current feature.

## 10. Example Authenticated Request

The following is a header pattern only; replace the host, port, token, and route values with those from the running environment.

```http
GET https://localhost:{port}/api/applications/GetDataAll
X-Session-Token: {session-token}
```

## 11. Access Control Summary

| Operation | Trader | Staff |
|---|---|---|
| Register account | Yes; server assigns Trader role | Public Staff registration is not documented |
| Log in | Yes | Yes |
| View own applications | Yes | Not applicable as a Trader view |
| View submitted application list | Own records | Submitted applications |
| Create application | Yes | Not documented |
| Edit application | Yes, subject to status/type rules | Not documented |
| Upload supporting files | Yes, subject to status rules | Not documented |
| Submit / resubmit | Yes | Not applicable |
| Cancel own application | Yes, remark required | Not applicable |
| Request revision / reject / approve | Not applicable | Yes |
| View eligible licenses | Yes | Not documented |

The table reflects the access notes in the README. Verify any additional role permissions against the current controller implementation.

## 12. Security and Operational Notes

- Use HTTPS and do not expose session tokens in URLs or logs.
- Session tokens expire after 8 hours.
- Password changes invalidate all sessions for the account.
- The password-change endpoint is callable without an existing session; add rate limiting and abuse protections before production use.
- Uploaded files are stored under `wwwroot/uploads`. Review direct public static-file access and consider moving files outside the web root for production.
- File upload writes files to disk before saving metadata; a failure can leave an unreferenced file.
- Application creation and Staff approval use serializable database transactions. Other multi-step operations, including file upload and some status/history updates, are not all wrapped in a single transaction.
- An automated unit/integration test project is not represented in the supplied project structure.

## 13. Testing the API

1. Start the API project.
2. Open `LicenseManagement.Api.http` in Visual Studio.
3. Call the login endpoint.
4. Copy the returned session token into the `X-Session-Token` header for protected application requests.
5. Test the relevant endpoint using the actual request fields defined by the controller/model.
6. Verify results in the application and SQL Server database.

---

**Project:** License Management System  
**API:** ASP.NET Core Web API (.NET 10)  
**Status:** Under active development
