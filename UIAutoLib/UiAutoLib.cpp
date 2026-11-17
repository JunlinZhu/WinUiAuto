// UiAutoLib.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include <atomic>
#include "framework.h"
#include "UiAutoLib.h"
#include "UiPlayer.h"
#include "BuildPathToUi.h"
#include "FindUiByPath.h"
#include "UiSnapshot.h"

HookProc UserMouseHookCallback = nullptr;
HookProc UserKeyboardHookCallback = nullptr;
HHOOK hookMouse = nullptr;
HHOOK hookKeyboard = nullptr;
static BuildPathToUi* pGetPath = nullptr;
static FindUiByPath* pFindTarget = nullptr;
static UiSnapshot* pSnapshot = nullptr;
std::atomic_bool g_abortLocateUiByPath{ false };

static LRESULT CALLBACK InternalMouseHookCallback(int code, WPARAM wparam, LPARAM lparam)
{
    if (code < 0)
    {
        return CallNextHookEx(hookMouse, code, wparam, lparam);
    }

    if (UserMouseHookCallback != nullptr)
    {
        UserMouseHookCallback(code, wparam, lparam);
    }

    return CallNextHookEx(hookMouse, code, wparam, lparam);
}

static LRESULT CALLBACK InternalKeyboardHookCallback(int code, WPARAM wparam, LPARAM lparam)
{
    if (code < 0)
    {
        return CallNextHookEx(hookKeyboard, code, wparam, lparam);
    }

    if (UserKeyboardHookCallback != nullptr)
    {
        UserKeyboardHookCallback(code, wparam, lparam);
    }

    return CallNextHookEx(hookKeyboard, code, wparam, lparam);
}

// This is the constructor of a class that has been exported.
CUiAutoLib::CUiAutoLib()
{
    return;
}

UIAUTOLIB_CAPI void WINAPI UninitializeHook(HHOOK hHook)
{
    UnhookWindowsHookEx(hHook);
}

UIAUTOLIB_CAPI HHOOK WINAPI SetWindowsHookExNative(HookProc userProc, UINT hookID, UINT uThreadId)
{
    if (hookID == WH_MOUSE_LL)
    {
        UserMouseHookCallback = userProc;
        hookMouse = SetWindowsHookEx(WH_MOUSE_LL, static_cast<HOOKPROC>(InternalMouseHookCallback), g_hModule, 0);
        return hookMouse;
    }
    else if (hookID == WH_KEYBOARD_LL)
    {
        UserKeyboardHookCallback = userProc;
        hookKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, static_cast<HOOKPROC>(InternalKeyboardHookCallback), g_hModule, 0);
        return hookKeyboard;
    }
    else
    {
        return nullptr;
    }
}

UIAUTOLIB_CAPI void WINAPI MouseTask(_In_ LPWSTR plTaskName, _In_  RECT rect, _In_ long offsetX, _In_ long offsetY, _In_ long deltaX, _In_ long deltaY)
{
    if (plTaskName == nullptr)
        return;

    if (wcscmp(plTaskName, L"LeftClick") == 0)
    {
        UiPlayer::Click(rect, offsetX, offsetY);
    }
    else if (wcscmp(plTaskName, L"RightClick") == 0)
    {
        UiPlayer::RightClick(rect, offsetX, offsetY);
    }
    else if (wcscmp(plTaskName, L"LeftDblClick") == 0)
    {
        UiPlayer::DoubleClick(rect, offsetX, offsetY);
    }
    else if (wcscmp(plTaskName, L"Drag") == 0)
    {
        UiPlayer::Drag(rect, offsetX, offsetY, deltaX, deltaY);
    }
    else if (wcscmp(plTaskName, L"MouseWheel") == 0)
    {
        UiPlayer::Wheel(rect, offsetX, offsetY, deltaX, deltaY);
    }
    else if (wcscmp(plTaskName, L"MouseHover") == 0)
    {
        UiPlayer::MouseHover(rect, offsetX, offsetY);
    }
}

UIAUTOLIB_CAPI void WINAPI KeyboardTask(_In_ LPWSTR lpText, _In_ BOOL isCapsLockOn, _In_ BOOL isNumLockOn, _In_ BOOL isScrollLockOn)
{
    UiPlayer::SendText(lpText, isCapsLockOn, isNumLockOn, isScrollLockOn);
}

namespace {
}

UIAUTOLIB_CAPI void WINAPI Init()
{
    if (pGetPath == nullptr)
    {
        pGetPath = new BuildPathToUi();
    }

    if (pFindTarget == nullptr)
    {
        pFindTarget = new FindUiByPath();
    }

    if (pSnapshot == nullptr)
    {
        pSnapshot = new UiSnapshot();
    }
}

UIAUTOLIB_CAPI void WINAPI UnInit()
{
    delete pGetPath;
    pGetPath = nullptr;

    delete pFindTarget;
    pFindTarget = nullptr;

    delete pSnapshot;
    pSnapshot = nullptr;
}

// Returns the node descriptor for the UI element located at the given screen coordinates.
UIAUTOLIB_CAPI ULONG WINAPI CaptureUiPath(_In_ long left, _In_ long top, _In_ BOOL bLeafToRoot, _Out_writes_to_(nMaxCount, return) LPWSTR lpUiPath, _In_ int nMaxCount, _Out_ RECT* pRect)
{
    if (!lpUiPath || nMaxCount <= 0) {
        return E_INVALIDARG;
    }

    if (pGetPath == nullptr)
    {
        Init();
    }

    if (pGetPath == nullptr)
    {
        return E_FAIL;
    }

    std::wstring resultPath;
    HRESULT hr = bLeafToRoot
        ? pGetPath->PathFromLeafToRoot(left, top, resultPath, *pRect)
        : pGetPath->PathFromRootToLeaf(left, top, resultPath, *pRect);

    if (SUCCEEDED(hr)) {
        // Copy the string into the provided LPWSTR buffer safely
        errno_t err = wcscpy_s(lpUiPath, nMaxCount, resultPath.c_str());
        if (err != 0) {
            return E_OUTOFMEMORY; // Buffer too small
        }
    }

    return static_cast<ULONG>(hr);
}

UIAUTOLIB_CAPI ULONG WINAPI InvokeElementAction(_In_ long left, _In_ long top)
{
    if (pGetPath == nullptr)
    {
        Init();
    }

    if (pGetPath == nullptr)
    {
        return E_FAIL;
    }

    return pGetPath->InvokeElementAction(left, top);
}

UIAUTOLIB_CAPI ULONG WINAPI LocateUiByPath(_In_ LPWSTR lpUiPath, _In_ BOOL bFlash, _Out_ RECT * pRect, _Out_writes_to_(nMaxCount, return) LPWSTR lpTargetState, _In_ int nMaxCount)
{
    if (!lpUiPath || !pRect) {
        return E_INVALIDARG;
    }

    if (lpTargetState != nullptr && nMaxCount > 0)
    {
        lpTargetState[0] = L'\0';
    }

    if (pFindTarget == nullptr)
    {
        Init();
    }

    if (pFindTarget == nullptr)
    {
        return E_FAIL;
    }

    std::wstring wstr(lpUiPath);
    std::wstring elementState = L"[]";
    g_abortLocateUiByPath.store(false, std::memory_order_release);
    HRESULT hr = pFindTarget->FindElement(wstr, bFlash, *pRect, elementState);

    if (SUCCEEDED(hr) && lpTargetState != nullptr && nMaxCount > 0)
    {
        errno_t err = wcscpy_s(lpTargetState, nMaxCount, elementState.c_str());
        if (err != 0)
        {
            return E_OUTOFMEMORY;
        }
    }

    return static_cast<ULONG>(hr);
}

UIAUTOLIB_CAPI ULONG WINAPI AbortLocateUiByPath()
{
    g_abortLocateUiByPath.store(true, std::memory_order_release);
    return static_cast<ULONG>(E_ABORT);
}

UIAUTOLIB_CAPI ULONG WINAPI FlashRect(_In_ RECT * pRect, int red, int green, int blue, int duration, int inflate)
{
    if (pRect == nullptr)
    {
        return static_cast<ULONG>(E_INVALIDARG);
    }

    return static_cast<ULONG>(UiTreeWalkCommon::ShowHighlightOverlay(*pRect, RGB(red, green, blue), duration, inflate));
}

namespace {
    ULONG CopySnapshotResult(const std::wstring& json, LPWSTR output, int outputCapacity, int* requiredCapacity)
    {
        if (!requiredCapacity) return E_INVALIDARG;
        if (json.length() >= static_cast<size_t>(INT_MAX)) return E_OUTOFMEMORY;

        *requiredCapacity = static_cast<int>(json.length() + 1);
        if (!output || outputCapacity == 0) return S_OK;
        if (outputCapacity < *requiredCapacity) return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);

        return wcscpy_s(output, outputCapacity, json.c_str()) == 0 ? S_OK : E_FAIL;
    }

    ULONG CaptureSnapshot(HWND rootWindow, const UiSnapshotOptions* options, const UiSnapshotQuery* query,
        LPWSTR output, int outputCapacity, int* requiredCapacity)
    {
        if (!requiredCapacity) return E_INVALIDARG;
        *requiredCapacity = 0;
        if (outputCapacity < 0 || (output == nullptr) != (outputCapacity == 0)) return E_INVALIDARG;
        if (options && options->StructSize < sizeof(UiSnapshotOptions)) return E_INVALIDARG;
        if (query && query->StructSize < sizeof(UiSnapshotQuery)) return E_INVALIDARG;
        if (pSnapshot == nullptr) Init();
        if (pSnapshot == nullptr) return E_FAIL;

        std::wstring json;
        HRESULT hr = pSnapshot->Capture(rootWindow, options, query, json);
        return FAILED(hr) ? static_cast<ULONG>(hr) : CopySnapshotResult(json, output, outputCapacity, requiredCapacity);
    }
}

UIAUTOLIB_CAPI ULONG WINAPI GetUiSnapshot(HWND rootWindow, const UiSnapshotOptions* options,
    LPWSTR output, int outputCapacity, int* requiredCapacity)
{
    return CaptureSnapshot(rootWindow, options, nullptr, output, outputCapacity, requiredCapacity);
}

UIAUTOLIB_CAPI ULONG WINAPI QueryUiSnapshot(HWND rootWindow, const UiSnapshotOptions* options,
    const UiSnapshotQuery* query, LPWSTR output, int outputCapacity, int* requiredCapacity)
{
    if (!query) return E_INVALIDARG;
    return CaptureSnapshot(rootWindow, options, query, output, outputCapacity, requiredCapacity);
}

