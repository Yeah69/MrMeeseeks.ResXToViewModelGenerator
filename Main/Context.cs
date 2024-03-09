using System;

namespace MrMeeseeks.ResXToViewModelGenerator;

internal static class Context
{
    internal static bool IsSupported(string fileName) => 
        fileName.EndsWith(ResXContext.Ext) || fileName.EndsWith(CsvContext.Ext) || fileName.EndsWith(JsonContext.Ext);

    internal static IContext CreateFor(string fileName)
    {
        return fileName switch
        {
            var f when f.EndsWith(ResXContext.Ext) => new ResXContext(),
            var f when f.EndsWith(CsvContext.Ext) => new CsvContext(),
            var f when f.EndsWith(JsonContext.Ext) => new JsonContext(),
            _ => throw new ArgumentException("Unknown file extension")
        };
    }
}

internal interface IContext
{
    string Extension { get; }
    IFileReader FileReader { get; }
}

internal sealed class ResXContext : IContext
{
    internal const string Ext = ".resx";
    public string Extension => Ext;
    public IFileReader FileReader => new ResXFileReader();
}

internal sealed class CsvContext : IContext
{
    internal const string Ext = ".csv";
    public string Extension => Ext;
    public IFileReader FileReader => new CsvFileReader();
}

internal sealed class JsonContext : IContext
{
    internal const string Ext = ".json";
    public string Extension => Ext;
    public IFileReader FileReader => new JsonFileReader();
}