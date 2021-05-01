﻿using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Scriban;

namespace MrMeeseeks.ResXToViewModelGenerator
{
	public static class Templating
	{
		private record ResXInterface(IReadOnlyList<string> Keys);

		private record ResXImplementationProperty(string Key, string Value);
		private record ResXImplementation(
			string Name, 
			string LanguageCode, 
			IReadOnlyList<ResXImplementationProperty> Properties);

		private record Main(
			string Namespace,
			string Name,
			ResXInterface Interface,
			IReadOnlyList<ResXImplementation> Implementations);

		public static string Render(
			string @namespace,
			string name,
			IReadOnlyDictionary<string, string> defaultKeyValues,
			IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> culturalKeyValues)
		{
			var keys = new ReadOnlyCollection<string>(defaultKeyValues.Keys.ToList());

			var main = new Main(
				@namespace,
				name,
				new ResXInterface(keys),
				culturalKeyValues
					.Select(kvp => Create(kvp.Key.Replace("-", ""), kvp.Key, kvp.Value))
					.Prepend(Create("Default", "iv", defaultKeyValues))
					.ToList());

			return  Template.Parse(@"#nullable enable
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

{{ resx_name = name }}

namespace {{ namespace }}
{
	public interface I{{ resx_name }}ViewModel : INotifyPropertyChanged
	{
		CultureInfo CultureInfo { get; }
		{{ for key in interface.keys }} 
		string {{ key }} { get; }
		{{ end }}
	}
	
	public interface I{{ resx_name }}OptionViewModel : INotifyPropertyChanged
	{
		CultureInfo CultureInfo { get; }
	}

	public interface ICurrent{{ resx_name }}ViewModel : INotifyPropertyChanged
	{
		I{{ resx_name }}ViewModel Current{{ resx_name }} { get; }

		I{{ resx_name }}OptionViewModel CurrentOption { get; set; }
        
		IReadOnlyList<I{{ resx_name }}OptionViewModel> AvailableOptions { get; }
	}

	public static class Current{{ resx_name }}ViewModel
	{
		public static ICurrent{{ resx_name }}ViewModel Create() => new Current{{ resx_name }}ViewModelInner();
        
		private sealed class Current{{ resx_name }}ViewModelInner : ICurrent{{ resx_name }}ViewModel
		{
			private I{{ resx_name }}ViewModel _current{{ resx_name }};
			private I{{ resx_name }}OptionViewModel _currentOption;
			public event PropertyChangedEventHandler? PropertyChanged;

			public Current{{ resx_name }}ViewModelInner()
			{
				AvailableOptions = new ReadOnlyCollection<I{{ resx_name }}OptionViewModel>(
					new List<I{{ resx_name }}OptionViewModel>
					{
					{{ for implementation in implementations }} 
						new {{ implementation.name }}{{ resx_name }}OptionViewModel(),
					{{ end }}
					});
				_currentOption = AvailableOptions[0];
				_current{{ resx_name }} = Create{{ resx_name }}(_currentOption);
			}

			public I{{ resx_name }}OptionViewModel CurrentOption
			{
				get => _currentOption;
				set
				{
					if (_currentOption == value) return;
					_currentOption = value;
					_current{{ resx_name }} = Create{{ resx_name }}(value);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentOption)));
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current{{ resx_name }})));
				}
			}

			private I{{ resx_name }}ViewModel Create{{ resx_name }}(I{{ resx_name }}OptionViewModel option) => ((option as I{{ resx_name }}OptionViewModelInternal) ?? new Default{{ resx_name }}OptionViewModel()).Create();

			public I{{ resx_name }}ViewModel Current{{ resx_name }} => _current{{ resx_name }};

			public IReadOnlyList<I{{ resx_name }}OptionViewModel> AvailableOptions { get; }

			public interface I{{ resx_name }}OptionViewModelInternal : I{{ resx_name }}OptionViewModel
			{
				I{{ resx_name }}ViewModel Create();
			}

			{{ for implementation in implementations }} 
			private class {{ implementation.name }}{{ resx_name }}OptionViewModel : I{{ resx_name }}OptionViewModelInternal
			{
#pragma warning disable 0067
				public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore 0067

	            public CultureInfo CultureInfo { get; } = CultureInfo.GetCultureInfo(""{{ implementation.language_code }}"");

				public I{{ resx_name }}ViewModel Create() => new {{ implementation.name }}{{ resx_name }}ViewModel();
			}
			{{ end }}

			{{ for implementation in implementations }} 
			private class {{ implementation.name }}{{ resx_name }}ViewModel : I{{ resx_name }}ViewModel
	        {
#pragma warning disable 0067
				public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore 0067

	            public CultureInfo CultureInfo { get; } = CultureInfo.GetCultureInfo(""{{ implementation.language_code }}"");
				{{ for property in implementation.properties }} 
				public string {{ property.key }} { get; } = {{ property.value }};
				{{ end }}
			}
			{{ end }}
		}
	}
}
#nullable disable").Render(main);


			static ResXImplementation Create(
				string name,
				string languageCode,
				IReadOnlyDictionary<string, string> propertyMapping)
			{
				return new(
					name,
					languageCode,
					propertyMapping
						.Select(kvp => new ResXImplementationProperty(kvp.Key, ValueToLiteral(kvp.Value)))
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
