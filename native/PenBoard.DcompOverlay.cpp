#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <shellapi.h>
#include <d3d11.h>
#include <dxgi1_3.h>
#include <dcomp.h>
#include <d2d1_1.h>
#include <d2d1helper.h>
#include <dwrite.h>
#include <wincodec.h>
#include <shlobj.h>
#include <wrl/client.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Input.Inking.h>
#include <mutex>
#include <string>
#include <cstdio>
#include <vector>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dcomp.lib")
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")
#pragma comment(lib, "windowscodecs.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "windowsapp.lib")

using Microsoft::WRL::ComPtr;

namespace {

HWND g_hwnd;
int g_width = 0;
int g_height = 0;
bool g_drawing = false;
bool g_running = true;
bool g_annotationMode = false;
bool g_hideToolbar = false;
ComPtr<ID2D1Bitmap1> g_backgroundBitmap;
HHOOK g_keyboardHook = nullptr;
bool g_textMode = true;
std::wstring g_language = L"zh";
std::vector<std::wstring> g_candidates;
D2D1_RECT_F g_recognizeButton = {};
D2D1_RECT_F g_languageButton = {};
D2D1_RECT_F g_backspaceButton = {};
D2D1_RECT_F g_undoButton = {};
D2D1_RECT_F g_clearButton = {};
D2D1_RECT_F g_closeButton = {};
D2D1_RECT_F g_savePngButton = {};
D2D1_RECT_F g_savePdfButton = {};
constexpr UINT WM_APP_RECOGNIZED = WM_APP + 11;

ComPtr<ID3D11Device> g_d3dDevice;
ComPtr<ID3D11DeviceContext> g_d3dContext;
ComPtr<IDXGISwapChain1> g_swapChain;
ComPtr<IDCompositionDevice> g_compDevice;
ComPtr<IDCompositionTarget> g_compTarget;
ComPtr<IDCompositionVisual> g_compVisual;

ComPtr<ID2D1Factory1> g_d2dFactory;
ComPtr<ID2D1Device> g_d2dDevice;
ComPtr<ID2D1DeviceContext> g_d2dContext;
ComPtr<ID2D1Bitmap1> g_targetBitmap;
ComPtr<ID2D1SolidColorBrush> g_brush;
ComPtr<IDWriteFactory> g_dwrite;
ComPtr<IDWriteTextFormat> g_textFormat;

struct Stroke
{
    std::vector<D2D1_POINT_2F> points;
};

std::vector<Stroke> g_strokes;

void Log(const char* format, ...)
{
    char path[MAX_PATH] = {};
    GetTempPathA(MAX_PATH, path);
    strcat_s(path, "PenBoard-dcomp.log");
    FILE* file = nullptr;
    if (fopen_s(&file, path, "a") == 0 && file)
    {
        va_list args;
        va_start(args, format);
        vfprintf(file, format, args);
        va_end(args);
        fputc('\n', file);
        fclose(file);
    }
}

bool CreateGraphics(HWND hwnd)
{
    D3D_FEATURE_LEVEL levels[] = {D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0};
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    D3D_FEATURE_LEVEL selected = D3D_FEATURE_LEVEL_11_0;
    HRESULT hr = D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
        levels, ARRAYSIZE(levels), D3D11_SDK_VERSION,
        &g_d3dDevice, &selected, &g_d3dContext);
    if (FAILED(hr))
    {
        Log("D3D11CreateDevice failed 0x%08X", hr);
        return false;
    }

    ComPtr<IDXGIDevice> dxgiDevice;
    g_d3dDevice.As(&dxgiDevice);
    ComPtr<IDXGIAdapter> adapter;
    dxgiDevice->GetAdapter(&adapter);
    ComPtr<IDXGIFactory2> factory;
    adapter->GetParent(IID_PPV_ARGS(&factory));

    DXGI_SWAP_CHAIN_DESC1 description = {};
    description.Width = g_width;
    description.Height = g_height;
    description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    description.BufferCount = 2;
    description.Scaling = DXGI_SCALING_STRETCH;
    description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    description.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
    hr = factory->CreateSwapChainForComposition(
        g_d3dDevice.Get(), &description, nullptr, &g_swapChain);
    if (FAILED(hr))
    {
        Log("CreateSwapChainForComposition failed 0x%08X", hr);
        return false;
    }

    hr = DCompositionCreateDevice(dxgiDevice.Get(), IID_PPV_ARGS(&g_compDevice));
    if (FAILED(hr))
    {
        Log("DCompositionCreateDevice failed 0x%08X", hr);
        return false;
    }
    g_compDevice->CreateTargetForHwnd(hwnd, TRUE, &g_compTarget);
    g_compDevice->CreateVisual(&g_compVisual);
    g_compVisual->SetContent(g_swapChain.Get());
    g_compTarget->SetRoot(g_compVisual.Get());
    g_compDevice->Commit();

    hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, IID_PPV_ARGS(&g_d2dFactory));
    if (FAILED(hr))
    {
        return false;
    }
    hr = g_d2dFactory->CreateDevice(dxgiDevice.Get(), &g_d2dDevice);
    if (FAILED(hr))
    {
        return false;
    }
    hr = g_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &g_d2dContext);
    if (FAILED(hr))
    {
        return false;
    }

    ComPtr<IDXGISurface> surface;
    g_swapChain->GetBuffer(0, IID_PPV_ARGS(&surface));
    auto properties = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
    hr = g_d2dContext->CreateBitmapFromDxgiSurface(surface.Get(), &properties, &g_targetBitmap);
    if (FAILED(hr))
    {
        Log("CreateBitmapFromDxgiSurface failed 0x%08X", hr);
        return false;
    }
    g_d2dContext->SetTarget(g_targetBitmap.Get());
    g_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0.06f, 0.06f, 0.06f, 1), &g_brush);

    DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory),
                        reinterpret_cast<IUnknown**>(g_dwrite.GetAddressOf()));
    g_dwrite->CreateTextFormat(L"Microsoft YaHei UI", nullptr,
                               DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL,
                               DWRITE_FONT_STRETCH_NORMAL, 14.0f, L"zh-CN", &g_textFormat);
    return true;
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

void DrawLabel(const wchar_t* text, const D2D1_RECT_F& rect, bool center)
{
    if (!g_dwrite || !g_textFormat)
    {
        return;
    }
    ComPtr<IDWriteTextLayout> layout;
    if (SUCCEEDED(g_dwrite->CreateTextLayout(
            text, static_cast<UINT32>(wcslen(text)), g_textFormat.Get(),
            rect.right - rect.left, rect.bottom - rect.top, &layout)) && layout)
    {
        if (center)
        {
            layout->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        }
        g_d2dContext->DrawTextLayout(D2D1::Point2F(rect.left, rect.top), layout.Get(), g_brush.Get());
    }
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
void CaptureScreen()
{
    HDC screen = GetDC(nullptr);
    HDC memory = CreateCompatibleDC(screen);
    BITMAPINFO info = {};
    info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth = g_width;
    info.bmiHeader.biHeight = -g_height;
    info.bmiHeader.biPlanes = 1;
    info.bmiHeader.biBitCount = 32;
    info.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    HBITMAP bitmap = CreateDIBSection(screen, &info, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!bitmap || !bits)
    {
        if (bitmap) DeleteObject(bitmap);
        DeleteDC(memory);
        ReleaseDC(nullptr, screen);
        return;
    }
    SelectObject(memory, bitmap);
    BitBlt(memory, 0, 0, g_width, g_height, screen, 0, 0, SRCCOPY);

    auto* pixels = static_cast<uint32_t*>(bits);
    for (size_t i = 0; i < static_cast<size_t>(g_width) * g_height; ++i)
    {
        pixels[i] |= 0xFF000000;
    }

    auto properties = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_NONE,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
    g_d2dContext->CreateBitmap(
        D2D1::SizeU(g_width, g_height), bits, g_width * 4, &properties, &g_backgroundBitmap);

    DeleteObject(bitmap);
    DeleteDC(memory);
    ReleaseDC(nullptr, screen);
}

void Render();

std::wstring AnnotationPath(const wchar_t* extension)
{
    wchar_t profile[MAX_PATH] = {};
    GetEnvironmentVariableW(L"USERPROFILE", profile, MAX_PATH);
    std::wstring directory = std::wstring(profile) + L"\\Pictures\\PenBoard";
    SHCreateDirectoryExW(nullptr, directory.c_str(), nullptr);
    SYSTEMTIME time = {};
    GetLocalTime(&time);
    wchar_t name[128] = {};
    swprintf_s(name, L"\\PenBoard_%04d%02d%02d_%02d%02d%02d.%s",
               time.wYear, time.wMonth, time.wDay, time.wHour, time.wMinute, time.wSecond, extension);
    return directory + name;
}
bool SavePng(const std::wstring& path)
{
    g_hideToolbar = true;
    Render();
    g_hideToolbar = false;

    ComPtr<ID3D11Texture2D> backBuffer;
    if (FAILED(g_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))))
    {
        return false;
    }
    D3D11_TEXTURE2D_DESC description = {};
    backBuffer->GetDesc(&description);
    description.Usage = D3D11_USAGE_STAGING;
    description.BindFlags = 0;
    description.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    description.MiscFlags = 0;
    ComPtr<ID3D11Texture2D> staging;
    if (FAILED(g_d3dDevice->CreateTexture2D(&description, nullptr, &staging)))
    {
        return false;
    }
    g_d3dContext->CopyResource(staging.Get(), backBuffer.Get());

    D3D11_MAPPED_SUBRESOURCE mapped = {};
    Log("SavePng staging");
    if (FAILED(g_d3dContext->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &mapped)))
    {
        return false;
    }

    ComPtr<IWICImagingFactory> wic;
    CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&wic));
    ComPtr<IWICStream> stream;
    ComPtr<IWICBitmapEncoder> encoder;
    ComPtr<IWICBitmapFrameEncode> frame;
    bool ok = false;
    if (wic)
    {
        wic->CreateStream(&stream);
        stream->InitializeFromFilename(path.c_str(), GENERIC_WRITE);
        wic->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder);
        encoder->Initialize(stream.Get(), WICBitmapEncoderNoCache);
        encoder->CreateNewFrame(&frame, nullptr);
        frame->Initialize(nullptr);
        frame->SetSize(description.Width, description.Height);
        WICPixelFormatGUID format = GUID_WICPixelFormat32bppBGRA;
        frame->SetPixelFormat(&format);
        frame->WritePixels(description.Height, mapped.RowPitch,
                           mapped.RowPitch * description.Height,
                           static_cast<BYTE*>(mapped.pData));
        frame->Commit();
        encoder->Commit();
        ok = true;
        Log("SavePng ok");
    }
    g_d3dContext->Unmap(staging.Get(), 0);
    return ok;
}
void Render()
{
    if (!g_d2dContext || !g_swapChain)
    {
        return;
    }

    g_d2dContext->BeginDraw();
    g_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
    g_d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));
    if (g_backgroundBitmap)
    {
        g_d2dContext->DrawBitmap(g_backgroundBitmap.Get(), D2D1::RectF(0, 0, (float)g_width, (float)g_height));
    }

    for (const auto& stroke : g_strokes)
    {
        g_brush->SetColor(D2D1::ColorF(0.06f, 0.06f, 0.06f, 1));
        for (size_t i = 1; i < stroke.points.size(); ++i)
        {
            g_d2dContext->DrawLine(stroke.points[i - 1], stroke.points[i], g_brush.Get(), 4.0f);
        }
    }

    if (g_hideToolbar)
    {
        g_d2dContext->EndDraw();
        g_swapChain->Present(0, 0);
        return;
    }

    // Candidate row.
    if (g_textMode && !g_candidates.empty())
    {
        float left = g_width / 2.0f - 260.0f;
        for (size_t i = 0; i < g_candidates.size() && i < 6; ++i)
        {
            D2D1_RECT_F rect = D2D1::RectF(left, g_height - 146.0f, left + 80.0f, g_height - 104.0f);
            g_brush->SetColor(D2D1::ColorF(0.12f, 0.12f, 0.12f, 0.96f));
            g_d2dContext->FillRoundedRectangle(D2D1::RoundedRect(rect, 8, 8), g_brush.Get());
            g_brush->SetColor(D2D1::ColorF(1, 1, 1, 1));
            DrawLabel(g_candidates[i].c_str(), rect, true);
            left += 86.0f;
        }
    }

    D2D1_RECT_F bar = D2D1::RectF(g_width / 2.0f - 340.0f, g_height - 90.0f,
                                  g_width / 2.0f + 340.0f, g_height - 20.0f);
    g_brush->SetColor(D2D1::ColorF(0.08f, 0.08f, 0.08f, 0.94f));
    g_d2dContext->FillRoundedRectangle(D2D1::RoundedRect(bar, 14, 14), g_brush.Get());

    auto button = [&](D2D1_RECT_F rect, const wchar_t* label, D2D1_COLOR_F color)
    {
        g_brush->SetColor(color);
        g_d2dContext->FillRoundedRectangle(D2D1::RoundedRect(rect, 8, 8), g_brush.Get());
        g_brush->SetColor(D2D1::ColorF(1, 1, 1, 1));
        DrawLabel(label, rect, true);
    };

    if (g_annotationMode)
    {
        float ax = bar.left + 12.0f;
        float ay = bar.top + 12.0f;
        float ah = bar.bottom - bar.top - 24.0f;
        g_savePngButton = D2D1::RectF(ax, ay, ax + 84.0f, ay + ah); ax += 92.0f;
        button(g_savePngButton, L"Save PNG", D2D1::ColorF(0.12f, 0.45f, 0.85f, 1.0f));
        g_savePdfButton = D2D1::RectF(ax, ay, ax + 84.0f, ay + ah); ax += 92.0f;
        button(g_savePdfButton, L"Save PDF", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
        g_closeButton = D2D1::RectF(bar.right - 82.0f, ay, bar.right - 12.0f, ay + ah);
        button(g_closeButton, L"Close", D2D1::ColorF(0.90f, 0.22f, 0.21f, 1.0f));
        g_d2dContext->EndDraw();
        g_swapChain->Present(0, 0);
        return;
    }
    float x = bar.left + 12.0f;
    float y = bar.top + 12.0f;
    float h = bar.bottom - bar.top - 24.0f;
    g_recognizeButton = D2D1::RectF(x, y, x + 84.0f, y + h); x += 92.0f;
    button(g_recognizeButton, L"Recognize", D2D1::ColorF(0.12f, 0.45f, 0.85f, 1.0f));
    g_languageButton = D2D1::RectF(x, y, x + 60.0f, y + h); x += 68.0f;
    button(g_languageButton, g_language == L"en" ? L"EN" : L"ZH", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_backspaceButton = D2D1::RectF(x, y, x + 64.0f, y + h); x += 72.0f;
    button(g_backspaceButton, L"Back", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_undoButton = D2D1::RectF(x, y, x + 64.0f, y + h); x += 72.0f;
    button(g_undoButton, L"Undo", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_clearButton = D2D1::RectF(x, y, x + 64.0f, y + h); x += 72.0f;
    button(g_clearButton, L"Clear", D2D1::ColorF(0.30f, 0.30f, 0.30f, 1.0f));
    g_closeButton = D2D1::RectF(bar.right - 82.0f, y, bar.right - 12.0f, y + h);
    button(g_closeButton, L"Close", D2D1::ColorF(0.90f, 0.22f, 0.21f, 1.0f));
    HRESULT hr = g_d2dContext->EndDraw();
    if (FAILED(hr))
    {
        Log("D2D EndDraw 0x%08X", hr);
    }
    hr = g_swapChain->Present(0, 0);
    if (FAILED(hr))
    {
        Log("Present 0x%08X", hr);
    }
}

void AddPoint(float x, float y)
{
    if (!g_drawing || g_strokes.empty())
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
    return InRect(g_recognizeButton, x, y) || InRect(g_languageButton, x, y) || InRect(g_savePngButton, x, y) || InRect(g_savePdfButton, x, y) ||
           InRect(g_backspaceButton, x, y) || InRect(g_undoButton, x, y) ||
           InRect(g_clearButton, x, y) || InRect(g_closeButton, x, y);
}

void HandleTap(float x, float y)
{
    if (InRect(g_closeButton, x, y))
    {
        PostQuitMessage(0);
    }
    else if (InRect(g_recognizeButton, x, y))
    {
        RecognizeAsync();
    }
    else if (InRect(g_languageButton, x, y))
    {
        g_language = g_language == L"zh" ? L"en" : L"zh";
        Render();
    }
    else if (InRect(g_backspaceButton, x, y))
    {
        SendBackspace();
    }
    else if (InRect(g_undoButton, x, y) && !g_strokes.empty())
    {
        g_strokes.pop_back();
        Render();
    }
    else if (InRect(g_clearButton, x, y))
    {
        g_strokes.clear();
        g_candidates.clear();
        Render();
    }
}
LRESULT CALLBACK KeyboardHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
    {
        auto* data = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        if (data->vkCode == VK_ESCAPE)
        {
            PostMessageW(g_hwnd, WM_CLOSE, 0, 0);
            return 1;
        }
    }
    return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
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
        g_drawing = false;
        Render();
        return 0;
    case WM_LBUTTONDOWN:
    {
        POINT point = {GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam)};
        Log("WM_LBUTTONDOWN %d,%d", point.x, point.y);
        if (InToolbar((float)point.x, (float)point.y))
        {
            HandleTap((float)point.x, (float)point.y);
            return 0;
        }
        g_drawing = true;
        g_strokes.push_back(Stroke{});
        g_strokes.back().points.push_back(D2D1::Point2F((float)point.x, (float)point.y));
        return 0;
    }
    case WM_MOUSEMOVE:
        Log("WM_MOUSEMOVE %d,%d", GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
        if (g_drawing)
        {
            AddPoint((float)GET_X_LPARAM(lParam), (float)GET_Y_LPARAM(lParam));
            Render();
        }
        return 0;
    case WM_LBUTTONUP:
        Log("WM_LBUTTONUP %d,%d", GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam));
        g_drawing = false;
        SetTimer(hwnd, 1, 900, nullptr);
        Render();
        return 0;    case WM_TIMER:
        Log("WM_TIMER recognize strokes=%zu", g_strokes.size());
        KillTimer(hwnd, 1);
        RecognizeAsync();
        return 0;
    case WM_APP_RECOGNIZED:
    {
        auto candidates = reinterpret_cast<std::vector<std::wstring>*>(lParam);
        if (candidates)
        {
            g_candidates = *candidates;
            delete candidates;
            if (!g_candidates.empty())
            {
                InjectText(g_candidates[0]);
                g_strokes.clear();
            }
        }
        Render();
        return 0;
    }
    case WM_KEYDOWN:
        if (wParam == VK_ESCAPE)
        {
            PostQuitMessage(0);
        }
        else if (wParam == 'C')
        {
            g_strokes.clear();
            Render();
        }
        return 0;
    case WM_PAINT:
    {
        PAINTSTRUCT paint = {};
        BeginPaint(hwnd, &paint);
        Render();
        EndPaint(hwnd, &paint);
        return 0;
    }
    case WM_CLOSE:
        PostQuitMessage(0);
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, message, wParam, lParam);
}

} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    for (int i = 1; i < argc; ++i)
    {
        if (wcscmp(argv[i], L"--annotation") == 0)
        {
            g_annotationMode = true;
        }
    }
    if (argv) LocalFree(argv);
    Log("Annotation mode=%d argc=%d", g_annotationMode, argc);
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    g_width = GetSystemMetrics(SM_CXSCREEN);
    g_height = GetSystemMetrics(SM_CYSCREEN);

    WNDCLASSEXW windowClass = {};
    windowClass.cbSize = sizeof(windowClass);
    windowClass.lpfnWndProc = WindowProc;
    windowClass.hInstance = instance;
    windowClass.hCursor = LoadCursor(nullptr, IDC_ARROW);
    windowClass.lpszClassName = L"PenBoardDcompOverlay";
    RegisterClassExW(&windowClass);

    g_hwnd = CreateWindowExW(
        WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        windowClass.lpszClassName, L"PenBoard DComp Overlay",
        WS_POPUP, 0, 0, g_width, g_height,
        nullptr, nullptr, instance, nullptr);
    if (!g_hwnd)
    {
        return 1;
    }

    if (!CreateGraphics(g_hwnd))
    {
        DestroyWindow(g_hwnd);
        return 2;
    }

    if (g_annotationMode)
    {
        CaptureScreen();
    }
    ShowWindow(g_hwnd, SW_SHOWNOACTIVATE);
    SetWindowPos(g_hwnd, HWND_TOPMOST, 0, 0, g_width, g_height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    Render();
    g_keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, GetModuleHandleW(nullptr), 0);
    Log("Keyboard hook=%p", g_keyboardHook);

    MSG message = {};
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return 0;
}
