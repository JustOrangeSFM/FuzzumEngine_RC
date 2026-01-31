using System.Collections.Generic;
using System.Reflection;

namespace FuzzumBuildTool
{
    //типы модулей
    public enum ModuleType { Runtime, Editor }

    public abstract class ModuleRules
    {
        public string Name { get; }
        public ModuleType Type { get; set; } = ModuleType.Runtime;

        // зависимости
        public List<string> PublicDependencies { get; set; } = new();
        public List<string> PrivateDependencies { get; set; } = new();
        public List<string> PublicIncludePaths { get; set; } = new();
        public List<string> PrivateIncludePaths { get; set; } = new();

        // бибилотеки
        public List<string> PublicAdditionalLibraries { get; set; } = new();
        public List<string> PrivateAdditionalLibraries { get; set; } = new();

        // исходники
        public List<string> PublicSourceFiles { get; set; } = new();
        public List<string> PrivateSourceFiles { get; set; } = new();

        // макросы
        public List<string> PublicDefinitions { get; set; } = new();
        public List<string> PrivateDefinitions { get; set; } = new();

        // обратная совместимость
        public List<string> SourceFiles
        {
            get => PrivateSourceFiles;
            set => PrivateSourceFiles = value;
        }

        public List<string> PublicDependencyModuleNames
        {
            get => PublicDependencies;
            set => PublicDependencies = value;
        }

        public List<string> PrivateDependencyModuleNames
        {
            get => PrivateDependencies;
            set => PrivateDependencies = value;
        }

        public List<string> DynamicallyLoadedModuleNames { get; set; } = new();
        public List<string> PublicIncludePathModuleNames { get; set; } = new();
        public List<string> PrivateIncludePathModuleNames { get; set; } = new();
        public List<string> PublicSystemLibraries { get; set; } = new();
        public List<string> PublicFrameworks { get; set; } = new();
        public List<string> PublicDelayLoadDLLs { get; set; } = new();

        public object Target { get; set; }

        protected ModuleRules(object target = null)
        {
            if (target != null)
            {
                Target = target;
                Name = GetType().Name.Replace("Module", "");

                // Устанавливаем платформу из таргета через reflection
                try
                {
                    var targetType = target.GetType();
                    var isPlatformWindowsProp = targetType.GetProperty("IsPlatformWindows");
                    var isPlatformLinuxProp = targetType.GetProperty("IsPlatformLinux");
                    var isPlatformMacProp = targetType.GetProperty("IsPlatformMac");

                    if (isPlatformWindowsProp != null)
                        IsPlatformWindows = (bool)isPlatformWindowsProp.GetValue(target);
                    if (isPlatformLinuxProp != null)
                        IsPlatformLinux = (bool)isPlatformLinuxProp.GetValue(target);
                    if (isPlatformMacProp != null)
                        IsPlatformMac = (bool)isPlatformMacProp.GetValue(target);
                }
                catch
                {
                    
                }
            }
            else
            {
                Name = GetType().Name.Replace("Module", "");
            }
        }

        protected ModuleRules(string name) => Name = name;

        // Платформенные проверки
        public bool IsPlatformWindows { get; set; }
        public bool IsPlatformLinux { get; set; }
        public bool IsPlatformMac { get; set; }
        public bool IsPlatform(string platform)
        {
            return platform.ToLower() switch
            {
                "win64" or "windows" => IsPlatformWindows,
                "linux" => IsPlatformLinux,
                "mac" => IsPlatformMac,
                _ => false
            };
        }

        //Едитор и рантайм
        public bool IsEditor => Type == ModuleType.Editor;
        public bool IsRuntime => Type == ModuleType.Runtime;
    }
}