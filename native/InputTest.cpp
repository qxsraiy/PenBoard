#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdio>

static HWND g_hwnd;
static void Log(const char* text)
{
    char path[MAX_PATH] = {};
    GetTempPathA(MAX_PATH, path);
    strcat_s(path, "PenBoard-input-test.log");
    FILE* file = nullptr;
    fopen_s(&file, path, "a");
    if (file)
    {
        fprintf(file, "%s\n", text);
        fclose(file);
    }
}

LRESULT CALLBACK Proc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_LBUTTONDOWN:
        Log("WM_LBUTTONDOWN");
        return 0;
    case WM_MOUSEMOVE:
        Log("WM_MOUSEMOVE");
        return 0;
    case WM_NCHITTEST:
        Log("WM_NCHITTEST");
        return HTCLIENT;
    case WM_PAINT:
    {
        PAINTSTRUCT ps = {};
        HDC dc = BeginPaint(hwnd, &ps);
        RECT rect = {0, 0, 400, 300};
        FillRect(dc, &rect, (HBRUSH)GetStockObject(GRAY_BRUSH));
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = Proc;
    wc.hInstance = instance;
    wc.lpszClassName = L"PenBoardInputTest";
    wc.hbrBackground = (HBRUSH)GetStockObject(GRAY_BRUSH);
    RegisterClassExW(&wc);
    g_hwnd = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED, wc.lpszClassName, L"InputTest",
                             WS_OVERLAPPEDWINDOW, 100, 100, 500, 400, nullptr, nullptr, instance, nullptr);
    SetLayeredWindowAttributes(g_hwnd, RGB(255,0,255), 0, LWA_COLORKEY);
    ShowWindow(g_hwnd, SW_SHOW);
    SetForegroundWindow(g_hwnd);
    Log("shown");
    MSG msg = {};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return 0;
}
