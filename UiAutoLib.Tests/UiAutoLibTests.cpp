#include <CppUnitTest.h>
#include <atomic>
#include <string>
#include "BuildPathToUi.h"
#include "FindUiByPath.h"
#include "UiTreeWalkCommon.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

std::atomic_bool g_abortLocateUiByPath{ false };

namespace UiAutoLibTests
{
    class BuildPathToUiWithoutAutomation : public BuildPathToUi
    {
    public:
        BuildPathToUiWithoutAutomation() noexcept : BuildPathToUi(false) {}
    };

    class FindUiByPathWithoutAutomation : public FindUiByPath
    {
    public:
        FindUiByPathWithoutAutomation() noexcept : FindUiByPath(false) {}
    };

    TEST_CLASS(UiTreeWalkCommonTests)
    {
    public:
        TEST_METHOD(EncodeJsonValueEscapesQuotesSlashesAndControlCharacters)
        {
            const std::wstring input = L"\"\\\b\f\n\r\t" + std::wstring(1, L'\x01');
            const std::wstring expected = L"\\\"\\\\\\b\\f\\n\\r\\t\\u0001";

            Assert::IsTrue(UiTreeWalkCommon::EncodeJsonValue(input) == expected);
        }

        TEST_METHOD(DecodeJsonValueRestoresEscapedCharacters)
        {
            const std::wstring encoded = L"\\\"\\\\\\b\\f\\n\\r\\t\\u0001";
            const std::wstring expected = L"\"\\\b\f\n\r\t" + std::wstring(1, L'\x01');

            Assert::IsTrue(UiTreeWalkCommon::DecodeJsonValue(encoded) == expected);
        }

        TEST_METHOD(JsonValueEncodingRoundTripsControlCharacters)
        {
            const std::wstring input = L"name=\"button\"\npath=C:\\app\\test\t";
            const std::wstring encoded = UiTreeWalkCommon::EncodeJsonValue(input);

            Assert::IsTrue(UiTreeWalkCommon::DecodeJsonValue(encoded) == input);
        }

        TEST_METHOD(ContainerControlTypesAreRecognized)
        {
            Assert::IsTrue(UiTreeWalkCommon::IsContainerControlType(L"Pane"));
            Assert::IsTrue(UiTreeWalkCommon::IsContainerControlType(L"Group"));
            Assert::IsTrue(UiTreeWalkCommon::IsContainerControlType(L"Custom"));
            Assert::IsFalse(UiTreeWalkCommon::IsContainerControlType(L"Window"));
        }

        TEST_METHOD(ControlTypeNamesUseUiAutomationNames)
        {
            Assert::IsTrue(UiTreeWalkCommon::GetControlTypeName(UIA_ButtonControlTypeId) == L"Button");
            Assert::IsTrue(UiTreeWalkCommon::GetControlTypeName(UIA_PaneControlTypeId) == L"Pane");
            Assert::IsTrue(UiTreeWalkCommon::GetControlTypeName(0) == L"Element");
        }
    };

    TEST_CLASS(BuildPathToUiTests)
    {
    public:
        TEST_METHOD(PathFromLeafToRootReturnsPointerErrorAndClearsOutputsWithoutAutomation)
        {
            BuildPathToUiWithoutAutomation builder;
            std::wstring path = L"previous path";
            RECT rect{ 1, 2, 3, 4 };

            const HRESULT result = builder.PathFromLeafToRoot(100, 200, path, rect);

            Assert::IsTrue(result == E_POINTER);
            Assert::IsTrue(path.empty());
            Assert::IsTrue(rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0);
        }

        TEST_METHOD(PathFromRootToLeafReturnsPointerErrorAndClearsOutputsWithoutAutomation)
        {
            BuildPathToUiWithoutAutomation builder;
            std::wstring path = L"previous path";
            RECT rect{ 1, 2, 3, 4 };

            const HRESULT result = builder.PathFromRootToLeaf(100, 200, path, rect);

            Assert::IsTrue(result == E_POINTER);
            Assert::IsTrue(path.empty());
            Assert::IsTrue(rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0);
        }

        TEST_METHOD(InvokeElementActionReturnsPointerErrorWithoutAutomation)
        {
            BuildPathToUiWithoutAutomation builder;

            Assert::IsTrue(builder.InvokeElementAction(100, 200) == E_POINTER);
        }
    };

    TEST_CLASS(FindUiByPathTests)
    {
    public:
        TEST_METHOD(FindElementRejectsPathWithoutNodesAndClearsOutputs)
        {
            FindUiByPathWithoutAutomation finder;
            std::wstring elementState = L"previous state";
            RECT rect{ 1, 2, 3, 4 };

            const HRESULT result = finder.FindElement(L"[]", FALSE, rect, elementState);

            Assert::IsTrue(result == E_INVALIDARG);
            Assert::IsTrue(elementState.empty());
            Assert::IsTrue(rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0);
        }

        TEST_METHOD(FindElementReturnsPointerErrorForValidPathWithoutAutomation)
        {
            FindUiByPathWithoutAutomation finder;
            std::wstring elementState = L"previous state";
            RECT rect{ 1, 2, 3, 4 };

            const HRESULT result = finder.FindElement(
                L"[{\"controlType\":\"Button\",\"position\":1}]", FALSE, rect, elementState);

            Assert::IsTrue(result == E_POINTER);
            Assert::IsTrue(elementState.empty());
            Assert::IsTrue(rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0);
        }
    };
}
