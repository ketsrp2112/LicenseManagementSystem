# Database Scripts

SQL scripts to create the `MyAppDb` database from scratch, including tables, relationships, indexes, and demo data. You do not need access to any existing database: run the scripts in order on any SQL Server instance and you are ready to go.

## Prerequisites

- SQL Server 2019 or later (Express, Developer, or Docker image all work)
- One of the following tools:
  - [`sqlcmd`](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility) (command line)
  - SQL Server Management Studio (SSMS) or Azure Data Studio (GUI)
- Permission to create databases on the target server

## Scripts and Execution Order

**Run the scripts in this exact order.** Each script is idempotent, so it is safe to run it more than once.

| Order | File | Purpose |
|-------|------|---------|
| 1 | `01_CreateDatabase.sql` | Creates the `MyAppDb` database if it does not exist |
| 2 | `02_CreateTables.sql` | Creates tables, primary keys, identity columns, and constraints |
| 3 | `03_CreateRelationships.sql` | Adds foreign keys and indexes |
| 4 | `04_SampleData.sql` | Inserts sample data for demos (fake data only) |

## How to Run

### Option A: sqlcmd with Windows Authentication

```bash
sqlcmd -S localhost -E -i 01_CreateDatabase.sql
sqlcmd -S localhost -E -d MyAppDb -i 02_CreateTables.sql
sqlcmd -S localhost -E -d MyAppDb -i 03_CreateRelationships.sql
sqlcmd -S localhost -E -d MyAppDb -i 04_SampleData.sql
```

### Option B: sqlcmd with SQL Authentication

```bash
sqlcmd -S localhost -U sa -P "<YourPassword>" -i 01_CreateDatabase.sql
sqlcmd -S localhost -U sa -P "<YourPassword>" -d MyAppDb -i 02_CreateTables.sql
sqlcmd -S localhost -U sa -P "<YourPassword>" -d MyAppDb -i 03_CreateRelationships.sql
sqlcmd -S localhost -U sa -P "<YourPassword>" -d MyAppDb -i 04_SampleData.sql
```

If you get a certificate error on a local or Docker instance, add `-C` to trust the server certificate.

### Option C: SSMS or Azure Data Studio

1. Connect to your SQL Server instance.
2. Open `01_CreateDatabase.sql` and click **Execute**.
3. Repeat for `02`, `03`, and `04` in order.

Save and open the files as UTF-8 so Thai characters in the sample data display correctly.

### Option D: Docker (no local SQL Server needed)

Start a SQL Server container:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sqlserver -d \
  mcr.microsoft.com/mssql/server:2022-latest
```

Then run the scripts against it:

```bash
for f in 01_CreateDatabase.sql 02_CreateTables.sql 03_CreateRelationships.sql 04_SampleData.sql; do
  sqlcmd -S localhost,1433 -U sa -P "YourStrong!Passw0rd" -C -i "$f"
done
```

The scripts contain their own `USE MyAppDb;` statement, so the `-d` flag is optional here.

## Verify the Setup

Run these queries to confirm everything was created correctly.

```sql
USE MyAppDb;

-- Tables with no primary key (expected: 0 rows)
SELECT t.name
FROM sys.tables t
WHERE NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = t.object_id AND i.is_primary_key = 1
);

-- All foreign keys
SELECT fk.name AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk;

-- Row counts
SELECT COUNT(*) AS Customers FROM dbo.Customers;
SELECT COUNT(*) AS Orders    FROM dbo.Orders;
```

## Reset the Database

To start over from a clean state:

```sql
USE master;
ALTER DATABASE MyAppDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE MyAppDb;
```

Then run the four scripts again in order.

## Connect Your Application

Use a connection string like the one below, and keep real credentials out of source control (use user secrets or environment variables).

```
Server=localhost;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True;
```

## Notes

- All sample data is fictional. Emails use the `@example.com` domain.
- Do not run `04_SampleData.sql` against a production database.
- Constraint names follow a prefix convention: `PK_`, `FK_`, `UQ_`, `DF_`, `CK_`; indexes use `IX_`.

## Troubleshooting

| Problem | Likely cause and fix |
|---------|----------------------|
| `Cannot open database "MyAppDb"` | `01_CreateDatabase.sql` has not been run yet, or it failed |
| `Foreign key constraint conflict` | Scripts were run out of order; follow the order above |
| Thai text appears as `???` | File was not saved or opened as UTF-8, or the `N'...'` prefix is missing |
| `Login failed for user` | Check the authentication mode and credentials |
| SSL/certificate error | Add `-C` to `sqlcmd`, or `TrustServerCertificate=True` to the connection string |