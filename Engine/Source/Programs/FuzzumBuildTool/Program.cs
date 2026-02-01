using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FuzzumBuildTool;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class Program
{
    static Dictionary<string, dynamic> loadedModules = new Dictionary<string, dynamic>();
    static Dictionary<string, string> moduleFilePaths = new Dictionary<string, string>();
    static string projectRootDir = "";
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





    static class GlobExpander
    {
        public static List<string> ExpandGlobs(List<string> patterns, string baseDir)
        {
            var result = new List<string>();
            foreach (var pattern in patterns)
            {
                Console.WriteLine($"  DEBUG GlobExpander: Expanding pattern '{pattern}' from base dir '{baseDir}'");
                var expanded = ExpandGlob(pattern, baseDir);
                Console.WriteLine($"  DEBUG GlobExpander: Found {expanded.Count} files");
                result.AddRange(expanded);
            }
            return result;
        }

        public static List<string> ExpandGlob(string pattern, string baseDir)
        {
            var results = new List<string>();

            if (string.IsNullOrEmpty(pattern))
                return results;

            try
            {
                Console.WriteLine($"    DEBUG: Processing pattern '{pattern}' with baseDir '{baseDir}'");

                // Проверяем, является ли путь абсолютным
                string fullPattern;
                if (Path.IsPathRooted(pattern))
                {
                    fullPattern = pattern;
                }
                else
                {
                    fullPattern = Path.Combine(baseDir, pattern);
                }

                Console.WriteLine($"    DEBUG: Full pattern: '{fullPattern}'");

                // Если это не glob-паттерн, просто возвращаем как есть
                if (!fullPattern.Contains('*') && !fullPattern.Contains('?'))
                {
                    Console.WriteLine($"    DEBUG: Not a glob pattern, checking file existence");
                    if (File.Exists(fullPattern) || Directory.Exists(fullPattern))
                    {
                        results.Add(pattern);
                        Console.WriteLine($"    DEBUG: File/directory exists, adding '{pattern}'");
                    }
                    else
                    {
                        Console.WriteLine($"    DEBUG: File/directory does not exist at '{fullPattern}'");
                    }
                    return results;
                }

                // Разделяем на директорию и маску
                string searchDirectory;
                string searchPattern;

                if (fullPattern.Contains('*') || fullPattern.Contains('?'))
                {
                    // Находим последний слэш перед первым символом wildcard
                    int lastSlashBeforeWildcard = -1;
                    for (int i = 0; i < fullPattern.Length; i++)
                    {
                        if ((fullPattern[i] == '*' || fullPattern[i] == '?') && i > 0)
                        {
                            // Ищем последний слэш перед этой позицией
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (fullPattern[j] == '\\' || fullPattern[j] == '/')
                                {
                                    lastSlashBeforeWildcard = j;
                                    break;
                                }
                            }
                            break;
                        }
                    }

                    if (lastSlashBeforeWildcard >= 0)
                    {
                        searchDirectory = fullPattern.Substring(0, lastSlashBeforeWildcard);
                        searchPattern = fullPattern.Substring(lastSlashBeforeWildcard + 1);
                    }
                    else
                    {
                        searchDirectory = Path.GetDirectoryName(fullPattern);
                        if (string.IsNullOrEmpty(searchDirectory))
                            searchDirectory = ".";
                        searchPattern = Path.GetFileName(fullPattern);
                    }
                }
                else
                {
                    searchDirectory = Path.GetDirectoryName(fullPattern);
                    searchPattern = Path.GetFileName(fullPattern);
                }

                Console.WriteLine($"    DEBUG: Search directory: '{searchDirectory}'");
                Console.WriteLine($"    DEBUG: Search pattern: '{searchPattern}'");

                // Нормализуем путь
                searchDirectory = Path.GetFullPath(searchDirectory);
                Console.WriteLine($"    DEBUG: Normalized search directory: '{searchDirectory}'");

                if (!Directory.Exists(searchDirectory))
                {
                    Console.WriteLine($"    DEBUG: Search directory does not exist!");
                    return results;
                }

                // Обрабатываем рекурсивные шаблоны с **
                if (searchPattern.Contains("**"))
                {
                    Console.WriteLine($"    DEBUG: Recursive pattern detected");
                    // Получаем все файлы рекурсивно
                    var allFiles = Directory.GetFiles(searchDirectory, "*", SearchOption.AllDirectories);
                    Console.WriteLine($"    DEBUG: Found {allFiles.Length} total files recursively");

                    foreach (var file in allFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        if (MatchesPattern(fileName, searchPattern))
                        {
                            results.Add(file);
                            Console.WriteLine($"    DEBUG: Added '{file}'");
                        }
                    }
                }
                else
                {
                    // Не рекурсивный поиск
                    Console.WriteLine($"    DEBUG: Non-recursive search");
                    var files = Directory.GetFiles(searchDirectory, searchPattern, SearchOption.TopDirectoryOnly);
                    Console.WriteLine($"    DEBUG: Found {files.Length} files matching pattern");

                    foreach (var file in files)
                    {
                        results.Add(file);
                        Console.WriteLine($"    DEBUG: Added '{file}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to expand glob pattern '{pattern}': {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine($"    DEBUG: Total results: {results.Count}");
            return results;
        }

        private static bool MatchesPattern(string fileName, string pattern)
        {
            if (pattern == "**")
                return true;

            if (pattern.StartsWith("**/"))
            {
                string subPattern = pattern.Substring(3);
                return fileName.Contains(subPattern.Replace("*", ""));
            }

            if (pattern.EndsWith("/**"))
            {
                string subPattern = pattern.Substring(0, pattern.Length - 3);
                return fileName.StartsWith(subPattern.Replace("*", ""));
            }

            if (pattern.EndsWith(".cpp") && pattern.Contains("**.cpp"))
            {
                // Преобразуем "**.cpp" → "**/*.cpp"
                pattern = pattern.Replace("**.cpp", "**/*.cpp");
            }
            return true;
        }
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

    static (List<string> publicIncludes, List<string> privateIncludes) GetAllIncludesForModule(
        string moduleName,
        dynamic target,
        HashSet<string> visited = null)
    {
        if (visited == null) visited = new HashSet<string>();
        if (visited.Contains(moduleName)) return (new List<string>(), new List<string>());
        visited.Add(moduleName);

        if (!loadedModules.TryGetValue(moduleName, out dynamic module))
            return (new List<string>(), new List<string>());

        var allPublicIncludes = new List<string>();
        var allPrivateIncludes = new List<string>();

        // Получаем базовую директорию модуля
        string moduleBaseDir = moduleFilePaths.ContainsKey(moduleName)
            ? moduleFilePaths[moduleName]
            : Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        string projectRoot = projectRootDir;

        // Глобальные include-пути таргета
        if (target.GlobalIncludePaths != null)
        {
            foreach (var include in target.GlobalIncludePaths)
            {
                if (include.Contains('*'))
                {
                    allPublicIncludes.AddRange(GlobExpander.ExpandGlob(include, projectRoot));
                }
                else
                {
                    allPublicIncludes.Add(include);
                }
            }
        }

        // Публичные include-пути текущего модуля
        var modulePublicIncludes = module.PublicIncludePaths ?? new List<string>();
        foreach (var include in modulePublicIncludes)
        {
            if (include.Contains('*'))
            {
                allPublicIncludes.AddRange(GlobExpander.ExpandGlob(include, moduleBaseDir));
            }
            else
            {
                allPublicIncludes.Add(include);
            }
        }

        // Приватные include-пути текущего модуля
        var modulePrivateIncludes = module.PrivateIncludePaths ?? new List<string>();
        foreach (var include in modulePrivateIncludes)
        {
            if (include.Contains('*'))
            {
                allPrivateIncludes.AddRange(GlobExpander.ExpandGlob(include, moduleBaseDir));
            }
            else
            {
                allPrivateIncludes.Add(include);
            }
        }

        // Рекурсивно собираем публичные include-пути из зависимостей
        foreach (string dep in module.PublicDependencyModuleNames ?? new List<string>())
        {
            if (!visited.Contains(dep))
            {
                var result = GetAllIncludesForModule(dep, target, visited);
                allPublicIncludes.AddRange(result.Item1); // Item1 = publicIncludes
            }
        }

        // Для приватных зависимостей
        foreach (string dep in module.PrivateDependencyModuleNames ?? new List<string>())
        {
            if (!visited.Contains(dep))
            {
                var result = GetAllIncludesForModule(dep, target, visited);
                allPrivateIncludes.AddRange(result.Item1); // Item1 = publicIncludes
                allPrivateIncludes.AddRange(result.Item2); // Item2 = privateIncludes
            }
        }

        // Убираем дубликаты
        allPublicIncludes = allPublicIncludes.Distinct().ToList();
        allPrivateIncludes = allPrivateIncludes.Distinct().ToList();

        return (allPublicIncludes, allPrivateIncludes);
    }


    static string EscapePathForNinja(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        path = path.Trim();
        path = path.Trim('"');
        path = path.Replace('\\', '/');
        var invalidChars = new char[] { '\r', '\n', '\t', '\0', '\b', '\a', '\f', '\v' };
        foreach (var ch in invalidChars)
        {
            path = path.Replace(ch.ToString(), "");
        }

        return path;
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

        projectRootDir = Path.GetDirectoryName(targetFile)!;

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

            string expectedFileName1 = module.Name + ".Build.cs";
            string expectedFileName2 = moduleType.Name.Replace("Module", "") + ".Build.cs";

            bool found = false;
            foreach (var buildFile in allBuildFiles)
            {
                string fileName = Path.GetFileName(buildFile);
                if (fileName.Equals(expectedFileName1, StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(expectedFileName2, StringComparison.OrdinalIgnoreCase))
                {
                    Program.moduleFilePaths[module.Name] = Path.GetDirectoryName(buildFile);
                    Console.WriteLine($"  Found module '{module.Name}' at: {Program.moduleFilePaths[module.Name]}");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Program.moduleFilePaths[module.Name] = engineSourceDir;
                Console.WriteLine($"  Warning: Using project root for module '{module.Name}'");
            }
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

            // Include пути
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

    static string MakeRelativePath(string absolutePath, string rootDir)
    {
        if (!Path.IsPathRooted(absolutePath))
            return absolutePath.Replace('\\', '/');

        var absUri = new Uri(absolutePath + (Directory.Exists(absolutePath) ? "/" : ""));
        var rootUri = new Uri(rootDir + "/");
        var relUri = rootUri.MakeRelativeUri(absUri);
        return Uri.UnescapeDataString(relUri.ToString()).Replace('\\', '/');
    }

    static void GenerateNinjaFile(dynamic target, string ninjaOutDir, string targetName)
    {
        string ninjaPath = Path.Combine(ninjaOutDir, $"{targetName}.ninja");
        Directory.CreateDirectory(Path.Combine(ninjaOutDir, bin_path_ninja));
        Directory.CreateDirectory(Path.Combine(ninjaOutDir, temp_path_ninja));

        using var writer = new StreamWriter(ninjaPath, false, new UTF8Encoding(false));

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
        writer.Write("cflags_base = -std=" + c_vers + " -Wall -Wextra "); /*-Werror*/
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


        writer.WriteLine($"includes =");

        // Добавляем глобальные includes
        foreach (var include in target.GlobalIncludePaths ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(include))
                continue;

            // Если путь уже абсолютный, оставляем как есть
            string absInclude;
            if (Path.IsPathRooted(include))
            {
                absInclude = include;
            }
            else
            {
                // Иначе добавляем корневую директорию проекта
                absInclude = Path.GetFullPath(Path.Combine(projectRootDir, include));
            }

            // Создаем относительный путь от ninjaOutDir
            string relativeInclude;
            try
            {
                relativeInclude = Path.GetRelativePath(ninjaOutDir, absInclude);
            }
            catch
            {
                // Если не получается создать относительный путь, используем абсолютный
                relativeInclude = absInclude;
            }

            string ninjaIncludePath = EscapePathForNinja(relativeInclude);

            ninjaIncludePath = ninjaIncludePath.Replace("\\.\\", "/");
            ninjaIncludePath = ninjaIncludePath.Replace("\\./", "/");
            ninjaIncludePath = ninjaIncludePath.Trim('.', '/', '\\');

            if (!string.IsNullOrWhiteSpace(ninjaIncludePath))
            {
                ninjaIncludePath = ninjaIncludePath.Replace("\"", "\\\"");
                writer.WriteLine($"includes = $includes -I\"{ninjaIncludePath}\"");
            }
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
                continue;

            // Получаем все include-пути для этого модуля
            var includesResult = GetAllIncludesForModule(moduleName, target);
            var publicIncludes = includesResult.Item1;
            var privateIncludes = includesResult.Item2;

            // Объединяем публичные и приватные
            var allIncludes = new List<string>();
            allIncludes.AddRange(publicIncludes);
            allIncludes.AddRange(privateIncludes);

            moduleIncludes[moduleName] = allIncludes;

            var libs = new List<string>();
            libs.AddRange(module.PublicAdditionalLibraries ?? new List<string>());
            libs.AddRange(module.PrivateAdditionalLibraries ?? new List<string>());
            moduleLibraries[moduleName] = libs;

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

            // Получаем все include-пути для этого модуля
            var includesResult = GetAllIncludesForModule(moduleName, target);
            var publicIncludes = includesResult.Item1; // Item1 = publicIncludes
            var privateIncludes = includesResult.Item2; // Item2 = privateIncludes

            var moduleObjs = new List<string>();
            var allSources = new List<string>();

            // string engineSourceDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Console.WriteLine($"\n  Processing module: {moduleName}");

            string moduleBaseDir = moduleFilePaths.ContainsKey(moduleName)
    ? moduleFilePaths[moduleName]
    : projectRootDir;

            Console.WriteLine($"  Module base directory: {moduleBaseDir}");

            var publicSources = module.PublicSourceFiles ?? new List<string>();
            var privateSources = module.PrivateSourceFiles ?? new List<string>();

            string privateDir = Path.Combine(moduleBaseDir, "Private");
            Console.WriteLine($"  Private directory: {privateDir}");
            Console.WriteLine($"  Private directory exists: {Directory.Exists(privateDir)}");

            if (Directory.Exists(privateDir))
            {
                var cppFiles = Directory.GetFiles(privateDir, "*.cpp", SearchOption.AllDirectories);
                Console.WriteLine($"  Found {cppFiles.Length} .cpp files in Private directory:");
                foreach (var file in cppFiles)
                {
                    Console.WriteLine($"    - {file}");
                }
            }

            foreach (var source in publicSources)
            {
                if (source.Contains('*')) 
                {
                    // var expandedFiles = GlobExpander.ExpandGlob(source, engineSourceDir);
                    var expandedFiles = GlobExpander.ExpandGlob(source, moduleBaseDir);
                    foreach (var expandedFile in expandedFiles)
                    {
                        bool isSource = expandedFile.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                       expandedFile.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                       expandedFile.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                        if (isSource)
                        {
                            allSources.Add(expandedFile);
                        }
                    }
                }
                else 
                {
                    bool isSource = source.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                   source.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                   source.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                    if (isSource)
                    {
                        allSources.Add(source);
                    }
                }
            }

            // Обрабатываем приватные исходные файлы с glob-паттернами
            foreach (var source in privateSources)
            {
                if (source.Contains('*')) 
                {
                    //   var expandedFiles = GlobExpander.ExpandGlob(source, engineSourceDir);
                    var expandedFiles = GlobExpander.ExpandGlob(source, moduleBaseDir);
                    foreach (var expandedFile in expandedFiles)
                    {
                        bool isSource = expandedFile.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                       expandedFile.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                       expandedFile.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                        if (isSource)
                        {
                            allSources.Add(expandedFile);
                        }
                    }
                }
                else 
                {
                    bool isSource = source.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                   source.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                   source.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                    if (isSource)
                    {
                        allSources.Add(source);
                    }
                }
            }
            if (allSources.Count == 0 && module.SourceFiles != null)
            {
                foreach (var source in module.SourceFiles)
                {
                    if (source.Contains('*')) 
                    {
                        // var expandedFiles = GlobExpander.ExpandGlob(source, engineSourceDir);
                        var expandedFiles = GlobExpander.ExpandGlob(source, moduleBaseDir);
                        foreach (var expandedFile in expandedFiles)
                        {
                            bool isSource = expandedFile.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                           expandedFile.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                           expandedFile.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                            if (isSource)
                            {
                                allSources.Add(expandedFile);
                            }
                        }
                    }
                    else 
                    {
                        bool isSource = source.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                                       source.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ||
                                       source.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);

                        if (isSource)
                        {
                            allSources.Add(source);
                        }
                    }
                }
            }

            foreach (var sourceFile in allSources)
            {
                if (string.IsNullOrEmpty(sourceFile))
                    continue;

                string absSourcePath = sourceFile;

                if (!File.Exists(absSourcePath))
                {
                    Console.WriteLine($"  ERROR: Source file not found: {absSourcePath}");
                    continue;
                }

                var safeModuleName = moduleName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var safeFileName = Path.GetFileNameWithoutExtension(absSourcePath)
                    .Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                var sourceHash = Math.Abs(absSourcePath.GetHashCode()).ToString("X8");
                var objName = $"{safeModuleName}_{safeFileName}_{sourceHash}.o";
                var objPath = $"{temp_path_ninja}/{objName}".Trim();
                moduleObjs.Add(objPath);

                Console.WriteLine($"  - {moduleName}: {Path.GetFileName(absSourcePath)} -> {objPath}");

                // Собираем все include пути
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
                string absoluteSourcePath;
                if (Path.IsPathRooted(sourceFile))
                {
                    absoluteSourcePath = sourceFile;
                }
                else
                {
                    absoluteSourcePath = Path.Combine(moduleBaseDir, sourceFile);
                }


                string relativeSourcePath = Path.GetRelativePath(ninjaOutDir, absSourcePath);
                string ninjaSourcePath = EscapePathForNinja(relativeSourcePath);

                string cleanObjPath = objPath.Trim();
                // string cleanSourcePath = ninjaSourcePath.Trim();
                // string cleanSourcePath = EscapePathForNinja(relativeSourcePath).Trim();

                string cleanSourcePath = ninjaSourcePath.Trim();

                writer.WriteLine($"build {cleanObjPath}: cxx {cleanSourcePath}");

                Console.WriteLine($"  DEBUG: cleanSourcePath = '{cleanSourcePath}'");

                // Добавляем include пути
                if (allIncludes.Count > 0)
                {
                    writer.Write("  includes =");
                    foreach (var inc in allIncludes.Distinct())
                    {
                        string absInc;
                        if (Path.IsPathRooted(inc))
                        {
                            absInc = inc;
                        }
                        else
                        {
                            // Преобразуем относительный путь в абсолютный
                            if (inc.StartsWith("Public") || inc.StartsWith("Private"))
                            {
                                absInc = Path.Combine(moduleBaseDir, inc);
                            }
                            else
                            {
                                absInc = Path.Combine(projectRootDir, inc);
                            }
                        }

                        // Нормализуем для ninja - используем относительный путь от ninjaOutDir
                        string relativeInc = Path.GetRelativePath(ninjaOutDir, absInc);

                        Console.WriteLine($"  DEBUG: absInc = '{absInc}'");
                        Console.WriteLine($"  DEBUG: ninjaOutDir = '{ninjaOutDir}'");
                        Console.WriteLine($"  DEBUG: relativeInc = '{relativeInc}'");

                        string ninjaIncPath = EscapePathForNinja(relativeInc);
                        Console.WriteLine($"  DEBUG: ninjaIncPath = '{ninjaIncPath}'");

                        if (ninjaIncPath.StartsWith(".."))
                        {
                            // Оставляем как есть
                        }
                        else
                        {
                            ninjaIncPath = ninjaIncPath.Replace("\\", "/");
                        }

                        if (!string.IsNullOrEmpty(ninjaIncPath))
                        {
                            ninjaIncPath = ninjaIncPath.Trim('\"').TrimEnd('/');
                            ninjaIncPath = ninjaIncPath.Replace("\"", "\\\"");
                            writer.Write($" -I\"{ninjaIncPath}\"");
                        }
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
                continue;

            // Пропускаем модули без объектных файлов
            if (!moduleObjectFiles.ContainsKey(moduleName) || moduleObjectFiles[moduleName].Count == 0)
                continue;

            string ruleType = "link_dll";
            string outputFileName = moduleName;

            // Определяем тип сборки
            try
            {
                var buildType = module.BuildType;
                if (buildType != null)
                {
                    string buildTypeStr = buildType.ToString();
                    if (buildTypeStr.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        buildTypeStr.IndexOf("exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_exe";
                        outputFileName = isWindows ? $"{moduleName}.exe" : moduleName;
                    }
                    else if (buildTypeStr.IndexOf("dynamic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             buildTypeStr.IndexOf("dll", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_dll";
                        outputFileName = isWindows ? $"{moduleName}.dll" :
                                        isLinux ? $"lib{moduleName}.so" :
                                        $"lib{moduleName}.dylib";
                    }
                    else if (buildTypeStr.IndexOf("static", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             buildTypeStr.IndexOf("lib", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ruleType = "link_lib";
                        outputFileName = isWindows ? $"{moduleName}.lib" : $"lib{moduleName}.a";
                    }
                }
            }
            catch { }

            var outputPath = $"{bin_path_ninja}/{outputFileName}";

            // Проверяем уникальность имени
            if (outputFiles.Contains(outputPath))
            {
                var ext = Path.GetExtension(outputFileName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(outputFileName);
                outputFileName = $"{nameWithoutExt}_{moduleName}{ext}";
                outputPath = $"{bin_path_ninja}/{outputFileName}";
            }
            outputFiles.Add(outputPath);

            Console.WriteLine($"  - {moduleName} -> {outputPath} ({ruleType})");

            var allLinkObjs = new List<string>();

            if (ruleType == "link_exe")
            {
                // Для исполняемого файла добавляем все объектные файлы этого модуля и его зависимостей
                allLinkObjs.AddRange(moduleObjectFiles[moduleName]);

                // Добавляем объекты всех зависимостей
                var allDeps = GetAllDependencies(moduleName);
                foreach (var dep in allDeps)
                {
                    if (moduleObjectFiles.TryGetValue(dep, out var depObjs))
                    {
                        allLinkObjs.AddRange(depObjs);
                    }
                }

                // Убираем дубликаты
                allLinkObjs = allLinkObjs.Distinct().ToList();
                Console.WriteLine($"    Will link with {allLinkObjs.Count} object files (including dependencies)");
            }
            else
            {
                // Для DLL/LIB используем только свои объекты
                allLinkObjs.AddRange(moduleObjectFiles[moduleName]);
            }

            // Правило линковки
            writer.Write($"build {outputPath}: {ruleType}");
            foreach (var objFile in allLinkObjs)
            {
                writer.Write($" {objFile}");
            }

            // Получаем зависимости для импортных библиотек
            var allDepsForLibs = new List<string>();
            if (module.PublicDependencyModuleNames != null)
                allDepsForLibs.AddRange(module.PublicDependencyModuleNames);
            if (module.PrivateDependencyModuleNames != null)
                allDepsForLibs.AddRange(module.PrivateDependencyModuleNames);

            // Добавляем зависимости сборки
            var depOutputs = new List<string>();
            foreach (var dep in allDepsForLibs)
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

            // Библиотеки для линковки
            var allModuleLibs = new List<string>();

            // Библиотеки самого модуля
            if (module.PublicAdditionalLibraries != null)
                allModuleLibs.AddRange(module.PublicAdditionalLibraries);
            if (module.PrivateAdditionalLibraries != null)
                allModuleLibs.AddRange(module.PrivateAdditionalLibraries);

            // Системные библиотеки
            if (module.PublicSystemLibraries != null)
                allModuleLibs.AddRange(module.PublicSystemLibraries);

            // Глобальные библиотеки
            if (target.GlobalLibraries != null)
                allModuleLibs.AddRange(target.GlobalLibraries);

            // Для Windows исполняемых файлов добавляем импортные библиотеки зависимостей
            if (isWindows && ruleType == "link_exe")
            {
                foreach (var dep in allDepsForLibs)
                {
                    if (moduleOutputs.ContainsKey(dep))
                    {
                        var depPath = moduleOutputs[dep];
                        if (depPath.EndsWith(".lib"))
                        {
                            // Добавляем .lib файл зависимости
                            allModuleLibs.Add(depPath);
                        }
                        else if (depPath.EndsWith(".dll"))
                        {
                            // Для DLL генерируем имя .lib файла
                            var libName = depPath.Replace(".dll", ".lib");
                            if (File.Exists(Path.Combine(ninjaOutDir, libName)))
                            {
                                allModuleLibs.Add(libName);
                            }
                        }
                    }
                }
            }

            // Добавляем библиотеки
            if (allModuleLibs.Count > 0)
            {
                writer.Write("  libs =");
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

        Console.WriteLine($"\n✅ Generated: {ninjaPath}");
        Console.WriteLine($"   Modules: {moduleOutputs.Count}");
        int totalObjects = moduleObjectFiles.Sum(m => m.Value?.Count ?? 0);
        Console.WriteLine($"   Object files: {totalObjects}");
     //   Console.WriteLine($"   Main output: {mainOutput}");
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