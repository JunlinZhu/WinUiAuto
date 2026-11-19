#pragma once
#include <string>
#include <vector>
#include "UiTreeWalkCommon.h"

class BuildPathToUi : public UiTreeWalkCommon {
public:
    BuildPathToUi() noexcept = default;
    HRESULT PathFromLeafToRoot(long left, long top, std::wstring& nodePath, RECT& outRect);
    HRESULT PathFromRootToLeaf(long left, long top, std::wstring& nodePath, RECT& outRect);
    HRESULT InvokeElementAction(int x, int y);

protected:
    explicit BuildPathToUi(bool initializeAutomation) noexcept;

private:
    int GetElementPosition(IUIAutomationElement* pCurrent, IUIAutomationElement* pParent, CONTROLTYPEID controlTypeId);
    std::wstring GetElementJsonSegment(IUIAutomationElement9* pElement, int position, int left, int top);
    std::wstring BuildSegment(IUIAutomationElement* pElement, IUIAutomationElement* pParent, int left, int top);
    HRESULT FindTargetElement(IUIAutomationElement* pParent, IUIAutomationElement* pTarget, std::vector<std::wstring>& jsonSegments, RECT& rectTarget, int left, int top, int depth);
    bool BuildPathViaParentWalk(IUIAutomationElement* pRoot, IUIAutomationElement* pLeaf, std::vector<std::wstring>& jsonSegments, int left, int top);
};