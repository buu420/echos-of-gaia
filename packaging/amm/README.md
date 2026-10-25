# Echos of Gaia manager release metadata

The game definition is a draft input for `amm-author game add --input` in the
existing `buu420/buu-s-mods` catalog. It adds one game without replacing the
catalog's other games or historical release rows. Release files are hosted in
`buu420/echos-of-gaia`.

`Build-EchosOfGaiaRelease.ps1` first builds with Author CLI 0.30.0, then replaces
its broad folder actions with one explicit file action per validated package
record. Every installed file has a matching SHA-256 verification rule. Staging
manifests, checks and helper scripts are excluded from those actions. The only
lifecycle hook is the read-only pre-install check; the manager owns all game
file writes, update receipts and uninstall restoration. Existing native
accessibility defaults also support installation without rewriting Memoria.ini.

The dependency downloads were resolved from the official
[Microsoft C++ page](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)
and [.NET Framework 4.8 page](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).
The exact downloaded EXEs passed SHA-256 and valid Microsoft Authenticode
signature checks. They were inspected without running them. The public
metadata pins immutable artifact URLs and hashes, rather than moving redirects.

- Visual C++ v14 x86: 14.51.36247.0, 6,941,536 bytes.
- Visual C++ v14 x64: 14.51.36247.0, 18,731,856 bytes.
- .NET Framework 4.8 offline installer: 4.8.04115.00, 121,346,568 bytes.

The C++ check reads the numeric `Minor` value under the architecture-specific
v14 runtime key, requiring at least 51 (14.51). This avoids comparing the
v-prefixed `Version` string in manager versions that do not normalize it. The
.NET check reads `Release`, requiring at least 528040. Both registry views are
checked. System installers require the manager's normal consent/elevation flow;
the mod's own pre-install script needs no elevation and installs nothing.

Windows 10/11 provides XAudio2_9.dll. OS XAudio binaries and C++ runtime DLLs are
excluded from the mod's archives; the package validates native imports against
the selected game and appropriate Windows system directory.
