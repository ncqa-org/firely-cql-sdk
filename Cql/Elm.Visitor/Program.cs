using Hl7.Cql.Packaging;
using Hl7.Cql.Packaging.ResourceWriters;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Serialization;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hl7.Cql.Elm.Visitor;

public static class Program
{

    private static readonly string[] _supportedArgs = new[]
    {
        "i",
        "o",
        "s",
        "help",
    };
   
    public static int Main(string[] args)
    {
        
        var config = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        if (args.Length == 0 || config["?"] != null || config["h"] != null || config["help"] != null)
            return ShowHelp();

        if (config.AsEnumerable()
                .Select(kv => kv.Key)
                .Except(_supportedArgs)
                .ToList() is { Count: > 0 } unknownArgs)
        {
            Console.Error.WriteLine($"Unknown args: {string.Join(", ", unknownArgs)}.");
            ShowHelp();
            return -1;
        }

        if (config["help"] is { } _)
        {
            ShowHelp();
            return -1;
        }

        DirectoryInfo inDir;
        if (config["i"] is { } a)
        {
            inDir = new DirectoryInfo(a);
            if (!inDir.Exists)
            {
                Console.Error.WriteLine($"-i: path {a} does not exist.");
                return -1;
            }
        }
        DirectoryInfo outDir;
        if (config["o"] is { } elmArg)
        {
            var elmDir = new DirectoryInfo(elmArg);
            if (!elmDir.Exists)
            {
                Console.Error.WriteLine($"-o: path {elmArg} does not exist.");
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
        Console.WriteLine("--i\t <directory>\tPath to directory of measure bundles.");
        Console.WriteLine("--o\t <directory>\tPath to output directory.");
        Console.WriteLine("--s Flag to generate a population defines summary.");

        Console.WriteLine();
        return -1;
    }
}