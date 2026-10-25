#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <d2d1.h>
#include <d2d1helper.h>
#include <dwrite.h>
#include <vector>
#include <string>

#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")

namespace {

ID2D1Factory* g_d2d = nullptr;
ID2D1DCRenderTarget* g_target = nullptr;
IDWriteFactory* g_dwrite = nullptr;
IDWriteTextFormat* g_text = nullptr;
ID2D1SolidColorBrush* g_brush = nullptr;
HBITMAP g_bitmap = nullptr;
HDC g_memoryDc = nullptr;
void* g_bits = nullptr;

HWND g_hwnd = nullptr;
int g_width = 0;
int g_height = 0;
bool g_drawing = false;
bool g_running = true;

struct Stroke
{
    std::vector<D2D1_POINT_2F> points;
    D2D1_COLOR_F color = D2D1::ColorF(0.06f, 0.06f, 0.06f, 1.0f);
    float width = 4.0f;
};

std::vector<Stroke> g_strokes;
D2D1_RECT_F g_closeButton = {};

void DrawLabel(const wchar_t* text, const D2D1_RECT_F& rect, bool center)
{
    if (!g_dwrite || !g_text)
    {
        return;
    }

    IDWriteTextLayout* layout = nullptr;
    if (SUCCEEDED(g_dwrite->CreateTextLayout(
            text, static_cast<UINT32>(wcslen(text)), g_text,
            rect.right - rect.left, rect.bottom - rect.top, &layout)) && layout)
    {
        if (center)
        {
            layout->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        }
        g_target->DrawTextLayout(D2D1::Point2F(rect.left, rect.top), layout, g_brush);
        layout->Release();
    }
}

void Render()
{
    RECT rect = {0, 0, g_width, g_height};
    g_target->BindDC(g_memoryDc, &rect);
    g_target->BeginDraw();
    g_target->SetTransform(D2D1::Matrix3x2F::Identity());
    g_target->Clear(D2D1::ColorF(0, 0, 0, 0));

    for (const auto& stroke : g_strokes)
    {
        g_brush->SetColor(stroke.color);
        for (size_t i = 1; i < stroke.points.size(); ++i)
        {
            g_target->DrawLine(stroke.points[i - 1], stroke.points[i], g_brush, stroke.width);
        }
    }

    // Bottom toolbar background.
    D2D1_RECT_F bar = D2D1::RectF(
        g_width / 2.0f - 220.0f,
        g_height - 80.0f,
        g_width / 2.0f + 220.0f,
        g_height - 20.0f);
    g_brush->SetColor(D2D1::ColorF(0.08f, 0.08f, 0.08f, 0.94f));
    g_target->FillRoundedRectangle(D2D1::RoundedRect(bar, 14.0f, 14.0f), g_brush);
    g_brush->SetColor(D2D1::ColorF(0.25f, 0.25f, 0.25f, 1.0f));
    g_target->DrawRoundedRectangle(D2D1::RoundedRect(bar, 14.0f, 14.0f), g_brush, 1.0f);

    g_brush->SetColor(D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f));
    DrawLabel(L"PenBoard  ·  Esc 关闭  ·  清除",
              D2D1::RectF(bar.left + 18.0f, bar.top + 14.0f, bar.right - 90.0f, bar.bottom),
              false);

    g_closeButton = D2D1::RectF(bar.right - 88.0f, bar.top + 10.0f, bar.right - 12.0f, bar.bottom - 10.0f);
    g_brush->SetColor(D2D1::ColorF(0.90f, 0.22f, 0.21f, 1.0f));
    g_target->FillRoundedRectangle(D2D1::RoundedRect(g_closeButton, 8.0f, 8.0f), g_brush);
    g_brush->SetColor(D2D1::ColorF(1, 1, 1, 1));
    DrawLabel(L"关闭", D2D1::RectF(g_closeButton.left + 12.0f, g_closeButton.top + 9.0f, g_closeButton.right, g_closeButton.bottom), true);

    g_target->EndDraw();

    HDC screen = GetDC(nullptr);
    POINT source = {0, 0};
    POINT destination = {0, 0};
    SIZE size = {g_width, g_height};
    BLENDFUNCTION blend = {AC_SRC_OVER, 0, 255, AC_SRC_ALPHA};
    UpdateLayeredWindow(g_hwnd, screen, &destination, &size, g_memoryDc, &source, 0, &blend, ULW_ALPHA);
    ReleaseDC(nullptr, screen);
}

bool InitializeGraphics(HWND hwnd)
{
    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, &g_d2d)))
    {
        return false;
    }

    auto properties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_SOFTWARE,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
    if (FAILED(g_d2d->CreateDCRenderTarget(&properties, &g_target)))
    {
        return false;
    }

    if (FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory),
                                   reinterpret_cast<IUnknown**>(&g_dwrite))))
    {
        return false;
    }
    if (FAILED(g_dwrite->CreateTextFormat(L"Microsoft YaHei UI", nullptr,
                                          DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL,
                                          DWRITE_FONT_STRETCH_NORMAL, 14.0f, L"zh-CN", &g_text)))
    {
        return false;
    }

    HDC screen = GetDC(nullptr);
    g_memoryDc = CreateCompatibleDC(screen);
    ReleaseDC(nullptr, screen);

    BITMAPINFO info = {};
    info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth = g_width;
    info.bmiHeader.biHeight = -g_height;
    info.bmiHeader.biPlanes = 1;
    info.bmiHeader.biBitCount = 32;
    info.bmiHeader.biCompression = BI_RGB;

    g_bitmap = CreateDIBSection(g_memoryDc, &info, DIB_RGB_COLORS, &g_bits, nullptr, 0);
    if (!g_bitmap)
    {
        return false;
    }
    SelectObject(g_memoryDc, g_bitmap);
    g_target->CreateSolidColorBrush(D2D1::ColorF(1, 1, 1, 1), &g_brush);
    return g_brush != nullptr;
}

void AddPoint(float x, float y)
{
    if (g_strokes.empty() || !g_drawing)
    {
        return;
    }
    auto& points = g_strokes.back().points;
    if (!points.empty())
    {
        auto dx = points.back().x - x;
        auto dy = points.back().y - y;
        if (dx * dx + dy * dy < 1.0f)
        {
            return;
        }
    }
    points.push_back(D2D1::Point2F(x, y));
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_POINTERDOWN:
    {
        POINTER_INFO info = {};
        if (GetPointerInfo(GET_POINTERID_WPARAM(wParam), &info))
        {
            POINT point = info.ptPixelLocation;
            ScreenToClient(hwnd, &point);
            g_drawing = true;
            g_strokes.push_back(Stroke{});
            g_strokes.back().points.push_back(D2D1::Point2F((float)point.x, (float)point.y));
            SetCapture(hwnd);
            Render();
        }
        return 0;
    }
    case WM_POINTERUPDATE:
    {
        POINTER_INFO info = {};
        if (g_drawing && GetPointerInfo(GET_POINTERID_WPARAM(wParam), &info))
        {
            POINT point = info.ptPixelLocation;
            ScreenToClient(hwnd, &point);
            AddPoint((float)point.x, (float)point.y);
            Render();
        }
        return 0;
    }
    case WM_POINTERUP:
    {
        g_drawing = false;
        ReleaseCapture();
        Render();
        return 0;
    }
    case WM_LBUTTONDOWN:
    {
        POINT point = {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
        if (point.x >= g_closeButton.left && point.x <= g_closeButton.right &&
            point.y >= g_closeButton.top && point.y <= g_closeButton.bottom)
        {
            g_running = false;
            PostQuitMessage(0);
            return 0;
        }
        g_drawing = true;
        g_strokes.push_back(Stroke{});
        g_strokes.back().points.push_back(D2D1::Point2F((float)point.x, (float)point.y));
        SetCapture(hwnd);
        return 0;
    }
    case WM_MOUSEMOVE:
    {
        if (g_drawing)
        {
            AddPoint((float)GET_X_LPARAM(lParam), (float)GET_Y_LPARAM(lParam));
            Render();
        }
        return 0;
    }
    case WM_LBUTTONUP:
    {
        g_drawing = false;
        ReleaseCapture();
        Render();
        return 0;
    }
    case WM_PAINT:
    {
        PAINTSTRUCT paint = {};
        BeginPaint(hwnd, &paint);
        Render();
        EndPaint(hwnd, &paint);
        return 0;
    }
    case WM_KEYDOWN:
    {
        if (wParam == VK_ESCAPE)
        {
            g_running = false;
            PostQuitMessage(0);
        }
        else if (wParam == VK_DELETE || wParam == 'C')
        {
            g_strokes.clear();
            Render();
        }
        return 0;
    }
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, message, wParam, lParam);
}

} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    g_width = GetSystemMetrics(SM_CXSCREEN);
    g_height = GetSystemMetrics(SM_CYSCREEN);

    WNDCLASSEXW windowClass = {};
    windowClass.cbSize = sizeof(windowClass);
    windowClass.lpfnWndProc = WindowProc;
    windowClass.hInstance = instance;
    windowClass.hCursor = LoadCursor(nullptr, IDC_ARROW);
    windowClass.lpszClassName = L"PenBoardNativeOverlay";
    RegisterClassExW(&windowClass);

    HWND hwnd = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        windowClass.lpszClassName, L"PenBoard Native Overlay",
        WS_POPUP, 0, 0, g_width, g_height,
        nullptr, nullptr, instance, nullptr);
    if (!hwnd)
    {
        return 1;
    }
    g_hwnd = hwnd;

    if (!InitializeGraphics(hwnd))
    {
        DestroyWindow(hwnd);
        return 2;
    }

    ShowWindow(hwnd, SW_SHOWNOACTIVATE);
    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, g_width, g_height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    Render();

    MSG message = {};
    while (g_running && GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    if (g_brush) g_brush->Release();
    if (g_text) g_text->Release();
    if (g_dwrite) g_dwrite->Release();
    if (g_target) g_target->Release();
    if (g_d2d) g_d2d->Release();
    if (g_bitmap) DeleteObject(g_bitmap);
    if (g_memoryDc) DeleteDC(g_memoryDc);
    return 0;
}
