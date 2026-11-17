#pragma once
#include "UiAutoLib.h"
#include "UiTreeWalkCommon.h"

class UiSnapshot : public UiTreeWalkCommon {
public:
    HRESULT Capture(HWND rootWindow, const UiSnapshotOptions* options, const UiSnapshotQuery* query, std::wstring& json);
};
