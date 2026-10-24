#include <windows.h>
#include <shellapi.h>
#include <tlhelp32.h>
#include <filesystem>
#include <string>
#include <system_error>
#include <vector>

#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "advapi32.lib")

namespace {

std::wstring Expand(const wchar_t* value)
{
    wchar_t buffer[32768] = {};
    const DWORD length = ExpandEnvironmentStringsW(value, buffer, ARRAYSIZE(buffer));
    if (length == 0 || length >= ARRAYSIZE(buffer))
    {
        return {};
    }
    return std::wstring(buffer, length - 1);
}

void DeleteTree(const std::wstring& path)
{
    if (path.empty())
    {
        return;
    }

    std::error_code error;
    std::filesystem::remove_all(std::filesystem::path(path), error);
}

void StopPenBoard()
{
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        return;
    }

    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry))
    {
        do
        {
            if (_wcsicmp(entry.szExeFile, L"PenBoard.exe") == 0)
            {
                HANDLE process = OpenProcess(PROCESS_TERMINATE, FALSE, entry.th32ProcessID);
                if (process != nullptr)
                {
                    TerminateProcess(process, 0);
                    CloseHandle(process);
                }
            }
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
}

void UnblockDirectory(const std::wstring& directory)
{
    if (directory.empty() ||
        GetFileAttributesW(directory.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return;
    }

    WIN32_FIND_DATAW data = {};
    const std::wstring pattern = directory + L"\\*";
    HANDLE find = FindFirstFileW(pattern.c_str(), &data);
    if (find == INVALID_HANDLE_VALUE)
    {
        return;
    }

    do
    {
        if (wcscmp(data.cFileName, L".") == 0 || wcscmp(data.cFileName, L"..") == 0)
        {
            continue;
        }

        const std::wstring full = directory + L"\\" + data.cFileName;
        if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            if ((data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
            {
                UnblockDirectory(full);
            }
        }
        else
        {
            const std::wstring zone = full + L":Zone.Identifier";
            DeleteFileW(zone.c_str());
        }
    } while (FindNextFileW(find, &data));

    FindClose(find);
}

void LaunchPenBoard()
{
    const std::wstring executable = Expand(L"%LOCALAPPDATA%\\Programs\\PenBoard\\PenBoard.exe");
    if (GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return;
    }

    std::wstring command = L"\"" + executable + L"\" --daemon";
    STARTUPINFOW startup = {};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process = {};
    if (CreateProcessW(
            executable.c_str(),
            command.data(),
            nullptr,
            nullptr,
            FALSE,
            CREATE_NEW_PROCESS_GROUP,
            nullptr,
            nullptr,
            &startup,
            &process))
    {
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
    }
}

} // namespace

int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv == nullptr)
    {
        return 0;
    }

    const std::wstring command = argc > 1 ? argv[1] : L"";
    if (_wcsicmp(command.c_str(), L"stop") == 0)
    {
        StopPenBoard();
    }
    else if (_wcsicmp(command.c_str(), L"cleanup-old") == 0)
    {
        DeleteTree(Expand(L"%LOCALAPPDATA%\\PenBoard"));
        DeleteFileW(Expand(L"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\PenBoard.lnk").c_str());
    }
    else if (_wcsicmp(command.c_str(), L"unblock") == 0)
    {
        UnblockDirectory(Expand(L"%LOCALAPPDATA%\\Programs\\PenBoard"));
    }
    else if (_wcsicmp(command.c_str(), L"remove-user-data") == 0)
    {
        DeleteTree(Expand(L"%APPDATA%\\PenBoard"));
        DeleteTree(Expand(L"%LOCALAPPDATA%\\PenBoard"));
    }
    else if (_wcsicmp(command.c_str(), L"launch") == 0)
    {
        LaunchPenBoard();
    }

    LocalFree(argv);
    return 0;
}
