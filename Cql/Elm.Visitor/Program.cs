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
        "b",
        "t",
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

        if (config["b"] is { } a)
        {
            var elmDir = new DirectoryInfo(a);
            if (!elmDir.Exists)
            {
                Console.Error.WriteLine($"-elm: path {a} does not exist.");
                return -1;
            }
        }

        if (config["t"] is { } elmArg)
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
        Console.WriteLine("--b\t <directory/file>\tPath to directory of bundles or path to single bundle.");

        Console.WriteLine(@$"--b <directory/file>\tPath to directory of 
                            bundles or path to single bundle.");
        Console.WriteLine();
        return -1;
    }
}