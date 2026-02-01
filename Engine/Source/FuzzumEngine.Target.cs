using System.Collections.Generic;

namespace FuzzumBuildTool
{
    public class FuzzumEngineTarget : TargetRules
    {
        public FuzzumEngineTarget()
            : base("FuzzumEngine")
        {
            Type = TargetType.Program;
            Platform = TargetPlatform.Win64;
            
            // Основные модули движка
            Modules = new List<string>
            {
                "Engine"
            };
            
            // Дополнительные для полноты
            GlobalDefinitions = new List<string>
            {
                "ENGINE_BUILD=1",
                "WITH_LOGGING=1",
                "DEBUG_ENABLED=1"
            };
            
            GlobalIncludePaths = new List<string>
            {
                "Modules"
            };
        }
    }
}