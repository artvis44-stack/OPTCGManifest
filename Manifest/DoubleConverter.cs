using System.Text.Json;
using System.Text.Json.Serialization;

namespace Manifest;

/// <summary>
/// Python renders a float that happens to be whole as "2.0"; .NET renders it "2".
/// Both parse to the same JavaScript number, so nothing in ui.html can tell the
/// difference - but keeping the bytes identical means a response captured before the
/// rewrite diffs cleanly against one captured after, which is worth more than the
/// three lines it costs.
/// </summary>
public sealed class PythonStyleDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions o)
        => reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions o)
    {
        if (double.IsFinite(value) && value == Math.Floor(value) && Math.Abs(value) < 1e16)
            writer.WriteRawValue(value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}

public sealed class PythonStyleNullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions o)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions o)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        new PythonStyleDoubleConverter().Write(writer, value.Value, o);
    }
}
