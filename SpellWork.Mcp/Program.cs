using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpellWork.Mcp
{
    public static class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // stdout carries JSON-RPC frames and nothing else. Any log line
            // that reaches it corrupts the stream and the client disconnects,
            // so every provider is pinned to stderr.
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services.AddSingleton(McpServerConfig.FromEnvironment());
            builder.Services.AddSingleton<SpellDataService>();

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            await builder.Build().RunAsync();
        }
    }
}
