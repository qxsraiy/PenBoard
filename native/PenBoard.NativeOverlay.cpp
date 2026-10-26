#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <d2d1.h>
#include <d2d1helper.h>
#include <dwrite.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Input.Inking.h>
#include <thread>
#include <mutex>
#include <vector>
#include <string>

#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")
#pragma comment(lib, "windowsapp.lib")

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
HWND g_targetHwnd = nullptr;
int g_width = 0;
int g_height = 0;
bool g_drawing = false;
bool g_running = true;
bool g_textMode = false;
std::wstring g_language = L"zh";
std::mutex g_candidateMutex;
std::vector<std::wstring> g_candidates;
std::vector<D2D1_RECT_F> g_candidateRects;
D2D1_RECT_F g_recognizeButton = {};
D2D1_RECT_F g_languageButton = {};
D2D1_RECT_F g_backspaceButton = {};
D2D1_RECT_F g_undoButton = {};
D2D1_RECT_F g_clearButton = {};

constexpr UINT WM_APP_RECOGNIZED = WM_APP + 1;

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

void InjectText(const std::wstring& text)
{
    if (text.empty())
    {
        return;
    }

    std::vector<INPUT> inputs;
    inputs.reserve(text.size() * 2);
    for (wchar_t ch : text)
    {
        INPUT down = {};
        down.type = INPUT_KEYBOARD;
        down.ki.wScan = ch;
        down.ki.dwFlags = KEYEVENTF_UNICODE;
        inputs.push_back(down);

        INPUT up = down;
        up.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        inputs.push_back(up);
    }
    SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
}

void SendBackspace()
{
    INPUT inputs[2] = {};
    inputs[0].type = INPUT_KEYBOARD;
    inputs[0].ki.wVk = VK_BACK;
    inputs[1].type = INPUT_KEYBOARD;
    inputs[1].ki.wVk = VK_BACK;
    inputs[1].ki.dwFlags = KEYEVENTF_KEYUP;
    SendInput(2, inputs, sizeof(INPUT));
}

void RecognizeAsync()
{
    if (!g_textMode || g_strokes.empty())
    {
        return;
    }

    auto snapshot = g_strokes;
    auto language = g_language;
    HWND hwnd = g_hwnd;
    std::thread([snapshot = std::move(snapshot), language, hwnd]()
    {
        try
        {
            winrt::init_apartment(winrt::apartment_type::multi_threaded);
            using namespace winrt::Windows::UI::Input::Inking;

            InkRecognizerContainer recognizer;
            InkRecognizer selected = nullptr;
            for (auto const& candidate : recognizer.GetRecognizers())
            {
                std::wstring name(candidate.Name().c_str());
                if (language == L"en")
                {
                    if (name.find(L"English") != std::wstring::npos)
                    {
                        selected = candidate;
                        break;
                    }
                }
                else if (name.find(L"中文") != std::wstring::npos ||
                         name.find(L"Chinese") != std::wstring::npos)
                {
                    selected = candidate;
                    break;
                }
            }
            if (!selected)
            {
                return;
            }
            recognizer.SetDefaultRecognizer(selected);

            InkStrokeBuilder builder;
            InkStrokeContainer container;
            for (const auto& stroke : snapshot)
            {
                auto points = winrt::single_threaded_vector<InkPoint>();
                for (const auto& point : stroke.points)
                {
                    points.Append(InkPoint(winrt::Windows::Foundation::Point(point.x, point.y), 0.5f));
                }
                if (points.Size() > 0)
                {
                    container.AddStroke(builder.CreateStrokeFromInkPoints(
                        points, winrt::Windows::Foundation::Numerics::float3x2::identity()));
                }
            }

            auto results = recognizer.RecognizeAsync(container, InkRecognitionTarget::All).get();
            auto candidates = new std::vector<std::wstring>();
            for (auto const& result : results)
            {
                for (auto const& text : result.GetTextCandidates())
                {
                    if (!text.empty() && candidates->size() < 8)
                    {
                        candidates->push_back(text.c_str());
                    }
                }
            }
            PostMessageW(hwnd, WM_APP_RECOGNIZED, 0, reinterpret_cast<LPARAM>(candidates));
        }
        catch (...)
        {
            PostMessageW(hwnd, WM_APP_RECOGNIZED, 0, reinterpret_cast<LPARAM>(new std::vector<std::wstring>()));
        }
    }).detach();
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

    // Candidate row.
    g_candidateRects.clear();
    if (g_textMode && !g_candidates.empty())
    {
        float left = g_width / 2.0f - 360.0f;
        for (size_t i = 0; i < g_candidates.size() && i < 8; ++i)
        {
            D2D1_RECT_F rect = D2D1::RectF(left, g_height - 146.0f, left + 84.0f, g_height - 102.0f);
            g_candidateRects.push_back(rect);
            g_brush->SetColor(D2D1::ColorF(0.12f, 0.12f, 0.12f, 0.96f));
            g_target->FillRoundedRectangle(D2D1::RoundedRect(rect, 8.0f, 8.0f), g_brush);
            g_brush->SetColor(D2D1::ColorF(1, 1, 1, 1));
            DrawLabel(g_candidates[i].c_str(), rect, true);
            left += 90.0f;
        }
    }

    // Bottom toolbar.
    D2D1_RECT_F bar = D2D1::RectF(
        g_width / 2.0f - 360.0f,
        g_height - 90.0f,
        g_width / 2.0f + 360.0f,
        g_height - 20.0f);
    g_brush->SetColor(D2D1::ColorF(0.08f, 0.08f, 0.08f, 0.94f));
    g_target->FillRoundedRectangle(D2D1::RoundedRect(bar, 14.0f, 14.0f), g_brush);
    g_brush->SetColor(D2D1::ColorF(0.25f, 0.25f, 0.25f, 1.0f));
    g_target->DrawRoundedRectangle(D2D1::RoundedRect(bar, 14.0f, 14.0f), g_brush, 1.0f);

    auto button = [&](D2D1_RECT_F rect, const wchar_t* label, D2D1_COLOR_F color)
    {
        g_brush->SetColor(color);
        g_target->FillRoundedRectangle(D2D1::RoundedRect(rect, 8.0f, 8.0f), g_brush);
        g_brush->SetColor(D2D1::ColorF(1, 1, 1, 1));
        DrawLabel(label, rect, true);
    };

    float x = bar.left + 12.0f;
    float y = bar.top + 12.0f;
    float h = bar.bottom - bar.top - 24.0f;
    g_recognizeButton = D2D1::RectF(x, y, x + 88.0f, y + h); x += 96.0f;
    button(g_recognizeButton, L"Recognize", D2D1::ColorF(0.12f, 0.45f, 0.85f, 1.0f));
    g_languageButton = D2D1::RectF(x, y, x + 70.0f, y + h); x += 78.0f;
    button(g_languageButton, g_language == L"en" ? L"EN" : L"ZH", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_backspaceButton = D2D1::RectF(x, y, x + 74.0f, y + h); x += 82.0f;
    button(g_backspaceButton, L"Back", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_undoButton = D2D1::RectF(x, y, x + 70.0f, y + h); x += 78.0f;
    button(g_undoButton, L"Undo", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_clearButton = D2D1::RectF(x, y, x + 70.0f, y + h); x += 78.0f;
    button(g_clearButton, L"Clear", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_closeButton = D2D1::RectF(bar.right - 86.0f, y, bar.right - 12.0f, y + h);
    button(g_closeButton, L"Close", D2D1::ColorF(0.90f, 0.22f, 0.21f, 1.0f));

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

bool InRect(const D2D1_RECT_F& rect, float x, float y)
{
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

bool InToolbar(float x, float y)
{
    if (InRect(g_recognizeButton, x, y) || InRect(g_languageButton, x, y) ||
        InRect(g_backspaceButton, x, y) || InRect(g_undoButton, x, y) ||
        InRect(g_clearButton, x, y) || InRect(g_closeButton, x, y))
    {
        return true;
    }
    for (const auto& rect : g_candidateRects)
    {
        if (InRect(rect, x, y))
        {
            return true;
        }
    }
    return false;
}

void HandleTap(float x, float y)
{
    if (InRect(g_closeButton, x, y))
    {
        g_running = false;
        PostQuitMessage(0);
        return;
    }
    if (InRect(g_recognizeButton, x, y))
    {
        RecognizeAsync();
        return;
    }
    if (InRect(g_languageButton, x, y))
    {
        g_language = g_language == L"zh" ? L"en" : L"zh";
        Render();
        return;
    }
    if (InRect(g_backspaceButton, x, y))
    {
        SendBackspace();
        return;
    }
    if (InRect(g_undoButton, x, y) && !g_strokes.empty())
    {
        g_strokes.pop_back();
        Render();
        return;
    }
    if (InRect(g_clearButton, x, y))
    {
        g_strokes.clear();
        g_candidates.clear();
        Render();
        return;
    }
    for (size_t i = 0; i < g_candidateRects.size() && i < g_candidates.size(); ++i)
    {
        if (InRect(g_candidateRects[i], x, y))
        {
            InjectText(g_candidates[i]);
            g_strokes.clear();
            g_candidates.clear();
            Render();
            return;
        }
    }
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
