using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace utasks;

public static class Helper
{
    public static void Log(string message, LogType logType = LogType.Default, bool newLine = true)
    {
        Console.ForegroundColor = (ConsoleColor)logType;
        if (newLine)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Write(message);
        }
        Console.ResetColor();
    }
    
    public static void LogIf(bool condition, string message, LogType logType = LogType.Default, bool newLine = true)
    {
        if (condition)
        {
            Log(message, logType, newLine);
        }
    }
    
    public static void LogExitCode(int exitCode, string okMessage, string errorMessage, bool newLine = true)
    {
        if (exitCode == 0)
        {
            Log(okMessage, LogType.Info, newLine);
        }
        else
        {
            Log(errorMessage, LogType.Error, newLine);
        }
    }

    public static int RunCommand(string program, string args, string workingDirectory)
    {
        if (program.ToLower() == "cmd" || program.ToLower() == "cmd.exe")
        {
            // if the program is cmd, this will run the command and close cmd
            args = "/C " + args;
        }
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = program,
            Arguments = args,
            // RedirectStandardOutput = true,
            // RedirectStandardError = true,
            // UseShellExecute = false,
            // CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        
        using (Process p = new Process {StartInfo = processStartInfo})
        {
            p.Start();
            p.WaitForExit();
            return p.ExitCode;
        }
    }
    
    public static bool RunConsoleCommand(string command, string args, string message, string workingDirectory)
    {
        Log("### " + message + " ###", LogType.Info);
        Log(command + " " + args);
        var exitCode = RunCommand(command, args, workingDirectory);
        Log("### Result ###\n", LogType.Info);
        LogExitCode(exitCode, "Task finished Successfully: " + message, "Failed to perform the task: " + message);
        Log("");
        return exitCode == 0;
    }
    
    public static void OpenFolder(string dir, string workingDirectory)
    {
        if (Directory.Exists(dir))
        {
            string program = "cmd.exe";
            string args = "start \"\" \""+ dir +"\"";
            RunCommand(program, args, workingDirectory);
        }
    }
    
    public static void RemoveDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Log($"Removing directory: {dir}", LogType.Info);
            Directory.Delete(dir, true);
        }
        else
        {
            Log($"Directory not found while attempting to delete it: {dir}", LogType.Warning);
        }
    }
    
    public static void RemoveFile(string file)
    {
        if (File.Exists(file))
        {
            Log($"Removing file: {file}", LogType.Info);
            File.Delete(file);
        }
        else
        {
            Log($"File not found while attempting to delete it: {file}", LogType.Warning);
        }
    }
    
    public static bool IsDirOrFile(string maybeDirOrFile)
    {
        // Check if string contains characters that make it a file or directory
        if (maybeDirOrFile.Contains("\\") || maybeDirOrFile.Contains("/"))
        {
            return true;
        }
        
        return false;
    }

    public static bool DoesDirOrFileExist(string maybeDirOrFile)
    {
        if (File.Exists(maybeDirOrFile) || Directory.Exists(maybeDirOrFile))
        {
            return true;
        }
        
        return false;
    }
    
    public static bool IsValidIndex<T>(this List<T> list, int index)
    {
        return index >= 0 && index < list.Count;
    }
    
    public static object CallMethodByName(string methodName, dynamic obj, params object[] parameters)
    {
        var types = parameters.Select(p => p.GetType()).ToArray();
        var type = obj.GetType();
        var method = type.GetMethod(methodName, types);
        return method.Invoke(obj, parameters);
    }
    
    /**
     * Get indexes from a string input, support ranges and commas
     * i.e. "1,2,5-7" will return [1,2,5,6,7]
     */
    public static List<int> GetSelectedIndexesFromInput(string chooseInput)
    {
        var result = new List<int>();
        try
        {
            var chooseInputs = chooseInput.Split(",");
            foreach (var element in chooseInputs)
            {
                if (element.Contains("-"))
                {
                    var elementRange = element.Trim().Split("-");
                    var rangeStart = int.Parse(elementRange[0].Trim());
                    var rangeEnd = int.Parse(elementRange[1].Trim());
                    
                    for (var index = rangeStart; index <= rangeEnd; index++)
                    {
                        result.Add(index - 1);
                    }
                }
                else
                {
                    var index = int.Parse(element.Trim());
                    result.Add(index - 1);
                }
            }
        }
        catch (Exception e)
        {
            Log("Invalid input: " + chooseInput, LogType.Error);
            Log(e.Message, LogType.Error);
            result = new List<int>();
        }
        return result;
    }
    
    public static List<T> MakeInstancesFor<T>() where T : class
    {
        // Could be used instead of dependency injection
        var instances =
            from t in Assembly.GetExecutingAssembly().GetTypes()
            where t.GetInterfaces().Contains(typeof(T)) && t.GetConstructor(Type.EmptyTypes) != null
            let instance = Activator.CreateInstance(t) as T
            where instance != null
            select instance;

        return instances.ToList();
    }
    
    public static List<Type> GetTypesFor<T>() where T : class
    {
        var types = from t in Assembly.GetExecutingAssembly().GetTypes()
            where t.GetInterfaces().Contains(typeof(T)) && !t.IsInterface && !t.IsAbstract
            select t;

        return types.ToList();
    }
    
    public static void AddSingletonsFromTypes(this IServiceCollection service, List<Type> types)
    {
        foreach (var type in types)
        {
            service.AddSingleton(type);
        }
    }

    public static List<T> GetSingletonsForTypes<T>(this IServiceProvider service, List<Type> types) where T : class
    {
        List<T> singletons = new List<T>();
        
        foreach (var type in types)
        {
            singletons.Add(service.GetService(type) as T);
        }

        return singletons;
    }
}
