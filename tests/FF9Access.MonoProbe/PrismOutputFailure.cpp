#include <cstdint>

// Test-only C ABI fixture: successful initialization followed by rejected output.
// This is loaded only from the isolated probe directory, never installed.
extern "C" {
void* __cdecl prism_init(void*) { return reinterpret_cast<void*>(1); }
void __cdecl prism_shutdown(void*) { }
void* __cdecl prism_registry_create_best(void*) { return reinterpret_cast<void*>(2); }
void __cdecl prism_backend_free(void*) { }
const char* __cdecl prism_backend_name(void*) { return "Injected output failure"; }
int __cdecl prism_backend_output(void*, const char*, bool) { return 6; }
int __cdecl prism_backend_is_speaking(void*, bool* speaking) { *speaking = false; return 0; }
int __cdecl prism_backend_stop(void*) { return 0; }
std::uint64_t __cdecl prism_backend_get_features(void*) { return (1ULL << 2) | (1ULL << 5); }
}
