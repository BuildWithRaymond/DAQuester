# DAWindower X new-client handoff

The player requested that X's + button use DAQuester, then explicitly chose a
blank managed login window rather than a saved-character picker. Each accepted
click creates a fresh blank client through the existing loopback proxy, supported
image validation, suspended X loading and owned-client tracking. It does not
select a saved character or type credentials. Normal Launch retains its current
saved-character and existing-window behavior.

Use a small retained patch against the pinned MIT upstream source. A managed
client receives a per-app named-pipe endpoint in its own Unicode environment
block before startup. X's existing launch worker sends a bounded request to that
endpoint instead of launching a standalone child. Standalone X without an
endpoint retains upstream behavior. An unavailable managed endpoint reports an
unconfirmed handoff and never falls back to an unmanaged launch.

The current-user-only endpoint accepts only the bounded new-client request and
checks Windows' actual pipe-client PID against live process handles registered
by this app's launch transaction before primary-thread resume. The app checks
ownership again on its dispatch thread. No caller-supplied PID, command line,
character, credential or arbitrary operation is accepted. X's existing pending
worker prevents duplicate clicks during a request. Only the new launch is
cancelled on failure; existing windows remain independent.

Blank launches use distinct internal ownership keys so repeated + actions create
separate clients. Existing named Launch deduplication, scheduler reservations,
login serialization, cancellation and shutdown safety remain in force. Startup
and handoff failures are retained in diagnostics and reported through X's existing
error feedback; no diagnostic interface is added.

Record the exact patch SHA-256 alongside upstream commit and actual DLL hash,
invalidate native caches when it changes, and verify the record in release
archives. Preserve the upstream license and disclose the integration patch.

Use failing synthetic regressions for fresh blank-client behavior, actual pipe
origin/ownership, malformed and unknown callers, cancellation and X's actual +
wiring. Run both complete xUnit projects, build the release and independently
verify its archives. Builds and tests never operate a real character.
