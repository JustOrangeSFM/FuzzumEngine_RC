using System.Collections.Generic;

namespace FuzzumBuildTool
{
    public class CoreUObjectModule : ModuleRules
    {
        public CoreUObjectModule() : base("CoreUObject")
        {
            BuildType = ModuleBuildType.DynamicLibrary;
            ModuleCategory = ModuleCategory.Editor;

            // Зависит от Core
            PublicDependencyModuleNames.Add("Core");
            
            // Исходники
            PrivateSourceFiles = new List<string>
            {
                "Engine/Source/Modules/CoreUObject/Private/UObject.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {
                "Engine/Source/Modules/CoreUObject/Public"
            };
            
            PrivateIncludePaths = new List<string>
            {
                "Engine/Source/Modules/CoreUObject/Private"
            };
            
            // Дефайны
            PublicDefinitions.Add("COREOBJECT_API=__declspec(dllexport)");
            PrivateDefinitions.Add("MAX_UOBJECTS=1000000");
        }
    }
}