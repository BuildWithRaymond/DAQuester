# DAWindower X client integration

The player requested DAWindower X for DAQuester's clients. Use upstream
`BuildWithRaymond/DAWindower` commit `ef70a98275444ffa0c1b306995c5bb579ae4e08a`,
whose X source identifies version 0.2.3 candidate. The older v0.2.2 release
contains the standard edition and cannot satisfy this request.

Bundle the pinned native x86 `DAWindowerX.dll` with the app and load it into each
new hash-validated DATester client while its main thread is still suspended.
Keep existing loopback proxy, multi-instance and agreement patches, then await
successful LoadLibrary before resuming the client. Saved login, scheduler and
owned-window tracking continue using the actual created client PID.

Use the same-bitness Unicode Windows loader route demonstrated in upstream X.
No extra initializer or graphics proxy is needed. Unknown client images, missing
or invalid DLLs and failed/timed-out loads stop before client resume. Cleanup
owns only the newly created suspended process; a live loader's path buffer is
not freed early. Existing clients and Excalibur processes are outside this path.

Alternatives considered were an external DLL setting (extra setup and incomplete
portable packages) and a separate launcher executable (incorrect upstream ABI
and lost client ownership). A pinned bundled DLL matches the upstream one-file
contract and the requested automatic client behavior.

Build native source in ignored artifacts using a small pinned build script;
do not commit generated binaries. Include its MIT notice and source pin. App
builds prepare the cached native dependency; release packaging verifies its
presence. No app-runtime download or automatic component update is introduced.

The later player-approved [new-client handoff](2026-10-14-dawindower-x-plus-design.md)
extends X's + action to request a fresh blank managed login through DAQuester.
A retained, hashed upstream patch implements that handoff. Saved-character
Launch retains normal saved login; standalone X without a host endpoint retains
its upstream new-client behavior.

Validate startup ordering, proxy patches, load failures, cleanup and library
format with synthetic tests. Run both full xUnit projects, Build-Release and
Verify-Release. Build/test operations never launch or operate real characters;
actual renderer/login acceptance is a separate live check.
