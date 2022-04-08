using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MrMeeseeks.ResXToViewModelGenerator
{
	[Generator]
	public class SourceGenerator : ISourceGenerator
	{
		public void Execute(GeneratorExecutionContext context)
		{
			const string @namespace = $"{nameof(MrMeeseeks)}.{nameof(ResXToViewModelGenerator)}";

			const string resxExtension = ".resx";
			IEnumerable<IGrouping<string,FileInfo>> resxFileGroups = context.AdditionalFiles
				.Where(af => af.Path.EndsWith(resxExtension))
				.Select(af => new FileInfo(af.Path))
				.GroupBy(fi => fi.Name.Substring(0, fi.Name.IndexOf('.')));

			foreach (var resxFileGroup in resxFileGroups)
			{
				var className = resxFileGroup.Key;
				
				var defaultFileName = $"{resxFileGroup.Key}{resxExtension}";
				if (resxFileGroup.FirstOrDefault(fi => fi.Name == defaultFileName) is not { } defaultFileInfo)
					return;

				var defaultKeyValues = GetLocalizationKeyValues(defaultFileInfo, "(default)", className);

				Dictionary<string, IReadOnlyDictionary<string, string>> localizations = new ();

				var localizationFiles = resxFileGroup
					.Where(fi => fi != defaultFileInfo)
					.Select(fi => (Specifier: fi.Name.Substring(resxFileGroup.Key.Length, fi.Name.Length - resxFileGroup.Key.Length - resxExtension.Length).Trim('.'),
						FileInfo: fi))
					.Where(vt => DoesCultureExist(vt.Specifier));
				
				foreach (var (specifier, file) in localizationFiles)
				{
					if (localizations.ContainsKey(specifier))
						continue;

					var localizationKeyValues = GetLocalizationKeyValues(file, specifier, className);
					
					localizations.Add(
						specifier,
						new ReadOnlyDictionary<string, string>(
							(defaultKeyValues
								.Keys ?? Enumerable.Empty<string>())
								.ToDictionary(k => k, k => localizationKeyValues.TryGetValue(k, out var value) ? value ?? "" : "")));
				}
				
				context.AddSource(
					$"{@namespace}.{className}.g.cs", 
					SourceText.From(
						Templating.Render(
							@namespace,
							className,
							defaultKeyValues,
							new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(localizations)), 
						Encoding.UTF8));
			}
			
			// https://stackoverflow.com/a/16476935/4871837 Thanks
			static bool DoesCultureExist(string cultureName) => CultureInfo
				.GetCultures(CultureTypes.AllCultures)
				.Any(culture => string.Equals(culture.Name, cultureName, StringComparison.CurrentCultureIgnoreCase));

			static IReadOnlyDictionary<string, string> GetLocalizationKeyValues(FileInfo localizationFile, string specifier, string className)
			{
				ResXResourceReader localizationReader =
					new (localizationFile.FullName)
					{
						UseResXDataNodes = true
					};
				
				Dictionary<string, string> localizationKeyValuesInner = new ();
				ReadOnlyDictionary<string, string> localizationKeyValues = new (localizationKeyValuesInner);
				
				foreach (var resXDataNode in localizationReader
					         .OfType<DictionaryEntry>()
					         .Select(de => de.Value)
					         .OfType<ResXDataNode>())
				{
					var name = resXDataNode.Name.Replace("-", "_").Replace(".", "_");
					if (localizationKeyValues.ContainsKey(name))
						throw new Exception($"ResXDataNode with name '{resXDataNode.Name}{(resXDataNode.Name != name ? $"(\"{name})\"" : "")}' in localization resx file with specifier '{specifier}' ('{className}') is contained multiple times.");
					localizationKeyValuesInner.Add(resXDataNode.Name, resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? "");
				}

				return localizationKeyValues;
			}
		}

		public void Initialize(GeneratorInitializationContext context)
        {
			/*
			if (!Debugger.IsAttached)
			{
				Debugger.Launch();
			}
			//*/
		}
	}
}
