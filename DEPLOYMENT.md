# Deploying BocaAPI to Windows Server 2019 (Production)

BocaAPI runs as a **Windows Service** (`_BocaService`) that hosts its own HTTP server
(Kestrel, port 9200). It does not need IIS.

> **Do not upgrade.** The app targets **.NET 6.0**, and its NuGet versions are pinned in `BocaAPI.csproj`.
> Deploy it as-is on the .NET 6 runtime.

---

## 1. Server prerequisites (one-time)

1. **ASP.NET Core 6.0 Runtime (x64)**
   * Download the *ASP.NET Core Runtime 6.0.x – Windows x64 installer* (or the *Hosting Bundle*) from
     https://dotnet.microsoft.com/download/dotnet/6.0. Use the latest 6.0.x patch.
   * Verify:
     ```powershell
     dotnet --list-runtimes    # must list Microsoft.AspNetCore.App 6.0.x and Microsoft.NETCore.App 6.0.x
     ```
   * Other runtimes (8.0 etc.) can be installed alongside it. The app only uses 6.0.x.
2. **Network access** from the server to:
   * SQL Server (default TCP 1433)
   * the SMTP relay (`relay.ci.boca-raton.fl.us`, port set in `EmailConfiguration:Port`)
3. **Service account.** Use a domain or local account (or `LocalSystem` / a gMSA) with:
   * Modify rights on the base working folder (`BaseFilePath`, where it writes `last-insert-id.txt`) and on its
     input, results and archive subfolders
   * Read/execute rights on the install folder
   * If SQL uses Windows auth, a SQL login with rights on `police_master`, `police_codes` and `ErrorLogs`
4. **Database objects** (`police_codes`, `police_master`, `ErrorLogs`, `archive_police_master`) exist, and
   `police_codes` has current data, including CTE/CTEJ mappings. See README → *Database objects*.
   `police_master.RecType` must be `nvarchar(50) NULL`, as in `archiveTable.sql`. It now receives the `File Date`
   value (e.g. `PPE 09/13`) or `" "`. Check it on the production database:
   ```sql
   SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
   FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'police_master' AND COLUMN_NAME = 'RecType';
   ```

## 2. Build the release package (on a build/dev machine)

From the repository root:

```powershell
dotnet publish Boca.API\BocaAPI.csproj -c Release -r win-x64 --self-contained false -o .\publish
```

* `--self-contained false` produces a framework-dependent build that runs on the installed .NET 6 runtime.
  This is how the current `publishcode` folder was built.
* Do **not** add package updates or `dotnet add package` / `dotnet outdated` steps.
* Before copying, check `publish\BocaAPI.runtimeconfig.json`: it must show `"version": "6.0.0"` for `Microsoft.NETCore.App`.

Copy the `publish` folder to the server, e.g. to `C:\weboracle\localdeploy\`.

## 3. Configure for production

Edit `appsettings.json` in the deployed folder. Do not commit production secrets back to git.

```jsonc
{
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://*:9200" } } },
  "ConnectionStrings": {
    "BocaDBConnectionString": "Data Source=<prod-sql>;Initial Catalog=<db>;User ID=<user>;Password=<pwd>;"
    // or: "Data Source=<prod-sql>;Initial Catalog=<db>;Integrated Security=True;"
  },
  "Folders": {
    "BaseFilePath": "C:\\weboracle\\PoliceFiles",   // production working root
    "InputFilePath": "input",
    "OutputFilePath": "results",
    "ArchiveFilePath": "archive",
    "Frequency": 20                                  // minutes between runs; missing/<=0 -> 30
  },
  "EmailConfiguration": {
    "From": "oraclesvc@CI.BOCA-RATON.FL.US",
    "SmtpServer": "relay.ci.boca-raton.fl.us",   // empty string = email disabled
    "Port": 25,                                   // as in appsettings.BocaProduction.json
    "To": "user1@ci.boca-raton.fl.us,user2@ci.boca-raton.fl.us"
  }
}
```

Create the working folders and grant the service account access:

```powershell
$base = 'C:\weboracle\PoliceFiles'
'input','results','archive' | ForEach-Object { New-Item -ItemType Directory -Force "$base\$_" }
icacls $base /grant "DOMAIN\svc_boca:(OI)(CI)M"
icacls C:\weboracle\localdeploy /grant "DOMAIN\svc_boca:(OI)(CI)RX"
```

To use an environment-specific file such as `appsettings.BocaProduction.json`, set a machine-level
environment variable. Restart the service after setting it:

```powershell
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','BocaProduction','Machine')
```

## 4. Install the Windows Service (first deployment)

Run in an **elevated** PowerShell:

```powershell
sc.exe create "_BocaService" binPath= "C:\weboracle\localdeploy\BocaAPI.exe" start= delayed-auto DisplayName= "Boca VCS Payroll Service"
sc.exe description "_BocaService" "Converts VCS police time exports into Oracle payroll import files"
sc.exe failure "_BocaService" reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Run as the service account (skip to keep LocalSystem):
sc.exe config "_BocaService" obj= "DOMAIN\svc_boca" password= "<password>"

# Event Log source used by the app (one-time; LocalSystem can create it automatically, others cannot)
New-EventLog -LogName Application -Source "_BocaService" -ErrorAction SilentlyContinue

sc.exe start "_BocaService"
```

Open the firewall port **only** if other machines need to call the API. The API has no authentication.

```powershell
New-NetFirewallRule -DisplayName "BocaAPI 9200" -Direction Inbound -Protocol TCP -LocalPort 9200 `
  -Action Allow -RemoteAddress <admin-subnet>
```

## 5. Upgrading an existing installation

```powershell
sc.exe stop "_BocaService"
# back up the current build and config
Copy-Item C:\weboracle\localdeploy "C:\weboracle\localdeploy_bak_$(Get-Date -f yyyyMMdd_HHmm)" -Recurse
# copy new files, but keep the production appsettings*.json
robocopy .\publish C:\weboracle\localdeploy /E /XF appsettings*.json
sc.exe start "_BocaService"
```

Check whether the new release added configuration keys. If it did, merge them into the production `appsettings.json` by hand.

**Rollback:** stop the service, copy the `localdeploy_bak_*` folder back, then start the service.

## 6. Verify the deployment

```powershell
sc.exe query "_BocaService"                                   # STATE : RUNNING
Invoke-RestMethod http://localhost:9200/api/hours/GetCodes    # returns pay codes -> DB OK
Invoke-RestMethod -Method Post http://localhost:9200/api/hours/SendEmail `
  -ContentType 'application/json' -Body '{"subject":"BocaAPI deploy test","body":"ok"}'   # email OK
```

In a browser on the server, open `http://localhost:9200/swagger`. It should list the six `/api/hours/*` endpoints,
and you can run the checks above from there as well (see README → *Swagger*).

End-to-end smoke test:

1. Copy a small known CSV into `<BaseFilePath>\input`.
2. Call `GET http://localhost:9200/api/hours/LoadFiles`, or wait up to `Frequency` minutes.
3. Expect all of the following:
   * `VCSTime_MMddyyyy_HHmm.csv` in `results`, checked as follows:
     * Its `Date` column equals each row's **STRDT** date.
     * CTE/CTEJ hours are ×1.5.
     * OT-shift rows appear as `OVERTIME POLICE` plus a `STRAIGHT OT POLICE` copy.
   * The input file moved to `archive` with a timestamp suffix.
   * A summary email.
   * `GET /api/hours/GetErrors` shows only the rows you expected to be rejected.
   * `<BaseFilePath>\last-insert-id.txt` exists. `GET /api/hours/ExportFile` then writes a second
     `VCSTime_*.csv` with the same rows (if the first export ran in the same minute, it overwrites that file).

Logs: **Event Viewer → Windows Logs → Application**, source `_BocaService`. The worker writes a
Warning entry at the start of each cycle.

## 7. Uninstall

```powershell
sc.exe stop "_BocaService"
sc.exe delete "_BocaService"
```

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Service fails to start, event says *"You must install or update .NET"* | ASP.NET Core **6.0** runtime (x64) is missing |
| Service runs but nothing happens | No `*.csv` in the input folder, wrong `BaseFilePath`, or the service account can't read the folder |
| Every row rejected with `Header with name '…' was not found` | A required column (PAYID, WCPID, ROSDT, STRDT, ENDDT, SHFTAB, PAYDURAT…) is missing or renamed in the VCS export. `RECTYP`/`File Date` is optional |
| Rows like `2026-008080- Knock and talk,,,…` rejected with *"The conversion cannot be performed"* | A multi-line comment split the row in the VCS export. Harmless: the actual time row still loads |
| `police_master` never shrinks / `archive_police_master` stays empty | Check the Event Log for the nightly *"Archiving police_master records…"* entry. If it's missing, `Frequency` is probably ≥ 60. If it's there but nothing moved, the `INSERT … SELECT *` likely failed silently: compare the columns of `archive_police_master` and `police_master` |
| Rows rejected as invalid `WCPID` | Code missing from `dbo.police_codes`. Add it to the table; no code change needed |
| No email | `SmtpServer` is empty, the relay rejects the server's IP, or the port is wrong. Check Event Log Critical entries |
| Port 9200 already in use | Change `Kestrel:Endpoints:Http:Url` |
| `ExportFile` returns 404 *"No uploaded file to export yet"* | No file has added rows since deployment, so `last-insert-id.txt` doesn't exist yet. Load a file first. If it still fails, the service account can't write to `BaseFilePath` |
| `ExportFile` returns 200 but `[]` (older builds only) | Known bug fixed on branch `fix/exportfile-latest`: the old build used the file name as the batch id. Deploy the fixed build |
| `/swagger` doesn't load | Service isn't running, or the port/firewall is blocking it. Check `sc.exe query "_BocaService"` and `Kestrel:Endpoints:Http:Url` |
