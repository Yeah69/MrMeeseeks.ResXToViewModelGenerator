using System;
using Microsoft.CodeAnalysis;

namespace MrMeeseeks.ResXToViewModelGenerator;

internal static class Utility
{
    internal static Diagnostic CreateDiagnostic(int id, string message, DiagnosticSeverity severity) =>
        Diagnostic.Create(
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