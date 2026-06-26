using System;
using System.Collections;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// The lifecycle of any async fetch (catalog, rooms, cost factors, session
    /// alignment). The design system asks for first-class loading / empty / error
    /// states everywhere data is fetched (see docs/DESIGN-SYSTEM.md "States &
    /// resilience"), so feature code carries one of these rather than juggling
    /// loose booleans. <see cref="Empty"/> is distinct from <see cref="Loaded"/>:
    /// a successful fetch that returns nothing is *not* an error and should never
    /// read as one.
    /// </summary>
    public enum LoadPhase
    {
        Idle,    // not yet requested
        Loading, // request in flight (show a calm spinner / skeleton)
        Loaded,  // data present
        Empty,   // succeeded, but there is nothing to show (no rooms yet, etc.)
        Error    // failed; carries a human message + offers retry
    }

    /// <summary>
    /// Immutable load-state model for a single async value of type
    /// <typeparamref name="T"/>. Pure and testable — no UnityEngine dependency.
    /// Build instances with the static factories (<see cref="Loading"/>,
    /// <see cref="Loaded"/>, <see cref="Error"/>) rather than the constructor so
    /// the empty-vs-loaded decision stays in one place.
    /// </summary>
    public readonly struct LoadState<T>
    {
        /// <summary>Where this fetch is in its lifecycle.</summary>
        public LoadPhase Phase { get; }

        /// <summary>
        /// The fetched value, when <see cref="Phase"/> is
        /// <see cref="LoadPhase.Loaded"/> or <see cref="LoadPhase.Empty"/>.
        /// Default(T) otherwise.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// A short, human, on-brand message — the error reason on
        /// <see cref="LoadPhase.Error"/>, otherwise null. Never a raw exception
        /// dump; keep it calm and honest.
        /// </summary>
        public string Message { get; }

        private LoadState(LoadPhase phase, T value, string message)
        {
            Phase = phase;
            Value = value;
            Message = message;
        }

        // --- Convenience predicates (read like the design states) ---
        public bool IsIdle    => Phase == LoadPhase.Idle;
        public bool IsLoading => Phase == LoadPhase.Loading;
        public bool IsLoaded  => Phase == LoadPhase.Loaded;
        public bool IsEmpty   => Phase == LoadPhase.Empty;
        public bool IsError   => Phase == LoadPhase.Error;

        /// <summary>True if there is a value worth rendering (loaded, non-empty).</summary>
        public bool HasValue => Phase == LoadPhase.Loaded;

        /// <summary>The unstarted state — nothing requested yet.</summary>
        public static LoadState<T> Idle()
            => new LoadState<T>(LoadPhase.Idle, default, null);

        /// <summary>Request in flight. Show a calm loading affordance.</summary>
        public static LoadState<T> Loading()
            => new LoadState<T>(LoadPhase.Loading, default, null);

        /// <summary>
        /// A successful fetch. Collapses to <see cref="LoadPhase.Empty"/> when the
        /// value is "nothing to show": null, or an empty collection
        /// (<see cref="ICollection"/> with Count 0, or any non-empty
        /// <see cref="IEnumerable"/> treated as having content). Pass
        /// <paramref name="isEmpty"/> to override the emptiness test for value
        /// types or domain-specific notions of empty.
        /// </summary>
        public static LoadState<T> Loaded(T value, Func<T, bool> isEmpty = null)
            => IsEffectivelyEmpty(value, isEmpty)
                ? new LoadState<T>(LoadPhase.Empty, value, null)
                : new LoadState<T>(LoadPhase.Loaded, value, null);

        /// <summary>
        /// Force the empty state explicitly (e.g. a known "no rooms yet" result),
        /// retaining the value (often an empty list) for the view.
        /// </summary>
        public static LoadState<T> Empty(T value = default)
            => new LoadState<T>(LoadPhase.Empty, value, null);

        /// <summary>
        /// A failed fetch. <paramref name="message"/> must be a calm, human reason
        /// suitable to show the user (the view pairs it with a Retry action).
        /// </summary>
        public static LoadState<T> Error(string message)
            => new LoadState<T>(LoadPhase.Error, default, string.IsNullOrEmpty(message)
                ? "Something went wrong."
                : message);

        private static bool IsEffectivelyEmpty(T value, Func<T, bool> isEmpty)
        {
            if (isEmpty != null) return isEmpty(value);
            if (value == null) return true;
            if (value is string s) return s.Length == 0;
            if (value is ICollection collection) return collection.Count == 0;
            if (value is IEnumerable enumerable) return !enumerable.GetEnumerator().MoveNext();
            return false;
        }
    }
}
