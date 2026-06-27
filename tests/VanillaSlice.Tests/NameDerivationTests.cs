using Xunit;

namespace VanillaSlice.Tests;

public class NameDerivationTests
{
    // Inline copy of NameDerivationService for isolated testing.
    // Kept in sync with Templates/SliceFactory/Services/NameDerivationService.cs.
    private static string DerivePrefix(string displayName)
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
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 3)
            return word[..^1];
        return word;
    }

    [Theory]
    [InlineData("Doctors", "Doctor")]
    [InlineData("Doctor Profile", "DoctorProfile")]
    [InlineData("Disable Doctor", "DisableDoctor")]
    [InlineData("Doctor Types", "DoctorType")]
    [InlineData("Babies", "Baby")]
    [InlineData("Categories", "Category")]
    [InlineData("Matters", "Matter")]
    [InlineData("Cases", "Case")]
    [InlineData("Settings", "Setting")]
    [InlineData("Active Doctors", "ActiveDoctor")]
    public void DerivePrefix_ReturnsExpectedPrefix(string input, string expected)
    {
        Assert.Equal(expected, DerivePrefix(input));
    }

    [Fact]
    public void DerivePrefix_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => DerivePrefix(""));
        Assert.Throws<ArgumentException>(() => DerivePrefix("   "));
    }
}
