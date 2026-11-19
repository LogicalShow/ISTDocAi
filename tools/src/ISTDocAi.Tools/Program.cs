using ISTDocAi.Tools;

return await new Launcher(new ProcessRunner(), Console.In, Console.Out)
    .RunAsync(args, Console.IsInputRedirected, AppContext.BaseDirectory, Environment.CurrentDirectory);
