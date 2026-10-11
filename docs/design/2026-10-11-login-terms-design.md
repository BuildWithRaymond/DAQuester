# Skip the launch agreement like DAOrganizer

The player requested DAOrganizer's agreement bypass after the attempted
accept-click fix missed the actual OK button. DAOrganizer changes two conditional
branches before resuming its newly launched supported client.

DAQuester applies the same `75` to `EB` changes at `0x4B897C` and `0x4B8ACF`,
preserving their relative jump distances. Both checksum and downloaded-content
paths skip Agreement construction and retain normal client processing.

Patches affect only a freshly created, suspended client after supported-image
validation. Existing write/readback checks still verify each write. Game files
on disk, already-running clients, Excalibur and user data remain untouched.
Remove the unsuccessful terms-click stage; saved login again waits for the
normal menu readiness gate, visible login fields and unblocked input.

An offline synthetic patch test checks both branches and every surrounding byte.
Existing field-entry, cancellation and native-reader regressions remain. Run
focused login/launcher tests, the full suite, Release build and
both behavior test projects. No real character is operated by these checks.

The follow-up lobby/redirect concern is checked with synthetic TCP peers. Send
greeting, encryption, stipulation and redirect together or in two-byte fragments,
including while initial handler setup is held behind a gate. End the lobby stream
immediately, verify all forwarded packets and unchanged transfer metadata, then
connect to the dedicated redirect listener and confirm replacement identity under
a new generation. Existing transport already handles these cases; no production
transport change is justified by these results alone.
