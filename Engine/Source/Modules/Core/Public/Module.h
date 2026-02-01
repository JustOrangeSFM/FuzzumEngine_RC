#pragma once
#include "Core.h"
#include "ModuleMacros.h"

class CORE_API IModule {
public:
    virtual ~IModule() = default;
    
    
    virtual void startupModule() = 0;     
    virtual void shutdownModule() = 0;    
    virtual const char* getName() const = 0;
    
  
    virtual bool supportsDynamicReloading() const { return false; }
    virtual void preUnloadCallback() {}   
    virtual void postLoadCallback() {}     
};
