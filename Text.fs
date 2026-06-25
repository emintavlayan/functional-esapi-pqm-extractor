namespace Functional.Esapi.PqmExtractor

open System

module Text =

    /// Checks whether text contains a token using ordinal ignore-case comparison.
    let containsIgnoreCase (token: string) (text: string) =
        if String.IsNullOrWhiteSpace token then true
        else
            let source = if isNull text then "" else text
            source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0

    /// Checks whether text does not contain a token using ordinal ignore-case comparison.
    let notContainsIgnoreCase (token: string) (text: string) = containsIgnoreCase token text |> not
