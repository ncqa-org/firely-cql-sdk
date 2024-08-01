using Hl7.Cql.Packaging;
using Hl7.Cql.Packaging.ResourceWriters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hl7.Cql.Elm.Visitor;

public static class Program
{

    private static string[] supportedArgs = new[] { "build", "b" };

    public static int Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        if (args.Length == 0 || config["?"] != null || config["h"] != null || config["help"] != null)
            return ShowHelp();

        if (config.AsEnumerable()
                .Select(kv => kv.Key)
                .Except(supportedArgs)
                .ToList() is { Count: > 0 } unknownArgs)
        {
            Console.Error.WriteLine($"Unknown args: {string.Join(", ", unknownArgs)}.");
            ShowHelp();
            return -1;
        }


        // Build
        if (config["build"] is { } elmArg)
        {
            var elmDir = new DirectoryInfo(elmArg);
            if (!elmDir.Exists)
            {
                Console.Error.WriteLine($"-elm: path {elmArg} does not exist.");
                return -1;
            }
        }

        
        return 0;
    }


    private static int ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Data Requirements Builder CLI");
        Console.WriteLine();
        Console.WriteLine($"\t--build <directory/file>\tPath to directory of bundles or path to single bundle.");
        Console.WriteLine();
        return -1;
    }
}