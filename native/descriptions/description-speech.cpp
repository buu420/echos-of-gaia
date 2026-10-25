// SPDX-License-Identifier: MPL-2.0
// One private SAPI voice per handle. No calls to the shared screen-reader queue.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <sapi.h>
#include <new>

namespace {
struct Voice {
    ISpVoice* speech;
    DWORD thread;
    bool uninitialize;
    bool paused;
};

HRESULT Check(Voice* voice) {
    if (!voice) return E_POINTER;
    return voice->thread == GetCurrentThreadId() ? S_OK : RPC_E_WRONG_THREAD;
}

HRESULT Stop(Voice* voice) {
    // Purge before resuming: queued speech must never escape a paused stop.
    HRESULT result = voice->speech->Speak(nullptr, SPF_ASYNC | SPF_PURGEBEFORESPEAK, nullptr);
    if (FAILED(result)) return result;
    if (voice->paused) {
        result = voice->speech->Resume();
        if (FAILED(result)) return result;
        voice->paused = false;
    }
    return S_OK;
}
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Create(void** output) {
    if (!output) return E_POINTER;
    *output = nullptr;
    HRESULT result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    // An existing MTA is usable; never uninitialize somebody else's apartment.
    bool uninitialize = SUCCEEDED(result);
    if (FAILED(result) && result != RPC_E_CHANGED_MODE) return result;
    Voice* voice = new (std::nothrow) Voice{nullptr, GetCurrentThreadId(), uninitialize, false};
    if (!voice) {
        if (uninitialize) CoUninitialize();
        return E_OUTOFMEMORY;
    }
    result = CoCreateInstance(CLSID_SpVoice, nullptr, CLSCTX_INPROC_SERVER,
        __uuidof(ISpVoice), reinterpret_cast<void**>(&voice->speech));
    if (SUCCEEDED(result)) result = voice->speech->SetAlertBoundary(SPEI_PHONEME);
    if (FAILED(result)) {
        if (voice->speech) voice->speech->Release();
        delete voice;
        if (uninitialize) CoUninitialize();
        return result;
    }
    *output = voice;
    return S_OK;
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Speak(void* handle, const wchar_t* text, int volume) {
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    if (FAILED(result)) return result;
    if (!text) return E_POINTER;
    if (volume < 0 || volume > 100) return E_INVALIDARG;
    result = voice->speech->SetVolume(static_cast<USHORT>(volume));
    if (FAILED(result)) return result;
    result = Stop(voice);
    if (FAILED(result)) return result;
    return voice->speech->Speak(text, SPF_ASYNC | SPF_PURGEBEFORESPEAK | SPF_IS_NOT_XML, nullptr);
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Pause(void* handle) {
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    if (FAILED(result) || voice->paused) return result;
    result = voice->speech->Pause();
    if (SUCCEEDED(result)) voice->paused = true;
    return result;
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Resume(void* handle) {
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    if (FAILED(result) || !voice->paused) return result;
    result = voice->speech->Resume();
    if (SUCCEEDED(result)) voice->paused = false;
    return result;
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Stop(void* handle) {
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    return FAILED(result) ? result : Stop(voice);
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Playing(void* handle) {
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    if (FAILED(result)) return result;
    result = voice->speech->WaitUntilDone(0);
    if (result == S_OK) return 0;
    if (result == S_FALSE) return 1;
    return FAILED(result) ? result : E_UNEXPECTED;
}

extern "C" __declspec(dllexport) int __cdecl FF9AD_Free(void* handle) {
    if (!handle) return S_OK;
    Voice* voice = static_cast<Voice*>(handle);
    HRESULT result = Check(voice);
    if (FAILED(result)) return result;
    result = Stop(voice);
    // A failed cancellation leaves the handle and COM balance intact for retry.
    if (FAILED(result)) return result;
    bool uninitialize = voice->uninitialize;
    voice->speech->Release();
    delete voice;
    if (uninitialize) CoUninitialize();
    return S_OK;
}
