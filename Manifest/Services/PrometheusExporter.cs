using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace Manifest.Services;

/// <summary>
/// Collects what <see cref="Telemetry"/> and Npgsql record and writes it out in the
/// Prometheus text format. Written by hand rather than pulled in from OpenTelemetry:
/// its Prometheus exporter is still a pre-release package, and what is needed here -
/// sums, last values and fixed-bucket histograms for two meters - is a page of code.
/// </summary>
public sealed class PrometheusExporter : IDisposable
{
    enum Kind { Counter, Gauge, Histogram }

    /// <summary>How one instrument is written out.</summary>
    sealed record Spec(string Name, Kind Kind, string Help, double[]? Buckets,
                       string[]? KeepLabels, double Scale, bool Observable);

    sealed class Series
    {
        public required Spec Spec;
        public required string Labels;
        public double Value;
        public long[]? Buckets;
        public double Sum;
        public long Count;
        public bool Seen;
    }

    /// <summary>
    /// Npgsql's instruments under names that sit beside ours. Its pool-name tag is
    /// dropped: there is one pool per process, and the tag is the connection string.
    /// </summary>
    static readonly Dictionary<string, (string Name, Kind Kind, string[]? Keep)> Npgsql = new()
    {
        ["db.client.commands.duration"] = ("manifest_db_command_duration_seconds", Kind.Histogram, null),
        ["db.client.commands.failed"] = ("manifest_db_command_failures_total", Kind.Counter, null),
        ["db.client.commands.executing"] = ("manifest_db_commands_executing", Kind.Gauge, null),
        ["db.client.connections.usage"] = ("manifest_db_connections", Kind.Gauge, new[] { "state" }),
        ["db.client.connections.max"] = ("manifest_db_connections_max", Kind.Gauge, null),
        ["db.client.connections.pending_requests"] = ("manifest_db_connection_waiters", Kind.Gauge, null),
        ["db.client.connections.timeouts"] = ("manifest_db_connection_timeouts_total", Kind.Counter, null),
    };

    readonly MeterListener _listener = new();
    readonly ConcurrentDictionary<Instrument, Spec> _specs = new();
    readonly ConcurrentDictionary<string, Series> _series = new();
    readonly object _collecting = new();

    public PrometheusExporter()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (SpecFor(instrument) is not { } spec) return;
            _specs[instrument] = spec;
            listener.EnableMeasurementEvents(instrument, spec);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, state) => Record(state, v, tags));
        _listener.SetMeasurementEventCallback<int>((i, v, tags, state) => Record(state, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, state) => Record(state, v, tags));
        _listener.Start();
    }

    static Spec? SpecFor(Instrument instrument)
    {
        var kind = KindOf(instrument);
        if (kind is null) return null;

        if (instrument.Meter.Name == Telemetry.MeterName)
        {
            double[]? buckets = null;
            if (kind == Kind.Histogram)
                buckets = instrument.Name.Contains("job") || instrument.Name.Contains("scan")
                    ? Telemetry.JobBuckets
                    : Telemetry.LatencyBuckets;
            return new Spec(instrument.Name, kind.Value, instrument.Description ?? "", buckets,
                            null, 1, instrument.IsObservable);
        }

        if (instrument.Meter.Name == "Npgsql" && Npgsql.TryGetValue(instrument.Name, out var mapped))
        {
            var scale = instrument.Unit == "ms" ? 0.001 : 1;
            return new Spec(mapped.Name, mapped.Kind, instrument.Description ?? instrument.Name,
                            mapped.Kind == Kind.Histogram ? Telemetry.LatencyBuckets : null,
                            mapped.Keep ?? Array.Empty<string>(), scale, instrument.IsObservable);
        }
        return null;
    }

    static Kind? KindOf(Instrument instrument)
    {
        if (!instrument.GetType().IsGenericType) return null;
        var def = instrument.GetType().GetGenericTypeDefinition();
        if (def == typeof(Counter<>) || def == typeof(ObservableCounter<>)) return Kind.Counter;
        if (def == typeof(Histogram<>)) return Kind.Histogram;
        if (def == typeof(UpDownCounter<>) || def == typeof(ObservableUpDownCounter<>)
            || def == typeof(ObservableGauge<>)) return Kind.Gauge;
        return null;
    }

    void Record(object? state, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (state is not Spec spec) return;
        value *= spec.Scale;
        var labels = Labels(tags, spec.KeepLabels);
        var series = _series.GetOrAdd(spec.Name + "{" + labels + "}", _ => new Series
        {
            Spec = spec,
            Labels = labels,
            Buckets = spec.Kind == Kind.Histogram ? new long[spec.Buckets!.Length] : null,
        });

        lock (series)
        {
            series.Seen = true;
            if (spec.Kind != Kind.Histogram)
            {
                // A counter's adds and an up-down counter's moves accumulate. An
                // observable's value is reset before each collection, so adding
                // here sums the measurements of that one round - which is also
                // what two pools collapsed into one series should show.
                series.Value += value;
                return;
            }
            series.Sum += value;
            series.Count++;
            var i = Array.FindIndex(spec.Buckets!, edge => value <= edge);
            if (i >= 0) series.Buckets![i]++;
        }
    }

    static string Labels(ReadOnlySpan<KeyValuePair<string, object?>> tags, string[]? keep)
    {
        if (tags.Length == 0) return "";
        var pairs = new List<(string Key, string Value)>(tags.Length);
        foreach (var (key, value) in tags)
        {
            if (keep is not null && Array.IndexOf(keep, key) < 0) continue;
            pairs.Add((LabelName(key), Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")));
        }
        pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return string.Join(",", pairs.Select(p => $"{p.Key}=\"{p.Value}\""));
    }

    static string LabelName(string key)
    {
        var chars = key.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsAsciiLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
        return chars.Length > 0 && char.IsAsciiDigit(chars[0]) ? "_" + new string(chars) : new string(chars);
    }

    static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    static string Number(double value) => value switch
    {
        double.PositiveInfinity => "+Inf",
        double.NegativeInfinity => "-Inf",
        double.NaN => "NaN",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The current values, in the text format Prometheus scrapes.</summary>
    public string Render()
    {
        lock (_collecting)
        {
            foreach (var s in _series.Values)
                if (s.Spec.Observable)
                    lock (s) { s.Value = 0; s.Seen = false; }

            _listener.RecordObservableInstruments();

            // A gauge whose callback had nothing to say this round - the database
            // was away, say - is left out rather than shown at its last value.
            foreach (var (key, s) in _series)
                if (s.Spec.Observable && !s.Seen) _series.TryRemove(key, out _);

            var text = new StringBuilder();
            foreach (var spec in _specs.Values.DistinctBy(s => s.Name).OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                text.Append("# HELP ").Append(spec.Name).Append(' ')
                    .Append(spec.Help.Replace("\\", "\\\\").Replace("\n", "\\n")).Append('\n');
                text.Append("# TYPE ").Append(spec.Name).Append(' ')
                    .Append(spec.Kind.ToString().ToLowerInvariant()).Append('\n');

                foreach (var s in _series.Values.Where(s => s.Spec.Name == spec.Name)
                             .OrderBy(s => s.Labels, StringComparer.Ordinal))
                {
                    lock (s)
                    {
                        if (spec.Kind != Kind.Histogram)
                        {
                            text.Append(spec.Name).Append(Braces(s.Labels)).Append(' ')
                                .Append(Number(s.Value)).Append('\n');
                            continue;
                        }
                        long cumulative = 0;
                        for (var i = 0; i < spec.Buckets!.Length; i++)
                        {
                            cumulative += s.Buckets![i];
                            text.Append(spec.Name).Append("_bucket")
                                .Append(Braces(Join(s.Labels, $"le=\"{Number(spec.Buckets[i])}\"")))
                                .Append(' ').Append(cumulative).Append('\n');
                        }
                        text.Append(spec.Name).Append("_bucket")
                            .Append(Braces(Join(s.Labels, "le=\"+Inf\""))).Append(' ').Append(s.Count).Append('\n');
                        text.Append(spec.Name).Append("_sum").Append(Braces(s.Labels)).Append(' ')
                            .Append(Number(s.Sum)).Append('\n');
                        text.Append(spec.Name).Append("_count").Append(Braces(s.Labels)).Append(' ')
                            .Append(s.Count).Append('\n');
                    }
                }
            }
            return text.ToString();
        }
    }

    static string Join(string labels, string extra) => labels.Length == 0 ? extra : labels + "," + extra;
    static string Braces(string labels) => labels.Length == 0 ? "" : "{" + labels + "}";

    public void Dispose() => _listener.Dispose();
}
