#pragma once
#include "Module.h"
#include <vector>

class CORE_API ModuleManager {
public:
    static ModuleManager& get();
    
    IModule* loadModule(const char* moduleName);
    IModule* getModule(const char* moduleName);
    bool unloadModule(const char* moduleName);
    bool isModuleLoaded(const char* moduleName) const;
    
    void loadAllModules();
    
    std::vector<IModule*> getAllModules() const;
    
private:
    ModuleManager();
    ~ModuleManager();
    
    // PImpl паттерн
    class Impl;
    Impl* pImpl;
};