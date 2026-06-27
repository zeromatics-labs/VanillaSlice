namespace {{RootNamespace}}.SliceFactory.Services;

/// <summary>
/// Derives a PascalCase code prefix from a human-readable display name.
/// Called by CliRunner and ManifestService when building SliceDescriptors.
/// </summary>
public static class NameDerivationService
{
    /// <summary>
    /// Converts a display name to a PascalCase prefix.
    /// Singularizes the last word, then PascalCases each word.
    /// Examples: "Doctors" → "Doctor", "Doctor Profile" → "DoctorProfile",
    ///           "Disable Doctor" → "DisableDoctor", "Doctor Types" → "DoctorType"
    /// </summary>
    public static string DerivePrefix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        var words = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words[^1] = Singularize(words[^1]);
        return string.Concat(words.Select(PascalCase));
    }

    private static string PascalCase(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string Singularize(string word)
    {
        // -ies → -y  (Babies → Baby, Categories → Category)
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";

        // -s (not -ss, min length 4) → strip -s  (Doctors → Doctor, Cases → Case)
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 3)
            return word[..^1];

        return word;
    }
}
