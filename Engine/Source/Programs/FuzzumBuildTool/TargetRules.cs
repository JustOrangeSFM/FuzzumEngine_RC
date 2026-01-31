using System.Collections.Generic;

namespace FuzzumBuildTool
{
    public enum TargetType { Game, Editor, Program }
    public enum TargetPlatform { Win64, Linux, Mac }

    public abstract class TargetRules
    {

        //база
        public string Name { get; }
        public TargetType Type { get; set; }
        public TargetPlatform Platform { get; set; }
        public List<string> Modules { get; set; } = new();

        
        // глобал
        public List<string> GlobalDefinitions { get; set; } = new();
        public List<string> GlobalIncludePaths { get; set; } = new();
        public List<string> GlobalLibraries { get; set; } = new();

        // Для удобства
        public bool IsEditor => Type == TargetType.Editor;
        public bool IsGame => Type == TargetType.Game;

        protected TargetRules(string name) => Name = name;

        // Платформенные проверки
        public bool IsPlatformWindows => Platform == TargetPlatform.Win64;
        public bool IsPlatformLinux => Platform == TargetPlatform.Linux;
        public bool IsPlatformMac => Platform == TargetPlatform.Mac;
    }
}