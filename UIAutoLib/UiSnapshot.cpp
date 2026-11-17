#include "pch.h"
#include <algorithm>
#include <cwctype>
#include <sstream>
#include <unordered_map>
#include <utility>
#include <vector>
#include "UiSnapshot.h"

namespace {
    constexpr ULONG kDefaultMaxDepth = 12;
    constexpr ULONG kDefaultMaxElements = 500;
    constexpr ULONG kDefaultMaxVisitedElements = 5000;
    constexpr ULONG kHardMaxDepth = 64;
    constexpr ULONG kHardMaxElements = 5000;
    constexpr ULONG kHardMaxVisitedElements = 20000;
    constexpr size_t kMaxPropertyLength = 2048;
    constexpr size_t kMaxMatchLength = 100;

    struct EffectiveOptions {
        ULONG maxDepth = kDefaultMaxDepth;
        ULONG maxElements = kDefaultMaxElements;
        ULONG maxVisitedElements = kDefaultMaxVisitedElements;
        ULONG flags = UiSnapshotFlag_None;
    };

    struct ElementProperties {
        CONTROLTYPEID controlTypeId = 0;
        std::wstring controlType;
        std::wstring name;
        std::wstring automationId;
        std::wstring className;
        std::wstring value;
        RECT bounds{};
        int processId = 0;
        UIA_HWND nativeWindow = 0;
        bool enabled = false;
        bool offscreen = false;
        bool focused = false;
        bool password = false;
        std::vector<std::wstring> actions;
    };

    struct PendingElement {
        ComPtr<IUIAutomationElement> element;
        std::wstring parentId;
        ULONG depth = 0;
        int position = 0;
        std::vector<std::wstring> path;
    };

    EffectiveOptions NormalizeOptions(const UiSnapshotOptions* options) {
        EffectiveOptions effective;
        if (!options) return effective;
        if (options->MaxDepth != 0) effective.maxDepth = (std::min)(options->MaxDepth, kHardMaxDepth);
        if (options->MaxElements != 0) effective.maxElements = (std::min)(options->MaxElements, kHardMaxElements);
        if (options->MaxVisitedElements != 0) effective.maxVisitedElements = (std::min)(options->MaxVisitedElements, kHardMaxVisitedElements);
        effective.flags = options->Flags;
        return effective;
    }

    std::wstring ReadString(IUIAutomationElement* element, HRESULT(STDMETHODCALLTYPE IUIAutomationElement::* getter)(BSTR*)) {
        BSTR value = nullptr;
        if (FAILED((element->*getter)(&value)) || !value) return L"";
        std::wstring result(value, ::SysStringLen(value));
        ::SysFreeString(value);
        if (result.length() > kMaxPropertyLength) result.resize(kMaxPropertyLength);
        return result;
    }

    bool SupportsPattern(IUIAutomationElement* element, PATTERNID patternId, REFIID iid) {
        ComPtr<IUnknown> pattern;
        return SUCCEEDED(element->GetCurrentPatternAs(patternId, iid, reinterpret_cast<void**>(pattern.GetAddressOf()))) && pattern;
    }

    std::wstring ReadValue(IUIAutomationElement* element) {
        ComPtr<IUIAutomationValuePattern> pattern;
        if (FAILED(element->GetCurrentPatternAs(UIA_ValuePatternId, IID_PPV_ARGS(&pattern))) || !pattern) return L"";
        BSTR value = nullptr;
        if (FAILED(pattern->get_CurrentValue(&value)) || !value) return L"";
        std::wstring result(value, ::SysStringLen(value));
        ::SysFreeString(value);
        if (result.length() > kMaxPropertyLength) result.resize(kMaxPropertyLength);
        return result;
    }

    ElementProperties ReadProperties(IUIAutomationElement* element, ULONG flags) {
        ElementProperties properties;
        element->get_CurrentControlType(&properties.controlTypeId);
        properties.controlType = UiTreeWalkCommon::GetControlTypeName(properties.controlTypeId);
        properties.name = ReadString(element, &IUIAutomationElement::get_CurrentName);
        properties.automationId = ReadString(element, &IUIAutomationElement::get_CurrentAutomationId);
        properties.className = ReadString(element, &IUIAutomationElement::get_CurrentClassName);
        element->get_CurrentBoundingRectangle(&properties.bounds);
        element->get_CurrentProcessId(&properties.processId);
        element->get_CurrentNativeWindowHandle(&properties.nativeWindow);

        BOOL value = FALSE;
        if (SUCCEEDED(element->get_CurrentIsEnabled(&value))) properties.enabled = value == TRUE;
        if (SUCCEEDED(element->get_CurrentIsOffscreen(&value))) properties.offscreen = value == TRUE;
        if (SUCCEEDED(element->get_CurrentHasKeyboardFocus(&value))) properties.focused = value == TRUE;
        if (SUCCEEDED(element->get_CurrentIsPassword(&value))) properties.password = value == TRUE;

        if (SupportsPattern(element, UIA_InvokePatternId, __uuidof(IUIAutomationInvokePattern))) properties.actions.push_back(L"invoke");
        if (SupportsPattern(element, UIA_ValuePatternId, __uuidof(IUIAutomationValuePattern))) properties.actions.push_back(L"setValue");
        if (SupportsPattern(element, UIA_SelectionItemPatternId, __uuidof(IUIAutomationSelectionItemPattern))) properties.actions.push_back(L"select");
        if (SupportsPattern(element, UIA_TogglePatternId, __uuidof(IUIAutomationTogglePattern))) properties.actions.push_back(L"toggle");
        if (SupportsPattern(element, UIA_ExpandCollapsePatternId, __uuidof(IUIAutomationExpandCollapsePattern))) properties.actions.push_back(L"expandCollapse");
        if (SupportsPattern(element, UIA_ScrollPatternId, __uuidof(IUIAutomationScrollPattern))) properties.actions.push_back(L"scroll");
        if (SupportsPattern(element, UIA_WindowPatternId, __uuidof(IUIAutomationWindowPattern))) properties.actions.push_back(L"window");
        if ((flags & UiSnapshotFlag_IncludeValues) != 0 && !properties.password) properties.value = ReadValue(element);
        return properties;
    }

    std::wstring ToLower(std::wstring value) {
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
        return value;
    }

    bool TextMatches(const std::wstring& actual, LPCWSTR expected, bool contains) {
        if (!expected || expected[0] == L'\0') return true;
        const std::wstring actualLower = ToLower(actual);
        const std::wstring expectedLower = ToLower(expected);
        return contains ? actualLower.find(expectedLower) != std::wstring::npos : actualLower == expectedLower;
    }

    bool MatchesQuery(const ElementProperties& properties, const UiSnapshotQuery* query) {
        if (!query) return true;
        if (query->ProcessId != 0 && static_cast<DWORD>(properties.processId) != query->ProcessId) return false;
        if (!TextMatches(properties.controlType, query->ControlType, false)) return false;
        if (!TextMatches(properties.name, query->Name, (query->Flags & UiQueryFlag_NameContains) != 0)) return false;
        if (!TextMatches(properties.automationId, query->AutomationId, (query->Flags & UiQueryFlag_AutomationIdContains) != 0)) return false;
        return TextMatches(properties.className, query->ClassName, (query->Flags & UiQueryFlag_ClassNameContains) != 0);
    }

    void WriteString(std::wstringstream& stream, const wchar_t* name, const std::wstring& value) {
        if (!value.empty()) stream << L",\"" << name << L"\":\"" << UiTreeWalkCommon::EncodeJsonValue(value) << L"\"";
    }

    std::wstring NormalizeSelector(const std::wstring& value) {
        if (value.length() <= kMaxMatchLength) return value;

        size_t truncateAt = kMaxMatchLength;
        if (IS_HIGH_SURROGATE(value[truncateAt - 1])) --truncateAt;
        return L"contains:" + value.substr(0, truncateAt);
    }

    std::wstring BuildPathSegment(const ElementProperties& properties, int position) {
        std::wstringstream segment;
        segment << L"{\"controlType\":\"" << UiTreeWalkCommon::EncodeJsonValue(properties.controlType) << L"\",\"position\":" << position;
        WriteString(segment, L"automationId", NormalizeSelector(properties.automationId));
        WriteString(segment, L"className", NormalizeSelector(properties.className));
        WriteString(segment, L"name", NormalizeSelector(properties.name));
        segment << L"}";
        return segment.str();
    }

    std::wstring SerializePath(const std::vector<std::wstring>& path) {
        std::wstringstream stream;
        stream << L"[";
        for (size_t index = 0; index < path.size(); ++index) {
            if (index != 0) stream << L",";
            stream << path[index];
        }
        stream << L"]";
        return stream.str();
    }

    int GetSiblingPosition(IUIAutomation6* automation, IUIAutomationTreeWalker* walker,
        IUIAutomationElement* element, IUIAutomationElement* parent, CONTROLTYPEID controlTypeId) {
        if (!automation || !walker || !element || !parent) return 0;

        ComPtr<IUIAutomationElement> child;
        HRESULT hr = walker->GetFirstChildElement(parent, &child);
        int sameTypeCount = 0;
        int scanned = 0;
        while (SUCCEEDED(hr) && child && scanned++ < UiTreeWalkCommon::kMaxSiblingScan) {
            CONTROLTYPEID childType = 0;
            if (SUCCEEDED(child->get_CurrentControlType(&childType)) && childType == controlTypeId) {
                ++sameTypeCount;
                BOOL same = FALSE;
                if (SUCCEEDED(automation->CompareElements(child.Get(), element, &same)) && same) return sameTypeCount;
            }

            ComPtr<IUIAutomationElement> next;
            hr = walker->GetNextSiblingElement(child.Get(), &next);
            if (SUCCEEDED(hr) && next) {
                BOOL same = FALSE;
                if (SUCCEEDED(automation->CompareElements(child.Get(), next.Get(), &same)) && same) break;
            }
            child = next;
        }
        return 0;
    }

    std::vector<std::wstring> BuildDesktopPathPrefix(IUIAutomation6* automation,
        IUIAutomationTreeWalker* walker, IUIAutomationElement* snapshotRoot, ULONG flags, int& rootPosition) {
        std::vector<std::wstring> reversePath;
        ComPtr<IUIAutomationElement> current = snapshotRoot;
        for (ULONG depth = 0; current && depth <= kHardMaxDepth; ++depth) {
            ComPtr<IUIAutomationElement> parent;
            const HRESULT parentResult = walker->GetParentElement(current.Get(), &parent);
            ElementProperties properties = ReadProperties(current.Get(), flags);
            const int position = parent
                ? GetSiblingPosition(automation, walker, current.Get(), parent.Get(), properties.controlTypeId)
                : 0;
            if (depth == 0) rootPosition = position;
            reversePath.push_back(BuildPathSegment(properties, position));
            if (FAILED(parentResult) || !parent) break;
            current = parent;
        }

        std::reverse(reversePath.begin(), reversePath.end());
        if (!reversePath.empty()) reversePath.pop_back();
        return reversePath;
    }

    void WriteElement(std::wstringstream& stream, const std::wstring& id, const PendingElement& pending,
        const ElementProperties& properties, const std::vector<std::wstring>& path) {
        stream << L"{\"id\":\"" << id << L"\"";
        if (!pending.parentId.empty()) stream << L",\"parentId\":\"" << pending.parentId << L"\"";
        stream << L",\"depth\":" << pending.depth << L",\"controlType\":\"" << UiTreeWalkCommon::EncodeJsonValue(properties.controlType) << L"\"";
        WriteString(stream, L"name", properties.name);
        WriteString(stream, L"automationId", properties.automationId);
        WriteString(stream, L"className", properties.className);
        if (properties.password) stream << L",\"valueRedacted\":true";
        else WriteString(stream, L"value", properties.value);
        stream << L",\"processId\":" << properties.processId << L",\"nativeWindowHandle\":" << reinterpret_cast<UINT_PTR>(properties.nativeWindow);
        stream << L",\"bounds\":{\"left\":" << properties.bounds.left << L",\"top\":" << properties.bounds.top
            << L",\"right\":" << properties.bounds.right << L",\"bottom\":" << properties.bounds.bottom << L"}";
        stream << L",\"enabled\":" << (properties.enabled ? L"true" : L"false")
            << L",\"offscreen\":" << (properties.offscreen ? L"true" : L"false")
            << L",\"focused\":" << (properties.focused ? L"true" : L"false") << L",\"actions\":[";
        for (size_t index = 0; index < properties.actions.size(); ++index) {
            if (index != 0) stream << L",";
            stream << L"\"" << properties.actions[index] << L"\"";
        }
        stream << L"],\"nodePath\":\"" << UiTreeWalkCommon::EncodeJsonValue(SerializePath(path)) << L"\"}";
    }
}

HRESULT UiSnapshot::Capture(HWND rootWindow, const UiSnapshotOptions* options, const UiSnapshotQuery* query, std::wstring& json) {
    json.clear();
    if (!m_pUIA6 || !m_pRawWalker) return E_POINTER;

    const EffectiveOptions effective = NormalizeOptions(options);
    ComPtr<IUIAutomationElement> root;
    HRESULT hr = rootWindow ? m_pUIA6->ElementFromHandle(rootWindow, &root) : m_pUIA6->GetRootElement(&root);
    if (FAILED(hr) || !root) return FAILED(hr) ? hr : E_FAIL;

    int rootPosition = 0;
    const std::vector<std::wstring> rootPathPrefix = rootWindow
        ? BuildDesktopPathPrefix(m_pUIA6.Get(), m_pRawWalker.Get(), root.Get(), effective.flags, rootPosition)
        : std::vector<std::wstring>{};
    std::vector<PendingElement> stack{ PendingElement{ root, L"", 0, rootPosition, rootPathPrefix } };
    std::wstringstream elements;
    ULONG visited = 0;
    ULONG emitted = 0;
    bool truncated = false;

    while (!stack.empty()) {
        if (visited >= effective.maxVisitedElements || emitted >= effective.maxElements) {
            truncated = true;
            break;
        }

        PendingElement pending = std::move(stack.back());
        stack.pop_back();
        ++visited;
        ElementProperties properties = ReadProperties(pending.element.Get(), effective.flags);
        std::vector<std::wstring> path = pending.path;
        path.push_back(BuildPathSegment(properties, pending.position));
        const bool visible = (effective.flags & UiSnapshotFlag_IncludeOffscreen) != 0 || !properties.offscreen;
        const bool interactive = (effective.flags & UiSnapshotFlag_InteractiveOnly) == 0 || !properties.actions.empty() || properties.focused;
        const bool accepted = visible && interactive && MatchesQuery(properties, query);

        std::wstring id;
        if (accepted) {
            id = L"e" + std::to_wstring(emitted + 1);
            if (emitted != 0) elements << L",";
            WriteElement(elements, id, pending, properties, path);
            ++emitted;
        }

        if (pending.depth >= effective.maxDepth) {
            ComPtr<IUIAutomationElement> child;
            if (SUCCEEDED(m_pRawWalker->GetFirstChildElement(pending.element.Get(), &child)) && child) truncated = true;
            continue;
        }

        std::vector<PendingElement> children;
        std::unordered_map<CONTROLTYPEID, int> positions;
        ComPtr<IUIAutomationElement> child;
        hr = m_pRawWalker->GetFirstChildElement(pending.element.Get(), &child);
        int siblingCount = 0;
        const size_t remainingVisitBudget = effective.maxVisitedElements - visited - stack.size();
        while (SUCCEEDED(hr) && child) {
            if (siblingCount >= kMaxSiblingScan || children.size() >= remainingVisitBudget) {
                truncated = true;
                break;
            }
            ++siblingCount;
            CONTROLTYPEID childType = 0;
            child->get_CurrentControlType(&childType);
            children.push_back(PendingElement{ child, accepted ? id : pending.parentId, pending.depth + 1, ++positions[childType], path });
            ComPtr<IUIAutomationElement> next;
            hr = m_pRawWalker->GetNextSiblingElement(child.Get(), &next);
            if (SUCCEEDED(hr) && next) {
                BOOL same = FALSE;
                if (SUCCEEDED(m_pUIA6->CompareElements(child.Get(), next.Get(), &same)) && same) break;
            }
            child = next;
        }
        for (auto iterator = children.rbegin(); iterator != children.rend(); ++iterator) stack.push_back(std::move(*iterator));
    }

    std::wstringstream result;
    result << L"{\"schemaVersion\":1,\"rootWindowHandle\":" << reinterpret_cast<UINT_PTR>(rootWindow)
        << L",\"visitedCount\":" << visited << L",\"elementCount\":" << emitted
        << L",\"truncated\":" << (truncated ? L"true" : L"false") << L",\"elements\":[" << elements.str() << L"]}";
    json = result.str();
    return S_OK;
}
