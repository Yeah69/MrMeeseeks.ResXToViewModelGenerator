using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Resources;
using Microsoft.CodeAnalysis;
using SoftCircuits.CsvParser;

namespace MrMeeseeks.ResXToViewModelGenerator;

internal interface ILocalizationFileReader
{
    bool TryGetLocalizationKeyValues(
        FileInfo localizationFile,
        string specifier,
        string className,
        Action<Diagnostic> processDiagnostic,
        out IReadOnlyDictionary<string, string> localizationKeyValues);
}

internal sealed class ResXLocalizationFileReader : ILocalizationFileReader
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

internal sealed class CsvLocalizationFileReader : ILocalizationFileReader
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