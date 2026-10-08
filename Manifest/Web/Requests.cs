using System.Text.Json;
using System.Text.Json.Serialization;
using Manifest.Models;

namespace Manifest.Web;

/// <summary>
/// Python's int() accepted "3" as readily as 3, and phones have been seen sending
/// both. Keeping that tolerance here means a rewrite doesn't quietly start rejecting
/// a client that used to work.
/// </summary>
public sealed class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => int.TryParse(reader.GetString(), out var n)
                ? n
                : throw new JsonException($"not a number: {reader.GetString()}"),
            _ => throw new JsonException($"not a number: {reader.TokenType}"),
        };

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}

public sealed class CollectionPost
{
    public string? CardId { get; set; }
    [JsonConverter(typeof(LenientIntConverter))] public int? Delta { get; set; }
    [JsonConverter(typeof(LenientIntConverter))] public int? Qty { get; set; }
    public string? Note { get; set; }
}

public sealed class BulkCardsPost
{
    public string? Text { get; set; }
    public List<CardQuantity>? Cards { get; set; }
    public string? Note { get; set; }
}

public sealed class DeckPost
{
    public string? Name { get; set; }
    public string? LeaderCardId { get; set; }
}

public sealed class DeckCardPost
{
    public string? CardId { get; set; }
    [JsonConverter(typeof(LenientIntConverter))] public int? Qty { get; set; }
}

public sealed class CollectionPrintPost
{
    public string? From { get; set; }
    public string? To { get; set; }
    [JsonConverter(typeof(LenientIntConverter))] public int? Qty { get; set; }
}

public sealed class DeckPrintPost
{
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>A print added by hand: any printing of the card, what is different, and a photo (base64).</summary>
public sealed class CustomPrintPost
{
    public string? CardId { get; set; }
    public string? Variant { get; set; }
    public string? SetLabel { get; set; }
    public string? Rarity { get; set; }
    public double? PriceGbp { get; set; }
    public string? Image { get; set; }
}

public sealed class ScanPost
{
    public List<string>? Variants { get; set; }
    public string? Image { get; set; }
    public string? MediaType { get; set; }
}

public sealed class LoginPost
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class RegisterPost
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Invite { get; set; }
}

/// <summary>Someone asking to be let in. The address is the whole payload.</summary>
public sealed class AccessRequestPost
{
    public string? Email { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// What the review page sends back. The token stands in for being the admin, which
/// is why this arrives as a POST from that page and not as the GET that opened it.
/// </summary>
public sealed class AccessDecisionPost
{
    public string? Token { get; set; }
    public string? Decision { get; set; }
}

public sealed class BinderPost
{
    public string? Name { get; set; }

    /// <summary>On create: move everything in your own binder into the new one.</summary>
    public bool MoveMine { get; set; }
}

public sealed class MemberPost
{
    public string? Username { get; set; }
}

public sealed class VisibilityPost
{
    public bool Visible { get; set; }
}

public sealed class MovePost
{
    public string? CardId { get; set; }
    public long From { get; set; }
    public long To { get; set; }
    [JsonConverter(typeof(LenientIntConverter))] public int? Qty { get; set; }
}
