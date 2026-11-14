# ISTDocAi

A .NET 10 application with SQL Server persistence and an Angular client retained outside the backend solution.

Setup tooling lives in the separate ISTDocAi.Setup repository.

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
