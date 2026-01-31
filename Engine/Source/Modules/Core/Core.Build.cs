using System.Collections.Generic;

namespace FuzzumBuildTool
{
    public class CoreModule : ModuleRules
    {
        public CoreModule() : base("Core")
        {
            ModuleCategory = ModuleCategory.Editor;
            BuildType = ModuleBuildType.DynamicLibrary;

            // Platform-specific настройки
            if (IsPlatformWindows)
            {
                PrivateDefinitions.Add("PLATFORM_WINDOWS=1");
                PrivateDefinitions.Add("_CRT_SECURE_NO_WARNINGS");
                
                PublicSystemLibraries.Add("user32.lib");
                PublicSystemLibraries.Add("kernel32.lib");
            }
            else if (IsPlatformLinux)
            {
                PrivateDefinitions.Add("PLATFORM_LINUX=1");
                PublicSystemLibraries.Add("dl");
                PublicSystemLibraries.Add("pthread");
            }
            
            // Исходники
            PublicSourceFiles = new List<string>
            {
                "Engine/Source/Modules/Core/Public/CoreTypes.h"
            };
            
            PrivateSourceFiles = new List<string>
            {
                "Engine/Source/Modules/Core/Private/Logging.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {};
            
            PrivateIncludePaths = new List<string>
            {};
            
            // Дефайны
            PublicDefinitions.Add("CORE_API=__declspec(dllexport)");
        }
    }
}