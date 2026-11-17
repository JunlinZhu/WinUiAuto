// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"


HMODULE g_hModule;
BOOL APIENTRY DllMain(HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        break;
    case DLL_THREAD_ATTACH:
        break;
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        // Ensure any global hooks are removed before the DLL is unloaded to avoid
        // the system calling into unloaded code (access violation). The hooks
        // are installed in UiAutoLib.cpp as globals; declare them as extern
        // and unhook here if present.
    {
        extern HHOOK hookMouse;
        extern HHOOK hookKeyboard;
        if (hookMouse)
        {
            UnhookWindowsHookEx(hookMouse);
            hookMouse = NULL;
        }
        if (hookKeyboard)
        {
            UnhookWindowsHookEx(hookKeyboard);
            hookKeyboard = NULL;
        }
    }
    g_hModule = NULL;
    break;
    }
    return TRUE;
}

