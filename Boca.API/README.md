# BocaAPI – VCS → Oracle Payroll Time Converter

BocaAPI is a .NET 6 service for the City of Boca Raton Police Department. It picks up
the VCS timekeeping export (CSV), validates it, stores it in SQL Server, and writes an
Oracle payroll import file (`VCSTime_*.csv`). An email summary goes out after each file.

> **Platform constraint:** the application targets **.NET 6.0** and uses the NuGet package
> versions pinned in `Boca.API/BocaAPI.csproj`. Do not upgrade the target framework or the packages.

---

## Solution layout

```
BocaAPI.sln
├── Boca.API/                       Main application (ASP.NET Core Web API + Windows Service)
│   ├── Program.cs                  Host setup: Kestrel, Swagger, DI, Windows Service, hosted Worker
│   ├── Worker.cs                   BackgroundService – polls the input folder every `Folders:Frequency` minutes
│   ├── Controllers/HoursController.cs   REST endpoints (manual trigger / diagnostics)
│   ├── Services/
│   │   ├── BocaService.cs          Core pipeline: read → validate → store → export → email → archive
│   │   ├── Email.cs                SMTP notifications (System.Net.Mail)
│   │   └── ServiceBase.cs          Shared logger base class
│   ├── Repository/BocaRepository.cs    Dapper data access (SQL Server)
│   ├── Validators/PoliceMasterValidator.cs  FluentValidation rules for input rows
│   ├── Extensions/CsvExtensions.cs CsvHelper read/write helpers
│   ├── Models/
│   │   ├── Settings.cs, EmailConfig.cs     Bound from appsettings "Folders" / "EmailConfiguration"
│   │   └── DTO/                    VCSExport (input row), RawExportData (DB row),
│   │                               FinalResult (output row), PoliceCode, Error, ControllerEmail
│   └── appsettings*.json
└── Runner/                         Placeholder console project (not used)
```

## How it works

The same process runs two ways at once:

* **Windows Service / background worker:** `Worker` calls `BocaService.UploadInputFileToDatabase()`
  at startup and then every **`Folders:Frequency` minutes** (20 in the shipped config). If the setting is
  missing, zero or negative, the worker uses 30 minutes.
  After each wait, the worker checks whether the nightly database archive is due (see *Nightly archive* below).
* **HTTP API** on port **9200** (Kestrel), with Swagger UI at `/swagger`, so an operator can trigger
  or inspect things by hand.

### Processing pipeline (`BocaService.UploadInputFileToDatabase`)

1. **Pick up a file.** The service takes the first `*.csv` in `{BaseFilePath}\{InputFilePath}`.
   If there isn't one, it does nothing. It handles one file per run.
2. **Load pay codes.** It reads `dbo.police_codes`, which maps each VCS/Infinium code (`WCPID`) to an Oracle
   payroll time type and an hours-type indicator (A/R).
3. **Parse CSV** into `VCSExport` with CsvHelper. Rows that can't be parsed are written to `ErrorLogs`.
4. **Validate** each row with `PoliceMasterValidator`. `WCPID` must exist in `police_codes`,
   and the validator also checks field lengths, required dates, and `PAYDURAT` precision
   (`RECTYP` may be empty, up to 50 characters).
   Invalid rows are written to `ErrorLogs` (employee, code, date = `STRDT`, hours).
5. **Store.** Valid rows are `MERGE`d into `dbo.police_master`, tagged with the file name and a
   new `InsertId` GUID. The merge key is PAYID, WCPID, WCABR, ROSDT, STRDT, ENDDT and SHFTAB,
   so a row that was already loaded is not inserted again.
6. **Export** (`ExportLatest`). The rows inserted under this `InsertId` are joined to `police_codes`
   and converted to `FinalResult`:
   | Output column | Source |
   |---|---|
   | EmployeeNumber | `PAYID` |
   | AssignmentNumber | `"E" + PAYID` |
   | **Date** | **`STRDT` (shift start date), formatted `MM/dd/yyyy`** |
   | Hours | `PAYDURAT` rounded to 2 decimals. **CTE and CTEJ: `PAYDURAT × 1.5`**, rounded to 2 decimals |
   | HoursTypeIndicator | first char of `police_codes.HourType` |
   | PayrollTimeType | `police_codes.Oracle`, except where the OT rules below apply |
   | StartTime / StopTime | blank |
   | Comments | blank (at Boca's request) |
   | OperationType | `ADD` |

   **Overtime rules** (CTE/CTEJ here means `WCPID` is `CTE` or `CTEJ`):
   * `SHFTAB` is `OT` or `OTC` and the row isn't CTE/CTEJ: `PayrollTimeType` becomes `OVERTIME POLICE`,
     whatever the pay code maps to.
   * These rows get a **second** output row with `PayrollTimeType = "STRAIGHT OT POLICE"`:
     * `SHFTAB` is `OT`/`OTC` and the row isn't CTE/CTEJ
     * `WCPID` is `OT` or `OTC`
   * CTE/CTEJ rows on an OT shift keep their own pay code (e.g. `CBR COMP PLAN FOP`) and get no duplicate.

   Output is sorted by Date, then EmployeeNumber, and written to
   `{BaseFilePath}\{OutputFilePath}\VCSTime_MMddyyyy_HHmm.csv`.
   If the file added any rows, its `InsertId` is also saved to `{BaseFilePath}\last-insert-id.txt`, which
   `GET /api/hours/ExportFile` uses to re-export the latest upload. `police_master` has no insert timestamp,
   so this file is the only record of which batch was uploaded most recently.
7. **Email** a summary to `EmailConfiguration.To`. The subject starts with `SUCCESS` when every record
   loaded, or `FAILURE` with the number of records not loaded. The body shows total records and records
   inserted. When relevant it also shows records skipped because they were already loaded (not a failure),
   records not loaded (unreadable / failed validation / rejected by the database), and comment line breaks
   that were ignored. The numbers always add up to the total.
   * A record the database rejects for bad data (e.g. a value too long for its column) is logged to
     `ErrorLogs`, and the rest of the file keeps loading. A connection failure, timeout or deadlock stops the
     run instead, and the file stays in `input` to be retried next cycle. No email is sent in that case.
   * VCS doesn't quote the `Comment` column, so a line break inside a comment produces lines with no pay code,
     dates or hours. These aren't counted as records and don't cause a `FAILURE`; they are still logged to
     `ErrorLogs`.
8. **Archive.** The input file is moved to `{BaseFilePath}\{ArchiveFilePath}\<name>-<UTC timestamp>.csv`.

Exceptions are caught and logged as Critical to the Windows Event Log (source `_BocaService`).

### Nightly archive (`BocaService.Archive`)

Once a day, on the first worker cycle between 11 PM and midnight (server local time), the worker copies
`police_master` rows with `ROSDate` older than one year into `dbo.archive_police_master`, then deletes them
from `police_master`. A Warning entry is written to the Event Log when this runs.

* It depends on the cycle timing: keep `Frequency` under 60 minutes, or a cycle may never land in the
  11 PM hour. A service restart during 11 PM can trigger one extra, harmless run.
* The copy uses `INSERT … SELECT *`, so `archive_police_master` must have the same columns in the same order
  as `police_master`, plus its leading identity column `arc-id`.
* Errors in this step are swallowed silently: nothing is logged, and the rows stay in `police_master`.

### Input file format

The CSV needs a header row. Current VCS exports use:

```
PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,RECTYP,PAYDURAT,Comment
```

Columns are matched by name, so their order doesn't matter.

* `ROSDT` – roster date. It is stored, used for de-duplication, and decides when a row gets archived.
* `STRDT` – shift start date/time. **This is the date written to the payroll output.** An earlier version
  used `ROSDT`; for overtime worked on a different day than the roster date, the two can differ.
* `ENDDT` – shift end date/time.
* `RECTYP` – **required column**; the value may be empty, and production VCS files send it empty. It is stored in
  `police_master.RecType` (nvarchar(50)), and a value over 50 characters fails validation. A file with a
  `File Date` column instead of `RECTYP` (as some files in September 2026 had) is not loaded: every row is logged as
  unreadable with `Header with name 'RECTYP' was not found`.
* An unquoted line break inside a `Comment` splits the row. The fragment after the break (e.g.
  `2026-008080- Knock and talk,,,,…`) fails to parse and is logged to `ErrorLogs`. The actual time row is unaffected,
  and the email reports the fragment as an ignored line, not a failed record.
* `ROSDT`, `STRDT` and `ENDDT` must fall between 1900-01-01 and 2079-06-06 23:59 (the SQL `smalldatetime` range).
  A record outside it fails validation.

### Database objects (SQL Server)

| Object | Purpose |
|---|---|
| `dbo.police_codes` | Code mapping (`Infinium_Codes`, `Oracle`, `VCS`, `HourType`) |
| `dbo.police_master` | All loaded rows (`PayId, WcpId, WCABR, ReasonCode, Reason, ROSDate, STRDate, ENDDate, SHFTAB, Removed, RecType, PayDuration, Comment, FileName, InsertId`) |
| `dbo.ErrorLogs` | Rejected rows (`Message, TimeStamp, Exception, RowNum, EmployeeNumber, PayrollTimeType, Date, Hours`). `RowNum` is the record's data row number in the CSV, header excluded (first record = 1, i.e. Excel row − 1). `Date` is the record's `STRDT` |
| `dbo.archive_police_master` | Archive for rows more than a year old. Same columns as `police_master`, plus an identity column `arc-id` (see `archiveTable.sql`) |

## Configuration (`appsettings.json`)

| Section | Key | Meaning |
|---|---|---|
| `Kestrel:Endpoints:Http:Url` | | Listen address, default `http://*:9200` |
| `ConnectionStrings` | `BocaDBConnectionString` | SQL Server connection string |
| `Folders` | `BaseFilePath` | Root working folder, e.g. `C:\_boca\police`. The service also writes `last-insert-id.txt` here |
| | `InputFilePath` / `OutputFilePath` / `ArchiveFilePath` | Subfolders under the base path (`input`, `results`, `archive`) |
| | `Frequency` | Minutes between worker runs (default config: 20; if missing or ≤ 0: 30) |
| `EmailConfiguration` | `From`, `SmtpServer`, `Port`, `To` | SMTP settings. `To` is comma-separated. **An empty `SmtpServer` turns email off.** SSL is off. |
| `Logging:EventLog` | | Event Log level (default Warning) |

`appsettings.{Environment}.json` overrides these values. The environment comes from the `ASPNETCORE_ENVIRONMENT`
environment variable. The service reads config from its own folder (`ContentRootPath = AppContext.BaseDirectory`).
The repo also has `appsettings.BocaProduction.json` (production folders `C:\weboracle\PoliceFiles`, SMTP port 25)
and `appsettings - local.json`. The space in that name means .NET never loads the second file automatically;
it's only a template to copy from.

## HTTP API

Base URL: `http://<server>:9200`. There is no authentication, so keep the port closed to anything outside the internal network.

| Method | Route | Description |
|---|---|---|
| GET | `/api/hours/LoadFiles` | Run the full pipeline now instead of waiting for the next worker cycle |
| GET | `/api/hours/GetCodes` | Return `police_codes` (quick DB connectivity check) |
| GET | `/api/hours/GetErrors` | Return all rows in `ErrorLogs` |
| DELETE | `/api/hours/DeleteErrors` | Truncate `ErrorLogs` |
| POST | `/api/hours/SendEmail` | Send a test email. Body: `{ "subject": "...", "body": "..." }` |
| GET | `/api/hours/ExportFile/{Name?}` | Re-export the **most recently uploaded** file (the batch in `last-insert-id.txt`) to `{Name}_MMddyyyy_HHmm.csv` in the results folder, and return its rows as JSON. `Name` defaults to `VCSTime`. Returns **404** if no file has been uploaded since this version was deployed |

Examples:

```powershell
Invoke-RestMethod http://localhost:9200/api/hours/LoadFiles
Invoke-RestMethod http://localhost:9200/api/hours/GetErrors
Invoke-RestMethod -Method Delete http://localhost:9200/api/hours/DeleteErrors
Invoke-RestMethod http://localhost:9200/api/hours/ExportFile            # -> results\VCSTime_<timestamp>.csv
Invoke-RestMethod http://localhost:9200/api/hours/ExportFile/Resend     # -> results\Resend_<timestamp>.csv
Invoke-RestMethod -Method Post http://localhost:9200/api/hours/SendEmail `
  -ContentType 'application/json' -Body '{"subject":"test","body":"hello"}'
```

### Swagger

Swagger UI is on in every environment (it isn't limited to Development), on the same port as the API:

* **UI:** `http://<server>:9200/swagger` (e.g. `http://localhost:9200/swagger` on the server itself)
* **OpenAPI JSON:** `http://<server>:9200/swagger/v1/swagger.json`, which you can import into Postman or similar tools

To call an endpoint from the UI:

1. Open the UI in a browser and expand the endpoint, e.g. **GET /api/hours/GetCodes**.
2. Click **Try it out**.
3. Fill in any parameters (e.g. `Name` for `ExportFile`, or the JSON body for `SendEmail`).
4. Click **Execute**. The response code and body appear below, along with the equivalent `curl` command.

Swagger has no authentication either, and `DeleteErrors` and `LoadFiles` really run, so only open it from trusted machines.

## Build and run locally

You need the .NET 6 SDK/runtime. Newer SDKs can build the project, but the target stays `net6.0`.

```powershell
dotnet build BocaAPI.sln -c Release
dotnet run --project Boca.API            # console mode, Ctrl+C to stop
```

In console mode the worker runs right away. Put a CSV in the configured input folder, and the results
show up in the output folder.

## Tests

`Boca.API.Tests` (xUnit, net6.0) covers CSV reading, validation, the payroll output (STRDT date, CTE/CTEJ and
overtime rules), the email summary, ErrorLogs row numbers, and per-record insert error handling.

```powershell
dotnet test Boca.API.Tests                                     # all tests
dotnet test Boca.API.Tests --filter "Category!=Integration"    # skip the SQL Server tests
```

Tests tagged `Category=Integration` need SQL Server LocalDB (`(localdb)\MSSQLLocalDB`). Each run creates a
throwaway database and drops it afterwards. The other tests use fakes and need no database.

Deployment to production: see [DEPLOYMENT.md](DEPLOYMENT.md).
