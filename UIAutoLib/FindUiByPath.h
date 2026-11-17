#pragma once
#include "UiTreeWalkCommon.h"

class FindUiByPath : public UiTreeWalkCommon {
public:
    HRESULT FindElement(const std::wstring& nodePath, BOOL bFlash, RECT& outRect, std::wstring& elementState);

private:
    struct NodeSegment {
        std::wstring controlTypeName;
        std::wstring automationId;
        std::wstring className;
        std::wstring name;
        std::wstring itemType;
        int position = 0;
        int lx = 0;
        int ly = 0;
        int x = 0;
        int y = 0;
        int width = 0;
        int height = 0;
        bool hasGeometry = false;
    };

    std::vector<NodeSegment> ParseJson(const std::wstring& json);
    bool ElementMatchesAttributes(IUIAutomationElement* pElement, const NodeSegment& seg,bool ignoreClassNameAndName, bool isLeafSegment, long lx, long ly);
    int CountMatchingIdentityAttributes(IUIAutomationElement* pElement, const NodeSegment& seg, bool isLeafSegment);
    std::vector<ComPtr<IUIAutomationElement>> FindMatchingChildren( BOOL fallbackToPosMatch, IUIAutomationElement* pParent, const NodeSegment& seg, bool isLeafSegment);
    bool ResolveNodePath(IUIAutomationElement* pParent, const std::vector<NodeSegment>& segments, size_t index, bool arrivedViaFallback, ComPtr<IUIAutomationElement>& outElement, bool& outUsedFallback, ComPtr<IUIAutomationElement>& outLastMatch, size_t& outNextSegment);
    bool FindLeafByDescendantSearch(IUIAutomationElement* pParent, const std::vector<NodeSegment>& segments, size_t firstSegment, ComPtr<IUIAutomationElement>& outElement);
    size_t m_lastFailedSegment = SIZE_MAX;
};