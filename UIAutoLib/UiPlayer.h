#pragma once
#include <wtypes.h> // Provides definition for BSTR

class UiPlayer
{
public:
    // offset from right if offsetLeft < 0 and offset from bottom if offsetTop < 0
    static void Click(RECT rect, int offsetLeft, int offsetTop);
    static void RightClick(RECT rect, int offsetLeft, int offsetTop);
    static void DoubleClick(RECT rect, int offsetLeft, int offsetTop);
    static void Drag(RECT rect, int offsetLeft, int offsetTop, int deltaX, int deltaY);
    static void Wheel(RECT rect, int offsetLeft, int offsetTop, int deltaX, int deltaY);
    static void MouseHover(RECT rect, int offsetLeft, int offsetTop);
    static void SendText(BSTR encodedText, BOOL isCapsLockOn, BOOL isNumLockOn, BOOL isScrollLockOn);

private:
    UiPlayer();
    virtual ~UiPlayer();
};

