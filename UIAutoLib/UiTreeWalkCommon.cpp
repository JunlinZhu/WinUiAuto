#include "pch.h"
#include <windowsx.h>
#include <sstream>
#include <memory>
#include <mutex>
#include "UiTreeWalkCommon.h"
#include "ControlTypeId.h"

namespace {
    constexpr wchar_t kHighlightWndClass[] = L"UiTreeWalkHighlightOverlay";
    constexpr UINT_PTR kHighlightTimerId = 1;
    constexpr UINT WM_HIGHLIGHT_SHOW = WM_APP + 1;
    constexpr UINT WM_HIGHLIGHT_SHUTDOWN = WM_APP + 2;
    constexpr UINT WM_HIGHLIGHT_HIDE = WM_APP + 3;
    constexpr COLORREF kTransparentKey = RGB(1, 2, 3);

    // Visual tuning for the outline frame.
    constexpr int kOutlinePenWidth = 3;
    constexpr int kDesktopFrameEraseMargin = 4; // slop added when invalidating the old frame's screen area
    constexpr LONG kSmallElementHeightThreshold = 30;

    struct ShowHighlightMsgParams {
        RECT rc;
        COLORREF color;
        UINT durationMs;
        UINT inflate;
    };

    // Touched only from the overlay thread's message loop (HighlightWndProc and the
    // helpers it calls), so plain globals are safe without extra synchronization.
    RECT g_desktopFrameRect{};
    bool g_desktopFrameVisible = false;

    // Strokes an unfilled rectangle outline in `color`, restoring the DC's prior
    // pen/brush selection. Shared by the screen-DC frame and the layered window's
    // own WM_PAINT so both draw identically.
    void DrawRectOutline(HDC hdc, const RECT& rc, COLORREF color, int penWidth) {
        HBRUSH hOldBrush = SelectBrush(hdc, GetStockBrush(NULL_BRUSH));
        HPEN hPen = ::CreatePen(PS_SOLID, penWidth, color);
        if (hPen) {
            HPEN hOldPen = SelectPen(hdc, hPen);
            ::Rectangle(hdc, rc.left, rc.top, rc.right, rc.bottom);
            SelectPen(hdc, hOldPen);
            ::DeleteObject(hPen);
        }
        SelectBrush(hdc, hOldBrush);
    }

    void HideDesktopFrame() {
        if (!g_desktopFrameVisible) return;

        RECT redrawRect = g_desktopFrameRect;
        ::InflateRect(&redrawRect, kDesktopFrameEraseMargin, kDesktopFrameEraseMargin);
        ::RedrawWindow(nullptr, &redrawRect, nullptr,
            RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
        g_desktopFrameVisible = false;
    }

    // Shell surfaces such as Start can occupy a privileged z-band above HWND_TOPMOST,
    // so the same frame is also drawn directly to the screen DC to stay visible over them.
    void ShowDesktopFrame(const RECT& rc, COLORREF color) {
        HDC hdc = ::GetDC(nullptr);
        if (!hdc) return;

        DrawRectOutline(hdc, rc, color, kOutlinePenWidth);
        g_desktopFrameRect = rc;
        g_desktopFrameVisible = true;

        ::ReleaseDC(nullptr, hdc);
    }

    // Kills the hide timer, hides the layered window, and erases the screen-DC frame.
    // Used by every code path that needs to stop showing a highlight (explicit hide,
    // timer expiry, a new highlight replacing the old one, and shutdown).
    void StopOverlayAndHideFrame(HWND hwnd) {
        ::KillTimer(hwnd, kHighlightTimerId);
        ::ShowWindow(hwnd, SW_HIDE);
        HideDesktopFrame();
    }

    // Small/short elements (height below the threshold) get the frame inflated
    // outward so the outline doesn't cover the element itself; larger elements get
    // it inset inward so the frame hugs their bounds instead of ballooning outward.
    RECT AdjustHighlightRect(RECT rc, UINT inflate) {
        int amount = static_cast<int>(inflate);
        if (rc.bottom - rc.top >= kSmallElementHeightThreshold) {
            amount = -amount;
        }
        ::InflateRect(&rc, amount, amount);
        return rc;
    }

    LRESULT CALLBACK HighlightWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
        switch (msg) {
        case WM_PAINT: {
            PAINTSTRUCT ps;
            HDC hdc = ::BeginPaint(hwnd, &ps);
            RECT rcClient;
            ::GetClientRect(hwnd, &rcClient);

            HBRUSH hKeyBrush = ::CreateSolidBrush(kTransparentKey);
            ::FillRect(hdc, &rcClient, hKeyBrush);
            ::DeleteObject(hKeyBrush);

            COLORREF color = static_cast<COLORREF>(::GetWindowLongPtr(hwnd, GWLP_USERDATA));
            RECT rcOutline = rcClient;
            ::InflateRect(&rcOutline, -1, -1); // inset so the stroke stays within the client area
            DrawRectOutline(hdc, rcOutline, color, kOutlinePenWidth);

            ::EndPaint(hwnd, &ps);
            return 0;
        }
        case WM_HIGHLIGHT_HIDE:
            StopOverlayAndHideFrame(hwnd);
            return 0;
        case WM_HIGHLIGHT_SHOW: {
            std::unique_ptr<ShowHighlightMsgParams> params(
                reinterpret_cast<ShowHighlightMsgParams*>(lParam));

            // Stop whatever highlight is currently showing (if any) before starting the new one.
            StopOverlayAndHideFrame(hwnd);

            // Make the window fully transparent so stale content can never be visible
            // while it is being moved/resized/repainted.
            ::SetLayeredWindowAttributes(hwnd, kTransparentKey, 0, LWA_COLORKEY | LWA_ALPHA);

            RECT rc = AdjustHighlightRect(params->rc, params->inflate);

            ::SetWindowLongPtr(hwnd, GWLP_USERDATA, static_cast<LONG_PTR>(params->color));
            ::SetWindowPos(hwnd, HWND_TOPMOST,
                rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // Paint the new frame synchronously (still invisible).
            ::RedrawWindow(hwnd, nullptr, nullptr,
                RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW);

            // Now reveal the freshly painted frame.
            ::SetLayeredWindowAttributes(hwnd, kTransparentKey, 255, LWA_COLORKEY | LWA_ALPHA);

            ShowDesktopFrame(rc, params->color);

            ::SetTimer(hwnd, kHighlightTimerId, params->durationMs, nullptr);
            return 0;
        }
        case WM_TIMER:
            if (wParam == kHighlightTimerId) {
                StopOverlayAndHideFrame(hwnd);
            }
            return 0;
        case WM_HIGHLIGHT_SHUTDOWN:
            StopOverlayAndHideFrame(hwnd);
            ::DestroyWindow(hwnd);
            return 0;
        case WM_DESTROY:
            ::PostQuitMessage(0);
            return 0;
        default:
            return ::DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }

    void EnsureHighlightWndClassRegistered() {
        static std::once_flag registeredOnce;
        std::call_once(registeredOnce, []() {
            WNDCLASSEX wc = { sizeof(WNDCLASSEX) };
            wc.lpfnWndProc = HighlightWndProc;
            wc.hInstance = ::GetModuleHandle(nullptr);
            wc.lpszClassName = kHighlightWndClass;
            wc.hCursor = ::LoadCursor(nullptr, IDC_ARROW);
            ::RegisterClassEx(&wc);
            });
    }

    struct HighlightOverlayState {
        HANDLE hThread = nullptr;
        HWND hwnd = nullptr;
        HANDLE hReadyEvent = nullptr;
        HRESULT startupResult = E_UNEXPECTED;
    };

    HighlightOverlayState& GetHighlightOverlayState() {
        static HighlightOverlayState state;
        return state;
    }

    DWORD WINAPI RunHighlightOverlayThread(LPVOID lpParam) {
        auto& state = GetHighlightOverlayState();
        EnsureHighlightWndClassRegistered();

        HWND hwnd = ::CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST,
            kHighlightWndClass, L"", WS_POPUP, 0, 0, 0, 0,
            nullptr, nullptr, ::GetModuleHandle(nullptr), nullptr);

        if (hwnd) {
            ::SetLayeredWindowAttributes(hwnd, kTransparentKey, 0, LWA_COLORKEY);
            state.startupResult = S_OK;
        }
        else {
            state.startupResult = HRESULT_FROM_WIN32(::GetLastError());
        }

        state.hwnd = hwnd;
        ::SetEvent(state.hReadyEvent);
        if (!hwnd) return 0;

        MSG msg;
        while (::GetMessage(&msg, nullptr, 0, 0) > 0) {
            ::TranslateMessage(&msg);
            ::DispatchMessage(&msg);
        }

        state.hwnd = nullptr;
        return 0;
    }

    HRESULT EnsureHighlightOverlayThreadRunning() {
        auto& state = GetHighlightOverlayState();
        static std::once_flag startedOnce;
        std::call_once(startedOnce, [&state]() {
            state.hReadyEvent = ::CreateEvent(nullptr, TRUE, FALSE, nullptr);
            if (!state.hReadyEvent) {
                state.startupResult = HRESULT_FROM_WIN32(::GetLastError());
                return;
            }

            state.hThread = ::CreateThread(
                nullptr, 0, RunHighlightOverlayThread, nullptr, 0, nullptr);

            if (state.hThread) {
                ::WaitForSingleObject(state.hReadyEvent, INFINITE);
            }
            else {
                state.startupResult = HRESULT_FROM_WIN32(::GetLastError());
            }
            });

        return state.startupResult;
    }
}

UiTreeWalkCommon::UiTreeWalkCommon() noexcept
    : UiTreeWalkCommon(true) {
}

UiTreeWalkCommon::UiTreeWalkCommon(bool initializeAutomation) noexcept
    : m_hdcScreen(nullptr), m_comInitialized(false) {
    if (!initializeAutomation) return;

    m_hdcScreen = ::GetDC(nullptr);
    m_comInitialized = SUCCEEDED(::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED));
    InitializeUIA();
}

UiTreeWalkCommon::~UiTreeWalkCommon() {
    if (m_hdcScreen) ::ReleaseDC(nullptr, m_hdcScreen);
    if (m_comInitialized) ::CoUninitialize();
}

void UiTreeWalkCommon::HideHighlightOverlay() {
    HWND hwnd = GetHighlightOverlayState().hwnd;
    if (hwnd) {
        ::PostMessage(hwnd, WM_HIGHLIGHT_HIDE, 0, 0);
    }
}

HRESULT UiTreeWalkCommon::InitializeUIA() {
    HRESULT hr = ::CoCreateInstance(
        CLSID_CUIAutomation8, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_pUIA6));

    if (FAILED(hr)) {
        ComPtr<IUIAutomation> pBaseUIA;
        hr = ::CoCreateInstance(
            CLSID_CUIAutomation, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&pBaseUIA));
        if (SUCCEEDED(hr)) {
            hr = pBaseUIA.As(&m_pUIA6);
        }
    }

    if (SUCCEEDED(hr)) {
        hr = m_pUIA6->get_RawViewWalker(&m_pRawWalker);
    }

    return hr;
}

std::wstring UiTreeWalkCommon::GetControlTypeName(CONTROLTYPEID controlTypeId) {
    std::wstring programmaticName = L"Element";
    if (controlTypeId >= UIA_ButtonControlTypeId && controlTypeId <= UIA_AppBarControlTypeId) {
        size_t index = static_cast<size_t>(controlTypeId - UIA_ButtonControlTypeId);
        if (index < sizeof(gc_controlTypesTable) / sizeof(gc_controlTypesTable[0])) {
            programmaticName = gc_controlTypesTable[index].pszName;
        }
    }
    return programmaticName;
}

HRESULT UiTreeWalkCommon::ShowHighlightOverlay(RECT rc, COLORREF color, UINT durationMs, UINT inflate) {
    HRESULT hr = EnsureHighlightOverlayThreadRunning();
    if (FAILED(hr)) return hr;

    auto params = std::make_unique<ShowHighlightMsgParams>(
        ShowHighlightMsgParams{ rc, color, durationMs,inflate });

    if (::PostMessage(GetHighlightOverlayState().hwnd, WM_HIGHLIGHT_SHOW, 0,
        reinterpret_cast<LPARAM>(params.get()))) {
        params.release();
        return S_OK;
    }

    return HRESULT_FROM_WIN32(::GetLastError());
}

void UiTreeWalkCommon::ShutdownHighlightOverlay() {
    auto& state = GetHighlightOverlayState();
    if (!state.hThread) return;

    if (state.hwnd) {
        ::PostMessage(state.hwnd, WM_HIGHLIGHT_SHUTDOWN, 0, 0);
    }

    ::WaitForSingleObject(state.hThread, 2000);
    ::CloseHandle(state.hThread);
    state.hThread = nullptr;

    if (state.hReadyEvent) {
        ::CloseHandle(state.hReadyEvent);
        state.hReadyEvent = nullptr;
    }
}

std::wstring UiTreeWalkCommon::EncodeJsonValue(const std::wstring& input) {
    std::wstringstream ss;
    for (wchar_t ch : input) {
        switch (ch) {
        case L'"':  ss << L"\\\""; break;
        case L'\\': ss << L"\\\\"; break;
        case L'\b': ss << L"\\b";  break;
        case L'\f': ss << L"\\f";  break;
        case L'\n': ss << L"\\n";  break;
        case L'\r': ss << L"\\r";  break;
        case L'\t': ss << L"\\t";  break;
        default:
            if (ch < 0x20) {
                wchar_t buf[8];
                swprintf_s(buf, L"\\u%04x", static_cast<unsigned int>(ch));
                ss << buf;
            }
            else {
                ss << ch;
            }
            break;
        }
    }
    return ss.str();
}

std::wstring UiTreeWalkCommon::DecodeJsonValue(const std::wstring& input) {
    std::wstring out;
    out.reserve(input.size());

    size_t i = 0;
    const size_t len = input.length();

    while (i < len) {
        wchar_t ch = input[i];

        if (ch != L'\\') {
            out += ch;
            ++i;
            continue;
        }

        if (i + 1 >= len) {
            out += ch;
            ++i;
            continue;
        }

        wchar_t next = input[i + 1];
        switch (next) {
        case L'"':  out += L'"';  i += 2; break;
        case L'\\': out += L'\\'; i += 2; break;
        case L'/':  out += L'/';  i += 2; break;
        case L'b':  out += L'\b'; i += 2; break;
        case L'f':  out += L'\f'; i += 2; break;
        case L'n':  out += L'\n'; i += 2; break;
        case L'r':  out += L'\r'; i += 2; break;
        case L't':  out += L'\t'; i += 2; break;
        case L'u':
            if (i + 6 <= len &&
                iswxdigit(input[i + 2]) && iswxdigit(input[i + 3]) &&
                iswxdigit(input[i + 4]) && iswxdigit(input[i + 5]))
            {
                unsigned int codeUnit = static_cast<unsigned int>(
                    wcstoul(input.substr(i + 2, 4).c_str(), nullptr, 16));
                out += static_cast<wchar_t>(codeUnit);
                i += 6;
            }
            else {
                out += ch;
                ++i;
            }
            break;
        default:
            out += ch;
            ++i;
            break;
        }
    }

    return out;
}

bool UiTreeWalkCommon::IsContainerControlType(const std::wstring& controlType) {
    return controlType == L"Pane" ||
        controlType == L"Group" ||
        controlType == L"Custom";
}