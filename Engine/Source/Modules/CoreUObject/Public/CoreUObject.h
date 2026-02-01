#pragma once

#include <cstdint>
#include <iostream>
#include "Core/Public/Core.h"
#include "Core/Public/Module.h"
#include "Core/Public/ModuleMacros.h"


#define COUT_LOG(Category, Message) \
    std::cout << "[" << Category << "] " << Message << std::endl

// Базовый класс
class UObject
{
public:
    virtual ~UObject() = default;
    
    virtual void BeginPlay() {}
    virtual void Tick(float DeltaTime) { (void)DeltaTime; } // Используем параметр
    virtual void Destroy() {}
    
    const char* GetName() const { return Name; }
    uint32_t GetID() const { return ID; }
    
protected:
    const char* Name = "UObject";
    uint32_t ID = 0;
};


class UClass
{
public:
    const char* Name;
    UClass* SuperClass;
    
    UClass(const char* InName, UClass* InSuperClass = nullptr)
        : Name(InName), SuperClass(InSuperClass) {}
    
    bool IsChildOf(const UClass* Other) const
    {
        const UClass* Current = this;
        while (Current)
        {
            if (Current == Other) return true;
            Current = Current->SuperClass;
        }
        return false;
    }
};


class COREOBJECT_API CoreUObjectModule : public IModule
{
public:
    CoreUObjectModule();
    virtual ~CoreUObjectModule() override;
    
    virtual void startupModule() override;
    virtual void shutdownModule() override;
    virtual const char* getName() const override { return "CoreUObject"; }
    virtual bool supportsDynamicReloading() const override { return true; }

    void TestUObjectSystem();
    
private:
    uint32_t NextObjectID = 1;
};

extern "C" COREOBJECT_API IModule* createModule();
extern "C" COREOBJECT_API void destroyModule(IModule* module);