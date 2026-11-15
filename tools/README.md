# ISTDocAi tools

The separate tools solution lives inside ISTDocAi. Run commands from the repository root.

| Tool | User | Guide |
|---|---|---|
| DevSetup | Developer workstation | [Developer setup](docs/DEV-SETUP-AR.md) |
| OperatorTool | IST operator: allowances, reports, models, Azure | [Operator guide](docs/OPERATOR-AR.md) |
| ServerSetup | IT on the IST DocAi server | [Server setup](docs/SERVER-SETUP-AR.md) |
| KitSetup | IT on the customer kit server | [Kit setup](docs/KIT-SETUP-AR.md) |

## Run and verify

Start Visual Studio as Administrator, open `tools/ISTDocAi.Tools.sln`, select DevSetup or OperatorTool as startup project, and press Ctrl+F5. Alternatively use an elevated terminal:

```powershell
dotnet run --project tools/src/ISTDocAi.DevSetup
dotnet run --project tools/src/ISTDocAi.OperatorTool -- --help
```

Runtime prompts still ask for product and kit paths. Developers receive docai-dev.pfx and docai-dev.json from Anter together; the PFX password arrives separately.
Use the SDK in global.json:

```powershell
dotnet restore tools/ISTDocAi.Tools.sln --locked-mode
dotnet build tools/ISTDocAi.Tools.sln -c Release --no-restore -warnaserror
dotnet test tools/tests/ISTDocAi.Tools.Tests -c Release --no-build --no-restore --filter 'TestCategory!=Sql&TestCategory!=Machine' --logger trx --results-directory TestResults/tools-ci
python .github/scripts/check-trx.py TestResults/tools-ci
```

## Publish

Publish ServerSetup and KitSetup for delivery to servers; run them in an elevated terminal. Help/status remain available without elevation. One command per executable:

```powershell
dotnet publish tools/src/ISTDocAi.DevSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/DevSetup
dotnet publish tools/src/ISTDocAi.OperatorTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/OperatorTool
dotnet publish tools/src/ISTDocAi.ServerSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/ServerSetup
dotnet publish tools/src/ISTDocAi.KitSetup -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o tools/publish/KitSetup
```

Single-file publish writes its temporary lock under obj/ to preserve committed locks across SDK patches. Embedded SQL supports `--export-sql FOLDER`; OperatorTool statements embed Infrastructure SQL directly. Azure templates live in tools/azure/infra; regenerate main.json with Bicep after editing main.bicep. PDFsharp-MigraDoc stays pinned to 6.2.4. SQL/Machine categories require explicit external prerequisites and are excluded from ordinary CI.
