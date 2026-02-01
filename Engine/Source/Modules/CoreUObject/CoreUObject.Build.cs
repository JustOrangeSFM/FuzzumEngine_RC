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
            
            // ТОЛЬКО UObject
            PrivateSourceFiles = new List<string>
            {
                "Private/**/*.cpp"
            };
            
            // Include пути
            PublicIncludePaths = new List<string>
            {
                "Public"
            };

            PrivateIncludePaths = new List<string>
            {
                "Private"
            };
            
            // Дефайны
            PublicDefinitions.Add("COREOBJECT_API=__declspec(dllexport)");
            PrivateDefinitions.Add("MAX_UOBJECTS=1000000");
        }
    }
}