using System;
using System.Collections.Generic;

namespace SuperRealEstate.AppCore
{
    /// <summary>One anonymous product-analytics event.</summary>
    public readonly struct TelemetryEvent
    {
        public readonly string Name;     // e.g. "room_measured", "plant_identified"
        public readonly string Detail;   // optional free-form (no PII)
        public readonly long ValueMillis; // optional numeric (duration/count); 0 if unused

        public TelemetryEvent(string name, string detail = null, long valueMillis = 0)
        {
            Name = name;
            Detail = detail;
            ValueMillis = valueMillis;
        }
    }

    /// <summary>Where telemetry events go (console, Supabase, a queue, …).</summary>
    public interface ITelemetrySink
    {
        void Record(TelemetryEvent evt);
    }

    /// <summary>Drops everything — the default until a real sink is wired.</summary>
    public sealed class NullTelemetrySink : ITelemetrySink
    {
        public void Record(TelemetryEvent evt) { }
    }

    /// <summary>Captures events in memory (for tests / a deferred upload buffer).</summary>
    public sealed class BufferTelemetrySink : ITelemetrySink
    {
        public List<TelemetryEvent> Events { get; } = new List<TelemetryEvent>();
        public void Record(TelemetryEvent evt) => Events.Add(evt);
    }

    /// <summary>
    /// Product analytics with consent baked in: events are forwarded to the sink
    /// ONLY while <see cref="Enabled"/> is true. Enablement is the AND of the
    /// user's Analytics consent and their settings opt-in — both must be true, so
    /// telemetry is off by default and the moment consent is revoked it stops.
    /// Pure (no Unity/network); the sink is where I/O happens. Unit-tested.
    /// </summary>
    public sealed class Telemetry
    {
        private readonly ITelemetrySink _sink;

        public bool Enabled { get; private set; }

        public Telemetry(ITelemetrySink sink, bool enabled = false)
        {
            _sink = sink ?? new NullTelemetrySink();
            Enabled = enabled;
        }

        /// <summary>Turn telemetry on/off (set from consent AND settings opt-in).</summary>
        public void SetEnabled(bool enabled) => Enabled = enabled;

        /// <summary>Record an event if enabled; a no-op otherwise.</summary>
        public void Track(TelemetryEvent evt)
        {
            if (Enabled) _sink.Record(evt);
        }

        /// <summary>Convenience overload.</summary>
        public void Track(string name, string detail = null, long valueMillis = 0)
            => Track(new TelemetryEvent(name, detail, valueMillis));
    }
}
