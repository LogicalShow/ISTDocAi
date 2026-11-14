using ISTDocAi.Application.Startup;
using System.Diagnostics;

namespace ISTDocAi.UI.Server.Startup;

/// <summary>
/// Orchestrates the execution of all registered startup tasks in stage-ordered sequence.
/// </summary>
public class StartupPipeline(IServiceProvider serviceProvider, ILogger<StartupPipeline> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Startup Pipeline execution...");
        var overallStartTime = Stopwatch.StartNew();

        // Resolve all registered startup tasks with duplicate protection
        // GroupBy concrete type ensures each task executes only once even if registered multiple times
        var tasks = serviceProvider.GetServices<IStartupTask>()
            .GroupBy(t => t.GetType())
            .Select(g => g.First())
            .OrderBy(t => t.Stage)
            .ThenBy(t => t.Order)
            .ToList();

        if (!tasks.Any())
        {
            logger.LogInformation("No startup tasks registered. Startup Pipeline completed.");
            return;
        }

        logger.LogInformation("Found {Count} startup task(s) to execute.", tasks.Count);
        
        // Log the execution plan
        logger.LogInformation("Startup Pipeline Execution Plan:");
        foreach (var task in tasks)
        {
            logger.LogInformation(
                "  → [Stage: {Stage}, Order: {Order}] {TaskName}",
                task.Stage,
                task.Order,
                task.GetType().Name
            );
        }

        foreach (var task in tasks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Startup Pipeline execution cancelled.");
                throw new OperationCanceledException();
            }

            var taskName = task.GetType().Name;
            var stage = task.Stage;
            var order = task.Order;

            logger.LogInformation(
                "[Startup] Executing stage {Stage} task {TaskName}",
                stage,
                taskName
            );

            var taskStopwatch = Stopwatch.StartNew();
            
            try
            {
                await task.ExecuteAsync(serviceProvider, cancellationToken);
                taskStopwatch.Stop();

                logger.LogInformation(
                    "[Stage: {Stage}, Order: {Order}] Task {TaskName} completed successfully in {Duration}ms",
                    stage,
                    order,
                    taskName,
                    taskStopwatch.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                taskStopwatch.Stop();

                // Apply stage-based failure policy
                if (stage <= StartupStage.SystemInitialization)
                {
                    // Critical stages: fail startup
                    logger.LogError(
                        ex,
                        "[Stage: {Stage}, Order: {Order}] CRITICAL task {TaskName} failed after {Duration}ms. Startup aborted.",
                        stage,
                        order,
                        taskName,
                        taskStopwatch.ElapsedMilliseconds
                    );
                    throw;
                }
                else
                {
                    // RuntimeInitialization stage: log and continue
                    logger.LogWarning(
                        ex,
                        "[Stage: {Stage}, Order: {Order}] Non-critical task {TaskName} failed after {Duration}ms. Continuing startup...",
                        stage,
                        order,
                        taskName,
                        taskStopwatch.ElapsedMilliseconds
                    );
                }
            }
        }

        overallStartTime.Stop();
        logger.LogInformation(
            "Startup Pipeline execution completed successfully. Total duration: {Duration}ms",
            overallStartTime.ElapsedMilliseconds
        );
    }
}
