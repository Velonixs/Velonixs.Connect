using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Velonixs.Connect.Api.HostedServices;

public sealed class FacebookMcpHostedService : IHostedService
{
    private readonly ILogger<FacebookMcpHostedService> logger;
    private readonly IHostEnvironment env;
    private Process? process;

    public FacebookMcpHostedService(ILogger<FacebookMcpHostedService> logger, IHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var autoStart = Environment.GetEnvironmentVariable("MCP_AUTOSTART");
        if (!string.Equals(autoStart, "true", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("MCP auto-start not enabled (MCP_AUTOSTART != true). Skipping.");
            return Task.CompletedTask;
        }

        var scriptRelative = Environment.GetEnvironmentVariable("MCP_START_SCRIPT") ?? ".tools/facebook-mcp/start-mcp.ps1";
        var scriptPath = Path.GetFullPath(Path.Combine(env.ContentRootPath ?? AppContext.BaseDirectory, scriptRelative));

        if (!File.Exists(scriptPath))
        {
            logger.LogWarning("MCP start script not found: {ScriptPath}", scriptPath);
            return Task.CompletedTask;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            process = Process.Start(psi);
            if (process is null)
            {
                logger.LogWarning("Failed to start MCP process.");
                return Task.CompletedTask;
            }

            process.OutputDataReceived += (s, e) => { if (e.Data is not null) logger.LogInformation("[MCP] {Line}", e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            logger.LogInformation("Started MCP process (PID={Pid})", process.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start MCP process.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                logger.LogInformation("Stopping MCP process (PID={Pid})", process.Id);
                process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error stopping MCP process.");
        }

        return Task.CompletedTask;
    }
}
