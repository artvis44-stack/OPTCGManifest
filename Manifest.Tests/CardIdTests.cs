using Manifest.Services;

namespace Manifest.Tests;

/// <summary>
/// The OCR confusion repairs, and normalising whatever a human types. Pure string
/// work, so no server and no tesseract needed.
/// </summary>
public class CardIdTests
{
    static string? FirstReading(string text) =>
        CardId.Repairs(text).Select(CardId.Normalise).FirstOrDefault(x => x is not null);

    [Theory]
    [InlineData("0P11-004", "OP11-004")]
    [InlineData("OP11-OO4", "OP11-004")]
    [InlineData("0PII-004", "OP11-004")]
    [InlineData("ST0I-001", "ST01-001")]
    [InlineData("EBO1-006", "EB01-006")]
    public void RepairsAConfusedReading(string bad, string want) =>
        Assert.Equal(want, FirstReading(bad));

    [Fact]
    public void RejectsNonsense() => Assert.Null(FirstReading("garbage"));

    [Theory]
    [InlineData("OP01-016", "OP01-016")]
    [InlineData("op01-016", "OP01-016")]
    [InlineData("  op 01 - 016  ", "OP01-016")]
    [InlineData("OP01–016", "OP01-016")]                 // en-dash
    [InlineData("I have an OP01-016 spare", "OP01-016")] // inside a sentence
    [InlineData("OP01-016_p1", "OP01-016_p1")]
    [InlineData("OP01-016 P1", "OP01-016_p1")]
    [InlineData("P-001", "P-001")]
    [InlineData("OP01-016_jp", "OP01-016_jp")]
    [InlineData("op01-016_p1_JP", "OP01-016_p1_jp")]
    [InlineData("OP01-016 p1 jp", "OP01-016_p1_jp")]
    public void NormalisesWhatAHumanTypes(string raw, string want) =>
        Assert.Equal(want, CardId.Normalise(raw));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a card")]
    public void ReturnsNullForAnUnparseableId(string? raw) =>
        Assert.Null(CardId.Normalise(raw));

    [Fact]
    public void StripsAnythingThatCouldEscapeTheImageCache()
    {
        Assert.Equal("etcpasswd", CardId.SafeFileStem("../../etc/passwd"));
        Assert.Equal("OP01-016_p1", CardId.SafeFileStem("OP01-016_p1"));
    }
}
