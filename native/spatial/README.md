# Steam Audio field cues

Pinned Valve Steam Audio 4.6.1 binaries, obtained from SteamAudio.NET.Natives 4.6.1 on NuGet (manifest includes package and binary SHA256). Native binary contents are unchanged; `phonon.dll` is renamed `Memoria.SpatialAudio.dll` to keep the mod dependency distinct. LICENSE.md includes Valve's Apache 2.0 terms and third-party notices. Source: https://github.com/ValveSoftware/steam-audio/tree/v4.6.1 .

Only the built-in HRTF/binaural C API is used. GPU simulation and optional OpenCL/TrueAudio dependencies are unused. Required imports are Windows system DLLs. The managed worker loads the architecture-matched DLL using an absolute path, without changing the process DLL search path. x86 exports use stdcall decoration; x64 exports are undecorated. Calls use the 4.6.1 header ABI and release effects, HRTF, buffers and context on worker teardown.

The game thread supplies current, detached cue snapshots. The worker verifies three canonical 24 kHz mono PCM16 recordings, processes 512-sample blocks into stereo, and owns three bounded WinMM buffers. Narration has a separate device and unchanged mono path. A missing backend produces one spoken/logged failure; there is no pan-only HRTF fallback.
