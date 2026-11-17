#pragma once
#include "pch.h"
#include <windows.h>
#include <ole2.h>
#include <uiautomation.h>
#include <wrl/client.h>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

class UiTreeWalkCommon {
public:
    UiTreeWalkCommon() noexcept;
    virtual ~UiTreeWalkCommon();

    static constexpr int kMaxSiblingScan = 5000;
    static bool IsContainerControlType(const std::wstring& controlType);
    static std::wstring GetControlTypeName(CONTROLTYPEID controlTypeId);
    static std::wstring EncodeJsonValue(const std::wstring& input);
    static std::wstring DecodeJsonValue(const std::wstring& input);
    static HRESULT ShowHighlightOverlay(RECT rc, COLORREF color, UINT durationMs, UINT inflate);
    static void ShutdownHighlightOverlay();

protected:
    HRESULT InitializeUIA();
    void HideHighlightOverlay();

    ComPtr<IUIAutomation6> m_pUIA6;
    ComPtr<IUIAutomationTreeWalker> m_pRawWalker;
    HDC m_hdcScreen;
};