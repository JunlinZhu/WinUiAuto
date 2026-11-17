#include "pch.h"
#include <string>
#include <vector> // Required for std::vector in SendText
#include "UiPlayer.h"

static const std::string base64_chars =
"ABCDEFGHIJKLMNOPQRSTUVWXYZ"
"abcdefghijklmnopqrstuvwxyz"
"0123456789+/";

static inline bool is_base64(unsigned char c) {
    return (isalnum(c) || (c == '+') || (c == '/'));
}

std::string base64_encode(unsigned char const* bytes_to_encode, unsigned int in_len) {
    std::string ret;
    int i = 0, j = 0;
    unsigned char char_array_3[3], char_array_4[4];

    while (in_len--) {
        char_array_3[i++] = *(bytes_to_encode++);
        if (i == 3) {
            char_array_4[0] = (char_array_3[0] & 0xfc) >> 2;
            char_array_4[1] = ((char_array_3[0] & 0x03) << 4) + ((char_array_3[1] & 0xf0) >> 4);
            char_array_4[2] = ((char_array_3[1] & 0x0f) << 2) + ((char_array_3[2] & 0xc0) >> 6);
            char_array_4[3] = char_array_3[2] & 0x3f;

            for (i = 0; (i < 4); i++)
                ret += base64_chars[char_array_4[i]];
            i = 0;
        }
    }

    if (i)
    {
        for (j = i; j < 3; j++)
            char_array_3[j] = '\0';

        char_array_4[0] = (char_array_3[0] & 0xfc) >> 2;
        char_array_4[1] = ((char_array_3[0] & 0x03) << 4) + ((char_array_3[1] & 0xf0) >> 4);
        char_array_4[2] = ((char_array_3[1] & 0x0f) << 2) + ((char_array_3[2] & 0xc0) >> 6);
        char_array_4[3] = char_array_3[2] & 0x3f;

        for (j = 0; (j < i + 1); j++)
            ret += base64_chars[char_array_4[j]];

        while ((i++ < 3))
            ret += '=';
    }

    return ret;
}

std::string base64_decode(std::string const& encoded_string) {
    int in_len = static_cast<int>(encoded_string.size());
    int i = 0, j = 0, in_ = 0;
    unsigned char char_array_4[4], char_array_3[3];
    std::string ret;

    while (in_len-- && (encoded_string[in_] != '=') && is_base64(encoded_string[in_])) {
        char_array_4[i++] = static_cast<unsigned char>(encoded_string[in_]); in_++;
        if (i == 4) {
            for (i = 0; i < 4; i++)
                char_array_4[i] = static_cast<unsigned char>(base64_chars.find(char_array_4[i]));

            char_array_3[0] = (char_array_4[0] << 2) + ((char_array_4[1] & 0x30) >> 4);
            char_array_3[1] = ((char_array_4[1] & 0xf) << 4) + ((char_array_4[2] & 0x3c) >> 2);
            char_array_3[2] = ((char_array_4[2] & 0x3) << 6) + char_array_4[3];

            for (i = 0; (i < 3); i++) ret += char_array_3[i];
            i = 0;
        }
    }

    if (i) {
        for (j = i; j < 4; j++) char_array_4[j] = 0;
        for (j = 0; j < 4; j++) char_array_4[j] = static_cast<unsigned char>(base64_chars.find(char_array_4[j]));

        char_array_3[0] = (char_array_4[0] << 2) + ((char_array_4[1] & 0x30) >> 4);
        char_array_3[1] = ((char_array_4[1] & 0xf) << 4) + ((char_array_4[2] & 0x3c) >> 2);
        char_array_3[2] = ((char_array_4[2] & 0x3) << 6) + char_array_4[3];

        for (j = 0; (j < i - 1); j++) ret += char_array_3[j];
    }

    return ret;
}

static void SendKeycode(short vc, int zeroDownOneUp)
{
    INPUT input = { 0 };
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = LOBYTE(vc);
    input.ki.dwFlags = (zeroDownOneUp != 0) ? KEYEVENTF_KEYUP : 0;
    SendInput(1, &input, sizeof(INPUT));
}

static void SetKeyState(BYTE bVk, BOOL bState)
{
    SHORT keyState = GetKeyState(bVk);
    BOOL IsKeyOn = (keyState & 1) != 0;
    if (bState != IsKeyOn)
    {
        INPUT inputs[2] = {};
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].ki.wVk = bVk;
        inputs[0].ki.dwFlags = KEYEVENTF_EXTENDEDKEY;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].ki.wVk = bVk;
        inputs[1].ki.dwFlags = KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP;

        SendInput(2, inputs, sizeof(INPUT));
    }
}

static void SendMouseInput(DWORD flags, DWORD data = 0)
{
    INPUT input = { 0 };
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = flags;
    input.mi.mouseData = data;
    SendInput(1, &input, sizeof(INPUT));
}

void GetXY(RECT& rect, int offsetLeft, int offsetTop, int* pX, int* pY)
{
    int x = (offsetLeft > 0) ? (rect.left + offsetLeft) : (rect.right + offsetLeft);
    int y = (offsetTop > 0) ? (rect.top + offsetTop) : (rect.bottom + offsetTop);

    *pX = x;
    *pY = y;
}

void UiPlayer::Click(RECT rect, int offsetLeft, int offsetTop)
{
    int x, y;
    GetXY(rect, offsetLeft, offsetTop, &x, &y);

    SetCursorPos(x, y);
    Sleep(25);
    SendMouseInput(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP);
    Sleep(25);
}

void UiPlayer::RightClick(RECT rect, int offsetLeft, int offsetTop)
{
    int x, y;
    GetXY(rect, offsetLeft, offsetTop, &x, &y);

    SetCursorPos(x, y);
    Sleep(25);
    SendMouseInput(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP);
    Sleep(25);
}

void UiPlayer::DoubleClick(RECT rect, int offsetLeft, int offsetTop)
{
    int x, y;
    GetXY(rect, offsetLeft, offsetTop, &x, &y);

    SetCursorPos(x, y);
    Sleep(25);
    SendMouseInput(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP);
    Sleep(25);
    SendMouseInput(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP);
    Sleep(25);
}

void UiPlayer::MouseHover(RECT rect, int offsetLeft, int offsetTop)
{
    int x, y;
    GetXY(rect, offsetLeft, offsetTop, &x, &y);
    SetCursorPos(x, y);
    Sleep(500);
}

void UiPlayer::Drag(RECT rect, int offsetLeft, int offsetTop, int dragX, int dragY)
{
    const int startX = rect.left + offsetLeft;
    const int startY = rect.top + offsetTop;
    const int endX = startX + dragX;
    const int endY = startY + dragY;

    SetCursorPos(startX, startY);
    SendMouseInput(MOUSEEVENTF_LEFTDOWN);
    Sleep(300);

    const int stepCount = 100;
    for (int i = 1; i < stepCount; i++)
    {
        int x = startX + ((endX - startX) * i) / stepCount;
        int y = startY + ((endY - startY) * i) / stepCount;
        SetCursorPos(x, y);
        SendMouseInput(MOUSEEVENTF_MOVE);
        Sleep(10);
    }

    SetCursorPos(endX, endY);
    SendMouseInput(MOUSEEVENTF_MOVE);
    Sleep(10);
    SendMouseInput(MOUSEEVENTF_LEFTUP);
    Sleep(300);
}

void UiPlayer::Wheel(RECT rect, int offsetLeft, int offsetTop, int steps, int totalDelta)
{
    int x, y;
    GetXY(rect, offsetLeft, offsetTop, &x, &y);
    SetCursorPos(x, y);
    Sleep(500);

    // Clamp step count to [1, 10]. A previous version allowed dragX <= 0 to
    // fall straight through to a "for (i < dragX)" loop that never ran,
    // silently dropping the entire scroll amount.
    if (steps < 1) steps = 1;
    if (steps > 10) steps = 10;

    // Distribute totalDelta across `steps` wheel events without losing the
    // remainder to integer truncation. E.g. totalDelta=350, steps=3 used to
    // send 3 x 116 = 348 (2 units silently lost); this sends 117+117+116=350.
    int baseDelta = totalDelta / steps;
    int remainder = totalDelta % steps; // same sign as totalDelta, |remainder| < steps
    int remainderSteps = (remainder >= 0) ? remainder : -remainder;
    int remainderSign = (remainder >= 0) ? 1 : -1;

    for (int i = 0; i < steps; i++)
    {
        int delta = baseDelta;
        if (i < remainderSteps)
        {
            delta += remainderSign;
        }

        SendMouseInput(MOUSEEVENTF_WHEEL, static_cast<DWORD>(delta));
        Sleep(333);
    }
}

void UiPlayer::SendText(BSTR encodedText, BOOL isCapsLockOn, BOOL isNumLockOn, BOOL isScrollLockOn)
{
    SetKeyState(VK_CAPITAL, isCapsLockOn);
    SetKeyState(VK_NUMLOCK, isNumLockOn);
    SetKeyState(VK_SCROLL, isScrollLockOn);

    int charCount = static_cast<int>(wcslen(encodedText));
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, encodedText, charCount, NULL, 0, NULL, NULL);
    std::string encodedStr(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, encodedText, charCount, &encodedStr[0], size_needed, NULL, NULL);

    std::string decodedStr = base64_decode(encodedStr);
    std::vector<char> bytes(decodedStr.begin(), decodedStr.end());

    size_t i = 0;
    while (i < bytes.size() / 2)
    {
        size_t n1 = i * 2;
        size_t n2 = i * 2 + 1;
        SendKeycode(static_cast<short>(bytes[n2]), bytes[n1] == 0 ? 0 : 1);
        i++;
        Sleep(77);
    }

    SetKeyState(VK_SCROLL, isScrollLockOn);
    SetKeyState(VK_NUMLOCK, isNumLockOn);
    SetKeyState(VK_CAPITAL, isCapsLockOn);
}