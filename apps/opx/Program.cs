using System.CommandLine;

namespace nathanbutlerDEV.opx;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("OP-42/OP-47 teletext processing utility for VBI, T42, and MXF data stream formats");

        var filterCommand = await Commands.CreateFilterCommand();
        var extractCommand = await Commands.CreateExtractCommand();
        var restripeCommand = await Commands.CreateRestripeCommand();

        rootCommand.Add(filterCommand);
        rootCommand.Add(extractCommand);
        rootCommand.Add(restripeCommand);
        
        return await rootCommand.Parse(args).InvokeAsync();
    }
}