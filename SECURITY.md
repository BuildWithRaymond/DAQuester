# Security policy

The latest stable release is supported. Install updates explicitly through
Settings or download a verified package from this repository's Releases page.

Report suspected vulnerabilities privately using
[GitHub's vulnerability reporting form](https://github.com/BuildWithRaymond/DAQuester/security/advisories/new).
Include the affected version, reproduction steps and potential impact. Remove
credentials, login traffic, session keys and personal game data from attachments.
Avoid public disclosure until maintainers can assess and address the report.

Relevant boundaries include untrusted packet parsing, local control pipe access,
credential storage, update packages and archive handling. Ordinary quest bugs or
UI issues belong in the public issue tracker.

Packages currently have no publisher code-signing certificate. Velopack verifies
downloaded packages against the release feed, and releases provide SHA-256 sums.
These checks detect mismatched files; they depend on the GitHub repository's
integrity and do not substitute for publisher signing. See [release trust](docs/RELEASING.md).
