using System.Collections.Generic;

namespace FuzzumBuildTool
{
    public class EngineModule : ModuleRules
    {
        public EngineModule() : base("Engine")
        {
            ModuleCategory = ModuleCategory.Editor;
            BuildType = ModuleBuildType.Executable;

            // Зависимости Engine
            PublicDependencyModuleNames.Add("Core");
            PublicDependencyModuleNames.Add("CoreUObject");
            
            // Platform-specific библиотеки
            if (IsPlatformWindows)
            {
                PublicAdditionalLibraries.Add("d3d11.lib");
                PublicAdditionalLibraries.Add("dxgi.lib");
                PublicAdditionalLibraries.Add("xinput.lib");
                PublicSystemLibraries.Add("user32.lib");
                PublicSystemLibraries.Add("kernel32.lib");
            }
            
            // Исходники Engine
            PrivateSourceFiles = new List<string>
            {
                "Engine/Source/Modules/Engine/Private/Engine.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {
                "Engine/Source/Modules/Engine/Public"
            };
            
            PrivateIncludePaths = new List<string>
            {
                "Engine/Source/Modules/Engine/Private"
            };
            
            // Дефайны
            PublicDefinitions.Add("ENGINE_API=__declspec(dllexport)");
            PrivateDefinitions.Add("MAX_ENTITIES=65536");
        }
    }
}