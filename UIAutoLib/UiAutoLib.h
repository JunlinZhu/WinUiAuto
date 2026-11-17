#pragma once

// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the UIAUTOLIB_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// UIAUTOLIB_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef UIAUTOLIB_EXPORTS
#define UIAUTOLIB_API __declspec(dllexport)
#else
#define UIAUTOLIB_API __declspec(dllimport)
#endif

// For functions that need a stable, unmangled (C-linkage) export name,
// e.g. for GetProcAddress, P/Invoke, ctypes, or non-MSVC consumers.
#define UIAUTOLIB_CAPI extern "C" UIAUTOLIB_API

typedef void (CALLBACK* HookProc)(int code, WPARAM w, LPARAM l);
extern HMODULE g_hModule;

enum UiSnapshotFlags : ULONG {
	UiSnapshotFlag_None = 0,
	UiSnapshotFlag_IncludeOffscreen = 1 << 0,
	UiSnapshotFlag_InteractiveOnly = 1 << 1,
	UiSnapshotFlag_IncludeValues = 1 << 2
};

enum UiQueryFlags : ULONG {
	UiQueryFlag_None = 0,
	UiQueryFlag_NameContains = 1 << 0,
	UiQueryFlag_AutomationIdContains = 1 << 1,
	UiQueryFlag_ClassNameContains = 1 << 2
};

struct UiSnapshotOptions {
	ULONG StructSize;
	ULONG MaxDepth;
	ULONG MaxElements;
	ULONG MaxVisitedElements;
	ULONG Flags;
};

struct UiSnapshotQuery {
	ULONG StructSize;
	LPCWSTR ControlType;
	LPCWSTR Name;
	LPCWSTR AutomationId;
	LPCWSTR ClassName;
	DWORD ProcessId;
	ULONG Flags;
};

// This class is exported from the dll
class UIAUTOLIB_API CUiAutoLib {
public:
	CUiAutoLib(void);
	// TODO: add your methods here.
};

UIAUTOLIB_CAPI void WINAPI Init();
UIAUTOLIB_CAPI void WINAPI UnInit();

// Returns the node descriptor for the UI element located at the given screen coordinates.
// Set bLeafToRoot to true to walk from leaf to root, or false to walk from root to leaf.
UIAUTOLIB_CAPI ULONG WINAPI CaptureUiPath(_In_ long left, _In_ long top, _In_ BOOL bLeafToRoot, _Out_writes_to_(nMaxCount, return) LPWSTR lpUiPath, _In_ int nMaxCount, _Out_ RECT* pRect);

// Locates a UI element by node path and optionally flashes it, returning its bounding rectangle and state JSON.
UIAUTOLIB_CAPI ULONG WINAPI LocateUiByPath(_In_ LPWSTR lpUiPath, _In_ BOOL bFlash, _Out_ RECT * pRect, _Out_writes_to_(nMaxCount, return) LPWSTR lpTargetState, _In_ int nMaxCount);
UIAUTOLIB_CAPI ULONG WINAPI AbortLocateUiByPath();
UIAUTOLIB_CAPI ULONG WINAPI FlashRect(_In_ RECT* pRect, int red, int green, int blue, int duration, int inflate);
UIAUTOLIB_CAPI ULONG WINAPI InvokeElementAction(_In_ long left, _In_ long top);

// Returns bounded flat JSON rooted at rootWindow, or at the desktop when rootWindow is null.
// Set output to null and outputCapacity to zero to query requiredCapacity, including the terminator.
UIAUTOLIB_CAPI ULONG WINAPI GetUiSnapshot(
	_In_opt_ HWND rootWindow,
	_In_opt_ const UiSnapshotOptions* options,
	_Out_writes_to_opt_(outputCapacity, *requiredCapacity) LPWSTR output,
	_In_ int outputCapacity,
	_Out_ int* requiredCapacity);

// Returns the same bounded JSON shape containing only nodes that match query.
UIAUTOLIB_CAPI ULONG WINAPI QueryUiSnapshot(
	_In_opt_ HWND rootWindow,
	_In_opt_ const UiSnapshotOptions* options,
	_In_ const UiSnapshotQuery* query,
	_Out_writes_to_opt_(outputCapacity, *requiredCapacity) LPWSTR output,
	_In_ int outputCapacity,
	_Out_ int* requiredCapacity);
