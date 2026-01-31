#include <iostream>
#ifdef _WIN32
    #define CORE_API __declspec(dllexport)
#else
    #define CORE_API
#endif

extern "C"{
CORE_API void Log(const char* message)
{
    std::cout << "[LOG] " << message << std::endl;
}
}