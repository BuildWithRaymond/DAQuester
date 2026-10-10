# Public repository and optional updates

Approved direction: match the public DA companion repositories, with optional
startup checks, explicit permission to download, and an explicit safe install.

- Publish `BuildWithRaymond/DAQuester` publicly under MIT, retaining Arbiter's
  upstream license and source provenance. Exclude personal Data, credentials,
  generated files, game binaries, and private working copies.
- Provide a player-first README with actual synthetic screenshots, quick start,
  supported quests, operational limits, companion links, and build instructions.
  Include contributor/security guides, changelog, issue/PR templates, Dependabot,
  editor conventions, pinned Windows CI, and documented release tooling.
- Use Velopack 1.2.161, matching DAOrganizer. Produce a self-contained x86
  installer, portable ZIP, full update package, update feed, and SHA-256 sums.
  Tagged releases must pass tests, dark UI smoke checks, and package verification.
- The Settings checkbox is **Check for updates when DAQuester opens**, off by
  default. Startup never downloads. **Check for updates**, **Download update**,
  and **Install and exit** are explicit separate actions. Only stable releases
  from this public repository are used; no GitHub credentials are needed.
- Installation requires no running quest/control task, no connected game session,
  and no still-running DAQuester-launched client. DAQuester never closes game
  processes. Disable Velopack's automatic startup application of pending updates.
- Store data in `%LOCALAPPDATA%/DAQuester/Data`, independent of package replacement.
  Import an adjacent legacy Data folder only into a missing destination, using a
  complete staged copy and atomic directory move. Retain the original. Existing
  destination data always wins; reject reparse points. Document copying legacy
  data when installing to a different directory. Smoke tests use isolated data.
- Verify policy, explicit download consent, busy/connected/launch gates, failures,
  pending updates, migration byte preservation, and existing data protection.
  Verify rendered update states and published package contents. No real gameplay
  is needed for validation, and Excalibur is never restarted.

Implementation sequence: policy and migration tests, core implementation,
Velopack integration and dark Settings, public docs/tooling, local validation,
source publication, hosted CI, then the first stable release.
