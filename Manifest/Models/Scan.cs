using Manifest.Models;

namespace Manifest.Models;

/// <summary>
/// What a scan attempt sends back. Fields the client doesn't need for a given
/// outcome stay null - ui.html reads them all by truthiness.
/// </summary>
public sealed class ScanResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string? CardId { get; set; }
    public CatalogRow? Card { get; set; }
    public bool InCatalog { get; set; }
    public string? Engine { get; set; }
    public string? Confidence { get; set; }
    public string? Note { get; set; }
}

/// <summary>The JSON the vision model is asked to reply with.</summary>
public sealed class VisionReading
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Confidence { get; set; }
    public string? Note { get; set; }
}
