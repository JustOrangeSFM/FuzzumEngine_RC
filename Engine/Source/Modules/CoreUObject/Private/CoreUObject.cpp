#include "CoreUObject.h"
#include <iostream>
#include <vector>
#include <memory>
#include <chrono>
#include <thread>

using namespace std;

class AActor : public UObject
{
public:
    AActor(const char* InName = "Actor")
    {
        Name = InName;
    }
    
    void BeginPlay() override
    {
        COUT_LOG("Actor", string(Name) + " BeginPlay");
        cout << "Actor " << Name << " is now playing!" << endl;
    }
    
    void Tick(float DeltaTime) override
    {
        static float TotalTime = 0.0f;
        TotalTime += DeltaTime;
        
        if (static_cast<int>(TotalTime) > static_cast<int>(TotalTime - DeltaTime))
        {
            cout << "[Actor] " << Name << " tick at time: " << TotalTime << endl;
        }
    }
    
    void Destroy() override
    {
        COUT_LOG("Actor", string(Name) + " destroying...");
        cout << "Destroying " << Name << "..." << endl;
    }
};

// Реализация модуля
CoreUObjectModule::CoreUObjectModule()
{
    COUT_LOG("CoreUObject", "Module constructor");
}

CoreUObjectModule::~CoreUObjectModule()
{
    COUT_LOG("CoreUObject", "Module destructor");
}

void CoreUObjectModule::startupModule()
{
    COUT_LOG("CoreUObject", "Starting module...");
    cout << "========================================" << endl;
    cout << "  CoreUObject Module Initialized!" << endl;
    cout << "  Version: 1.0.0" << endl;
    cout << "  Features:" << endl;
    cout << "    - UObject base class" << endl;
    cout << "    - AActor implementation" << endl;
    cout << "    - RTTI with UClass" << endl;
    cout << "========================================" << endl;

    TestUObjectSystem();
}

void CoreUObjectModule::shutdownModule()
{
    COUT_LOG("CoreUObject", "Shutting down module...");
    cout << "CoreUObject module shutdown complete." << endl;
}

void CoreUObjectModule::TestUObjectSystem()
{
    COUT_LOG("CoreUObject", "Testing UObject system...");

    vector<unique_ptr<AActor>> actors;
    
    actors.push_back(make_unique<AActor>("Player"));
    actors.push_back(make_unique<AActor>("Enemy"));
    actors.push_back(make_unique<AActor>("NPC_Guard"));
    
    // Демонстрация работы с акторами
    for (auto& actor : actors)
    {
        actor->BeginPlay();

        for (int i = 0; i < 3; i++)
        {
            actor->Tick(0.016f); // 60 FPS
        }
    }

    for (auto& actor : actors)
    {
        actor->Destroy();
    }
    
    COUT_LOG("CoreUObject", "UObject system test completed!");
}

IMPLEMENT_MODULE(CoreUObjectModule)