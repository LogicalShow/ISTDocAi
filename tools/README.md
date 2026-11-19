# ISTDocAi tools

All projects build together in `ISTDocAi.sln`. Run commands from the repository root.

| Tool | User | Guide |
|---|---|---|
| DevSetup | Developer workstation | [Developer setup](docs/DEV-SETUP-AR.md) |
| OperatorTool | IST operator: allowances, reports, models, Azure | [Operator guide](docs/OPERATOR-AR.md) |
| ServerSetup | IT on the IST DocAi server | [Server setup](docs/SERVER-SETUP-AR.md) |
| KitSetup | IT on the customer kit server | [Kit setup](docs/KIT-SETUP-AR.md) |

## Run and verify

Start Visual Studio as Administrator, open `ISTDocAi.sln`, select DevSetup or OperatorTool as startup project, and press Ctrl+F5. Alternatively use an elevated terminal:

```powershell
dotnet run --project tools/src/ISTDocAi.DevSetup
dotnet run --project tools/src/ISTDocAi.OperatorTool -- --help
```

Runtime prompts still ask for product and kit paths. Developers receive docai-dev.pfx and docai-dev.json from Anter together; the PFX password arrives separately.
Use the SDK in global.json:

```powershell
dotnet restore ISTDocAi.sln --locked-mode
dotnet build ISTDocAi.sln -c Release --no-restore -warnaserror
dotnet test tools/tests/ISTDocAi.Tools.Tests -c Release --no-build --no-restore --filter 'TestCategory!=Sql&TestCategory!=Machine' --logger trx --results-directory TestResults/tools-ci
python .github/scripts/check-trx.py TestResults/tools-ci
```

## Publish

Set `ISTDocAi.Tools` as Startup Project and press F5/Ctrl+F5 for the publish-only menu. To debug a tool, set that tool project as Startup and press F5; DevSetup needs Visual Studio as Administrator.

```powershell
dotnet run --project tools/src/ISTDocAi.Tools
dotnet run --project tools/src/ISTDocAi.Tools -- publish all
dotnet run --project tools/src/ISTDocAi.Tools -- publish OperatorTool
```

Requires a repository checkout and the .NET SDK. Fixed outputs in `tools/publish/<Name>` are overwritten; close old executables first. All four runs sequentially, continues after failures, and prints a summary. The menu offers Open folder/Start it after success, or Open folder for all four. CLI has no prompts: 0 success, 1 failure/refused, 2 usage. Use `--help` for usage.

Raw commands for CI/IT:


Publish ServerSetup and KitSetup for delivery to servers; run them in an elevated terminal. Help/status remain available without elevation. One command per executable:

```powershell
dotnet publish tools/src/ISTDocAi.DevSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/DevSetup
dotnet publish tools/src/ISTDocAi.OperatorTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/OperatorTool
dotnet publish tools/src/ISTDocAi.ServerSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/ServerSetup
dotnet publish tools/src/ISTDocAi.KitSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/KitSetup
```

Single-file publish writes its temporary lock under obj/ to preserve committed locks across SDK patches. Embedded SQL supports `--export-sql FOLDER`; OperatorTool statements embed Infrastructure SQL directly. Azure templates live in tools/azure/infra; regenerate main.json with Bicep after editing main.bicep. PDFsharp-MigraDoc stays pinned to 6.2.4. SQL/Machine categories require explicit external prerequisites and are excluded from ordinary CI.

OperatorTool: double-click the exe or press F5 with no arguments for the interactive menu. Submenus cover pages/balance, customer reports, IST reports, models, and setup/administration; 9 shows all CLI commands, c changes SQL connection, and 0 exits (or goes back within a submenu). Actions return to the same submenu. Reverse/document actions offer a numbered top-up picker with manual fallback; reversed grants can be reported but cannot be reversed again. Sensitive actions require lowercase `yes` (surrounding whitespace accepted). CLI validation and preview confirmations remain authoritative; command-line usage remains available.

Customer reports by local calendar date (read-only SQL; no kit secrets needed):

```text
pages statement --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--pdf FILE | --json] [--company-name NAME] [--force]
pages documents-by-date --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--csv FILE | --json] [--force]
```

Both accept `--sql-server SERVER --database DATABASE --trust-server-certificate`. Dates are inclusive, strictly `yyyy-MM-dd`, with at most 3660 calendar days (future dates allowed). Time zone is required every time: Egypt uses Cairo DST rules, Saudi Arabia uses Riyadh time. Boundaries are local midnight converted to a half-open UTC range; invalid midnight advances to the transition instant, ambiguous midnight chooses the earlier UTC instant. Statements show local and UTC periods, reconciled ledger balances, up to 1000 movements, and daily charged usage. Documents = charged processing runs. Reserved pages are excluded. CSV streams through a temporary file and replaces the destination only on success; JSON refuses more than 10000 documents and directs the operator to CSV. `--generate-sql`, `--grant-id`, and `--audience` are refused for these two commands. Omitting a file selects JSON.

Customer reports menu: 1) statement by dates (PDF), 2) documents by dates (CSV), 3) statement by top-up (PDF), 4) documents by top-up (CSV), 5) service API report (kit secrets required), 0) Back. Date routes ask consumer, time zone, From, To (defaults to today in that zone), output file, then SQL connection if needed. Default files are `Documents\ISTDocAi\<consumer>-statement-<from>-to-<to>.pdf` or `<consumer>-documents-<from>-to-<to>.csv`.


### Azure cost per page (IST internal)

```text
report azure-cost --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--consumer CODE] [--pdf FILE | --csv FILE | --json] [--force] [--sql-server SERVER --database DB --trust-server-certificate]
```

IST internal ? do not send to the customer. Omit consumer for all customers. No audience or SQL-script option. Menu: IST reports ? 1) Azure cost per page by dates (PDF or CSV), 2) IST internal report by top-up (PDF), 3) IST documents with Azure cost by top-up (CSV), 0) Back. The time zone is requested every time.

This is a PAYG list-price ESTIMATE; the Azure invoice is authoritative. Commitment tiers can exceed the estimate; discounts can differ. Charged cohorts use charge recording dates and ALL usage for the same consumer/job/run. Uncharged usage uses occurrence dates and excludes any run with a recorded charge at report time. Frozen costs are retained; missing prices are recovered by usage date when possible. Unresolved pages make per-page values incomplete (*); zero charged pages displays '-'. Totals divide total charged estimated cost by total charged pages. Current price list dates are effective dates, not retrieval timestamps; future prices are shown separately.


### Customer priced usage statement

```text
report customer-price --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi --price-per-page AMOUNT --currency EGP|USD|SAR [--pdf FILE | --csv FILE | --json] [--company-name NAME] [--force] [--sql-server SERVER] [--database DB] [--trust-server-certificate]
```

All six selection/rate options are required. The rate uses invariant decimal syntax, is greater than zero and at most 100000, with at most four decimal places. Currency is EGP, USD or SAR; each uses two decimals. Each charged processing run is priced separately and rounded to two decimals away from zero; totals and daily amounts sum those rounded lines. This is a usage statement at the entered rate, not a tax invoice. No output option prints JSON (maximum 10000 documents); PDF shows the first 1000 documents; CSV streams the full list with a TOTAL row. Company names use the existing customer-safe text rules.

Customer reports menu: 1 date statement PDF; 2 date documents CSV; 3 priced date statement PDF/CSV; 4 top-up statement PDF; 5 top-up documents CSV; 6 service API report; 0 Back. Choice 3 asks for consumer, time zone, dates, currency, rate, format, destination and connection. Zone and currency are selected every time.

### Azure price refresh visibility (read only)

`ISTDocAi.OperatorTool.exe prices show [--json] --sql-server SERVER --database ISTDocAi [--trust-server-certificate]`

Menu: **3) IST reports ? 4) Azure prices (read only)**. Shows current/scheduled USD prices per 1000 pages, source, latest completed check, per-meter latest outcome, last update, and open warnings. No `--generate-sql` option. SELECT only; the tool never writes prices or history.

After setting/changing a connection, the main menu caches status once; showing Azure prices refreshes the cache. Refused/Failed runs warn until a later NoChange/Updated for that meter. A completed check older than 26 hours warns that the job is not running. Missing history, never-run jobs and reader errors produce no banner. The screen explains unavailable history or SELECT permission failure. The refresh job runs every 12 hours when enabled (disabled on dev machines); there is no percentage-change guard.

Azure cost reports include open/stale warnings in console output, bold PDF text under the estimate notice, JSON `priceCheckWarnings`, and a CSV metadata column joined with ` | `. Status is a separate read alongside the unchanged cost-query snapshot, so the status may be slightly newer than the costs.

Check https://prices.azure.com and the Azure pricing page. Investigate network/firewall/proxy, job deployment/enabling, permissions, or the displayed conflict. If a manual price is required, authorized DB staff can insert a future-dated row (example only; not executed by OperatorTool):

```sql
INSERT dbo.AzureMeterPrices(MeterCode, EffectiveFrom, PricePer1000, Currency, Source)
VALUES ('DI_CUSTOM_EXTRACT', '2099-01-01', 35.0000, 'USD', 'Manual price verified by operator');
```

Replace the example date with the intended **future UTC date** and verify the meter/price first. The job never overwrites an existing row. Older servers show `Price check history not available (service not updated yet)`; update the service/migrations through the normal deployment process.

المشغّلون أو فريق IT محتاجين Windows login بصلاحية `db_owner` على قاعدة بيانات DocAi.
