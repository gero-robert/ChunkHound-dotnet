using BenchmarkDotNet.Running;
using ChunkHound.Core.Tests.Benchmarks;
using System.Diagnostics;

namespace ChunkHound.Tests;

public class Program
{
    public static void Main(string[] args)
    {
        // Run quick manual benchmark instead of full BenchmarkDotNet
        RunQuickBenchmark();
    }

    private static void RunQuickBenchmark()
    {
        Console.WriteLine("Running quick .NET parsing benchmark...");

        var benchmark = new IndexingBenchmarks();
        benchmark.Setup();

        var stopwatch = new Stopwatch();

        // Test Markdown parsing
        stopwatch.Start();
        var markdownTask = benchmark.FileParsing_MarkdownFile();
        markdownTask.Wait();
        stopwatch.Stop();
        Console.WriteLine($"Markdown parsing: {stopwatch.ElapsedMilliseconds}ms");

        // Test YAML parsing
        stopwatch.Restart();
        var yamlTask = benchmark.FileParsing_YamlFile();
        yamlTask.Wait();
        stopwatch.Stop();
        Console.WriteLine($"YAML parsing: {stopwatch.ElapsedMilliseconds}ms");

        // Test Vue parsing
        stopwatch.Restart();
        var vueTask = benchmark.FileParsing_VueFile();
        vueTask.Wait();
        stopwatch.Stop();
        Console.WriteLine($"Vue parsing: {stopwatch.ElapsedMilliseconds}ms");

        // Test C# parsing
        stopwatch.Restart();
        var csTask = benchmark.FileParsing_CSharpFile();
        csTask.Wait();
        stopwatch.Stop();
        Console.WriteLine($"C# parsing: {stopwatch.ElapsedMilliseconds}ms");

        Console.WriteLine("Quick benchmark completed.");
    }
}