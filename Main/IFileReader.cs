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
        out IReadOnlyDictionary<string, string> localizationKeyValues);
}

internal sealed class ResXFileReader : IFileReader
{
    public bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, string> localizationKeyValues)
    {
        ResXResourceReader localizationReader = new (localizationFile.FullName) { UseResXDataNodes = true };
				
        Dictionary<string, string> localizationKeyValuesInner = new ();
        ReadOnlyDictionary<string, string> temp = new (localizationKeyValuesInner);
				
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
                localizationKeyValues = new Dictionary<string, string>();
                return false;
            }
            localizationKeyValuesInner.Add(name, resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? "");
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
        out IReadOnlyDictionary<string, string> localizationKeyValues)
    {
        var reader = new CsvReader(localizationFile.FullName);
				
        Dictionary<string, string> localizationKeyValuesInner = new ();
        ReadOnlyDictionary<string, string> temp = new (localizationKeyValuesInner);
        
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
                localizationKeyValues = new Dictionary<string, string>();
                return false;
            }
            localizationKeyValuesInner.Add(name, resXDataNode.Value);
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
        out IReadOnlyDictionary<string, string> localizationKeyValues)
    {
        try
        {
            //
            /*var fileContent = 
                """
                {
                  "boolean_key": "--- true\n",
                  "empty_string_translation": "",
                  "key_with_description": "Check it out! This key has a description! (At least in some formats)",
                  "key_with_line-break": "This translations contains\na line-break.",
                  "nested.deeply.key": "Wow, this key is nested even deeper.",
                  "nested.key": "This key is nested inside a namespace.",
                  "null_translation": null,
                  "simple_key": "Just a simple key with a simple message.",
                  "unverified_key": "This translation is not yet verified and waits for it. (In some formats we also export this status)"
                }
                """;*/
            var fileContent = File.ReadAllText(localizationFile.FullName);
            var jsonRoot = JObject.Parse(fileContent);
            Dictionary<string, string> localizationKeyValuesInner = new ();
            ReadOnlyDictionary<string, string> temp = new (localizationKeyValuesInner);
            foreach (var jsonElement in jsonRoot)
            {
                var value = jsonElement.Value switch
                {
                    //JObject jObject => throw new NotImplementedException(),
                    JValue jValue => jValue.Value?.ToString() ?? "",
                    _ => ""
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
            localizationKeyValues = new Dictionary<string, string>();
            return false;
        }
    }
}