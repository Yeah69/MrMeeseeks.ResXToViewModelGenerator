using System.Collections.Generic;

namespace MrMeeseeks.ResXToViewModelGenerator;

internal interface IValue;

internal interface IPlainString : IValue
{
    string Value { get; }
}

internal sealed class PlainString(string value) : IPlainString
{
    public string Value { get; } = value;
}

internal interface IPluralStrings : IValue
{
    IReadOnlyDictionary<string, string> Value { get; }
}

internal sealed class PluralStrings(IReadOnlyDictionary<string, string> value) : IPluralStrings
{
    public IReadOnlyDictionary<string, string> Value { get; } = value;
}