# Memoria runtime support

The manifest pins the exact community support and public Memoria default assets
that accompany this fork. Original Steam executables, UnityEngine/firstpass,
Steam/native game plugins, movies and packed game assets are excluded.

The Data, Shaders and shared FF9_Data defaults come from the tracked
Memoria.Patcher trees in this source checkout. Memoria.Scripts.dll is compiled
from Memoria.Scripts against the staged game assembly rather than copied from
the developer's installation. Memoria uses the MIT license at accessibility/LICENSE.
UnityEngine.UI uses the separately included accessibility/UnityEngine.UI_LICENSE.

The six native DLLs are the exact bytes already vendored in this fork's source
baseline 0b5f29fcded398e1ffbc2e09ed55b1117af8a7f7. Their SHA256, size,
architecture and direct PE imports are recorded in manifest.json. They have not
been rebuilt here; the license source snapshots below identify the projects and
license text, not a claim that these binaries were rebuilt from those snapshots.

- SaXAudio: SamsamTS/SaXAudio, MIT. License retained from source commit
  1504ed27965d1de89805a619c48b5f27f972ea8c:
  https://github.com/SamsamTS/SaXAudio/blob/1504ed27965d1de89805a619c48b5f27f972ea8c/LICENSE
- SoLoud: Jari Komppa, zlib/libpng. License retained from source commit
  e82fd32c1f62183922f08c14c814a02b58db1873:
  https://github.com/jarikomppa/soloud/blob/e82fd32c1f62183922f08c14c814a02b58db1873/LICENSE
- JoyShockLibrary: Julian Smart and contributors, MIT. The exact license already
  vendored in Memoria.XInputDotNetPure/JoyShockLibrary is retained, including its
  third-party acknowledgments.

SaXAudio uses the Windows XAudio2_9.dll and the Microsoft Visual C++ v14 runtime.
Windows 10/11 system XAudio2 and separately installed Microsoft VC runtime
components are prerequisites. No Windows OS or VC runtime DLL is redistributed
in this package. In particular the inherited XAudio2_9.dll copies are excluded.

launcher-packages contains the actual NuGet package metadata for all eight
launcher runtime dependencies (not just direct PackageReference entries), plus
the LICENSE.TXT and THIRD-PARTY-NOTICES.TXT files supplied in those packages.
SharpCompress 0.50.5's MIT license is retained from its package-recorded source
commit e4193af83b6ec8bdd5fb4f6ebffc2315dd3fc626. The Microsoft maintenance
packages declare MIT in their retained nuspec files; .NET Foundation MIT license
text and the package-supplied third-party notices are included.
