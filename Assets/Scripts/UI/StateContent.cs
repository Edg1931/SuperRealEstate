namespace SuperRealEstate.UI
{
    /// <summary>
    /// The text shown on a loading / empty / error card. Data-only on purpose —
    /// no UnityEngine rendering here; a view binds these strings into a Spatial
    /// Glass panel. The voice is calm, honest, and content-forward (see
    /// docs/DESIGN-SYSTEM.md): say the one thing that matters, never blame the
    /// user, and offer a clear next step.
    /// </summary>
    public readonly struct StateContent
    {
        /// <summary>One short line — the headline of the card.</summary>
        public string Title { get; }

        /// <summary>A sentence or two of reassuring, plain-language context.</summary>
        public string Message { get; }

        /// <summary>
        /// Label for the single primary action (e.g. "Try again", "Scan a room"),
        /// or null when the card is purely informational.
        /// </summary>
        public string ActionLabel { get; }

        /// <summary>
        /// True for failure states. The view should render the error treatment
        /// (the <see cref="DesignTokens.Caution"/> role, paired with an icon —
        /// never color alone), not the neutral empty treatment. Estimated /
        /// uncertain content uses <see cref="DesignTokens.Advisory"/> instead.
        /// </summary>
        public bool IsError { get; }

        public StateContent(string title, string message, string actionLabel = null, bool isError = false)
        {
            Title = title;
            Message = message;
            ActionLabel = actionLabel;
            IsError = isError;
        }
    }

    /// <summary>
    /// Calm, on-brand presets for the common empty/error/loading moments. Centralized
    /// so the product's voice stays consistent and every card reads premium and
    /// honest, never gimmicky.
    /// </summary>
    public static class StatePresets
    {
        // --- Loading (neutral; pair with a calm spinner / skeleton) ---

        /// <summary>While the room/space alignment is settling.</summary>
        public static StateContent AligningSession => new StateContent(
            "Getting your bearings",
            "Look slowly around the room so we can lock onto the space.");

        /// <summary>Generic in-flight fetch (catalog, cost factors, etc.).</summary>
        public static StateContent Loading => new StateContent(
            "One moment",
            "Bringing this in…");

        // --- Empty (success, but nothing to show; neutral, inviting) ---

        /// <summary>The finishes / furniture catalog returned no items.</summary>
        public static StateContent NoCatalog => new StateContent(
            "Nothing here yet",
            "This catalog is empty for now. New finishes and pieces show up here as they're added.");

        /// <summary>No rooms have been scanned or saved yet — the first-run zero state.</summary>
        public static StateContent NoRoomsYet => new StateContent(
            "Start with one room",
            "Scan a room to measure it and see the cost. It takes about a minute.",
            actionLabel: "Scan a room");

        // --- Error (failure; renders with the caution treatment + an icon) ---

        /// <summary>We couldn't reach the network / backend.</summary>
        public static StateContent OfflineError => new StateContent(
            "You're offline",
            "We can't reach the network right now. Your scans are safe — try again when you're back.",
            actionLabel: "Try again",
            isError: true);

        /// <summary>Session alignment failed and needs another pass.</summary>
        public static StateContent AlignFailed => new StateContent(
            "Couldn't map the space",
            "We lost track of the room. Step back, make sure there's enough light, and try the scan again.",
            actionLabel: "Try again",
            isError: true);

        /// <summary>
        /// A feature is gated off on this device / plan / build — graceful
        /// degradation, not a hard error. Advisory in tone, never alarming.
        /// </summary>
        public static StateContent FeatureUnavailable(string reason) => new StateContent(
            "Not available here",
            string.IsNullOrEmpty(reason)
                ? "This isn't available on this device yet."
                : reason,
            isError: false);
    }
}
