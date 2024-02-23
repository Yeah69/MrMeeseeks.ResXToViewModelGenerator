using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	public class SourceGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			const string resxExtension = ".resx";
			const string @namespace = $"{nameof(MrMeeseeks)}.{nameof(ResXToViewModelGenerator)}";
			
			var provider = context
				.AdditionalTextsProvider
				.Where(text => text.Path.EndsWith(".resx"))
				.Collect()
				.SelectMany((texts, _) =>
				{
					return texts
						.Select(text => new FileInfo(text.Path))
						.GroupBy(fi => fi.Name.Substring(0, fi.Name.IndexOf('.')))
						.Select(group =>
						{
							var defaultFileName = $"{group.Key}{resxExtension}";
							return new IncrementalPipeItemMonad<(string ClassName, FileInfo? DefaultFile, IGrouping<string, FileInfo> Files)>((
								ClassName: group.Key,
								DefaultFile: group.FirstOrDefault(fi => fi.Name == defaultFileName),
								Files: group));
						})
						.Select(monad => monad.Bind((tuple, processDiagnostic) =>
						{
							if (tuple.DefaultFile is null)
							{
								processDiagnostic(CreateDiagnostic(
									1, 
									$"No default file \"{tuple.ClassName}{resxExtension}\" found for ResX family \"{tuple.ClassName}\".", 
									DiagnosticSeverity.Error));
							}

							return tuple;
						}))
						.OfType<IncrementalPipeItemMonad<(string ClassName, FileInfo DefaultFile, IGrouping<string, FileInfo> Files)>>()
						.ToImmutableArray();
				})
				.Select((monad, _) => monad.Bind((tuple, processDiagnostic) =>
				{
					var className = tuple.ClassName;
					var defaultFileInfo = tuple.DefaultFile;
					var resxFileGroup = tuple.Files;

					if (!TryGetLocalizationKeyValues(defaultFileInfo, "(default)", className, processDiagnostic, out var defaultKeyValues))
						return (FileName: "", Source: SourceText.From("", Encoding.UTF8));

					Dictionary<string, IReadOnlyDictionary<string, string>> localizations = new ();

					var localizationFilesGroups = resxFileGroup
						.Where(fi => fi != defaultFileInfo)
						.Select(fi => (
							Specifier: fi.Name.Substring(className.Length, fi.Name.Length - className.Length - resxExtension.Length).Trim('.'),
							FileInfo: fi))
						.GroupBy(vt => DoesCultureExist(vt.Specifier))
						.ToArray();
					
					foreach (var (specifier, file) in localizationFilesGroups.Where(g => !g.Key).SelectMany(g => g))
					{
						processDiagnostic(CreateDiagnostic(
							2,
							$"Invalid culture specifier \"{specifier}\" on file \"{file.Name}\" found for ResX family \"{className}\". It'll be ignored.",
							DiagnosticSeverity.Warning));
					}
				
					foreach (var (specifier, file) in localizationFilesGroups.Where(g => g.Key).SelectMany(g => g))
					{
						if (localizations.ContainsKey(specifier))
							continue;
						
						if (!TryGetLocalizationKeyValues(file, specifier, className, processDiagnostic, out var localizationKeyValues))
							return (FileName: "", Source: SourceText.From("", Encoding.UTF8));
					
						localizations.Add(
							specifier,
							new ReadOnlyDictionary<string, string>(
								(defaultKeyValues
									.Keys ?? Enumerable.Empty<string>())
								.ToDictionary(k => k, k => localizationKeyValues.TryGetValue(k, out var value) ? value ?? "" : "")));
					}
					
					return (FileName: $"{@namespace}.{className}.g.cs", 
						Source: SourceText.From(
							Templating.Render(
								@namespace,
								className,
								defaultKeyValues,
								new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(localizations)), 
							Encoding.UTF8));
				}));
			
			context.RegisterSourceOutput(provider, (sourceProductionContext, monad) => 
				monad.Sink(sourceProductionContext, sourceDescription => 
					sourceProductionContext.AddSource(sourceDescription.FileName, sourceDescription.Source)));
			return;

			// https://stackoverflow.com/a/16476935/4871837 Thanks
			static bool DoesCultureExist(string cultureName) => CultureInfo
				.GetCultures(CultureTypes.AllCultures)
				.Any(culture => string.Equals(culture.Name, cultureName, StringComparison.CurrentCultureIgnoreCase));

			static bool TryGetLocalizationKeyValues(
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
						processDiagnostic(CreateDiagnostic(
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
		
		private static Diagnostic CreateDiagnostic(int id, string message, DiagnosticSeverity severity)
		{
			return Diagnostic.Create(
				new DiagnosticDescriptor(
					$"RX2VM{id:D3}",
					severity switch
					{
						DiagnosticSeverity.Error => "Error",
						DiagnosticSeverity.Warning => "Warning",
						DiagnosticSeverity.Info => "Info",
						DiagnosticSeverity.Hidden => "Hidden",
						_ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
					},
					message,
					"ResXToViewModelGenerator",
					severity,
					true),
				Location.None);
		}
	}
}
