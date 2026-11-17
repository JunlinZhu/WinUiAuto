#include "pch.h"
#include <fstream>
#include <sstream>
#include <functional>
#include <algorithm>
#include <atomic>
#include "UiTreeWalkCommon.h"
#include "FindUiByPath.h"

extern std::atomic_bool g_abortLocateUiByPath;

namespace {
    static std::wstring GetElementStateLine(
        IUIAutomationElement* pElement,
        int depth,
        const std::wstring& role,
        const std::wstring& indent,
        const std::function<std::wstring(CONTROLTYPEID)>& getTypeNameFunc)
    {
        if (!pElement) return L"";

        BSTR bstr = nullptr;
        CONTROLTYPEID controlTypeId = 0;
        pElement->get_CurrentControlType(&controlTypeId);
        std::wstring controlTypeName = getTypeNameFunc(controlTypeId);

        pElement->get_CurrentName(&bstr);
        std::wstring name = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
        if (bstr) ::SysFreeString(bstr);

        pElement->get_CurrentClassName(&bstr);
        std::wstring className = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
        if (bstr) ::SysFreeString(bstr);

        pElement->get_CurrentAutomationId(&bstr);
        std::wstring automationId = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
        if (bstr) ::SysFreeString(bstr);

        RECT rc{};
        std::wstring rectText = L"";
        if (SUCCEEDED(pElement->get_CurrentBoundingRectangle(&rc))) {
            std::wstringstream ss;
            ss << L"(" << rc.left << L"," << rc.top << L"," << rc.right - rc.left << L"," << rc.bottom - rc.top << L")";
            rectText = ss.str();
        }

        std::wstringstream line;
        line << indent << role << L" depth=" << depth;
        if (!controlTypeName.empty()) line << L",controlType=" << controlTypeName;
        if (!automationId.empty()) line << L",automationId=" << automationId;
        if (!className.empty()) line << L",className=" << className;
        if (!name.empty()) line << L",name=" << name;

        if (role == L"Target") {
            BOOL enabled = FALSE;
            BOOL offscreen = FALSE;
            pElement->get_CurrentIsEnabled(&enabled);
            pElement->get_CurrentIsOffscreen(&offscreen);

            pElement->get_CurrentLocalizedControlType(&bstr);
            std::wstring localizedControlType = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentHelpText(&bstr);
            std::wstring helpText = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentItemStatus(&bstr);
            std::wstring itemStatus = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentAccessKey(&bstr);
            std::wstring accessKey = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentAcceleratorKey(&bstr);
            std::wstring acceleratorKey = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentAriaRole(&bstr);
            std::wstring ariaRole = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            pElement->get_CurrentAriaProperties(&bstr);
            std::wstring ariaProperties = (bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
            if (bstr) ::SysFreeString(bstr);

            std::wstring legacyValue = L"";
            {
                ComPtr<IUIAutomationLegacyIAccessiblePattern> spLegacy;
                if (SUCCEEDED(pElement->GetCurrentPatternAs(UIA_LegacyIAccessiblePatternId, __uuidof(IUIAutomationLegacyIAccessiblePattern), (void**)&spLegacy)) && spLegacy) {
                    BSTR bstrValue = nullptr;
                    if (SUCCEEDED(spLegacy->get_CurrentValue(&bstrValue)) && bstrValue && ::SysStringLen(bstrValue) > 0)
                        legacyValue = bstrValue;
                    if (bstrValue) ::SysFreeString(bstrValue);
                }
            }

            std::wstring expandCollapseState = L"";
            {
                ComPtr<IUIAutomationExpandCollapsePattern> spExpand;
                HRESULT hrExp = pElement->GetCurrentPatternAs(UIA_ExpandCollapsePatternId, __uuidof(IUIAutomationExpandCollapsePattern), (void**)&spExpand);
                if (SUCCEEDED(hrExp) && spExpand) {
                    ExpandCollapseState state = ExpandCollapseState_Collapsed;
                    if (SUCCEEDED(spExpand->get_CurrentExpandCollapseState(&state))) {
                        switch (state) {
                        case ExpandCollapseState_Collapsed: expandCollapseState = L"Collapsed"; break;
                        case ExpandCollapseState_Expanded: expandCollapseState = L"Expanded"; break;
                        case ExpandCollapseState_PartiallyExpanded: expandCollapseState = L"PartiallyExpanded"; break;
                        case ExpandCollapseState_LeafNode: expandCollapseState = L"LeafNode"; break;
                        default: expandCollapseState = L"Unknown"; break;
                        }
                    }
                }
            }

            std::wstring value = L"";
            {
                ComPtr<IUIAutomationValuePattern> spValue;
                if (SUCCEEDED(pElement->GetCurrentPatternAs(UIA_ValuePatternId, __uuidof(IUIAutomationValuePattern), (void**)&spValue)) && spValue) {
                    BSTR bstrValue = nullptr;
                    if (SUCCEEDED(spValue->get_CurrentValue(&bstrValue)) && bstrValue && ::SysStringLen(bstrValue) > 0)
                        value = bstrValue;
                    if (bstrValue) ::SysFreeString(bstrValue);
                }
            }

            if (!localizedControlType.empty()) line << L",localizedControlType=" << localizedControlType;
            if (!helpText.empty()) line << L",helpText=" << helpText;
            if (!itemStatus.empty()) line << L",itemStatus=" << itemStatus;
            if (!accessKey.empty()) line << L",accessKey=" << accessKey;
            if (!acceleratorKey.empty()) line << L",acceleratorKey=" << acceleratorKey;
            if (!ariaRole.empty()) line << L",ariaRole=" << ariaRole;
            if (!ariaProperties.empty()) line << L",ariaProperties:" << ariaProperties;
            if (!value.empty()) line << L",value=" << value;
            if (!legacyValue.empty() && legacyValue != value) line << L",legacyValue=" << legacyValue;
            line << L",enabled=" << ((enabled == TRUE) ? L"true" : L"false");
            line << L",state=" << ((offscreen == TRUE) ? L"Offscreen" : L"Visible");
            if (!expandCollapseState.empty()) line << L",expandCollapseState=" << expandCollapseState;
        }

        if (!rectText.empty()) line << L",rect=" << rectText;
        line << L"\r\n";
        return line.str();
    }
}

std::vector<FindUiByPath::NodeSegment> FindUiByPath::ParseJson(const std::wstring& json) {
    std::vector<NodeSegment> segments;
    size_t pos = 0;
    const size_t len = json.length();

    while (pos < len) {
        size_t objStart = json.find(L'{', pos);
        if (objStart == std::wstring::npos) break;
        size_t objEnd = json.find(L'}', objStart);
        if (objEnd == std::wstring::npos) break;

        std::wstring obj = json.substr(objStart + 1, objEnd - objStart - 1);
        NodeSegment seg;

        auto GetValue = [&](const std::wstring& key) -> std::wstring {
            std::wstring pattern = L"\"" + key + L"\"";
            size_t k = obj.find(pattern);
            if (k == std::wstring::npos) return L"";
            size_t colon = obj.find(L':', k + pattern.length());
            if (colon == std::wstring::npos) return L"";
            size_t valStart = obj.find_first_not_of(L" \t\r\n", colon + 1);
            if (valStart == std::wstring::npos) return L"";
            if (obj[valStart] == L'\"') {
                size_t valEnd = obj.find(L'\"', valStart + 1);
                if (valEnd == std::wstring::npos) return L"";
                return obj.substr(valStart + 1, valEnd - valStart - 1);
            }
            size_t valEnd = obj.find_first_of(L",\r\n\t ", valStart);
            if (valEnd == std::wstring::npos) valEnd = obj.length();
            std::wstring raw = obj.substr(valStart, valEnd - valStart);
            if (raw == L"null") return L"";
            return raw;
            };

        seg.controlTypeName = GetValue(L"controlType");
        seg.automationId = DecodeJsonValue(GetValue(L"automationId"));
        seg.className = DecodeJsonValue(GetValue(L"className"));
        seg.name = DecodeJsonValue(GetValue(L"name"));
        std::wstring posStr = GetValue(L"position");
        if (!posStr.empty()) seg.position = _wtoi(posStr.c_str());

        std::wstring lxStr = GetValue(L"lx");
        if (!lxStr.empty()) seg.lx = _wtoi(lxStr.c_str());
        std::wstring lyStr = GetValue(L"ly");
        if (!lyStr.empty()) seg.ly = _wtoi(lyStr.c_str());

        if (!seg.controlTypeName.empty())
            segments.push_back(seg);

        pos = objEnd + 1;
    }

    return segments;
}

bool FindUiByPath::ElementMatchesAttributes(IUIAutomationElement* pElement, const NodeSegment& seg,
    bool ignoreClassNameAndName, bool isLeafSegment, long lx, long ly)
{
    if (!pElement) return false;

    auto MatchesProperty = [](HRESULT hr, BSTR bstr, const std::wstring& expected, bool allowContainsPrefix) -> bool {
        std::wstring actual = (SUCCEEDED(hr) && bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
        if (bstr) ::SysFreeString(bstr);

        if (expected.empty()) return true;
        if (expected.find_first_not_of(L" \t\r\n") == std::wstring::npos) return actual.empty();

        const std::wstring containsPrefix = L"contains:";
        if (allowContainsPrefix && expected.rfind(containsPrefix, 0) == 0) {
            const std::wstring needle = expected.substr(containsPrefix.length());
            if (needle.empty()) return true;
            return actual.find(needle) != std::wstring::npos;
        }

        return actual == expected;
        };

    if (!seg.automationId.empty()) {
        BSTR bstr = nullptr;
        HRESULT hr = pElement->get_CurrentAutomationId(&bstr);
        if (!MatchesProperty(hr, bstr, seg.automationId, true)) return false;
    }

    const bool isContainer = IsContainerControlType(seg.controlTypeName);

    bool skipClassName = ignoreClassNameAndName && !isLeafSegment && isContainer;
    if (!skipClassName && !seg.className.empty()) {
        BSTR bstr = nullptr;
        HRESULT hr = pElement->get_CurrentClassName(&bstr);
        std::wstring actualClassName = (SUCCEEDED(hr) && bstr && ::SysStringLen(bstr) > 0) ? bstr : L"";
        if (bstr) ::SysFreeString(bstr);

        if (actualClassName.empty()) return false;

        const std::wstring containsPrefix = L"contains:";
        if (seg.className.rfind(containsPrefix, 0) == 0) {
            const std::wstring expectedNeedle = seg.className.substr(containsPrefix.length());
            if (expectedNeedle.empty()) return true;
            if (actualClassName.find(expectedNeedle) == std::wstring::npos) return false;
        }
        else {
            const size_t separatorPos = seg.className.find_first_of(L" ");
            if (separatorPos != std::wstring::npos) {
                const std::wstring expectedPrefix = seg.className.substr(0, separatorPos);
                const std::wstring expectedSuffix = seg.className.substr(separatorPos + 1);
                if (actualClassName.rfind(expectedPrefix, 0) != 0 ||
                    actualClassName.find(expectedSuffix, expectedPrefix.length()) == std::wstring::npos) {
                    return false;
                }
            }
            else {
                if (actualClassName != seg.className) return false;
            }
        }
    }

    bool skipName = ignoreClassNameAndName && !isLeafSegment && isContainer;
    if (!skipName && !seg.name.empty()) {
        BSTR bstr = nullptr;
        HRESULT hr = pElement->get_CurrentName(&bstr);
        if (!MatchesProperty(hr, bstr, seg.name, true)) return false;
    }

    if (!seg.itemType.empty()) {
        BSTR bstr = nullptr;
        HRESULT hr = pElement->get_CurrentItemType(&bstr);
        if (!MatchesProperty(hr, bstr, seg.itemType, true)) return false;
    }

    return true;
}

int FindUiByPath::CountMatchingIdentityAttributes(
    IUIAutomationElement* pElement,
    const NodeSegment& seg,
    bool isLeafSegment)
{
    int matchCount = 0;
    NodeSegment attribute;
    attribute.controlTypeName = seg.controlTypeName;

    if (!seg.automationId.empty()) {
        attribute.automationId = seg.automationId;
        if (ElementMatchesAttributes(pElement, attribute, false, isLeafSegment, 0, 0)) {
            ++matchCount;
        }
        attribute.automationId.clear();
    }

    if (!seg.className.empty()) {
        attribute.className = seg.className;
        if (ElementMatchesAttributes(pElement, attribute, false, isLeafSegment, 0, 0)) {
            ++matchCount;
        }
        attribute.className.clear();
    }

    if (!seg.name.empty()) {
        attribute.name = seg.name;
        if (ElementMatchesAttributes(pElement, attribute, false, isLeafSegment, 0, 0)) {
            ++matchCount;
        }
    }

    return matchCount;
}

std::vector<ComPtr<IUIAutomationElement>> FindUiByPath::FindMatchingChildren(
    BOOL fallbackToPosMatch,
    IUIAutomationElement* pParent,
    const NodeSegment& seg,
    bool isLeafSegment)
{
    // Scan same-control-type siblings and rank candidates from strongest to weakest:
    // position plus attributes, attributes alone, a unique partial identity match, and
    // position alone. The fallback pass restricts matching to the recorded position;
    // leaf searches also include TitleBar/MenuBar candidates whose children may hold
    // the actual target omitted from the recorded path.
    std::vector<ComPtr<IUIAutomationElement>> exactMatches;
    std::vector<ComPtr<IUIAutomationElement>> attributeMatches;
    std::vector<ComPtr<IUIAutomationElement>> uniqueAttributeFallback;
    std::vector<ComPtr<IUIAutomationElement>> positionOnly;
    std::vector<ComPtr<IUIAutomationElement>> titleBarMatches;

    if (!pParent || !m_pRawWalker) return {};

    ComPtr<IUIAutomationElement> pChild;
    HRESULT hr = m_pRawWalker->GetFirstChildElement(pParent, &pChild);

    int sameTypeCount = 0;
    int scanned = 0;
    int childrenWithIdentityMatch = 0;
    ComPtr<IUIAutomationElement> identityMatchCandidate;

    const bool hasAnyAttribute =
        !seg.automationId.empty() ||
        !seg.className.empty() ||
        !seg.name.empty() ||
        !seg.itemType.empty();

    const bool canFallBackToPosition =
        seg.name.empty() || IsContainerControlType(seg.controlTypeName);

    LONG lx = 0, ly = 0;
    RECT rectParent;
    if (SUCCEEDED(pParent->get_CurrentBoundingRectangle(&rectParent)))
    {
        lx = rectParent.left + seg.lx;
        ly = rectParent.top + seg.ly;
    }

    while (SUCCEEDED(hr) && pChild) {
        if (++scanned > kMaxSiblingScan) break;

        CONTROLTYPEID controlTypeId = 0;
        pChild->get_CurrentControlType(&controlTypeId);
        std::wstring ctrnTypeName = GetControlTypeName(controlTypeId);

        if (ctrnTypeName == seg.controlTypeName) {
            ++sameTypeCount;
            bool isPosMatch = (seg.position > 0 && sameTypeCount == seg.position);

            if (fallbackToPosMatch) {
                if (isPosMatch) {
                    if (ElementMatchesAttributes(pChild.Get(), seg, true, isLeafSegment, 0, 0)) {
                        exactMatches.push_back(pChild);
                        break;
                    }
                    else if (canFallBackToPosition) {
                        positionOnly.push_back(pChild);
                    }
                }
            }
            else {
                bool isAttrMatch = ElementMatchesAttributes(pChild.Get(), seg, false, isLeafSegment, lx, ly);
                if (CountMatchingIdentityAttributes(pChild.Get(), seg, isLeafSegment) > 0) {
                    ++childrenWithIdentityMatch;
                    identityMatchCandidate = pChild;
                }

                if (isPosMatch && isAttrMatch) {
                    exactMatches.push_back(pChild);
                    break;
                }
                else if (isAttrMatch) {
                    attributeMatches.push_back(pChild);
                }
                else if (isPosMatch && !hasAnyAttribute) {
                    positionOnly.push_back(pChild);
                }
            }
        }

        if (isLeafSegment && (ctrnTypeName == L"TitleBar" || ctrnTypeName == L"MenuBar")) {
            titleBarMatches.push_back(pChild);
        }

        ComPtr<IUIAutomationElement> pNext;
        hr = m_pRawWalker->GetNextSiblingElement(pChild.Get(), &pNext);

        if (SUCCEEDED(hr) && pNext && m_pUIA6) {
            BOOL isSameSibling = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(pChild.Get(), pNext.Get(), &isSameSibling)) && isSameSibling) {
                break;
            }
        }

        pChild = pNext;
    }

    if (!fallbackToPosMatch &&
        exactMatches.empty() &&
        attributeMatches.empty() &&
        childrenWithIdentityMatch == 1 &&
        identityMatchCandidate) {
        uniqueAttributeFallback.push_back(identityMatchCandidate);
    }

    std::vector<ComPtr<IUIAutomationElement>> ordered;
    ordered.reserve(exactMatches.size() + attributeMatches.size() + uniqueAttributeFallback.size() + positionOnly.size() + titleBarMatches.size());
    for (auto& e : exactMatches)     ordered.push_back(e);
    for (auto& e : attributeMatches) ordered.push_back(e);
    for (auto& e : uniqueAttributeFallback) ordered.push_back(e);
    for (auto& e : positionOnly)     ordered.push_back(e);
    for (auto& e : titleBarMatches)  ordered.push_back(e);
    return ordered;
}

bool FindUiByPath::FindLeafByDescendantSearch(
    IUIAutomationElement* pParent,
    const std::vector<NodeSegment>& segments,
    size_t firstSegment,
    ComPtr<IUIAutomationElement>& outElement)
{
    if (!pParent || !m_pUIA6 || firstSegment >= segments.size()) return false;

    RECT rectParent;
    pParent->get_CurrentBoundingRectangle(&rectParent);
    if (rectParent.right > rectParent.left && rectParent.bottom > rectParent.top) {
        int left = rectParent.left + segments[firstSegment - 1].lx;
        int top = rectParent.top + segments[firstSegment - 1].ly;
        SetCursorPos(left, top);
    }

    ComPtr<IUIAutomationCondition> trueCondition;
    if (FAILED(m_pUIA6->CreateTrueCondition(&trueCondition)) || !trueCondition) return false;

    ComPtr<IUIAutomationElementArray> descendants;
    if (FAILED(pParent->FindAll(TreeScope_Descendants, trueCondition.Get(), &descendants)) || !descendants) return false;

    int count = 0;
    descendants->get_Length(&count);
    const size_t leafSegment = segments.size() - 1;
    int bestScore = 0;
    ComPtr<IUIAutomationElement> bestMatch;

    auto MatchesSegment = [this, &segments](IUIAutomationElement* element, size_t segmentIndex) {
        CONTROLTYPEID type = 0;
        if (FAILED(element->get_CurrentControlType(&type)) ||
            GetControlTypeName(type) != segments[segmentIndex].controlTypeName) return false;
        return ElementMatchesAttributes(element, segments[segmentIndex], false,
            segmentIndex + 1 == segments.size(), 0, 0);
        };

    for (int i = 0; i < count && i < kMaxSiblingScan; ++i) {
        if (g_abortLocateUiByPath.load(std::memory_order_acquire)) return false;

        ComPtr<IUIAutomationElement> candidate;
        if (FAILED(descendants->GetElement(i, &candidate)) || !candidate ||
            !MatchesSegment(candidate.Get(), leafSegment)) continue;

        std::vector<ComPtr<IUIAutomationElement>> ancestry;
        ComPtr<IUIAutomationElement> current = candidate;
        bool reachedParent = false;
        while (current && ancestry.size() < segments.size() + static_cast<size_t>(kMaxSiblingScan)) {
            ComPtr<IUIAutomationElement> parent;
            if (FAILED(m_pRawWalker->GetParentElement(current.Get(), &parent)) || !parent) break;
            BOOL same = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(parent.Get(), pParent, &same)) && same) {
                reachedParent = true;
                break;
            }
            ancestry.push_back(parent);
            current = parent;
        }
        if (!reachedParent) continue;

        std::reverse(ancestry.begin(), ancestry.end());
        const size_t intermediateCount = leafSegment - firstSegment;
        std::vector<int> previous(intermediateCount + 1, 0);
        for (const auto& ancestor : ancestry) {
            std::vector<int> scores(previous);
            for (size_t j = 1; j <= intermediateCount; ++j) {
                scores[j] = (std::max)(scores[j], scores[j - 1]);
                if (MatchesSegment(ancestor.Get(), firstSegment + j - 1))
                    scores[j] = (std::max)(scores[j], previous[j - 1] + 1);
            }
            previous.swap(scores);
        }

        const int score = previous[intermediateCount] + 1;
        if (score > bestScore) {
            bestScore = score;
            bestMatch = candidate;
        }
    }

    outElement = bestMatch;
    return outElement != nullptr;
}

bool FindUiByPath::ResolveNodePath(
    IUIAutomationElement* pParent,
    const std::vector<NodeSegment>& segments,
    size_t index,
    bool arrivedViaFallback,
    ComPtr<IUIAutomationElement>& outElement,
    bool& outUsedFallback,
    ComPtr<IUIAutomationElement>& outLastMatch,
    size_t& outNextSegment)
{
    RECT rectParent{};
    if (index > outNextSegment &&
        SUCCEEDED(pParent->get_CurrentBoundingRectangle(&rectParent)) &&
        rectParent.right > rectParent.left &&
        rectParent.bottom > rectParent.top) {
        outLastMatch = pParent;
        outNextSegment = index;
    }

    if (index == segments.size()) {
        outElement = pParent;
        outUsedFallback = arrivedViaFallback;
        return true;
    }

    bool isLeafSegment = (index == segments.size() - 1);

    auto strictCandidates = FindMatchingChildren(FALSE, pParent, segments[index], isLeafSegment);
    for (auto& cand : strictCandidates) {
        CONTROLTYPEID controlTypeId = 0;
        cand->get_CurrentControlType(&controlTypeId);
        std::wstring ctrnTypeName = GetControlTypeName(controlTypeId);

        // Target element like Close button may be a child of a TitleBar or MenuBar, but the path 
        // may not include those intermediate elements because leaf-to-root walks may skip them.
        // So, if the current segment is a leaf and the candidate is a TitleBar or MenuBar, 
        // we need to look for matching children of that candidate.
        // A workaround is to build the node path by walking from the root to the target element, 
        // which will include the TitleBar or MenuBar in the path.
        if (isLeafSegment && (ctrnTypeName == L"TitleBar" || ctrnTypeName == L"MenuBar")) {
            auto childCandidates = FindMatchingChildren(FALSE, cand.Get(), segments[index], true);
            for (auto& childCand : childCandidates) {
                if (ResolveNodePath(childCand.Get(), segments, index + 1, arrivedViaFallback, outElement, outUsedFallback, outLastMatch, outNextSegment))
                    return true;
            }
            continue;
        }

        if (ResolveNodePath(cand.Get(), segments, index + 1, arrivedViaFallback, outElement, outUsedFallback, outLastMatch, outNextSegment))
            return true;
    }

    auto fallbackCandidates = FindMatchingChildren(TRUE, pParent, segments[index], isLeafSegment);
    for (auto& cand : fallbackCandidates) {
        bool alreadyTested = false;
        for (auto& strictCand : strictCandidates) {
            BOOL isSame = FALSE;
            if (SUCCEEDED(m_pUIA6->CompareElements(cand.Get(), strictCand.Get(), &isSame)) && isSame) {
                alreadyTested = true;
                break;
            }
        }
        if (alreadyTested) continue;

        if (ResolveNodePath(cand.Get(), segments, index + 1, true, outElement, outUsedFallback, outLastMatch, outNextSegment))
            return true;
    }

    return false;
}

HRESULT FindUiByPath::FindElement(const std::wstring& nodePath, BOOL bFlash, RECT& outRect, std::wstring& elementState) {
    ::ZeroMemory(&outRect, sizeof(outRect));
    elementState.clear();

    // Clear any highlight left over from a previous call.
    HideHighlightOverlay();

    if (g_abortLocateUiByPath.load(std::memory_order_acquire)) {
        return E_ABORT;
    }

    HRESULT hr = S_OK;
    std::vector<NodeSegment> segments = ParseJson(nodePath);
    if (segments.empty()) return E_INVALIDARG;

    ComPtr<IUIAutomationElement> pRoot;
    hr = m_pUIA6->GetRootElement(&pRoot);
    if (FAILED(hr) || !pRoot) return FAILED(hr) ? hr : E_FAIL;

    ComPtr<IUIAutomationElement> pCurrent;
    ComPtr<IUIAutomationElement> spLastMatch = pRoot;
    bool usedFallbackForLastMatch = false;
    size_t nextSegment = 1;
    m_lastFailedSegment = SIZE_MAX;

    if (g_abortLocateUiByPath.load(std::memory_order_acquire)) {
        return E_ABORT;
    }

    if (!ResolveNodePath(pRoot.Get(), segments, 1, false, pCurrent, usedFallbackForLastMatch, spLastMatch, nextSegment)) {
        if (g_abortLocateUiByPath.load(std::memory_order_acquire)) return E_ABORT;

        m_lastFailedSegment = nextSegment;
        ComPtr<IUIAutomationElement> descendantMatch;
        if (FindLeafByDescendantSearch(spLastMatch.Get(), segments, nextSegment, descendantMatch)) {
            pCurrent = descendantMatch;
            usedFallbackForLastMatch = true;
        }
        else {
            if (g_abortLocateUiByPath.load(std::memory_order_acquire)) return E_ABORT;
            try {
                SYSTEMTIME now;
                ::GetLocalTime(&now);
                wchar_t name[128];
                swprintf_s(name, L"FindUiByPath_FailedSegment_%04d%02d%02d_%02d%02d.log",
                    now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute);
                std::wofstream logFile(name, std::ios::out | std::ios::app);
                if (logFile.is_open()) {
                    wchar_t timestamp[128];
                    swprintf_s(timestamp, L"%04d-%02d-%02d %02d:%02d:%02d",
                        now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond);
                    logFile << timestamp << L" - Failed segment index: " << m_lastFailedSegment << L"\r\n";
                }
            }
            catch (...) {}
            return E_FAIL;
        }
    }


    hr = pCurrent->get_CurrentBoundingRectangle(&outRect);
    if (FAILED(hr)) return hr;

    std::vector<ComPtr<IUIAutomationElement>> ancestry;
    ancestry.push_back(pCurrent);

    ComPtr<IUIAutomationElement> walker = pCurrent;
    while (walker) {
        ComPtr<IUIAutomationElement> parent;
        hr = m_pRawWalker->GetParentElement(walker.Get(), &parent);
        if (FAILED(hr) || !parent) break;

        ancestry.push_back(parent);

        BOOL sameAsRoot = FALSE;
        if (m_pUIA6 && SUCCEEDED(m_pUIA6->CompareElements(parent.Get(), pRoot.Get(), &sameAsRoot)) && sameAsRoot)
            break;

        walker = parent;
    }

    std::reverse(ancestry.begin(), ancestry.end());
    std::wstringstream state;
    auto typeNameLambda = [this](CONTROLTYPEID id) { return this->GetControlTypeName(id); };

    for (size_t i = 0; i < ancestry.size(); ++i) {
        state << GetElementStateLine(ancestry[i].Get(), static_cast<int>(i),
            i == 0 ? L"Root" : (i + 1 == ancestry.size() ? L"Target" : L"Ancestor"), L"", typeNameLambda);
    }
    elementState = state.str();

    if (g_abortLocateUiByPath.load(std::memory_order_acquire)) {
        return E_ABORT;
    }

    if (bFlash) {
        // Purple for fallback, green for exact match
        COLORREF color = usedFallbackForLastMatch ? RGB(143, 32, 255) : RGB(32, 255, 32);
        ShowHighlightOverlay(outRect, color, 500, 2);
    }

    return S_OK;
}