using System.Text.Json;
using System.Text.Json.Serialization;

namespace Manifest;

/// <summary>
/// One serializer configuration for the whole app. The property naming policy is
/// what keeps ui.html working unchanged: every C# property lands on the wire under
/// the same snake_case key the Python emitted. Dictionary keys are deliberately left
/// alone - the cost curve is keyed by "0".."10" and "?".
/// </summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new PythonStyleDoubleConverter(),
            new PythonStyleNullableDoubleConverter(),
        },
    };
}
