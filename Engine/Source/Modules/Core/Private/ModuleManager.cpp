#include "ModuleManager.h"
#include <unordered_map>
#include <mutex>
#include <filesystem>

#ifdef _WIN32
    #include <Windows.h>
    #define LOAD_LIB LoadLibraryA
    #define FREE_LIB FreeLibrary
    #define GET_SYMBOL GetProcAddress
    using ModuleHandle = HMODULE;
#else
    #include <dlfcn.h>
    #define LOAD_LIB(name) dlopen(name, RTLD_LAZY)
    #define FREE_LIB dlclose
    #define GET_SYMBOL dlsym
    using ModuleHandle = void*;
#endif

namespace fs = std::filesystem;

// PImpl реализация
class ModuleManager::Impl {
public:
    struct ModuleData {
        ModuleHandle handle = nullptr;
        IModule* instance = nullptr;
        void (*destroyFunc)(IModule*) = nullptr;
        bool isLoaded = false;
    };
    
    std::unordered_map<std::string, ModuleData> modules;
    mutable std::mutex mutex;
    
    // Найти все модули
    std::vector<std::string> findModuleFiles() {
        std::vector<std::string> result;
        
        try {
            if (!fs::exists(".")) {
                return result;
            }
            
            for (const auto& entry : fs::directory_iterator(".")) {
                if (entry.is_regular_file()) {
                    std::string ext = entry.path().extension().string();
                    #ifdef _WIN32
                        if (ext == ".dll")
                    #else
                        if (ext == ".so")
                    #endif
                    {
                        std::string name = entry.path().stem().string();
                        result.push_back(name);
                    }
                }
            }
        }
        catch (...) {
            // Игнорируем ошибки
        }
        
        return result;
    }
};

// Реализация ModuleManager
ModuleManager& ModuleManager::get() {
    static ModuleManager instance;
    return instance;
}

ModuleManager::ModuleManager() : pImpl(new Impl()) {}
ModuleManager::~ModuleManager() {
    // Выгружаем все модули
    for (auto& [name, data] : pImpl->modules) {
        if (data.instance) {
            data.instance->shutdownModule();
            data.destroyFunc(data.instance);
        }
        if (data.handle) {
            FREE_LIB(data.handle);
        }
    }
    delete pImpl;
}

IModule* ModuleManager::loadModule(const char* moduleName) {
    std::lock_guard<std::mutex> lock(pImpl->mutex);
    std::string name(moduleName);
    
    // Уже загружен?
    auto it = pImpl->modules.find(name);
    if (it != pImpl->modules.end() && it->second.isLoaded) {
        return it->second.instance;
    }
    
    // Формируем путь к DLL/SO
    std::string path = "Modules/" + name;
    #ifdef _WIN32
        path += ".dll";
    #else
        path += ".so";
    #endif
    
    // Проверяем существование
    if (!fs::exists(path)) {
        return nullptr;
    }
    
    // Загружаем библиотеку
    ModuleHandle handle = LOAD_LIB(path.c_str());
    if (!handle) return nullptr;
    
    // Получаем фабричные функции с безопасным приведением типов
#ifdef _WIN32
    FARPROC createFuncPtr = GET_SYMBOL(handle, "createModule");
    FARPROC destroyFuncPtr = GET_SYMBOL(handle, "destroyModule");
    
    // Приводим через void* чтобы избежать предупреждений
    auto createFunc = reinterpret_cast<IModule*(*)()>(
        reinterpret_cast<void*>(createFuncPtr)
    );
    auto destroyFunc = reinterpret_cast<void(*)(IModule*)>(
        reinterpret_cast<void*>(destroyFuncPtr)
    );
#else
    auto createFunc = reinterpret_cast<IModule*(*)()>(
        GET_SYMBOL(handle, "createModule")
    );
    auto destroyFunc = reinterpret_cast<void(*)(IModule*)>(
        GET_SYMBOL(handle, "destroyModule")
    );
#endif
    
    if (!createFunc || !destroyFunc) {
        FREE_LIB(handle);
        return nullptr;
    }
    
    // Создаем экземпляр модуля
    IModule* module = createFunc();
    if (!module) {
        FREE_LIB(handle);
        return nullptr;
    }
    
    // Инициализируем модуль
    module->startupModule();
    
    // Сохраняем данные
    Impl::ModuleData data;
    data.handle = handle;
    data.instance = module;
    data.destroyFunc = destroyFunc;
    data.isLoaded = true;
    
    pImpl->modules[name] = data;
    
    return module;
}

IModule* ModuleManager::getModule(const char* moduleName) {
    std::lock_guard<std::mutex> lock(pImpl->mutex);
    auto it = pImpl->modules.find(moduleName);
    return (it != pImpl->modules.end() && it->second.isLoaded) ? it->second.instance : nullptr;
}

bool ModuleManager::unloadModule(const char* moduleName) {
    std::lock_guard<std::mutex> lock(pImpl->mutex);
    
    auto it = pImpl->modules.find(moduleName);
    if (it == pImpl->modules.end() || !it->second.isLoaded) {
        return false;
    }
    
    // Выгружаем модуль
    it->second.instance->shutdownModule();
    it->second.destroyFunc(it->second.instance);
    FREE_LIB(it->second.handle);
    
    // Удаляем из списка
    pImpl->modules.erase(it);
    
    return true;
}

bool ModuleManager::isModuleLoaded(const char* moduleName) const {
    std::lock_guard<std::mutex> lock(pImpl->mutex);
    auto it = pImpl->modules.find(moduleName);
    return it != pImpl->modules.end() && it->second.isLoaded;
}

void ModuleManager::loadAllModules() {
    // Сначала собираем имена модулей БЕЗ блокировки
    std::vector<std::string> moduleNames;
    {
        std::lock_guard<std::mutex> lock(pImpl->mutex);
        moduleNames = pImpl->findModuleFiles();
    }
    
    // Затем загружаем каждый модуль (у каждого будет своя блокировка)
    for (const auto& name : moduleNames) {
        {
            std::lock_guard<std::mutex> lock(pImpl->mutex);
            if (pImpl->modules.find(name) != pImpl->modules.end()) {
                continue; // Уже загружен
            }
        }
        loadModule(name.c_str());
    }
}

std::vector<IModule*> ModuleManager::getAllModules() const {
    std::lock_guard<std::mutex> lock(pImpl->mutex);
    std::vector<IModule*> result;
    
    for (const auto& [name, data] : pImpl->modules) {
        if (data.isLoaded && data.instance) {
            result.push_back(data.instance);
        }
    }
    
    return result;
}