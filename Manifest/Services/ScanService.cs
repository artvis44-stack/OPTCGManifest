using System.Text.Json;
using Manifest.Data;
using Manifest.Models;
using Manifest.Services.Jobs;
using Manifest.Web;

namespace Manifest.Services;

/// <summary>
/// Reads a card number from what the phone sent: local OCR on the cropped corner
/// variants first, then - only if a key is configured - the paid vision API on the
/// whole photo. The same code runs inside the request (MANIFEST_SCAN_MODE=sync) or
/// as a RunOcrScan job the page polls for (async), so the two cannot disagree.
/// </summary>
public sealed class ScanService(TesseractScanner tesseract, ApiScanner api, CardRepository cards,
                                AppConfig config) : IJobHandler
{
    public async Task<ScanResponse> Read(long userId, ScanPost body)
    {
        var variants = body.Variants ?? new List<string>();
        var img = body.Image;

        // Local OCR first: free, offline, and usually faster.
        if (variants.Count > 0 && tesseract.Available)
        {
            var (cid, note) = tesseract.Read(variants);
            if (cid is null && config.Verbose)
                // Only on request, and only if what arrived really is a PNG -
                // this writes caller-supplied bytes to disk.
                tesseract.DumpFailedScan(variants);

            if (cid is not null)
            {
                cards.LogScan(userId, cid, "tesseract");
                var card = cards.Resolve(cid);
                return new ScanResponse
                {
                    Ok = true,
                    CardId = cid,
                    Card = card,
                    InCatalog = card is not null,
                    Engine = "tesseract",
                    Confidence = card is not null ? "high" : "low",
                    Note = note,
                };
            }
            if (AppConfig.ApiKey is null)
                return new ScanResponse { Ok = false, Error = "no_read", Engine = "tesseract", Message = note };
        }

        if (!string.IsNullOrEmpty(img))
        {
            var head = img.Length > 64 ? img[..64] : img;
            if (head.Contains(',')) img = img[(img.IndexOf(',') + 1)..];

            var result = await api.Read(userId, img, body.MediaType ?? "image/jpeg");
            result.Engine = "api";
            return result;
        }

        return new ScanResponse
        {
            Ok = false,
            Error = "no_engine",
            Message = "Scanning needs tesseract installed on this machine. "
                      + "See the README, or type the number instead.",
        };
    }

    // ---- as a job

    public string Type => JobTypes.RunOcrScan;

    /// <summary>A photo of someone's card is not kept once it has been read.</summary>
    public bool ForgetPayload => true;

    public async Task<object?> Run(Job job, CancellationToken cancel)
    {
        var body = JsonSerializer.Deserialize<ScanPost>(job.Payload, Json.Options) ?? new ScanPost();
        return await Read(job.UserId ?? Database.Unclaimed, body);
    }
}
