using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class Program
{
    static Dictionary<string, dynamic> loadedModules = new Dictionary<string, dynamic>();
    static string c_vers = "c++20";
    static string bin_path_ninja = "Binaries";
    static string temp_path_ninja = "Temp";
    public class ModuleRulesShim
    {
        public string Name { get; }

        public ModuleRulesShim(string name)
        {
            Name = name;
        }

        public List<string> PublicDependencies { get; set; } = new();
        public List<string> PrivateDependencies { get; set; } = new();
        public List<string> PublicIncludePaths { get; set; } = new();
        public List<string> PrivateIncludePaths { get; set; } = new();
        public List<string> PublicAdditionalLibraries { get; set; } = new();
        public List<string> PrivateAdditionalLibraries { get; set; } = new();
        public List<string> PublicSourceFiles { get; set; } = new();
        public List<string> PrivateSourceFiles { get; set; } = new();
        public List<string> SourceFiles { get => PrivateSourceFiles; set => PrivateSourceFiles = value; }
        public List<string> PublicDefinitions { get; set; } = new();
        public List<string> PrivateDefinitions { get; set; } = new();
        public bool IsPlatformWindows { get; set; }
        public bool IsPlatformLinux { get; set; }
        public bool IsPlatformMac { get; set; }
    }

    static dynamic CreateModule(Type moduleType, dynamic target)
    {
        try
        {
            var constructor = moduleType.GetConstructor(new[] { typeof(object) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { target });
            }

            constructor = moduleType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                return constructor.Invoke(null);
            }
            return Activator.CreateInstance(moduleType)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create module {moduleType.Name}: {ex.Message}");

            try
            {
                return Activator.CreateInstance(moduleType)!;
            }
            catch
            {
                return new ModuleRulesShim(moduleType.Name.Replace("Module", ""));
            }
        }
    }


    static List<string> GetAllDependencies(string moduleName, HashSet<string> visited = null)
    {
        if (visited == null) visited = new HashSet<string>();
        if (visited.Contains(moduleName)) return new List<string>();
        visited.Add(moduleName);

        if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            return new List<string>();

        var allDeps = new List<string>();

        // Публичные зависимости
        foreach (string dep in module.PublicDependencyModuleNames ?? new List<string>())
        {
            if (!visited.Contains(dep))
            {
                allDeps.Add(dep);
                allDeps.AddRange(GetAllDependencies(dep, visited));
            }
        }

        // Приватные зависимости
        foreach (string dep in module.PrivateDependencyModuleNames ?? new List<string>())
        {
            if (!visited.Contains(dep))
            {
                allDeps.Add(dep);
                allDeps.AddRange(GetAllDependencies(dep, visited));
            }
        }

        return allDeps.Distinct().ToList();
    }

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: FuzzumBuildTool.exe <TargetFile> [NinjaOutDir]");
            return 1;
        }

        string targetName, targetFile, ninjaOutDir = ".";

        if (args.Length == 1)
        {
            // Только Target файл
            targetFile = args[0];
            targetName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(targetFile));
        }
        else if (args.Length == 2)
        {
            // Target файл + выходная папка
            targetFile = args[0];
            ninjaOutDir = args[1];
            targetName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(targetFile));
        }
        else
        {
            Console.WriteLine("Usage: FuzzumBuildTool.exe <TargetFile> [NinjaOutDir]");
            return 1;
        }

        targetFile = Path.GetFullPath(targetFile);
        ninjaOutDir = Path.GetFullPath(ninjaOutDir);

        Console.WriteLine($"Target: {targetName}");
        Console.WriteLine($"Target: {targetFile}");
        Console.WriteLine($"Ninja:  {ninjaOutDir}/{targetName}.ninja");

        if (!File.Exists(targetFile))
        {
            Console.WriteLine($"Missing target file: {targetFile}");
            return 1;
        }

        
        // string engineSourceDir = Path.GetDirectoryName(buildFile)!;

        string engineSourceDir = Path.GetDirectoryName(targetFile)!;
        Console.WriteLine($"\nSearching for .Build.cs files in: {engineSourceDir}");

        var allBuildFiles = new List<string>();

        //ищем в Modules
        string modulesDir = Path.Combine(engineSourceDir, "Modules");
        if (Directory.Exists(modulesDir))
        {
            var moduleFiles = Directory.GetFiles(modulesDir, "*.Build.cs", SearchOption.AllDirectories);
            allBuildFiles.AddRange(moduleFiles);
            Console.WriteLine($"Found {moduleFiles.Length} in Modules/");
        }

        //ищем в Source
        var sourceFiles = Directory.GetFiles(engineSourceDir, "*.Build.cs", SearchOption.TopDirectoryOnly);
        allBuildFiles.AddRange(sourceFiles);
        Console.WriteLine($"Found {sourceFiles.Length} in Source/");

        allBuildFiles = allBuildFiles.Select(f => Path.GetFullPath(f)).Distinct().ToList();

        Console.WriteLine($"\nTotal found {allBuildFiles.Count} .Build.cs files:");
        foreach (var file in allBuildFiles)
        {
            var relativePath = Path.GetRelativePath(engineSourceDir, file);
            Console.WriteLine($"  - {relativePath}");
        }

        string execPath = Assembly.GetEntryAssembly().Location;
        string execDir = Path.GetDirectoryName(execPath);
        string sourceDir = Path.GetFullPath(Path.Combine(execDir, "../../../Source/Programs/FuzzumBuildTool/"));
        string moduleRulesSource = File.ReadAllText(Path.Combine(sourceDir, "ModuleRules.cs"));
        string targetRulesSource = File.ReadAllText(Path.Combine(sourceDir, "TargetRules.cs"));

        var allCsFiles = new List<string>
        {
            moduleRulesSource,
            targetRulesSource,
            File.ReadAllText(targetFile)
        };

        foreach (var file in allBuildFiles)
        {
            allCsFiles.Add(File.ReadAllText(file));
        }

        var syntaxTrees = allCsFiles.Select(content => CSharpSyntaxTree.ParseText(content)).ToArray();

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            $"{targetName}_Rules",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            Console.WriteLine("Compilation failed!");
            foreach (var diag in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Error))
                Console.WriteLine(diag.ToString());
            return 1;
        }

        ms.Seek(0, SeekOrigin.Begin);
        var rulesAssembly = Assembly.Load(ms.ToArray());

        Console.WriteLine($"\nLoaded assembly with {rulesAssembly.GetTypes().Length} types:");

        var targetType = rulesAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == $"{targetName}Target" && !t.IsAbstract);

        if (targetType == null)
        {
            Console.WriteLine($"Could not find {targetName}Target class");
            return 1;
        }

        var targetRules = (dynamic)Activator.CreateInstance(targetType)!;
        Console.WriteLine($"Found target: {targetRules.Name} ({targetRules.Type})");

        //загрузка всех модулей
        var allModuleTypes = rulesAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Module") && !t.IsAbstract)
            .ToList();

        Console.WriteLine($"\nFound {allModuleTypes.Count} module types:");
        foreach (var type in allModuleTypes)
        {
            Console.WriteLine($"  - {type.Name}");
        }

        foreach (var moduleType in allModuleTypes)
        {
            dynamic module = CreateModule(moduleType, targetRules);

            if (!module.IsPlatformWindows && !module.IsPlatformLinux && !module.IsPlatformMac)
            {
                module.IsPlatformWindows = targetRules.IsPlatformWindows;
                module.IsPlatformLinux = targetRules.IsPlatformLinux;
                module.IsPlatformMac = targetRules.IsPlatformMac;
            }

            loadedModules[module.Name] = module;
        }

        Console.WriteLine($"\nProcessing modules for target {targetName}:");
        Console.WriteLine($"Platform: {(targetRules.IsPlatformWindows ? "Windows" : targetRules.IsPlatformLinux ? "Linux" : "Mac")}");
        Console.WriteLine($"Type: {targetRules.Type}");


        var allModulesToProcess = new HashSet<string>();
        foreach (string moduleName in targetRules.Modules)
        {
            allModulesToProcess.Add(moduleName);

            // получение зависимостей модули
            var deps = GetAllDependencies(moduleName);
            foreach (var dep in deps)
            {
                allModulesToProcess.Add(dep);
            }
        }

        Console.WriteLine($"\nAll modules to process ({allModulesToProcess.Count}):");
        foreach (var moduleName in allModulesToProcess.OrderBy(m => m))
        {
            Console.WriteLine($"  - {moduleName}");
        }

        foreach (string moduleName in allModulesToProcess)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            {
                Console.WriteLine($"  ERROR: Module '{moduleName}' not found!");
                continue;
            }

            Console.WriteLine($"\n  Module: {module.Name}");

            // Все исходники
            var allSources = new List<string>();
            allSources.AddRange(module.PublicSourceFiles ?? new List<string>());
            allSources.AddRange(module.PrivateSourceFiles ?? new List<string>());
            if (allSources.Count == 0 && module.SourceFiles != null)
                allSources.AddRange(module.SourceFiles);

            if (allSources.Count > 0)
            {
                Console.WriteLine($"    Sources ({allSources.Count}):");
                foreach (var src in allSources)
                    Console.WriteLine($"      - {src}");
            }

            // Зависимости
            var deps = new List<string>();
            deps.AddRange(module.PublicDependencies ?? new List<string>());
            deps.AddRange(module.PrivateDependencies ?? new List<string>());
            if (deps.Count > 0)
                Console.WriteLine($"    Dependencies: {string.Join(", ", deps)}");

            // Include paths
            var includes = new List<string>();
            includes.AddRange(module.PublicIncludePaths ?? new List<string>());
            includes.AddRange(module.PrivateIncludePaths ?? new List<string>());
            if (includes.Count > 0)
                Console.WriteLine($"    Include paths: {string.Join(", ", includes)}");

            // Библиотеки
            var libs = new List<string>();
            libs.AddRange(module.PublicAdditionalLibraries ?? new List<string>());
            libs.AddRange(module.PrivateAdditionalLibraries ?? new List<string>());
            if (libs.Count > 0)
                Console.WriteLine($"    Libraries: {string.Join(", ", libs)}");

            // Определения
            var defines = new List<string>();
            defines.AddRange(module.PublicDefinitions ?? new List<string>());
            defines.AddRange(module.PrivateDefinitions ?? new List<string>());
            if (defines.Count > 0)
                Console.WriteLine($"    Defines: {string.Join(", ", defines)}");
        }

        temp_path_ninja = temp_path_ninja + "/" + targetName;

        //генерация ninja.build
        GenerateNinjaFile(targetRules, ninjaOutDir, targetName);
        return 0;
    }

    static void GenerateNinjaFile(dynamic target, string ninjaOutDir, string targetName)
    {
        string ninjaPath = Path.Combine(ninjaOutDir, $"{targetName}.ninja");

        Directory.CreateDirectory(Path.Combine(ninjaOutDir, bin_path_ninja));
        Directory.CreateDirectory(Path.Combine(ninjaOutDir, temp_path_ninja));

        using var writer = new StreamWriter(ninjaPath);

        // Сетап компилятора
        writer.WriteLine("# Generated by FuzzumBuildTool");
        writer.WriteLine($"# Target: {targetName}");
        writer.WriteLine($"# Platform: {(target.IsPlatformWindows ? "Windows" : target.IsPlatformLinux ? "Linux" : "Mac")}");
        writer.WriteLine($"# Type: {target.Type}");
        writer.WriteLine();

        if (target.IsPlatformWindows)
        {
            writer.WriteLine("cxx = cl.exe");
            writer.WriteLine("link = link.exe");
            writer.WriteLine("cflags = /std:" + c_vers + " /EHsc /W4 /nologo");
            writer.WriteLine("ldflags = /nologo");

            // Определяем правило компиляции для Windows
            writer.WriteLine("rule cxx");
            writer.WriteLine("  command = $cxx $cflags $in /c /Fo$out");
            writer.WriteLine("  description = Compiling $in");

            // Определяем правило линковки для Windows
            writer.WriteLine("rule link");
            writer.WriteLine("  command = $link $ldflags $in $libs /OUT:$out");
            writer.WriteLine("  description = Linking $out");
        }
        else
        {
            writer.WriteLine("cxx = clang++");
            writer.WriteLine("link = clang++");
            writer.WriteLine("cflags = -std:" + c_vers + " -Wall -Wextra");
            writer.WriteLine("ldflags = ");

            // Определяем правило компиляции для Linux/Mac
            writer.WriteLine("rule cxx");
            writer.WriteLine("  command = $cxx $cflags -c $in -o $out");
            writer.WriteLine("  description = Compiling $in");

            // Определяем правило линковки для Linux/Mac
            writer.WriteLine("rule link");
            writer.WriteLine("  command = $link $ldflags $in $libs -o $out");
            writer.WriteLine("  description = Linking $out");
        }

        // Global includes from target
        foreach (var include in target.GlobalIncludePaths ?? new List<string>())
        {
            if (target.IsPlatformWindows)
                writer.WriteLine($"cflags = $cflags /I\"{include}\"");
            else
                writer.WriteLine($"cflags = $cflags -I\"{include}\"");
        }

        writer.WriteLine();

        List<string> allObjectFiles = new List<string>();
        Dictionary<string, List<string>> moduleIncludes = new Dictionary<string, List<string>>();

        // Собираем все модули для Ninja (включая зависимости)
        var allModulesForNinja = new HashSet<string>();
        foreach (string moduleName in target.Modules)
        {
            allModulesForNinja.Add(moduleName);
            var deps = GetAllDependencies(moduleName);
            foreach (var dep in deps)
            {
                allModulesForNinja.Add(dep);
            }
        }

        Console.WriteLine($"\nModules for Ninja generation ({allModulesForNinja.Count}):");
        foreach (var moduleName in allModulesForNinja.OrderBy(m => m))
        {
            Console.WriteLine($"  - {moduleName}");
        }

        // Собираем все include paths для каждого модуля
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            {
                Console.WriteLine($"  WARNING: Skipping module '{moduleName}' in ninja generation");
                continue;
            }

            var includes = new List<string>();
            includes.AddRange(module.PublicIncludePaths ?? new List<string>());
            includes.AddRange(module.PrivateIncludePaths ?? new List<string>());
            moduleIncludes[moduleName] = includes;
        }

        // Собираем все исходники и создаем правила компиляции
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
                continue;

            var allSources = new List<string>();
            allSources.AddRange(module.PublicSourceFiles ?? new List<string>());
            allSources.AddRange(module.PrivateSourceFiles ?? new List<string>());
            if (allSources.Count == 0 && module.SourceFiles != null)
                allSources.AddRange(module.SourceFiles);

            foreach (var sourceFile in allSources)
            {
                if (string.IsNullOrEmpty(sourceFile) || !sourceFile.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Объектный файл
                var safeName = sourceFile.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var objName = $"{Path.GetFileNameWithoutExtension(safeName)}.obj";
                var objPath = $"{temp_path_ninja}/{objName}";
                allObjectFiles.Add(objPath);

                // Собираем все include пути для этого файла
                var allIncludes = new List<string>();

                // Include paths этого модуля
                if (moduleIncludes.TryGetValue(moduleName, out var incs))
                {
                    foreach (var inc in incs)
                    {
                        allIncludes.Add(inc);
                    }
                }

                // Include paths из зависимостей
                var allDeps = new List<string>();
                allDeps.AddRange(module.PublicDependencies ?? new List<string>());
                allDeps.AddRange(module.PrivateDependencies ?? new List<string>());

                foreach (var dep in allDeps)
                {
                    if (moduleIncludes.TryGetValue(dep, out var depIncs))
                    {
                        foreach (var inc in depIncs)
                        {
                            allIncludes.Add(inc);
                        }
                    }
                }

                // Правило компиляции
                writer.WriteLine($"build {objPath}: cxx {sourceFile}");

                // Если есть include пути, добавляем их как переменную
                if (allIncludes.Count > 0)
                {
                    writer.Write("  includes =");
                    foreach (var inc in allIncludes.Distinct())
                    {
                        if (target.IsPlatformWindows)
                            writer.Write($" /I\"{inc}\"");
                        else
                            writer.Write($" -I\"{inc}\"");
                    }
                    writer.WriteLine();
                }
            }
        }

        writer.WriteLine();

        writer.Write($"build " + bin_path_ninja + "/{targetName}.exe: link ");
        writer.WriteLine(string.Join(" ", allObjectFiles));

        // Собираем все библиотеки
        var allLibs = new List<string>();
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
                continue;

            var libs = new List<string>();
            libs.AddRange(module.PublicAdditionalLibraries ?? new List<string>());
            libs.AddRange(module.PrivateAdditionalLibraries ?? new List<string>());
            allLibs.AddRange(libs);
        }

        // Глобальные библиотеки из таргета
        allLibs.AddRange(target.GlobalLibraries ?? new List<string>());

        if (allLibs.Count > 0)
        {
            writer.Write("    libs = ");
            if (target.IsPlatformWindows)
            {
                writer.WriteLine(string.Join(" ", allLibs.Select(l => $"\"{l}\"")));
            }
            else
            {
                writer.WriteLine(string.Join(" ", allLibs.Select(l => $"-l{l}")));
            }
        }

        // Добавляем макросы из таргета
        if (target.GlobalDefinitions != null && target.GlobalDefinitions.Count > 0)
        {
            writer.Write("    defines = ");
            foreach (var define in target.GlobalDefinitions)
            {
                if (target.IsPlatformWindows)
                    writer.Write($" /D\"{define}\"");
                else
                    writer.Write($" -D{define}");
            }
            writer.WriteLine();
        }

        // Правило по умолчанию
        writer.WriteLine($"\ndefault " + bin_path_ninja + "/{targetName}.exe");

        Console.WriteLine($"\n✅ Generated: {ninjaPath}");
        Console.WriteLine($"   Object files: {allObjectFiles.Count}");
        Console.WriteLine($"   Libraries: {allLibs.Count}");
        Console.WriteLine($"   Use: ninja -f \"{Path.GetFileName(ninjaPath)}\"");
    }
}