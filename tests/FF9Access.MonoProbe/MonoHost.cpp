#include <windows.h>
#include <stdio.h>
#include <stdlib.h>

// A separate process hosts the installed runtime; nothing is injected into FF9.
template<typename T> T Bind(HMODULE module, const char* name) {
    FARPROC address = GetProcAddress(module, name);
    if (!address) { fprintf(stderr, "Missing Mono export: %s\n", name); exit(2); }
    return reinterpret_cast<T>(address);
}
int main(int argc, char** argv) {
    if (argc < 5) { fprintf(stderr, "MonoHost mono.dll managed-directory config-directory probe.exe [probe arguments]\n"); return 2; }
    HMODULE module = LoadLibraryA(argv[1]);
    if (!module) { fprintf(stderr, "Mono load error: %lu\n", GetLastError()); return 2; }
    Bind<void(__cdecl*)(const char*)>(module, "mono_set_assemblies_path")(argv[2]);
    Bind<void(__cdecl*)(const char*, const char*)>(module, "mono_set_dirs")(argv[2], argv[3]);
    Bind<void(__cdecl*)(const char*)>(module, "mono_config_parse")(NULL);
    void* rootDomain = Bind<void*(__cdecl*)(const char*, const char*)>(module, "mono_jit_init_version")("FF9Access.SpeechProbe", "v2.0.50727");
    void* domain = rootDomain;
    if (!domain) { fprintf(stderr, "Mono domain initialization failed\n"); return 2; }
    if (getenv("FF9ACCESS_PROBE_CHILD_DOMAIN")) {
        domain = Bind<void*(__cdecl*)(char*, char*)>(module, "mono_domain_create_appdomain")(const_cast<char*>("Unity Child Domain"), NULL);
        if (!domain || !Bind<int(__cdecl*)(void*, int)>(module, "mono_domain_set")(domain, 0)) {
            fprintf(stderr, "Mono child domain initialization failed\n"); return 2;
        }
        printf("Probe running in a Unity-style child domain\n");
    }
    void* assembly = Bind<void*(__cdecl*)(void*, const char*)>(module, "mono_domain_assembly_open")(domain, argv[4]);
    if (!assembly) { fprintf(stderr, "Mono probe assembly could not load\n"); return 2; }
    if (getenv("FF9ACCESS_PROBE_WAIT_TRACE")) Sleep(5000);
    int result = Bind<int(__cdecl*)(void*, void*, int, char**)>(module, "mono_jit_exec")(domain, assembly, argc - 4, argv + 4);
    // Match Mono's executable host: join foreground threads, stop its thread
    // pool and finish background-thread detachment before freeing the runtime.
    // Keep Main's result and the real domain/JIT cleanup, including failures.
    Bind<void(__cdecl*)(void)>(module, "mono_thread_manage")();
    if (domain != rootDomain) {
        Bind<int(__cdecl*)(void*, int)>(module, "mono_domain_set")(rootDomain, 0);
        Bind<void(__cdecl*)(void*)>(module, "mono_domain_unload")(domain);
    }
    Bind<void(__cdecl*)(void*)>(module, "mono_jit_cleanup")(rootDomain);
    return result;
}
