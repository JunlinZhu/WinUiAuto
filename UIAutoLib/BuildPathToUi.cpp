#include "pch.h"
#include <sstream>
#include <string>
#include <utility>
#include <vector>
#include "UiTreeWalkCommon.h"
#include "BuildPathToUi.h"

namespace {

    // Hard stop so a misbehaving provider (cyclic parent/child links, absurdly
    // deep trees) cannot cause an endless loop or a stack overflow.
    constexpr int kMaxTreeDepth = 256;

    // Values longer than this are truncated and emitted as "contains:<prefix>".
    constexpr size_t kMaxMatchLength = 100;

    // Works on the RAW (unescaped) text. Truncating after JSON-escaping could cut
    // an escape sequence in half (e.g. "\u00e9" or "\"") and produce invalid JSON,
    // so callers must escape the result, not the input.
    std::wstring TruncateAndPrependContains(const std::wstring& input) {
        if (input.length() <= kMaxMatchLength) {
            return input;
        }

        size_t truncateAt = kMaxMatchLength;
        // Do not split a surrogate pair.
        if (IS_HIGH_SURROGATE(input[truncateAt - 1])) {
            --truncateAt;
        }

        return L"contains:" + input.substr(0, truncateAt);
    }

    // Single serializer shared by both walkers so the output format cannot drift.
    // Expects segments ordered root-first, leaf-last. Produces: [{seg},{seg},...]
    std::wstring SerializePath(const std::vector<std::wstring>& rootFirstSegments) {
        std::wstringstream ss;
        ss << L"[";
        for (size_t i = 0; i < rootFirstSegments.size(); ++i) {
            if (i > 0) ss << L",";
            ss << L"{" << rootFirstSegments[i] << L"}";
        }
        ss << L"]";
        return ss.str();
    }

    // Returns "" when the element has no ExpandCollapse pattern.
    std::wstring GetExpandCollapseStateName(IUIAutomationElement* pElement) {
        ComPtr<IUIAutomationExpandCollapsePattern> spExpand;
        if (FAILED(pElement->GetCurrentPatternAs(UIA_ExpandCollapsePatternId, IID_PPV_ARGS(&spExpand))) || !spExpand) {
            return L"";
        }

        ExpandCollapseState state = ExpandCollapseState_Collapsed;
        if (FAILED(spExpand->get_CurrentExpandCollapseState(&state))) {
            return L"";
        }

        switch (state) {
        case ExpandCollapseState_Collapsed:          return L"Collapsed";
        case ExpandCollapseState_Expanded:           return L"Expanded";
        case ExpandCollapseState_PartiallyExpanded:  return L"PartiallyExpanded";
        case ExpandCollapseState_LeafNode:           return L"LeafNode";
        default:                                     return L"Unknown";
        }
    }

    // Takes ownership of the BSTR (frees it) and copies it length-aware, so
    // embedded NULs do not silently truncate the value.
    std::wstring ExtractAndFree(BSTR& bstr) {
        std::wstring str;
        if (bstr) {
            str.assign(bstr, ::SysStringLen(bstr));
            ::SysFreeString(bstr);
            bstr = nullptr;
        }
        return str;
    }

} // namespace

BuildPathToUi::BuildPathToUi(bool initializeAutomation) noexcept
    : UiTreeWalkCommon(initializeAutomation) {
}

// 1-based index of pCurrent among the siblings of the same control type, or 0
// if it was not found (or the sibling scan limit was hit first).
int BuildPathToUi::GetElementPosition(IUIAutomationElement* pCurrent,
    IUIAutomationElement* pParent,
    CONTROLTYPEID controlTypeId)
{
    if (!pCurrent || !pParent || !m_pRawWalker || !m_pUIA6) return 0;

    ComPtr<IUIAutomationElement> pChild;
    HRESULT hr = m_pRawWalker->GetFirstChildElement(pParent, &pChild);

    int sameTypeCount = 0;
    int scanned = 0;

    while (SUCCEEDED(hr) && pChild) {
        if (++scanned > kMaxSiblingScan) break;

        CONTROLTYPEID childType = 0;
        if (SUCCEEDED(pChild->get_CurrentControlType(&childType)) && childType == controlTypeId) {
            ++sameTypeCount;

            BOOL isSame = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(pChild.Get(), pCurrent, &isSame)) && isSame) {
                return sameTypeCount;
            }
        }

        ComPtr<IUIAutomationElement> pNext;
        hr = m_pRawWalker->GetNextSiblingElement(pChild.Get(), &pNext);

        // Some providers return the same element as its own next sibling.
        if (SUCCEEDED(hr) && pNext) {
            BOOL isSameSibling = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(pChild.Get(), pNext.Get(), &isSameSibling)) && isSameSibling) {
                break;
            }
        }

        pChild = pNext;
    }

    return 0;
}

// Returns the members of one path node WITHOUT the surrounding braces.
std::wstring BuildPathToUi::GetElementJsonSegment(IUIAutomationElement9* pElement, int position, int left, int top) {
    if (!pElement) return L"";

    CONTROLTYPEID controlTypeId = 0;
    pElement->get_CurrentControlType(&controlTypeId);
    const std::wstring programmaticName = GetControlTypeName(controlTypeId);

    BSTR bstrAutoId = nullptr;
    BSTR bstrClassName = nullptr;
    BSTR bstrName = nullptr;
    BSTR bstrFullDescription = nullptr;

    pElement->get_CurrentAutomationId(&bstrAutoId);
    pElement->get_CurrentClassName(&bstrClassName);
    pElement->get_CurrentName(&bstrName);
    pElement->get_CurrentFullDescription(&bstrFullDescription);

    // Keep the RAW strings; escaping happens only when writing them out.
    const std::wstring autoId = ExtractAndFree(bstrAutoId);
    const std::wstring className = ExtractAndFree(bstrClassName);
    const std::wstring name = ExtractAndFree(bstrName);
    const std::wstring fullDescription = ExtractAndFree(bstrFullDescription);

    RECT rect{};
    if (FAILED(pElement->get_CurrentBoundingRectangle(&rect))) {
        rect = RECT{};
    }

    const std::wstring expandCollapseState = GetExpandCollapseStateName(pElement);

    std::wstringstream ss;
    ss << L"\"controlType\":\"" << EncodeJsonValue(programmaticName) << L"\"";
    ss << L",\"position\":" << position;

    if (!autoId.empty())              ss << L",\"automationId\":\"" << EncodeJsonValue(TruncateAndPrependContains(autoId)) << L"\"";
    if (!className.empty())           ss << L",\"className\":\"" << EncodeJsonValue(TruncateAndPrependContains(className)) << L"\"";
    if (!name.empty())                ss << L",\"name\":\"" << EncodeJsonValue(TruncateAndPrependContains(name)) << L"\"";
    if (!fullDescription.empty())     ss << L",\"fullDescription\":\"" << EncodeJsonValue(fullDescription) << L"\"";
    if (!expandCollapseState.empty()) ss << L",\"expandCollapseState\":\"" << expandCollapseState << L"\"";

    ss << L",\"x\":" << rect.left;
    ss << L",\"y\":" << rect.top;
    ss << L",\"w\":" << (rect.right - rect.left);
    ss << L",\"h\":" << (rect.bottom - rect.top);
    ss << L",\"lx\":" << (left - rect.left);
    ss << L",\"ly\":" << (top - rect.top);

    return ss.str();
}

// The one place that turns (element, parent) into a path segment. Used by the
// bottom-up walk, the top-down search and the fallback, so all three produce
// identical segments. pParent == nullptr means "root": position is 0.
// Returns "" if the segment could not be built.
std::wstring BuildPathToUi::BuildSegment(IUIAutomationElement* pElement, IUIAutomationElement* pParent, int left, int top) {
    if (!pElement) return L"";

    ComPtr<IUIAutomationElement9> pElement9;
    if (FAILED(pElement->QueryInterface(IID_PPV_ARGS(&pElement9))) || !pElement9) return L"";

    int position = 0;
    if (pParent) {
        CONTROLTYPEID controlTypeId = 0;
        pElement->get_CurrentControlType(&controlTypeId);
        position = GetElementPosition(pElement, pParent, controlTypeId);
    }

    return GetElementJsonSegment(pElement9.Get(), position, left, top);
}

HRESULT BuildPathToUi::PathFromLeafToRoot(long left, long top, std::wstring& nodePath, RECT& outRect) {
    nodePath.clear();
    ::ZeroMemory(&outRect, sizeof(outRect));

    if (!m_pUIA6 || !m_pRawWalker) return E_POINTER;

    POINT pt = { left, top };

    ComPtr<IUIAutomationElement> pCurrent;
    HRESULT hr = m_pUIA6->ElementFromPoint(pt, &pCurrent);
    if (FAILED(hr) || !pCurrent) {
        return FAILED(hr) ? hr : E_FAIL;
    }

    pCurrent->get_CurrentBoundingRectangle(&outRect);
    ShowHighlightOverlay(outRect, RGB(255, 32, 32), 10000, 2);

    std::vector<std::wstring> leafFirst;

    for (int depth = 0; pCurrent; ++depth) {
        if (depth >= kMaxTreeDepth) return E_FAIL;   // cyclic or absurdly deep parent chain

        ComPtr<IUIAutomationElement> pParent;
        hr = m_pRawWalker->GetParentElement(pCurrent.Get(), &pParent);
        if (FAILED(hr)) return hr;

        // pParent is null for the root, which BuildSegment reports as position 0.
        std::wstring segment = BuildSegment(pCurrent.Get(), pParent.Get(), static_cast<int>(left), static_cast<int>(top));
        if (!segment.empty()) {
            leafFirst.push_back(std::move(segment));
        }

        pCurrent = pParent;   // becomes null after the root, ending the loop
    }

    if (leafFirst.empty()) return E_FAIL;

    // Collected leaf-first; the output format is root-first.
    const std::vector<std::wstring> rootFirst(leafFirst.rbegin(), leafFirst.rend());
    nodePath = SerializePath(rootFirst);
    ShowHighlightOverlay(outRect, RGB(32, 255, 32), 10000, 2);

    return S_OK;
}

// Depth-first search from pParent for pTarget. On S_OK, jsonSegments holds the
// segments of every node below pParent down to and including pTarget
// (pParent's own segment is added by the caller). On failure, jsonSegments is
// left exactly as it was passed in.
HRESULT BuildPathToUi::FindTargetElement(IUIAutomationElement* pParent, IUIAutomationElement* pTarget,
    std::vector<std::wstring>& jsonSegments, RECT& rectTarget, int left, int top, int depth)
{
    if (!pParent || !pTarget || !m_pUIA6 || !m_pRawWalker) return E_POINTER;

    BOOL isSameElement = FALSE;
    if (SUCCEEDED(m_pUIA6->CompareElements(pParent, pTarget, &isSameElement)) && isSameElement) {
        return S_OK;
    }

    if (depth >= kMaxTreeDepth) return E_FAIL;

    RECT rectParent{};
    pParent->get_CurrentBoundingRectangle(&rectParent);
    if (rectParent.left == rectTarget.left &&
        rectParent.top == rectTarget.top &&
        rectParent.right == rectTarget.right &&
        rectParent.bottom == rectTarget.bottom)
    {
        return S_OK;
    }

    ComPtr<IUIAutomationElement> pChild;
    HRESULT hr = m_pRawWalker->GetFirstChildElement(pParent, &pChild);

    int scanned = 0;

    while (SUCCEEDED(hr) && pChild) {
        if (++scanned > kMaxSiblingScan) break;

        RECT childRect{};
        pChild->get_CurrentBoundingRectangle(&childRect);

        // The target came from ElementFromPoint(left, top), so every real
        // ancestor must cover that point. Testing the point (rather than the
        // target's whole rectangle) stays correct when the target is only
        // partly visible or extends past its parent's clip. Elements without
        // an area are always searched, since they can still have children.
         const bool hasArea = (childRect.right > childRect.left) && (childRect.bottom > childRect.top);
        const bool mayContain = !hasArea || (
            rectTarget.left >= childRect.left && rectTarget.right <= childRect.right &&
            rectTarget.top >= childRect.top && rectTarget.bottom <= childRect.bottom);

        if (mayContain) {
            std::wstring segment = BuildSegment(pChild.Get(), pParent, left, top);
            const bool pushed = !segment.empty();
            if (pushed) {
                jsonSegments.push_back(std::move(segment));
            }

            if (S_OK == FindTargetElement(pChild.Get(), pTarget, jsonSegments, rectTarget, left, top, depth + 1)) {
                return S_OK;
            }

            if (pushed) {
                jsonSegments.pop_back();
            }
        }

        ComPtr<IUIAutomationElement> pNext;
        hr = m_pRawWalker->GetNextSiblingElement(pChild.Get(), &pNext);

        // Some providers return the same element as its own next sibling.
        // That is a dead end, not a match.
        if (SUCCEEDED(hr) && pNext) {
            BOOL isSameSibling = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(pChild.Get(), pNext.Get(), &isSameSibling)) && isSameSibling) {
                break;
            }
        }

        pChild = pNext;
    }

    return E_FAIL;
}

HRESULT BuildPathToUi::PathFromRootToLeaf(long left, long top, std::wstring& nodePath, RECT& outRect) {
    nodePath.clear();
    ::ZeroMemory(&outRect, sizeof(outRect));

    if (!m_pUIA6 || !m_pRawWalker) return E_POINTER;

    POINT pt = { left, top };

    ComPtr<IUIAutomationElement> pTarget;
    HRESULT hr = m_pUIA6->ElementFromPoint(pt, &pTarget);
    if (FAILED(hr) || !pTarget) {
        return FAILED(hr) ? hr : E_FAIL;
    }

    pTarget->get_CurrentBoundingRectangle(&outRect);
    ShowHighlightOverlay(outRect, RGB(255, 32, 32), 10000, 2);

    ComPtr<IUIAutomationElement> pRoot;
    hr = m_pUIA6->GetRootElement(&pRoot);
    if (FAILED(hr) || !pRoot) return FAILED(hr) ? hr : E_FAIL;

    std::vector<std::wstring> jsonSegments;

    // The root has no parent, so its sibling position is always 0.
    std::wstring rootSegment = BuildSegment(pRoot.Get(), nullptr, static_cast<int>(left), static_cast<int>(top));
    if (rootSegment.empty()) return E_FAIL;
    jsonSegments.push_back(std::move(rootSegment));

    const size_t baseCount = jsonSegments.size();

    HRESULT findHr = FindTargetElement(pRoot.Get(), pTarget.Get(), jsonSegments, outRect, left, top, 0);

    if (FAILED(findHr)) {
        // Discard any partial path and fall back to walking up from the target.
        jsonSegments.resize(baseCount);
        if (BuildPathViaParentWalk(pRoot.Get(), pTarget.Get(), jsonSegments,
            static_cast<int>(left), static_cast<int>(top))) {
            findHr = S_OK;
        }
    }

    if (SUCCEEDED(findHr)) {
        // jsonSegments is already root-first.
        nodePath = SerializePath(jsonSegments);

        pTarget->get_CurrentBoundingRectangle(&outRect);
        // bright cyan/aqua for the final target, so it stands out from initial red
        ShowHighlightOverlay(outRect, RGB(16, 255, 255), 10000, 2);
    }

    return findHr;
}

// Fallback for when the top-down search cannot reach the leaf: walk up from
// the leaf to the root and append the nodes below the root, root-first, using
// the same segments PathFromLeafToRoot produces. The root's own segment must
// already be in jsonSegments.
bool BuildPathToUi::BuildPathViaParentWalk(IUIAutomationElement* pRoot, IUIAutomationElement* pLeaf,
    std::vector<std::wstring>& jsonSegments, int left, int top)
{
    if (!pRoot || !pLeaf || !m_pUIA6 || !m_pRawWalker) return false;

    std::vector<std::wstring> leafFirst;
    ComPtr<IUIAutomationElement> pCurrent(pLeaf);
    bool reachedRoot = false;

    for (int depth = 0; pCurrent && depth < kMaxTreeDepth; ++depth) {
        BOOL isRoot = FALSE;
        if (SUCCEEDED(m_pUIA6->CompareElements(pCurrent.Get(), pRoot, &isRoot)) && isRoot) {
            reachedRoot = true;
            break;
        }

        ComPtr<IUIAutomationElement> pParent;
        HRESULT hr = m_pRawWalker->GetParentElement(pCurrent.Get(), &pParent);
        if (FAILED(hr) || !pParent) break;   // chain ended without reaching the root

        std::wstring segment = BuildSegment(pCurrent.Get(), pParent.Get(), left, top);
        if (!segment.empty()) {
            leafFirst.push_back(std::move(segment));
        }

        pCurrent = pParent;
    }

    if (!reachedRoot) return false;

    jsonSegments.insert(jsonSegments.end(), leafFirst.rbegin(), leafFirst.rend());
    return true;
}

HRESULT BuildPathToUi::InvokeElementAction(int left, int top)
{
    if (!m_pUIA6 || !m_pRawWalker) return E_POINTER;

    POINT pt = { left, top };

    ComPtr<IUIAutomationElement> pCurrent;
    HRESULT hr = m_pUIA6->ElementFromPoint(pt, &pCurrent);

    while (SUCCEEDED(hr) && pCurrent)
    {
        ComPtr<IUIAutomationInvokePattern> invoke;
        hr = pCurrent->GetCurrentPatternAs(
            UIA_InvokePatternId,
            IID_PPV_ARGS(&invoke));

        if (SUCCEEDED(hr) && invoke)
        {
            hr = invoke->Invoke();
            break;
        }

        ComPtr<IUIAutomationElement> parent;
        hr = m_pRawWalker->GetParentElement(pCurrent.Get(), &parent);
        pCurrent = parent;
    }

    return hr;
}