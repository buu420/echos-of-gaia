# Owned description speech

`Memoria.DescriptionSpeech.dll` provides a private Windows SAPI `ISpVoice` for
audio-description fallback when a generated recording cannot play. Each handle
owns its voice and queue. Purging, pausing, resuming and disposal affect only that
voice; the adapter never calls Prism, NVDA, Tolk or the game's sound engine.

The installed Unity Mono runtimes do not implement `Type.GetTypeFromProgID`, so
the managed caller uses this small C ABI rather than late-bound COM. The source
uses Windows SDK declarations and links the Windows COM libraries; `/MT` embeds
the MSVC runtime. Both x86 and x64 builds export the same undecorated names.

## ABI

All functions use `cdecl`. Handles are pointer-sized. Text is null-terminated
UTF-16 (`const wchar_t*`). Operations return an HRESULT as a signed 32-bit integer:
zero means success and a negative value means failure.

| Export | Parameters | Behavior |
| --- | --- | --- |
| `FF9AD_Create` | `void** output` | Creates an owned voice; clears output on failure. |
| `FF9AD_Speak` | `void* handle, const wchar_t* text, int volume` | Sets this voice's volume (0 to 100), purges its old queue, clears its pause, and speaks plain text asynchronously. Empty text is allowed. |
| `FF9AD_Pause` | `void* handle` | Pauses at a phoneme boundary; repeated calls are harmless. |
| `FF9AD_Resume` | `void* handle` | Balances this handle's pause once; repeated calls are harmless. |
| `FF9AD_Stop` | `void* handle` | Purges only this voice and clears its pause. |
| `FF9AD_Playing` | `void* handle` | Returns 1 when `WaitUntilDone(0)` is `S_FALSE`, 0 when it is `S_OK`, or a negative HRESULT on failure. |
| `FF9AD_Free` | `void* handle` | Purges and releases the voice. A null handle succeeds. |

Every operation, including `Free`, must run on the thread that called `Create`.
A different thread receives `RPC_E_WRONG_THREAD` without touching the voice.
Creation balances each successful `CoInitializeEx`, including `S_FALSE`, on the
same thread. An existing apartment of a different type is reused without owning
its initialization. No COM work runs in `DllMain` or a finalizer.

After successful `Free`, the caller must clear its handle and never reuse it.
When `Stop` or `Free` fails, retain the handle and keep the DLL loaded; retry on
the creating thread. Do not interpret failed status queries as speech completion.
There is no estimated completion timer and no inference about NVDA's queue.

## Build and validation

Run `tools/Build-DescriptionSpeech.ps1 -OutRoot <absolute separate stage>` from
PowerShell with Visual Studio C++ Build Tools and a Windows SDK installed. It
creates `x86/Memoria.DescriptionSpeech.dll`, `x64/Memoria.DescriptionSpeech.dll`,
build logs and `manifest.json` containing source, architecture and output hashes.
The build rejects output paths inside the source checkout or a detected FFIX
installation, and never deploys files.

Silent probes must use empty text or volume zero. Original-Mono probes should
check both architectures, two independent voices, idempotent pause/resume,
purge while paused, zero-time completion status, and wrong-thread rejection.
These checks do not establish live-game timing or audible narration quality.

References: [SAPI Speak](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee125024(v=vs.85)),
[SAPI WaitUntilDone](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms719834(v=vs.85)).
