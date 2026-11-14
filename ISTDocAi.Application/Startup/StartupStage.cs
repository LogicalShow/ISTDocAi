namespace ISTDocAi.Application.Startup;

public enum StartupStage
{
    PreMigration = 10,
    DatabaseMigration = 20,
    DataInitialization = 30,
    SystemInitialization = 40,
    RuntimeInitialization = 50 
}
