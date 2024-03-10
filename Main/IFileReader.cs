using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Resources;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using SoftCircuits.CsvParser;

namespace MrMeeseeks.ResXToViewModelGenerator;

internal interface IFileReader
{
    bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, IValue> localizationKeyValues);
}

internal sealed class ResXFileReader : IFileReader
{
    public bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, IValue> localizationKeyValues)
    {
        ResXResourceReader localizationReader = new (localizationFile.FullName) { UseResXDataNodes = true };
				
        Dictionary<string, IValue> localizationKeyValuesInner = new ();
        ReadOnlyDictionary<string, IValue> temp = new (localizationKeyValuesInner);
				
        foreach (var resXDataNode in localizationReader
                     .OfType<DictionaryEntry>()
                     .Select(de => de.Value)
                     .OfType<ResXDataNode>())
        {
            var name = resXDataNode.Name.Replace("-", "_").Replace(".", "_");
            if (temp.ContainsKey(name))
            {
                processDiagnostic(Utility.CreateDiagnostic(
                    3,
                    $"ResXDataNode with name '{resXDataNode.Name}{(resXDataNode.Name != name ? $"(\"{name})\"" : "")}' in localization resx file with specifier '{specifier}' ('{className}') is contained multiple times.",
                    DiagnosticSeverity.Error));
                localizationKeyValues = new Dictionary<string, IValue>();
                return false;
            }
            localizationKeyValuesInner.Add(name, new PlainString(resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? ""));
        }

        localizationKeyValues = temp;
        return true;
    }
}

internal sealed class CsvFileReader : IFileReader
{
    private sealed class CsvItem
    {
        internal string Key { get; init; } = null!;
        internal string Value { get; init; } = null!;
    }
    
    public bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, IValue> localizationKeyValues)
    {
        var reader = new CsvReader(localizationFile.FullName);
				
        Dictionary<string, IValue> localizationKeyValuesInner = new ();
        ReadOnlyDictionary<string, IValue> temp = new (localizationKeyValuesInner);
        
        _ = reader.ReadRow(); // Skip header
        
        while (reader.ReadRow() is { } rowData)
        {
            var resXDataNode = new CsvItem
            {
                Key = rowData[0],
                Value = rowData[1]
            };
            
            var name = resXDataNode.Key.Replace("-", "_").Replace(".", "_");
            if (temp.ContainsKey(name))
            {
                processDiagnostic(Utility.CreateDiagnostic(
                    3,
                    $"CsvItem with name '{resXDataNode.Key}{(resXDataNode.Key != name ? $"(\"{name})\"" : "")}' in localization resx file with specifier '{specifier}' ('{className}') is contained multiple times.",
                    DiagnosticSeverity.Error));
                localizationKeyValues = new Dictionary<string, IValue>();
                return false;
            }
            localizationKeyValuesInner.Add(name, new PlainString(resXDataNode.Value));
        }

        localizationKeyValues = temp;
        return true;
    }
}

internal sealed class JsonFileReader : IFileReader
{
    public bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, IValue> localizationKeyValues)
    {
        try
        {
            var fileContent = File.ReadAllText(localizationFile.FullName);
            var jsonRoot = JObject.Parse(fileContent);
            Dictionary<string, IValue> localizationKeyValuesInner = new ();
            ReadOnlyDictionary<string, IValue> temp = new (localizationKeyValuesInner);
            foreach (var jsonElement in jsonRoot)
            {
                IValue value = jsonElement.Value switch
                {
                    JObject jObject => new PluralStrings(jObject.Properties().ToDictionary(p => p.Name, p => p.Value.ToString())),
                    JValue jValue => new PlainString(jValue.Value?.ToString() ?? ""),
                    _ => new PlainString("")
                };
                localizationKeyValuesInner.Add(jsonElement.Key.Replace("-", "_").Replace(".", "_"), value);
            }
            localizationKeyValues = temp;
            return true;
        }
        catch (Exception e)
        {
            processDiagnostic(Utility.CreateDiagnostic(
                3,
                $"Error while reading json file '{localizationFile.FullName}': {e.Message}",
                DiagnosticSeverity.Error));
            localizationKeyValues = new Dictionary<string, IValue>();
            return false;
        }
    }
}