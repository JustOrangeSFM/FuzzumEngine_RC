#pragma once
#include "Core.h"

#define IMPLEMENT_MODULE(ModuleClass) \
    extern "C" MODULE_API IModule* createModule() { \
        return new ModuleClass(); \
    } \
    extern "C" MODULE_API void destroyModule(IModule* module) { \
        delete static_cast<ModuleClass*>(module); \
    }


#define DECLARE_MODULE_INTERFACE(InterfaceName, ModuleName) \
    class InterfaceName; \
    inline InterfaceName* Get##ModuleName##Module() { \
        return static_cast<InterfaceName*>(ModuleManager::get().getModule(#ModuleName)); \
    }


#define GET_MODULE_INTERFACE(ModuleName, InterfaceName) \
    static_cast<InterfaceName*>(ModuleManager::get().getModule(#ModuleName))