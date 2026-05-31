#include <windows.h>
#include <string.h>

extern IMAGE_DOS_HEADER __ImageBase;

typedef BOOL (WINAPI *GetFileVersionInfoA_t)(LPCSTR, DWORD, DWORD, LPVOID);
typedef DWORD (WINAPI *GetFileVersionInfoSizeA_t)(LPCSTR, LPDWORD);
typedef BOOL (WINAPI *VerQueryValueA_t)(LPCVOID, LPCSTR, LPVOID *, PUINT);

static HMODULE version_module;
static GetFileVersionInfoA_t real_GetFileVersionInfoA;
static GetFileVersionInfoSizeA_t real_GetFileVersionInfoSizeA;
static VerQueryValueA_t real_VerQueryValueA;

static void load_version_module(void)
{
    if (version_module != NULL) {
        return;
    }

    char system_dir[MAX_PATH];
    GetSystemDirectoryA(system_dir, MAX_PATH);
    lstrcatA(system_dir, "\\VERSION.dll");
    version_module = LoadLibraryA(system_dir);

    real_GetFileVersionInfoA = (GetFileVersionInfoA_t)GetProcAddress(version_module, "GetFileVersionInfoA");
    real_GetFileVersionInfoSizeA = (GetFileVersionInfoSizeA_t)GetProcAddress(version_module, "GetFileVersionInfoSizeA");
    real_VerQueryValueA = (VerQueryValueA_t)GetProcAddress(version_module, "VerQueryValueA");
}

static DWORD WINAPI load_melonloader_bootstrap(void *parameter)
{
    (void)parameter;

    char module_path[MAX_PATH];
    char game_dir[MAX_PATH];
    char dependency_dir[MAX_PATH];
    char managed_dir[MAX_PATH];
    char mlproxy_path[MAX_PATH];
    char dobby_path[MAX_PATH];
    char bootstrap_path[MAX_PATH];

    if (GetModuleFileNameA((HMODULE)&__ImageBase, module_path, MAX_PATH) == 0) {
        return 1;
    }

    lstrcpyA(game_dir, module_path);
    char *last_slash = strrchr(game_dir, '\\');
    if (last_slash == NULL) {
        return 1;
    }
    *last_slash = '\0';

    lstrcpyA(dependency_dir, game_dir);
    lstrcatA(dependency_dir, "\\MelonLoader\\Dependencies");
    SetDllDirectoryA(dependency_dir);

    lstrcpyA(managed_dir, game_dir);
    lstrcatA(managed_dir, "\\MelonLoader\\Managed");
    SetEnvironmentVariableA("MONO_PATH", managed_dir);
    SetEnvironmentVariableA("MONO_PATH_64", managed_dir);

    lstrcpyA(mlproxy_path, game_dir);
    lstrcatA(mlproxy_path, "\\mlproxy.dll");
    LoadLibraryA(mlproxy_path);

    lstrcpyA(dobby_path, dependency_dir);
    lstrcatA(dobby_path, "\\dobby.dll");
    LoadLibraryA(dobby_path);

    lstrcpyA(bootstrap_path, dependency_dir);
    lstrcatA(bootstrap_path, "\\Bootstrap.dll");
    LoadLibraryA(bootstrap_path);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)instance;
    (void)reserved;

    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);
        load_version_module();
        CreateThread(NULL, 0, load_melonloader_bootstrap, NULL, 0, NULL);
    }

    return TRUE;
}

__declspec(dllexport) BOOL WINAPI GetFileVersionInfoA(LPCSTR filename, DWORD handle, DWORD len, LPVOID data)
{
    load_version_module();
    return real_GetFileVersionInfoA(filename, handle, len, data);
}

__declspec(dllexport) DWORD WINAPI GetFileVersionInfoSizeA(LPCSTR filename, LPDWORD handle)
{
    load_version_module();
    return real_GetFileVersionInfoSizeA(filename, handle);
}

__declspec(dllexport) BOOL WINAPI VerQueryValueA(LPCVOID block, LPCSTR sub_block, LPVOID *buffer, PUINT len)
{
    load_version_module();
    return real_VerQueryValueA(block, sub_block, buffer, len);
}
