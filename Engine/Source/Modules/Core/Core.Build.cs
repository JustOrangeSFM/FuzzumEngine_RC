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
                
            };
            
            PrivateSourceFiles = new List<string>
            {
                "Private/**/*.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {"Public"};
            
            PrivateIncludePaths = new List<string>
            {"Private"};
            
            // Дефайны
            PublicDefinitions.Add("CORE_EXPORTS=1");
        }
    }
}