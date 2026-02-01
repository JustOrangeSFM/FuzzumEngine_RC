#pragma once

// Макросы экспорта
#ifdef _WIN32
    #ifdef CORE_EXPORTS
        #define CORE_API __declspec(dllexport)
    #else
        #define CORE_API __declspec(dllimport)
    #endif
    #define MODULE_API __declspec(dllexport)
#else
    #define CORE_API __attribute__((visibility("default")))
    #define MODULE_API __attribute__((visibility("default")))
#endif

// Базовые макросы
#define STRINGIFY(x) #x
#define TOSTRING(x) STRINGIFY(x)