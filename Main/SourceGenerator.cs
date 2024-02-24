using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
			const string csvExtension = ".csv";
			const string @namespace = $"{nameof(MrMeeseeks)}.{nameof(ResXToViewModelGenerator)}";

			var provider = context
				.AdditionalTextsProvider
				.Where(text => text.Path.EndsWith(resxExtension) || text.Path.EndsWith(csvExtension))
				.Collect()
				.SelectMany((texts, _) =>
				{
					return texts
						.Select(text => new FileInfo(text.Path))
						.GroupBy(fi => fi.Name.Substring(0, fi.Name.IndexOf('.')))
						.Select(group =>
						{
							var defaultResxFileName = $"{group.Key}{resxExtension}";
							var defaultCsvFileName = $"{group.Key}{csvExtension}";
							return new IncrementalPipeItemMonad<(string ClassName, FileInfo? DefaultFile,
								IGrouping<string, FileInfo> Files)>((
								ClassName: group.Key,
								DefaultFile: group.FirstOrDefault(fi =>
									fi.Name == defaultResxFileName || fi.Name == defaultCsvFileName),
								Files: group));
						})
						.Select(monad => monad.Bind((tuple, processDiagnostic) =>
						{
							if (tuple.DefaultFile is null)
							{
								processDiagnostic(Utility.CreateDiagnostic(
									1,
									$"No default file found for file family \"{tuple.ClassName}\".",
									DiagnosticSeverity.Error));
							}
							else
							{
								var extension = tuple.DefaultFile.Extension;
								if (tuple.Files.Any(fi => fi.Extension != extension))
								{
									processDiagnostic(Utility.CreateDiagnostic(
										4,
										$"Files with different extensions found for file family \"{tuple.ClassName}\".",
										DiagnosticSeverity.Error));
								}
							}

							return tuple;
						}))
						.OfType<IncrementalPipeItemMonad<(string ClassName, FileInfo DefaultFile,
							IGrouping<string, FileInfo> Files)>>()
						.ToImmutableArray();
				})
				.Select((monad, _) => monad.Bind((tuple, processDiagnostic) =>
				{
					var className = tuple.ClassName;
					var defaultFileInfo = tuple.DefaultFile;
					var resxFileGroup = tuple.Files;
					
					var localizationFileReader = defaultFileInfo.Name.EndsWith(resxExtension)
						? (ILocalizationFileReader) new ResXLocalizationFileReader()
						: new CsvLocalizationFileReader();
					
					var extension = defaultFileInfo.Name.EndsWith(resxExtension) ? resxExtension : csvExtension;

					if (!localizationFileReader.TryGetLocalizationKeyValues(
						    defaultFileInfo, 
						    "(default)", 
						    className, 
						    processDiagnostic,
						    out var defaultKeyValues))
						return (FileName: "", Source: SourceText.From("", Encoding.UTF8));

					Dictionary<string, IReadOnlyDictionary<string, string>> localizations = new();

					var localizationFilesGroups = resxFileGroup
						.Where(fi => fi != defaultFileInfo)
						.Select(fi => (
							Specifier: fi.Name.Substring(className.Length,
								fi.Name.Length - className.Length - extension.Length).Trim('.'),
							FileInfo: fi))
						.GroupBy(vt => DoesCultureExist(vt.Specifier))
						.ToArray();

					foreach (var (specifier, file) in localizationFilesGroups.Where(g => !g.Key).SelectMany(g => g))
					{
						processDiagnostic(Utility.CreateDiagnostic(
							2,
							$"Invalid culture specifier \"{specifier}\" on file \"{file.Name}\" found for file family \"{className}\". It'll be ignored.",
							DiagnosticSeverity.Warning));
					}

					foreach (var (specifier, file) in localizationFilesGroups.Where(g => g.Key).SelectMany(g => g))
					{
						if (localizations.ContainsKey(specifier))
							continue;

						if (!localizationFileReader.TryGetLocalizationKeyValues(
							    file, 
							    specifier,
							    className, 
							    processDiagnostic,
							    out var localizationKeyValues))
							return (FileName: "", Source: SourceText.From("", Encoding.UTF8));

						localizations.Add(
							specifier,
							new ReadOnlyDictionary<string, string>(
								(defaultKeyValues
									.Keys ?? Enumerable.Empty<string>())
								.ToDictionary(k => k,
									k => localizationKeyValues.TryGetValue(k, out var value) ? value ?? "" : "")));
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
				.Any(culture =>
					string.Equals(culture.Name, cultureName, StringComparison.CurrentCultureIgnoreCase));
		}
	}
}
