using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FuzzumBuildTool;
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
            dynamic module;

            if (constructor != null)
            {
                module = constructor.Invoke(new object[] { target });
            }
            else
            {
                constructor = moduleType.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                {
                    module = constructor.Invoke(null);
                }
                else
                {
                    module = Activator.CreateInstance(moduleType)!;
                }
            }

            // Устанавливаем BuildType по умолчанию, если не задан
            try
            {
                var buildTypeProp = module.GetType().GetProperty("BuildType");
                if (buildTypeProp != null && buildTypeProp.GetValue(module) == null)
                {
                    // Устанавливаем значение по умолчанию
                    if (module.Name == "Engine" && target.Type == TargetType.Program)
                    {
                        buildTypeProp.SetValue(module, ModuleBuildType.Executable);
                    }
                    else
                    {
                        buildTypeProp.SetValue(module, ModuleBuildType.DynamicLibrary);
                    }
                }
            }
            catch
            { }

            return module;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create module {moduleType.Name}: {ex.Message}");
            return new ModuleRulesShim(moduleType.Name.Replace("Module", ""));
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
            deps.AddRange(module.PublicDependencyModuleNames ?? new List<string>());
            deps.AddRange(module.PrivateDependencyModuleNames ?? new List<string>());
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

        bool isWindows = target.IsPlatformWindows;
        bool isLinux = target.IsPlatformLinux;
        bool isMac = target.IsPlatformMac;
        string platform = isWindows ? "windows" : isLinux ? "linux" : "macos";
        string arch = "x86_64";

        writer.WriteLine("# Generated by FuzzumBuildTool");
        writer.WriteLine($"# Target: {targetName}");
        writer.WriteLine($"# Platform: {platform}");
        writer.WriteLine($"# Arch: {arch}");
        writer.WriteLine($"# Type: {target.Type}");
        writer.WriteLine();

        writer.WriteLine("cxx = clang++");
        writer.WriteLine("link = clang++");
        writer.WriteLine("ar = ar");

        // Базовые флаги
        writer.Write("cflags_base = -std=" + c_vers + " -Wall -Wextra -Werror");
        if (!isWindows) writer.Write(" -fPIC");
        writer.WriteLine();

        // Платформенные флаги
        string platformFlags = isWindows ?
            "-DWIN32 -D_WINDOWS -DUNICODE -D_UNICODE -m64 -fms-extensions -fms-compatibility-version=19.29" :
            isLinux ? "-D_LINUX -D__LINUX__ -D_GNU_SOURCE" :
            "-D__APPLE__ -D__MACH__ -mmacosx-version-min=11.0";

        string targetTriple = isWindows ? "-target x86_64-pc-windows-msvc" :
                              isLinux ? "-target x86_64-linux-gnu" :
                              "-target x86_64-apple-macosx11.0";

        string linkerFlags = isWindows ?
            "-Wl,/subsystem:console -Wl,/entry:mainCRTStartup" :
            isLinux ? "-Wl,--as-needed -Wl,--gc-sections" :
            "-Wl,-dead_strip";

        writer.WriteLine($"cflags_platform = {platformFlags}");
        writer.WriteLine($"ldflags_platform = {linkerFlags}");
        writer.WriteLine($"target_triple = {targetTriple}");

        writer.WriteLine($"cflags = $cflags_base $cflags_platform $target_triple");
        writer.WriteLine($"ldflags = $ldflags_platform $target_triple");
        writer.WriteLine();

        // Правила
        writer.WriteLine("rule cxx");
        writer.WriteLine("  command = $cxx $cflags $defines $includes -c $in -o $out");
        writer.WriteLine("  description = CC $in");
        writer.WriteLine();

        writer.WriteLine("rule link_exe");
        writer.WriteLine("  command = $link $ldflags $libs $in -o $out");
        writer.WriteLine("  description = LINK EXE $out");
        writer.WriteLine();

        writer.WriteLine("rule link_dll");
        if (isWindows)
        {
            writer.WriteLine($"  command = $link $target_triple -shared $in -o $out -Wl,$implibout");
        }
        else if (isLinux)
        {
            writer.WriteLine("  command = $link $ldflags -shared $libs $in -o $out");
        }
        else if (isMac)
        {
            writer.WriteLine("  command = $link $ldflags -dynamiclib $libs $in -o $out");
        }
        writer.WriteLine("  description = LINK DLL $out");
        writer.WriteLine();

        //генерация .lib
        if (isWindows)
        {
            writer.WriteLine("rule create_implib");
            writer.WriteLine("  command = echo EXPORTS > $defout && for %%f in ($in) do (echo %%~nf >> $defout) && llvm-lib /def:$defout /out:$out /machine:x64");
            writer.WriteLine("  description = CREATE IMPLIB $out");
            writer.WriteLine();
        }

        writer.WriteLine("rule link_lib");
        writer.WriteLine("  command = $ar rcs $out $in");
        writer.WriteLine("  description = LINK LIB $out");
        writer.WriteLine();



        // Инициализируем includes
        writer.WriteLine($"includes =");

        // Добавляем глобальные includes
        foreach (var include in target.GlobalIncludePaths ?? new List<string>())
        {
            writer.WriteLine($"includes = $includes -I\"{include}\"");
        }

        writer.WriteLine();

        // Собираем все модули для Ninja
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

        // хранение данных модулей
        Dictionary<string, List<string>> moduleObjectFiles = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> moduleIncludes = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> moduleLibraries = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> moduleDefinitions = new Dictionary<string, List<string>>();

        // Собираем данные всех модулей
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            {
                Console.WriteLine($"  WARNING: Skipping module '{moduleName}' in ninja generation");
                continue;
            }

            // Собираем include пути
            var includes = new List<string>();
            includes.AddRange(module.PublicIncludePaths ?? new List<string>());
            includes.AddRange(module.PrivateIncludePaths ?? new List<string>());
            moduleIncludes[moduleName] = includes;

            // Собираем библиотеки
            var libs = new List<string>();
            libs.AddRange(module.PublicAdditionalLibraries ?? new List<string>());
            libs.AddRange(module.PrivateAdditionalLibraries ?? new List<string>());
            moduleLibraries[moduleName] = libs;

            // Собираем макросес
            var defines = new List<string>();
            defines.AddRange(module.PublicDefinitions ?? new List<string>());
            defines.AddRange(module.PrivateDefinitions ?? new List<string>());
            moduleDefinitions[moduleName] = defines;
        }

        // Собираем все исходники и компилируем объектные файлы
        Console.WriteLine("\nCompiling source files:");
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
                continue;

            var moduleObjs = new List<string>();
            var allSources = new List<string>();
            allSources.AddRange(module.PublicSourceFiles ?? new List<string>());
            allSources.AddRange(module.PrivateSourceFiles ?? new List<string>());
            if (allSources.Count == 0 && module.SourceFiles != null)
                allSources.AddRange(module.SourceFiles);

            foreach (var sourceFile in allSources)
            {
                if (string.IsNullOrEmpty(sourceFile))
                    continue;

                // Определяем тип файла
                bool isSource = sourceFile.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                               sourceFile.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                               sourceFile.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                if (!isSource) continue; // Пропускаем заголовочные файлы

                // Объектный файл с уникальным именем (включая путь к модулю)
                var safeModuleName = moduleName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var safeFileName = Path.GetFileNameWithoutExtension(sourceFile)
                    .Replace('/', '_').Replace('\\', '_').Replace(':', '_');

                // Добавляем хеш пути для уникальности
                var sourceHash = Math.Abs(sourceFile.GetHashCode()).ToString("X8");
                var objName = $"{safeModuleName}_{safeFileName}_{sourceHash}.o";
                var objPath = $"{temp_path_ninja}/{objName}";
                moduleObjs.Add(objPath);

                Console.WriteLine($"  - {moduleName}: {sourceFile} -> {objPath}");

                // Собираем все include пути для этого файла
                var allIncludes = new List<string>();

                // Include paths этого модуля
                if (moduleIncludes.TryGetValue(moduleName, out var incs))
                {
                    foreach (var inc in incs)
                    {
                        if (!string.IsNullOrEmpty(inc))
                            allIncludes.Add(inc);
                    }
                }

                // Include paths из зависимостей
                var allDeps = new List<string>();
                allDeps.AddRange(module.PublicDependencyModuleNames ?? new List<string>());
                allDeps.AddRange(module.PrivateDependencyModuleNames ?? new List<string>());

                foreach (var dep in allDeps)
                {
                    if (moduleIncludes.TryGetValue(dep, out var depIncs))
                    {
                        foreach (var inc in depIncs)
                        {
                            if (!string.IsNullOrEmpty(inc))
                                allIncludes.Add(inc);
                        }
                    }
                }

                // Собираем все макросы
                var allDefines = new List<string>();

                // Макросы этого модуля
                if (moduleDefinitions.TryGetValue(moduleName, out var defs))
                {
                    foreach (var def in defs)
                    {
                        if (!string.IsNullOrEmpty(def))
                            allDefines.Add(def);
                    }
                }

                // Макросы из зависимостей
                foreach (var dep in allDeps)
                {
                    if (moduleDefinitions.TryGetValue(dep, out var depDefs))
                    {
                        foreach (var def in depDefs)
                        {
                            if (!string.IsNullOrEmpty(def))
                                allDefines.Add(def);
                        }
                    }
                }

                // Глобальные макросы таргета
                if (target.GlobalDefinitions != null)
                {
                    foreach (var def in target.GlobalDefinitions)
                    {
                        if (!string.IsNullOrEmpty(def))
                            allDefines.Add(def);
                    }
                }

                // Правило компиляции
                writer.WriteLine($"build {objPath}: cxx {sourceFile}");

                // Добавляем include пути
                if (allIncludes.Count > 0)
                {
                    writer.Write("  includes =");
                    foreach (var inc in allIncludes.Distinct())
                    {
                        writer.Write($" -I\"{inc}\"");
                    }
                    writer.WriteLine();
                }

                // Добавляем макросы
                if (allDefines.Count > 0)
                {
                    writer.Write("  defines =");
                    foreach (var define in allDefines.Distinct())
                    {
                        writer.Write($" -D{define}");
                    }
                    writer.WriteLine();
                }

                writer.WriteLine();
            }

            if (moduleObjs.Count > 0)
            {
                moduleObjectFiles[moduleName] = moduleObjs;
            }
            else
            {
                Console.WriteLine($"  NOTE: Module '{moduleName}' has no source files to compile");
            }
        }

        // Создаем выходные файлы для каждого модуля
        Dictionary<string, string> moduleOutputs = new Dictionary<string, string>();
        HashSet<string> outputFiles = new HashSet<string>(); // Для отслеживания уникальности имен выходных файлов

        Console.WriteLine("\nLinking modules:");
        foreach (string moduleName in allModulesForNinja)
        {
            if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            {
                Console.WriteLine("\nWarning! Skipping line 678");
                continue;
            }


            // Пропускаем модули без объектных файлов
            if (!moduleObjectFiles.ContainsKey(moduleName) || moduleObjectFiles[moduleName].Count == 0)
            {
                Console.WriteLine("\nWarning! Skipping line 686. No obj files!");
                continue;
            }


            string ruleType = "link_dll";
            string outputFileName = moduleName;

            try
            {
                var buildType = module.BuildType;

                if (buildType != null)
                {
                    string buildTypeStr = buildType.ToString();
                    Console.WriteLine($"  DEBUG: {moduleName} BuildType = '{buildTypeStr}'");

                    if (buildTypeStr.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        buildTypeStr.IndexOf("exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_exe";
                        if (moduleName == "Engine")
                        {
                            outputFileName = isWindows ? "Engine.exe" : "Engine";
                        }
                        else
                        {
                            outputFileName = isWindows ? $"{moduleName}.exe" : moduleName;
                        }
                        Console.WriteLine($"  -> Determined as EXE");
                    }
                    else if (buildTypeStr.IndexOf("dynamic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             buildTypeStr.IndexOf("dll", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_dll";
                        outputFileName = isWindows ? $"{moduleName}.dll" :
                                        isLinux ? $"lib{moduleName}.so" :
                                        $"lib{moduleName}.dylib";
                        Console.WriteLine($"  -> Determined as DLL");
                    }
                    else if (buildTypeStr.IndexOf("static", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             buildTypeStr.IndexOf("lib", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_lib";
                        outputFileName = isWindows ? $"{moduleName}.lib" : $"lib{moduleName}.a";
                        Console.WriteLine($"  -> Determined as LIB");
                    }
                }
                else
                {
                    // Fallback: если BuildType не установлен
                    Console.WriteLine($"  WARNING: BuildType is null for {moduleName}");
                    if (moduleName == "Engine" && target.Type.ToString() == "Program")
                    {
                        ruleType = "link_exe";
                        outputFileName = isWindows ? "Engine.exe" : "Engine";
                        Console.WriteLine($"  -> Determined as EXE (fallback for Engine)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR getting BuildType for {moduleName}: {ex.Message}");

                if (moduleName == "Engine" && target.Type.ToString() == "Program")
                {
                    ruleType = "link_exe";
                    outputFileName = isWindows ? "Engine.exe" : "Engine";
                    Console.WriteLine($"  -> Determined as EXE (error fallback)");
                }
            }

            var outputPath = $"{bin_path_ninja}/{outputFileName}";

            // Проверяем уникальность имени выходного файла
            if (outputFiles.Contains(outputPath))
            {
                // Если имя уже используется, добавляем суффикс с именем модуля
                var ext = Path.GetExtension(outputFileName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(outputFileName);
                outputFileName = $"{nameWithoutExt}_{moduleName}{ext}";
                outputPath = $"{bin_path_ninja}/{outputFileName}";
            }
            outputFiles.Add(outputPath);

            Console.WriteLine($"  - {moduleName} -> {outputPath} ({ruleType})");

            writer.Write($"build {outputPath}: {ruleType}");

            // Добавляем объектные файлы этого модуля
            foreach (var objFile in moduleObjectFiles[moduleName])
            {
                writer.Write($" {objFile}");
            }

            // Получаем зависимости модуля
            var allDeps = new List<string>();
            try
            {
                var moduleType = module.GetType();
                var publicDepsProp = moduleType.GetProperty("PublicDependencyModuleNames");
                var privateDepsProp = moduleType.GetProperty("PrivateDependencyModuleNames");

                if (publicDepsProp != null)
                {
                    var deps = publicDepsProp.GetValue(module) as List<string>;
                    if (deps != null) allDeps.AddRange(deps);
                }

                if (privateDepsProp != null)
                {
                    var deps = privateDepsProp.GetValue(module) as List<string>;
                    if (deps != null) allDeps.AddRange(deps);
                }
            }
            catch
            {
                // Fallback через dynamic
                if (module.PublicDependencyModuleNames != null)
                    allDeps.AddRange(module.PublicDependencyModuleNames);
                if (module.PrivateDependencyModuleNames != null)
                    allDeps.AddRange(module.PrivateDependencyModuleNames);
            }

            // Добавляем зависимости сборки
            var depOutputs = new List<string>();
            foreach (var dep in allDeps)
            {
                if (moduleOutputs.ContainsKey(dep))
                {
                    depOutputs.Add(moduleOutputs[dep]);
                }
            }

            if (depOutputs.Count > 0)
            {
                writer.Write($" ||");
                foreach (var depOutput in depOutputs)
                {
                    writer.Write($" {depOutput}");
                }
            }

            writer.WriteLine();

            // Собираем все библиотеки для этого модуля
            var allModuleLibs = new List<string>();

            try
            {
                var moduleType = module.GetType();
                var publicLibsProp = moduleType.GetProperty("PublicAdditionalLibraries");
                var privateLibsProp = moduleType.GetProperty("PrivateAdditionalLibraries");

                if (publicLibsProp != null)
                {
                    var libs = publicLibsProp.GetValue(module) as List<string>;
                    if (libs != null) allModuleLibs.AddRange(libs);
                }

                if (privateLibsProp != null)
                {
                    var libs = privateLibsProp.GetValue(module) as List<string>;
                    if (libs != null) allModuleLibs.AddRange(libs);
                }
            }
            catch
            {
                // Пробуем через dynamic как fallback
                if (module.PublicAdditionalLibraries != null)
                    allModuleLibs.AddRange(module.PublicAdditionalLibraries);
                if (module.PrivateAdditionalLibraries != null)
                    allModuleLibs.AddRange(module.PrivateAdditionalLibraries);
            }

            // Библиотеки из зависимостей (через уже собранные данные)
            foreach (var dep in allDeps)
            {
                if (moduleLibraries.TryGetValue(dep, out var depLibs))
                {
                    foreach (var lib in depLibs)
                    {
                        if (!string.IsNullOrEmpty(lib))
                            allModuleLibs.Add(lib);
                    }
                }
            }

            // Добавляем системные библиотеки
            try
            {
                var moduleType = module.GetType();
                var sysLibsProp = moduleType.GetProperty("PublicSystemLibraries");
                if (sysLibsProp != null)
                {
                    var sysLibs = sysLibsProp.GetValue(module) as List<string>;
                    if (sysLibs != null)
                    {
                        foreach (var sysLib in sysLibs)
                        {
                            if (!string.IsNullOrEmpty(sysLib))
                                allModuleLibs.Add(sysLib);
                        }
                    }
                }
            }
            catch
            {
                // Fallback
                if (module.PublicSystemLibraries != null)
                {
                    foreach (var sysLib in module.PublicSystemLibraries)
                    {
                        if (!string.IsNullOrEmpty(sysLib))
                            allModuleLibs.Add(sysLib);
                    }
                }
            }

            // Глобальные библиотеки таргета
            if (target.GlobalLibraries != null)
            {
                foreach (var lib in target.GlobalLibraries)
                {
                    if (!string.IsNullOrEmpty(lib))
                        allModuleLibs.Add(lib);
                }
            }

            // Добавляем библиотеки
            if (allModuleLibs.Count > 0 || allDeps.Count > 0)
            {
                writer.Write("  libs =");

                // Для Windows: добавляем импортные библиотеки зависимостей
                if (isWindows)
                {
                    foreach (var dep in allDeps)
                    {
                        if (moduleOutputs.ContainsKey(dep))
                        {
                            var depPath = moduleOutputs[dep];
                            if (depPath.EndsWith(".lib"))
                            {
                                writer.Write($" \"{depPath}\"");
                            }
                        }
                    }
                }

                // Добавляем библиотеки самого модуля
                foreach (var lib in allModuleLibs.Distinct())
                {
                    if (isWindows && (lib.EndsWith(".lib") || lib.Contains(".lib")))
                    {
                        writer.Write($" \"{lib}\"");
                    }
                    else if (lib.StartsWith("-"))
                    {
                        writer.Write($" {lib}");
                    }
                    else
                    {
                        writer.Write($" -l{lib}");
                    }
                }
                writer.WriteLine();
            }

            moduleOutputs[moduleName] = outputPath;
            writer.WriteLine();
        }

        string mainOutput = "";

        if (moduleOutputs.Count > 0)
        {
            // Создаем target "all", который зависит от всех выходных файлов
            writer.Write($"build all: phony");
            foreach (var output in moduleOutputs.Values)
            {
                writer.Write($" {output}");
            }
            writer.WriteLine();

            // Делаем "all" default target
            writer.WriteLine($"default all");

            Console.WriteLine($"  Main output: {mainOutput}");
            Console.WriteLine($"  All targets will be built");
        }

        Console.WriteLine($"\n✅ Generated: {ninjaPath}");
        Console.WriteLine($"   Modules: {moduleOutputs.Count}");
        int totalObjects = moduleObjectFiles.Sum(m => m.Value?.Count ?? 0);
        Console.WriteLine($"   Object files: {totalObjects}");
        Console.WriteLine($"   Main output: {mainOutput}");
        Console.WriteLine($"   Compiler: Clang++");
        Console.WriteLine($"   Use: ninja -f \"{Path.GetFileName(ninjaPath)}\"");
    }


    // ========== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ==========

    static string GetPlatformCFlags(string platform, string arch)
    {
        return platform switch
        {
            "windows" => "-DWIN32 -D_WINDOWS -DUNICODE -D_UNICODE " +
                        (arch == "x86" ? "-m32" : arch == "x86_64" ? "-m64" : "") +
                        " -fms-extensions -fms-compatibility-version=19.29",
            "linux" => "-D_LINUX -D__LINUX__ -D_GNU_SOURCE",
            "macos" => "-D__APPLE__ -D__MACH__ -mmacosx-version-min=11.0",
            _ => ""
        };
    }

    static string GetPlatformLdFlags(string platform, string arch)
    {
        return platform switch
        {
            "windows" => arch switch
            {
                "x86" => "-fuse-ld=lld-link /subsystem:console",
                "x86_64" => "-fuse-ld=lld-link /subsystem:console",
                "arm64" => "-fuse-ld=lld-link /subsystem:console",
                _ => "-fuse-ld=lld-link /subsystem:console"
            },
            "linux" => "-fuse-ld=lld -Wl,--as-needed -Wl,--gc-sections",
            "macos" => "-fuse-ld=lld -dead_strip",
            _ => ""
        };
    }

    static string GetTargetTriple(string platform, string arch)
    {
        return platform switch
        {
            "windows" => arch switch
            {
                "x86" => "-target i686-pc-windows-msvc",
                "x86_64" => "-target x86_64-pc-windows-msvc",
                "arm64" => "-target aarch64-pc-windows-msvc",
                _ => "-target x86_64-pc-windows-msvc"
            },
            "linux" => arch switch
            {
                "x86" => "-target i686-linux-gnu",
                "x86_64" => "-target x86_64-linux-gnu",
                "aarch64" => "-target aarch64-linux-gnu",
                _ => "-target x86_64-linux-gnu"
            },
            "macos" => arch switch
            {
                "x86_64" => "-target x86_64-apple-macosx11.0",
                "arm64" => "-target arm64-apple-macosx11.0",
                _ => "-target x86_64-apple-macosx11.0"
            },
            _ => ""
        };
    }
}