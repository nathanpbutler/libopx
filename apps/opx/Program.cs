using System.CommandLine;

namespace nathanbutlerDEV.opx;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            var rootCommand = new RootCommand("OP-42/OP-47 teletext processing utility for VBI, T42, and MXF data stream formats");

            var filterCommand = Commands.CreateFilterCommand();
            var extractCommand = Commands.CreateExtractCommand();
            var restripeCommand = Commands.CreateRestripeCommand();
            var convertCommand = Commands.CreateConvertCommand();

            rootCommand.Add(filterCommand);
            rootCommand.Add(extractCommand);
            rootCommand.Add(restripeCommand);
            rootCommand.Add(convertCommand);
            
            return rootCommand.Parse(args).Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}