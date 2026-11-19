# ISTDocAi

A .NET 10 application with SQL Server persistence and an Angular client retained outside the backend solution.

Product and tools projects build together in `ISTDocAi.sln`.

Open `ISTDocAi.sln` in Visual Studio. To run the backend, use the `ISTDocAiLocal` launch profile or `dotnet run --project ISTDocAi.UI/ISTDocAi.UI.Server` after configuring local settings.

Database-free verification:

```sh
bash .github/scripts/check-config.sh
dotnet restore ISTDocAi.sln --locked-mode
dotnet build ISTDocAi.sln -c Release --no-restore -warnaserror
dotnet test ISTDocAi.sln -c Release --no-build --no-restore --filter "TestCategory!=Sql&TestCategory!=Machine" --logger trx --results-directory TestResults/ci
python .github/scripts/check-trx.py TestResults/ci
```

Sql and Machine tests require explicitly configured disposable infrastructure. Runtime startup applies migrations and starts background services.

## Tools

Developer, server, kit, and operator projects are in the `tools` solution folder. See [tools guide](tools/README.md).

Set `ISTDocAi.Tools` as Startup Project and press F5/Ctrl+F5 for the publish-only menu, or run `dotnet run --project tools/src/ISTDocAi.Tools -- publish all`. Output is overwritten in `tools/publish/<Name>`; close an existing executable before publishing again. The launcher requires a repo checkout and the .NET SDK.

To debug a tool, set that tool project as Startup Project and press F5. DevSetup requires Visual Studio running as Administrator.
