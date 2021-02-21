#include "HashCode.h"
#include "Debug.h"
#include "../Managers/Game.h"
#include "../Core.h"
#include "AssemblyGenerator.h"
#include "../Managers/BaseAssembly.h"
#include <wincrypt.h>
#include "Assertion.h"
#include "Logger.h"
#include <sstream>

std::string HashCode::Hash;

bool HashCode::Initialize()
{
    if (!SetupPaths())
        return false;
    return GenerateHash(Core::Path);
}

bool HashCode::SetupPaths()
{
    std::string CorePath = std::string(Game::BasePath) + "\\MelonLoader\\Dependencies\\Bootstrap.dll";
    Core::Path = new char[CorePath.size() + 1];
    std::copy(CorePath.begin(), CorePath.end(), Core::Path);
    Core::Path[CorePath.size()] = '\0';

    std::string BaseAssemblyPath = std::string(Game::BasePath) + "\\MelonLoader\\MelonLoader.dll";
    if (!Core::FileExists(BaseAssemblyPath.c_str()))
    {
        Assertion::ThrowInternalFailure("MelonLoader.dll Does Not Exist!");
        return false;
    }
    BaseAssembly::Path = new char[BaseAssemblyPath.size() + 1];
    std::copy(BaseAssemblyPath.begin(), BaseAssemblyPath.end(), BaseAssembly::Path);
    BaseAssembly::Path[BaseAssemblyPath.size()] = '\0';

    if (!Game::IsIl2Cpp)
        return true;
    std::string AssemblyGeneratorPath = std::string(Game::BasePath) + "\\MelonLoader\\Dependencies\\AssemblyGenerator\\AssemblyGenerator.dll";
    if (!Core::FileExists(AssemblyGeneratorPath.c_str()))
    {
        Assertion::ThrowInternalFailure("AssemblyGenerator.dll Does Not Exist!");
        return false;
    }
    AssemblyGenerator::Path = new char[AssemblyGeneratorPath.size() + 1];
    std::copy(AssemblyGeneratorPath.begin(), AssemblyGeneratorPath.end(), AssemblyGenerator::Path);
    AssemblyGenerator::Path[AssemblyGeneratorPath.size()] = '\0';

	return true;
}

bool HashCode::GenerateHash(const char* path)
{
    if ((path == NULL) || (!Core::FileExists(path)))
        return false;
    HCRYPTPROV cryptprov = 0;
    if (!CryptAcquireContextA(&cryptprov, NULL, NULL, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT))
        return false;
    HCRYPTHASH crypthash = 0;
    if (!CryptCreateHash(cryptprov, CALG_MD5, 0, 0, &crypthash))
    {
        CryptReleaseContext(cryptprov, 0);
        return false;
    }
    HANDLE filehandle = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, NULL);
    if (filehandle == INVALID_HANDLE_VALUE)
    {
        CryptReleaseContext(cryptprov, 0);
        return false;
    }
    DWORD filebufsize = 2048;
    BYTE filebuf[2048];
    DWORD readoffset = NULL;
    bool readsuccess = false;
    while (readsuccess = ReadFile(filehandle, filebuf, filebufsize, &readoffset, NULL))
    {
        if (readoffset == NULL)
            break;
        if (!CryptHashData(crypthash, filebuf, readoffset, 0))
        {
            readsuccess = false;
            break;
        }
    }
    CloseHandle(filehandle);
    if (!readsuccess)
    {
        CryptReleaseContext(cryptprov, 0);
        CryptDestroyHash(crypthash);
        return false;
    }
    DWORD dhash = 16;
    BYTE hashbuf[16];
    CHAR chartbl[] = "0123456789abcdef";
    std::string hashout;
    if (!CryptGetHashParam(crypthash, HP_HASHVAL, (BYTE*)&hashbuf, &dhash, 0))
    {
        CryptReleaseContext(cryptprov, 0);
        CryptDestroyHash(crypthash);
        return false;
    }
    for (DWORD i = 0; i < dhash; i++)
        hashout += std::to_string(chartbl[hashbuf[i] >> 4]) + std::to_string(chartbl[hashbuf[i] & 0xf]);
    Hash = hashout;
    CryptDestroyHash(crypthash);
    CryptReleaseContext(cryptprov, 0);
    return true;
}