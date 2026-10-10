# Third-party notices

## Arbiter

Protocol serialization, security, opcodes, types, and message layouts in
`src/DAQuester.DarkAges/Vendor/Arbiter.Net` are from
[ewrogers/Arbiter](https://github.com/ewrogers/Arbiter), pinned to
`3b5af118bfc3310189b42879d2bab59205af9c88`. Copyright 2025 Erik Rogers.
The complete MIT notice is retained in
[ARBITER-LICENSE.txt](src/DAQuester.DarkAges/Vendor/ARBITER-LICENSE.txt)
and included in application packages. The proxy/UI framework was not imported.
Namespace/target adaptations and observed packet variants are described in
[source provenance](docs/provenance.md).

## Velopack

Optional updates and Windows packaging use
[Velopack](https://github.com/velopack/velopack), version 1.2.161, under MIT.
The [complete upstream MIT notice](src/DAQuester.App/Licenses/Velopack/LICENSE.txt)
is retained from package source commit `92d6a1c91716729d449034df5c50307dcce39493`
and shipped under `licenses/Velopack/`. The update
source is the public DAQuester repository and requires no account token.

## .NET and test dependencies

Microsoft .NET runtime licenses and notices are copied from the exact restored
core/desktop runtime packs into `licenses/` in self-contained distributions.
Microsoft.NET.Test.Sdk, xUnit, and xunit.runner.visualstudio are development-only
dependencies; their upstream licenses apply.

## Community references and original work

DAQuester's original code and vector artwork are covered by the root MIT license.
Framing and launcher adaptations and route-only observations from the owner's
Excalibur project are documented in [provenance](docs/provenance.md). The private
Excalibur working copy is not distributed.

The quest catalog contains factual names, requirements, reward metadata and
links to [Vorlof](https://vorlof.com/quests.html). Full guide prose and images
are not redistributed. Short server dialog fixtures are retained as protocol
test evidence, with observed and assisted results clearly labeled. They do not
relicense the game's underlying dialogue or imply endorsement.

Dark Ages, its client, maps, sprites and other game assets belong to their
respective owners. Raw game files are read from a user's installation and are
not included in this repository or release packages. Routes contain normalized
map identifiers, labels and portal observations, rather than raw map files.

The compass and other UI drawings are original code-native artwork. Screenshots
are actual isolated application renders with sample characters. Segoe UI,
Georgia and Consolas are requested from Windows; no font files are bundled.
This is an independent community project, unaffiliated with the game publisher.
