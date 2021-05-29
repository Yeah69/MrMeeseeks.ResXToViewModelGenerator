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
			const string @namespace = "MrMeeseeks.ResXToViewModelGenerator";

			const string resxExtension = ".resx";
			var resxFileGroups = context.AdditionalFiles
				.Where(af => af.Path.EndsWith(resxExtension))
				.Select(af => new FileInfo(af.Path))
				.GroupBy(fi => fi.Name.Substring(0, fi.Name.IndexOf('.')));

			foreach (var resxFileGroup in resxFileGroups)
			{
				var className = resxFileGroup.Key;
				
				var defaultFileName = $"{resxFileGroup.Key}{resxExtension}";
				if (resxFileGroup.FirstOrDefault(fi => fi.Name == defaultFileName) is not { } defaultFileInfo)
					return;

				var reader =
					new ResXResourceReader(defaultFileInfo.FullName)
					{
						UseResXDataNodes = true
					};

				var defaultKeyValuesInner = new Dictionary<string, string>();
				var defaultKeyValues = new ReadOnlyDictionary<string, string>(defaultKeyValuesInner);
					
				foreach (var resXDataNode in reader
					.OfType<DictionaryEntry>()
					.Select(de => de.Value)
					.OfType<ResXDataNode>())
				{
					if (defaultKeyValuesInner.ContainsKey(resXDataNode.Name))
						throw new Exception(
							$"ResXDataNode with name '{resXDataNode.Name}' in default resx file is contained multiple times ('{className}').");
					defaultKeyValuesInner.Add(resXDataNode.Name, resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? "");
				}

				var localizations = new Dictionary<string, IReadOnlyDictionary<string, string>>();

				var localizationFiles = resxFileGroup
					.Where(fi => fi != defaultFileInfo)
					.Select(fi => (Specifier: fi.Name.Substring(resxFileGroup.Key.Length, fi.Name.Length - resxFileGroup.Key.Length - resxExtension.Length).Trim('.'),
						FileInfo: fi))
					.Where(vt => DoesCultureExist(vt.Specifier));
				
				foreach (var vt in localizationFiles)
				{
					if (localizations.ContainsKey(vt.Specifier))
						continue;
						//throw new Exception(
						//	$"Multiple localization files with specifier '{vt.Specifier}' ('{className}').");
					
					var localizationReader =
						new ResXResourceReader(vt.FileInfo.FullName)
						{
							UseResXDataNodes = true
						};

					var localizationKeyValuesInner = new Dictionary<string, string>();
					var localizationKeyValues = new ReadOnlyDictionary<string, string>(localizationKeyValuesInner);
					
					foreach (var resXDataNode in localizationReader
						.OfType<DictionaryEntry>()
						.Select(de => de.Value)
						.OfType<ResXDataNode>())
					{
						if (localizationKeyValuesInner.ContainsKey(resXDataNode.Name))
							throw new Exception(
								$"ResXDataNode with name '{resXDataNode.Name}' in localization resx file with specifier '{vt.Specifier}' ('{className}') is contained multiple times.");
						localizationKeyValuesInner.Add(resXDataNode.Name, resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? "");
					}
					
					localizations.Add(
						vt.Specifier,
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
		}

		public void Initialize(GeneratorInitializationContext context)
		{
			/*
#if DEBUG
			if (!Debugger.IsAttached)
			{
				Debugger.Launch();
			}
#endif
			//*/
		}
	}
}
