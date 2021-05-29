﻿using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace MrMeeseeks.ResXToViewModelGenerator
{
	public static class Templating
	{
		public static string Render(
			string @namespace,
			string name,
			IReadOnlyDictionary<string, string> defaultKeyValues,
			IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> culturalKeyValues)
		{
			var keys = new ReadOnlyCollection<string>(defaultKeyValues.Keys.ToList());
			var implementations = culturalKeyValues
				.Select(kvp => Create(kvp.Key.Replace("-", ""), kvp.Key, kvp.Value))
				.Prepend(Create("Default", "iv", defaultKeyValues))
				.ToList();

			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(@$"#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------


namespace {@namespace}
{{
	public interface I{name}ViewModel : INotifyPropertyChanged
	{{
		CultureInfo CultureInfo {{ get; }}");
			foreach (var key in keys)
			{
				stringBuilder.AppendLine($"		string {key} {{ get; }}");
			}
			stringBuilder.AppendLine(@$"
	}}
	
	public interface I{name}OptionViewModel : INotifyPropertyChanged
	{{
		CultureInfo CultureInfo {{ get; }}
	}}

	public interface ICurrent{name}ViewModel : INotifyPropertyChanged
	{{
		I{name}ViewModel Current{name} {{ get; }}

		I{name}OptionViewModel CurrentOption {{ get; set; }}
        
		IReadOnlyList<I{name}OptionViewModel> AvailableOptions {{ get; }}
	}}
        
	public sealed class Current{name}ViewModel : ICurrent{name}ViewModel
	{{
		private I{name}ViewModel _current{name};
		private I{name}OptionViewModel _currentOption;
		public event PropertyChangedEventHandler? PropertyChanged;

		public Current{name}ViewModel()
		{{
			AvailableOptions = new ReadOnlyCollection<I{name}OptionViewModel>(
				new List<I{name}OptionViewModel>
				{{");
			
			foreach (var resXImplementation in implementations)
			{
				stringBuilder.AppendLine($"					new {resXImplementation.Name }{name}OptionViewModel(),");
			}
			stringBuilder.AppendLine(@$"
				}});
			_currentOption = AvailableOptions[0];
			_current{name} = Create{name}(_currentOption);
		}}

		public I{name}OptionViewModel CurrentOption
		{{
			get => _currentOption;
			set
			{{
				if (_currentOption == value) return;
				_currentOption = value;
				_current{name} = Create{name}(value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentOption)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current{name})));
			}}
		}}

		private I{name}ViewModel Create{name}(I{name}OptionViewModel option) => ((option as I{name}OptionViewModelInternal) ?? new Default{name}OptionViewModel()).Create();

		public I{name}ViewModel Current{name} => _current{name};

		public IReadOnlyList<I{name}OptionViewModel> AvailableOptions {{ get; }}

		public interface I{name}OptionViewModelInternal : I{name}OptionViewModel
		{{
			I{name}ViewModel Create();
		}}
");
			
			foreach (var implementation in implementations)
			{
				stringBuilder.AppendLine(@$"		private class {implementation.Name}{name}OptionViewModel : I{name}OptionViewModelInternal
		{{
#pragma warning disable 0067
			public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore 0067

			public CultureInfo CultureInfo {{ get; }} = CultureInfo.GetCultureInfo(""{implementation.LanguageCode}"");

			public I{name}ViewModel Create() => new {implementation.Name}{name}ViewModel();
		}}");
			}
			
			foreach (var implementation in implementations)
			{
				stringBuilder.AppendLine(@$"		private class {implementation.Name}{name}ViewModel : I{name}ViewModel
        {{
#pragma warning disable 0067
			public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore 0067

			public CultureInfo CultureInfo {{ get; }} = CultureInfo.GetCultureInfo(""{implementation.LanguageCode}"");");
				foreach (var resXImplementationProperty in implementation.Properties)
				{
					stringBuilder.AppendLine($"			public string {resXImplementationProperty.Key} {{ get; }} = {resXImplementationProperty.Value};");
				}
				stringBuilder.AppendLine("		}");
			}
			stringBuilder.AppendLine(@"	}
}
#nullable disable");
			

			return stringBuilder.ToString();


			static (string Name, string LanguageCode, IReadOnlyList<(string Key, string Value)> Properties) Create(
				string name,
				string languageCode,
				IReadOnlyDictionary<string, string> propertyMapping)
			{
				return (
					name,
					languageCode,
					propertyMapping
						.Select(kvp => (kvp.Key, ValueToLiteral(kvp.Value)))
						.ToList());
				
				static string ValueToLiteral(string input)
				{
					using var writer = new StringWriter();
					using var provider = CodeDomProvider.CreateProvider("CSharp");
					provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
					return writer.ToString();
				}
			}
		}
	}
}
