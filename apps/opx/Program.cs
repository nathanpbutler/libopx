using System.CommandLine;

namespace nathanbutlerDEV.opx;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var rootCommand = new RootCommand("OP-42/OP-47 teletext processing utility for VBI, T42, and MXF data stream formats (Enhanced with Async Support)");

            var filterCommand = Commands.CreateFilterCommand();
            var extractCommand = Commands.CreateExtractCommand();
            var restripeCommand = Commands.CreateRestripeCommand();
            var convertCommand = Commands.CreateConvertCommand();

            rootCommand.Add(filterCommand);
            rootCommand.Add(extractCommand);
            rootCommand.Add(restripeCommand);
            rootCommand.Add(convertCommand);
            
            // Setup cancellation for Ctrl+C
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true;
                cts.Cancel();
                Console.Error.WriteLine("\nOperation cancelled by user.");
            };

            return await rootCommand.Parse(args).InvokeAsync(cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Application was cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
