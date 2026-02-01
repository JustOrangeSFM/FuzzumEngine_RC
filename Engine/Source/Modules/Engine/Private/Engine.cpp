#include <iostream>
#include <memory>
#include <chrono>
#include <thread>
#include "Core/Public/ModuleManager.h"
#include "CoreUObject/Public/CoreUObject.h"

using namespace std;

void EngineMain()
{
    cout << "========================================" << endl;
    cout << "   Fuzzum Engine Starting..." << endl;
    cout << "   Version: 0.1.0" << endl;
    cout << "========================================" << endl;
    
    cout << "[Engine] Initializing Engine..." << endl;
    
    // Получаем менеджер модулей
    auto& moduleManager = ModuleManager::get();
    
    // Загружаем все модули
    cout << "\n[Engine] Loading modules..." << endl;
    moduleManager.loadAllModules();
    
    // Пробуем загрузить CoreUObject вручную
    cout << "\n[Engine] Attempting to load CoreUObject module..." << endl;
    IModule* coreUObjectModule = moduleManager.loadModule("CoreUObject");
    
    if (coreUObjectModule)
    {
        cout << "[Engine] CoreUObject module loaded successfully!" << endl;
        
        // Получаем модуль CoreUObject с кастомным интерфейсом
        if (CoreUObjectModule* coreUObject = static_cast<CoreUObjectModule*>(coreUObjectModule))
        {
            cout << "[Engine] Module name: " << coreUObject->getName() << endl;
        }
        
        // Проверяем статус модуля
        if (moduleManager.isModuleLoaded("CoreUObject"))
        {
            cout << "[Engine] CoreUObject module is confirmed loaded." << endl;
        }
    }
    else
    {
        cout << "[Engine] Failed to load CoreUObject module!" << endl;
        cout << "[Engine] Make sure CoreUObject.dll is in Engine/Modules/" << endl;
    }
    
    // Получаем список всех загруженных модулей
    auto allModules = moduleManager.getAllModules();
    cout << "\n[Engine] Total loaded modules: " << allModules.size() << endl;
    
    cout << "\n[Engine] Entering main loop..." << endl;
    cout << "Press Enter to exit..." << endl;
    
    // Простая имитация игрового цикла
    for (int frame = 0; frame < 5; frame++)
    {
        cout << "\n[Engine] Frame " << frame + 1 << endl;
        cout << "[Engine] Processing frame " << frame << endl;
        this_thread::sleep_for(chrono::milliseconds(1000));
    }
    
    // Выгрузка модулей
    cout << "\n[Engine] Shutting down..." << endl;
    
    if (moduleManager.unloadModule("CoreUObject"))
    {
        cout << "[Engine] CoreUObject module unloaded." << endl;
    }
    
    cout << "\n[Engine] Goodbye!" << endl;
}

int main()
{
    try
    {
        EngineMain();
    }
    catch (const std::exception& e)
    {
        cerr << "[Engine] Exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[Engine] Unknown exception occurred!" << endl;
    }
    
    cout << "\nPress Enter to exit...";
    cin.get();
    return 0;
}
