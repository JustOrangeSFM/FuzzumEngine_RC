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
                PublicSystemLibraries.Add("ole32.lib");
                PublicSystemLibraries.Add("oleaut32.lib");
                PublicSystemLibraries.Add("uuid.lib");
                PublicSystemLibraries.Add("winmm.lib");
                PublicSystemLibraries.Add("advapi32.lib");
            }
            
            // Исходники Engine
            PrivateSourceFiles = new List<string>
            {
                "Private/**.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {
                "Public"
            };
            
            PrivateIncludePaths = new List<string>
            {
                
            };
            
            // Дефайны
            PublicDefinitions.Add("ENGINE_API=__declspec(dllexport)");
            PrivateDefinitions.Add("MAX_ENTITIES=65536");

            if (IsPlatformWindows)
            {
                PrivateAdditionalLibraries.Add("Binaries/Core.lib");
                PrivateAdditionalLibraries.Add("Binaries/CoreUObject.lib");
            }
        }
    }
}