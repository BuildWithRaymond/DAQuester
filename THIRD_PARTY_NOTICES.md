# Third-party notices

## Avalonia and rendering dependencies

The desktop presentation uses [Avalonia](https://github.com/AvaloniaUI/Avalonia)
11.3.12 under the MIT license. The upstream notice from commit
`37fbd9655cc581ff5b1c6b1fb1be4e3118c889d0` is retained in
[Avalonia/LICENSE.txt](src/DAQuester.App/Licenses/Avalonia/LICENSE.txt).
Build telemetry is excluded from this application's dependency assets.

Rendering uses SkiaSharp 2.88.9 and HarfBuzzSharp 8.3.1.1 under MIT. Their full
licenses and generated native dependency notices are retained from the exact
restored packages under [SkiaSharp](src/DAQuester.App/Licenses/SkiaSharp/) and
[HarfBuzzSharp](src/DAQuester.App/Licenses/HarfBuzzSharp/). The MIT text was
verified against the package source commits
`9699cdf046a989423a5e5eca68e7fd813486d81c` and
`64f24b1cddb68d30ec0ac6b661964178fc21d5ec`, respectively.

Avalonia's COM interop uses MicroCom.Runtime 0.11.0 under MIT. Its
[complete notice](src/DAQuester.App/Licenses/MicroCom.Runtime/LICENSE.txt) is
retained from upstream commit `7e773759cfc1012a5d080d4129c4718a662884b0`, whose
build metadata identifies version 0.11.0. That package's NuGet metadata does not
record a repository commit.

The pinned Tmds.DBus.Protocol 0.21.3 desktop dependency uses MIT. Its
[full upstream notice](src/DAQuester.App/Licenses/Tmds.DBus.Protocol/LICENSE.txt)
is retained from package source commit
`8cb04f66c330b64244e996ff68f685f27f7381a0`.

Release packaging also retains exact NuGet metadata and the .NET runtime
notices under `licenses/`. No game artwork or fonts are bundled; the interface
uses vector paths and Windows system typefaces.

## Lucide icons

Interface icons use [Lucide](https://github.com/lucide-icons/lucide), pinned to
`70562c1ee1c4fdcf736fe97bc893fb8511927934`. The geometry in `Controls/LucideIcons.cs` comes from
upstream SVGs. Lucide's ISC license and the MIT notice for Feather-derived icons
are retained in [Lucide/LICENSE.txt](src/DAQuester.App/Licenses/Lucide/LICENSE.txt)
and included in application packages. No icon font or network access is needed.

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

## DAOrganizer

The saved-character form reader, verified input workflow, ticket substitution,
and character archive/palette rendering conventions
adapt [BuildWithRaymond/DAOrganizer](https://github.com/BuildWithRaymond/DAOrganizer).
DAQuester retains the following upstream notice for those adaptations:

```text
MIT License

Copyright (c) 2026 BuildWithRaymond and DAOrganizer contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## DALib

Character sprite decoding also adapts the MIT-licensed [DALib](https://github.com/Sichii/DALib)
SPF format, and the Arbiter MPF decoder retains its DALib adaptation.
The complete notice is retained in [DALib/LICENSE.txt](src/DAQuester.App/Licenses/DALib/LICENSE.txt)
and shipped with the application. Original game artwork is read from the installed
client; no game artwork is bundled.

## Velopack

Optional updates and Windows packaging use
[Velopack](https://github.com/velopack/velopack), version 1.2.161, under MIT.
The [complete upstream MIT notice](src/DAQuester.App/Licenses/Velopack/LICENSE.txt)
is retained from package source commit `92d6a1c91716729d449034df5c50307dcce39493`
and shipped under `licenses/Velopack/`. The update
source is the public DAQuester repository and requires no account token.

## .NET and test dependencies

Microsoft .NET runtime licenses and notices are copied from the exact restored
core runtime pack into `licenses/` in self-contained distributions.
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

The supported DATester profile also includes functional walkability grids:
one blocked/open flag per tile, recorded dimensions and source SHA-256 digests.
They cover 1,417 recorded maps; the manifest lists 15 unavailable maps. These
grids contain no floor or sprite identifiers, artwork, raw map files or archive
entries. They retain the existing collision rules and explicit map-97 entrance
exception. Their derivation and coverage are documented in
[provenance](docs/provenance.md).

The compass branding is original artwork; interface icons come from Lucide. Screenshots
are actual isolated application renders with sample characters. Segoe UI,
Georgia and Consolas are requested from Windows; no font files are bundled.
This is an independent community project, unaffiliated with the game publisher.
