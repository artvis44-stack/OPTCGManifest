using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Manifest.Data;
using Manifest.Models;

namespace Manifest.Services;

/// <summary>photo -> card number, via the API (optional, costs money).</summary>
public sealed class ApiScanner
{
    const string VisionPrompt = """
        Read the card number off this One Piece Trading Card Game card.

        It is printed in small type in the bottom-right corner of the card face. Formats:
        OP01-016, ST01-001, EB01-006, PRB01-002, P-001.

        Reply with JSON only, no fences:
        {"id": "OP01-016", "name": "Nami", "confidence": "high", "note": ""}

        - Read every character of the number. Never guess one you cannot see.
        - confidence is "high" only if the whole number is legible.
        - note: short reason if blurred, glared, cropped, or several cards are in frame.
        - If no card number is readable, use {"id": null, "confidence": "low", "note": "why"}.
        """;

    // Changing the model changes what a scan costs and how it reads, so that stays a
    // deliberate edit.
    const string Model = "claude-sonnet-4-6";

    readonly CardRepository _cards;
    public ApiScanner(CardRepository cards) => _cards = cards;

    public bool Available => AppConfig.ApiKey is not null;

    /// <summary>
    /// The account is carried in only so the scan lands in that user's scan log;
    /// the reading itself is the same whoever asked for it.
    /// </summary>
    public async Task<ScanResponse> Read(long userId, string imageBase64, string mediaType)
    {
        var key = AppConfig.ApiKey;
        if (key is null)
            return new ScanResponse
            {
                Ok = false,
                Error = "no_key",
                Message = "Scanning is off. Set ANTHROPIC_API_KEY and restart, or type the number.",
            };

        Message response;
        try
        {
            var client = new AnthropicClient { ApiKey = key };
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 300,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new MessageParamContent(new List<ContentBlockParam>
                        {
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource
                                {
                                    MediaType = MediaTypeOf(mediaType),
                                    Data = imageBase64,
                                },
                            },
                            new TextBlockParam { Text = VisionPrompt },
                        }),
                    },
                ],
            });
        }
        catch (Exception e)
        {
            return new ScanResponse
            {
                Ok = false,
                Error = "upstream",
                Message = $"Card reader unreachable: {e.Message}",
            };
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>()
                                         .Select(t => t.Text))
                         .Replace("```json", "").Replace("```", "").Trim();

        VisionReading? reading;
        try
        {
            reading = JsonSerializer.Deserialize<VisionReading>(text, Json.Options);
        }
        catch (JsonException)
        {
            reading = null;
        }
        if (reading is null)
            return new ScanResponse
            {
                Ok = false,
                Error = "parse",
                Message = "Unreadable response from the card reader.",
            };

        var cid = CardId.Normalise(reading.Id);
        _cards.LogScan(userId, cid,
                       Truncate(JsonSerializer.Serialize(reading, Json.Options), 500));

        if (cid is null)
            return new ScanResponse
            {
                Ok = false,
                Error = "no_id",
                Message = string.IsNullOrEmpty(reading.Note)
                    ? "No card number found in that shot."
                    : reading.Note,
            };

        var card = _cards.Resolve(cid);
        return new ScanResponse
        {
            Ok = true,
            CardId = cid,
            Card = card,
            InCatalog = card is not null,
            Confidence = reading.Confidence,
            Note = reading.Note ?? "",
        };
    }

    static MediaType MediaTypeOf(string mediaType) => mediaType switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg,
    };

    static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
