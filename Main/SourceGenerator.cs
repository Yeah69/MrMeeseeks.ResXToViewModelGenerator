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
			string @namespace = $"{nameof(MrMeeseeks)}.{nameof(ResXToViewModelGenerator)}";

			const string resxExtension = ".resx";
			IEnumerable<IGrouping<string,FileInfo>> resxFileGroups = context.AdditionalFiles
				.Where(af => af.Path.EndsWith(resxExtension))
				.Select(af => new FileInfo(af.Path))
				.GroupBy(fi => fi.Name.Substring(0, fi.Name.IndexOf('.')));

			foreach (IGrouping<string,FileInfo> resxFileGroup in resxFileGroups)
			{
				string className = resxFileGroup.Key;
				
				string defaultFileName = $"{resxFileGroup.Key}{resxExtension}";
				if (resxFileGroup.FirstOrDefault(fi => fi.Name == defaultFileName) is not { } defaultFileInfo)
					return;

				ResXResourceReader reader =
					new (defaultFileInfo?.FullName ?? "")
					{
						UseResXDataNodes = true
					};

				Dictionary<string, string> defaultKeyValuesInner = new ();
				ReadOnlyDictionary<string, string> defaultKeyValues = new (defaultKeyValuesInner);
					
				foreach (ResXDataNode resXDataNode in reader
					.OfType<DictionaryEntry>()
					.Select(de => de.Value)
					.OfType<ResXDataNode>())
				{
					if (defaultKeyValuesInner.ContainsKey(resXDataNode.Name))
						throw new Exception(
							$"ResXDataNode with name '{resXDataNode.Name}' in default resx file is contained multiple times ('{className}').");
					defaultKeyValuesInner.Add(resXDataNode.Name, resXDataNode.GetValue((ITypeResolutionService?) null).ToString() ?? "");
				}

				Dictionary<string, IReadOnlyDictionary<string, string>> localizations = new ();

				IEnumerable<(string Specifier, FileInfo FileInfo)> localizationFiles = resxFileGroup
					.Where(fi => fi != defaultFileInfo)
					.Select(fi => (Specifier: fi.Name.Substring(resxFileGroup.Key.Length, fi.Name.Length - resxFileGroup.Key.Length - resxExtension.Length).Trim('.'),
						FileInfo: fi))
					.Where(vt => DoesCultureExist(vt.Specifier));
				
				foreach ((string Specifier, FileInfo FileInfo) vt in localizationFiles)
				{
					if (localizations.ContainsKey(vt.Specifier))
						continue;
					
					ResXResourceReader localizationReader =
						new (vt.FileInfo.FullName)
						{
							UseResXDataNodes = true
						};

					Dictionary<string, string> localizationKeyValuesInner = new ();
					ReadOnlyDictionary<string, string> localizationKeyValues = new (localizationKeyValuesInner);
					
					foreach (ResXDataNode resXDataNode in localizationReader
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
								.ToDictionary(k => k, k => localizationKeyValues.TryGetValue(k, out string? value) ? value ?? "" : "")));
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
			if (!Debugger.IsAttached)
			{
				Debugger.Launch();
			}
			//*/
		}
	}
}
