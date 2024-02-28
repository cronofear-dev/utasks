// This program is packaged into a single exe by using the command:
// dotnet publish -r win-x64 --self-contained=false /p:PublishSingleFile=true /p:DebugType=None /p:PublishDir="..\..\"
//
// Use `self-contained=true /p:PublishTrimmed=true` for embedding the .NET runtime
// More info: https://stackoverflow.com/questions/49967697/make-net-core-console-app-a-single-exe

using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Win32; // For access to the registry for the engine location``
using Microsoft.Extensions.DependencyInjection; // For dependency injection (Settings and action/filter instances)

namespace utasks;

// Todo - Move the json parsing logic to a state machine or something that can handle states?
// Only if it's needed to add more support for more complex json files (permutation of variables, etc)

public static class Program
{
    
#if DEBUG
    private static string _debugCurrentPath = @"C:\WS\SkillCraftGame";
#endif
    private static USettings _settings = new USettings();
    private static List<ITaskPreAction> _taskPreActions;
    private static List<ITaskPostAction> _taskPostActions;
    private static List<ISubtaskFilter> _subtaskFilters;
    private static List<ISubtasksAction> _subtaskActions;
    private static List<IGetSubtasks> _getSubtasks;
    
    //a=TestLevel1
    private static void Main(string[] args)
    {
        // Setup Dependency Injection
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(_settings);
            
            var _taskPreActionsTypes = Helper.GetTypesFor<ITaskPreAction>();
            var _taskPostActionsTypes = Helper.GetTypesFor<ITaskPostAction>();
            var _subtaskFiltersTypes = Helper.GetTypesFor<ISubtaskFilter>();
            var _subtaskActionsTypes = Helper.GetTypesFor<ISubtasksAction>();
            var _getSubtasksTypes = Helper.GetTypesFor<IGetSubtasks>();
            
            services.AddSingletonsFromTypes(_taskPreActionsTypes);
            services.AddSingletonsFromTypes(_taskPostActionsTypes);
            services.AddSingletonsFromTypes(_subtaskFiltersTypes);
            services.AddSingletonsFromTypes(_subtaskActionsTypes);
            services.AddSingletonsFromTypes(_getSubtasksTypes);
            
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            _taskPreActions = serviceProvider.GetSingletonsForTypes<ITaskPreAction>(_taskPreActionsTypes);
            _taskPostActions = serviceProvider.GetSingletonsForTypes<ITaskPostAction>(_taskPostActionsTypes);
            _subtaskFilters = serviceProvider.GetSingletonsForTypes<ISubtaskFilter>(_subtaskFiltersTypes);
            _subtaskActions = serviceProvider.GetSingletonsForTypes<ISubtasksAction>(_subtaskActionsTypes);
            _getSubtasks = serviceProvider.GetSingletonsForTypes<IGetSubtasks>(_getSubtasksTypes);
        }
        
        // Parse utasks.jsonc and open the main menu
        {
            ParseJson();
            
            if (args.Length == 0)
            {
                bool bIsAutoTask = _settings.GetVariable("{DefaultMainMenu}") == "AutoTasks";
                OpenMainMenu(bIsAutoTask);
            }
            else
            {
                HandleTaskArgs(ParseArgs(args));
            }
        }
    }
    
    private static void ParseJson()
    {
        string jsonString = File.ReadAllText("utasks.jsonc");
        JObject utasksJson = JsonConvert.DeserializeObject<JObject>(jsonString);

        _settings.ClearSettings();
        
#if DEBUG
        _settings.SetVariable("{CURRENT_PATH}", _debugCurrentPath);
#endif
#if RELEASE
        _settings.SetVariable("{CURRENT_PATH}", Environment.CurrentDirectory);
#endif
        ParseJsonMainVariables(utasksJson);
        ParseJsonVariables(utasksJson);
        ParseJsonTasks(utasksJson);
        ParseJsonAutoTasks(utasksJson);
    }
    
    private static void OpenMainMenu(bool showAutoTasks = false)
    {
        int i = 0;

        if (showAutoTasks)
        {
            Helper.Log("### Select AutoTask ###", LogType.Info);
            foreach (var autoTask in _settings.AutoTasks)
            {
                Helper.Log($"{i + 1} - {autoTask.Title}");
                foreach (var description in autoTask.StepsDescriptions)
                {
                    Helper.Log($"   - {description}");
                }
                i++;
            }
        }
        else
        {
            Helper.Log("### Select Task ###", LogType.Info);
            foreach (var task in _settings.Tasks)
            {
                Helper.Log($"{i + 1} - {task.Title}");
                i++;
            }
        }
        Helper.Log("");
        Helper.Log($"Or type <h> for help | <r> to reload the json settings | <*> to toggle tasks/autotasks", LogType.Info);
        Helper.Log("Action: ", LogType.Info);
        string chooseInput = Console.ReadLine();
        Helper.Log("");

        try
        {
            // run the commands for the selected task
            if (chooseInput?.ToLower() == "h" || chooseInput?.ToLower() == "help")
            {
                Debug_PrintHelp();
            }
            else if (chooseInput?.ToLower() == "r")
            {
                ParseJson();
                Helper.Log("Settings reloaded...");
            }
            else if (chooseInput?.ToLower() == "*")
            {
                Console.Clear();
                OpenMainMenu(!showAutoTasks);
            }
            else
            {
                if (showAutoTasks)
                {
                    HandleTaskArgs(ParseArgs(new string[] { "a=" + _settings.AutoTasks[int.Parse(chooseInput) - 1].Id }));
                }
                else
                {
                    HandleSelectedTask(chooseInput);
                }
            }
        }
        catch (Exception e)
        {
            Helper.Log(e.Message, LogType.Error);
        }
        finally
        {
            // back to the main menu
            Helper.Log("\n### Press Enter to Continue ###", LogType.Info);
            Console.ReadLine();
            Console.Clear();
            OpenMainMenu(showAutoTasks);   
        }
    }
    
    private static void ParseJsonMainVariables(JObject json)
    {
        string uprojectFileName = json.Value<string>("UProjectFileName");
        string uprojectFilePath = json.Value<string>("UProjectFilePath");
        string enginePath = json.Value<string>("EnginePath");
        
        _settings.SetVariable("{UProjectFileName}", ParseStringVariable(uprojectFileName).FirstOrDefault());
        _settings.SetVariable("{UProjectFilePath}", ParseStringVariable(uprojectFilePath).FirstOrDefault());
        ParseJsonEnginePath(_settings.GetVariable("{UProjectFilePath}"));
        _settings.SetVariable("{EnginePath}", ParseStringVariable(enginePath).FirstOrDefault());
    }
    
    private static void ParseJsonVariables(JObject json)
    {
        foreach (JProperty property in json.Properties())
        {
            string propertyName = property.Name;
            JToken propertyValue = property.Value;
            
            if (propertyName == "Tasks" || propertyName == "AutoTasks" || propertyName == "UProjectFileName" || propertyName == "UProjectFilePath" || propertyName == "EnginePath")
            {
                continue;
            }
            
            // Handle String Variables
            if (propertyValue.Type == JTokenType.String)
            {
                string keyName = "{" + propertyName + "}";
                var values = ParseStringVariable(propertyValue.Value<string>());
                _settings.AddVariableList(keyName, values);
            }
            
            // Handle Array of strings
            if (propertyValue.Type == JTokenType.Array)
            {
                List<string> finalResult = new List<string>();
                string keyName = "{" + propertyName + "}";
                foreach (JValue innerValue in propertyValue)
                {
                    var result = ParseStringVariable(innerValue.Value<string>());
                    finalResult.AddRange(result);
                }
                _settings.AddVariableList(keyName, finalResult);
            }
        }
    }
    
    private static void ParseJsonTasks(JObject json)
    {
        // Get the "Tasks" property as JArray, and only keep a list of JObjects
        var tasks = json.GetValue("Tasks") is JArray tasksArray ? tasksArray.Where(x => x is JObject).Select(x => x as JObject).ToList() : new List<JObject>();
        
        foreach (var task in tasks)
        {
            var taskName = ParseStringVariable(task.Value<string>("Title")).FirstOrDefault();
            List<USubtask> finalSubtasks = new List<USubtask>();

            var subTaskToken = task.GetValue("Subtasks");
            
            if (subTaskToken is JArray subtasksArray)
            {
                var subtasks = subtasksArray.Select(x => x.ToObject<USubtask>()).ToList();
                foreach (var subtask in subtasks)
                {
                    List<string> subtaskNames = new List<string>();
                    List<string> subtaskPrograms = new List<string>();
                    List<string> subtaskArgs = new List<string>();
                
                    subtaskNames.AddRange(ParseStringVariable(subtask.Msg));
                    subtaskPrograms.AddRange(ParseStringVariable(subtask.Program));
                    subtaskArgs.AddRange(ParseStringVariable(subtask.Args));
                
                    int maxCount = Math.Max(subtaskNames.Count, Math.Max(subtaskPrograms.Count, subtaskArgs.Count));
                    for (int i = 0; i < maxCount; i++)
                    {
                        string msg = subtaskNames.ElementAtOrDefault(i);
                        string program = subtaskPrograms.ElementAtOrDefault(i);
                        string args = subtaskArgs.ElementAtOrDefault(i);
                    
                        if (subtaskNames.IsValidIndex(i) == false)
                        {
                            msg = subtaskNames.LastOrDefault();
                        }
                        if (subtaskPrograms.IsValidIndex(i) == false)
                        {
                            program = subtaskPrograms.LastOrDefault();
                        }
                        if (subtaskArgs.IsValidIndex(i) == false)
                        {
                            args = subtaskArgs.LastOrDefault();
                        }
                    
                        finalSubtasks.Add(new USubtask { Msg = msg, Program = program, Args = args });
                    }   
                }
            }
            else if (subTaskToken?.Type == JTokenType.String)
            {
                var getSubtasksInstance = _getSubtasks.FirstOrDefault(x => x.GetType().Name == subTaskToken.Value<string>());
                finalSubtasks = getSubtasksInstance?.Invoke() ?? new List<USubtask>();
            }

            var id = task.Value<string>("Id");
            var subtasksFilterNames = task.GetValue("SubtasksFilters") is JArray subtasksFilterNamesArray ? subtasksFilterNamesArray.Select(x => x.Value<string>()).ToList() : new List<string>();
            var subtasksFilters = _subtaskFilters.Where(x => subtasksFilterNames.Contains(x.GetType().Name)).ToList();
            var subtaskAction = _subtaskActions.FirstOrDefault(x => x.GetType().Name == task.Value<string>("SubtasksAction"));
            var taskPreAction = _taskPreActions.FirstOrDefault(x => x.GetType().Name == task.Value<string>("TaskPreAction"));
            var taskPostAction = _taskPostActions.FirstOrDefault(x => x.GetType().Name == task.Value<string>("TaskPostAction"));

            UTask utask = new UTask
            {
                Id = id,
                Title = taskName,
                Subtasks = finalSubtasks,
                SubtaskFilters = subtasksFilters,
                SubtasksAction = subtaskAction,
                TaskPreAction = taskPreAction,
                TaskPostAction = taskPostAction
            };
                    
            _settings.Tasks.Add(utask);
        }
    }
    
    private static void ParseJsonAutoTasks(JObject json)
    {
        // Simply parse the AutoTasks property and add them to the settings
        _settings.AutoTasks = json.GetValue("AutoTasks") is JArray autoTasksArray ? autoTasksArray.Where(x => x is JObject).Select(x => x.ToObject<UAutoTask>()).ToList() : new List<UAutoTask>();
    }

    private static void ParseJsonEnginePath(string uprojectFilePath)
    {
        // Parse .uproject file and get "EngineAssociation" value
        string jsonString = File.ReadAllText(uprojectFilePath);
        JObject uprojectJson = JsonConvert.DeserializeObject<JObject>(jsonString);
        string engineAssociation = uprojectJson.Value<string>("EngineAssociation");
        string engineAssociationGuid = engineAssociation.Replace("{", "").Replace("}", "");
        string enginePath = "";

        // If it's a GUID, we're dealing with a custom engine
        if (Guid.TryParse(engineAssociationGuid, out _))
        {
            // Get the data from HKEY_CURRENT_USER\SOFTWARE\Epic Games\Unreal Engine\Builds
            string subKey = @"SOFTWARE\\Epic Games\\Unreal Engine\\Builds";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey);
            enginePath = Path.Combine(key.GetValue(engineAssociation).ToString(), "Engine");
            enginePath = Path.GetFullPath(enginePath);
            
        }
        // If it's a number, then we're dealing with an epic build
        else
        {
            // Get the data from Computer\HKEY_LOCAL_MACHINE\SOFTWARE\EpicGames\Unreal Engine\{engineAssociation} ("InstalledDirectory")
            string subKey = @"SOFTWARE\\EpicGames\\Unreal Engine\\" + engineAssociation;
            RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey);
            enginePath = Path.Combine(key.GetValue("InstalledDirectory").ToString(), "Engine");
            enginePath = Path.GetFullPath(enginePath);
        }
        
        _settings.SetVariable("{ENGINE_PATH}", enginePath);
    }
    
    private static List<string> ParseStringVariable(string input)
    {
        // Replace variables for their values
        List<string> parsedVariableResults = new List<string>();
        
        // Replace single ${} variables
        foreach (var (key, values) in _settings.StringVariables)
        {
            if (input.Contains("$"+key))
            {
                input = input.Replace("$"+key, values.FirstOrDefault());
            }
        }
        
        // Replace multiple %{} variables, only the first match will be replaced
        // TODO support more matches per input?
        foreach (var (key, values) in _settings.StringVariables)
        {
            if (input.Contains("%"+key))
            {
                foreach (var value in values)
                {
                    parsedVariableResults.Add(input.Replace("%"+key, value));
                }
            }
        }

        if (parsedVariableResults.Count == 0)
        {
            parsedVariableResults.Add(input);
        }

        // Apply pattern matching
        List<string> parsedPatternResults = new List<string>();
        foreach (var result in parsedVariableResults)
        {
            // Perform a glob pattern search for the content within "${content}"
            // TODO - to support multiple pattern matching it should work with a list of patterns found instead of the first
            // match ${} for single result
            // match %{} for multiple results
            Match? match = Regex.Matches(result, @"(?<=\${)(.*?)(?=\})").FirstOrDefault();
            bool bPerformSingleMatching = match != null && match.Success;
            if (bPerformSingleMatching == false)
            {
                match = Regex.Matches(result, @"(?<=\%{)(.*?)(?=\})").FirstOrDefault();
            }
            bool bPerformMultipleMatching = match != null && match.Success;
            
            if (bPerformSingleMatching || bPerformMultipleMatching)
            {
                List<string> tokens = match.Value.Split("->").ToList();
                string path = tokens.First().TrimEnd();
                List<string> patterns = tokens.Skip(1).Select(x => x.Trim()).ToList();
                
                // Add includes and excludes patterns
                var matcher = new Matcher();
                foreach (var pattern in patterns)
                {
                    if (pattern.Contains("!"))
                    {
                        matcher.AddExclude(pattern.Replace("!", ""));
                    }
                    else
                    {
                        matcher.AddInclude(pattern);
                    }
                }
                
                var matchResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(path)));
                if (matchResult.HasMatches)
                {
                    if (bPerformSingleMatching)
                    {
                        parsedPatternResults.Add(result.Replace("${"+match.Value+"}", matchResult.Files.FirstOrDefault().Path));
                    }
                    else
                    {
                        foreach (var file in matchResult.Files)
                        {
                            parsedPatternResults.Add(result.Replace("%{"+match.Value+"}", file.Path));
                        }    
                    }
                }
                else
                {
                    parsedPatternResults.Add(result);
                }
            }
            else
            {
                parsedPatternResults.Add(result);
            }
        }
        
        // Apply methods
        List<string> parsedMethodsResults = new List<string>();
        foreach (string result in parsedPatternResults)
        {
            if (result.Contains("->"))
            {
                var split = result.Split("->");
                string value = split[0];
                var methods = split.Skip(1).ToList();
                
                foreach (string method in methods)
                {
                    // get method name and arguments
                    string methodName = method.Split("(")[0];
                    var methodArgs = method.Split("(")[1].Replace(")", "").Replace("'", "").Split(",").Select(x => x.Trim()).ToArray();
                    
                    value = (string)Helper.CallMethodByName(methodName, value, methodArgs);
                }
                parsedMethodsResults.Add(value);
            }
            else
            {
                parsedMethodsResults.Add(result);
            }
        }
        
        // Try to convert to valid paths
        List<string> finalResults = new List<string>();
        foreach (var result in parsedMethodsResults)
        {
            if (result.IndexOfAny(Path.GetInvalidPathChars()) == -1 && Path.IsPathRooted(result))
            {
                finalResults.Add(Path.GetFullPath(result));
            }
            else
            {
                finalResults.Add(result);
            }
        }

        return finalResults;
    }
    
    private static bool HandleSelectedTask(string selectedTask)
    {
        try
        {
            int index = int.Parse(selectedTask) - 1;
            var task = _settings.Tasks[index];
            
            // filter subtasks
            var subtasks = task.Subtasks;
            foreach (var subtaskFilter in task.SubtaskFilters)
            {
                subtasks = subtaskFilter?.Invoke(task.Title, subtasks);
            }
            
            // Exit if no subtasks exist after all the filters have been applied
            if (subtasks.Count == 0)
            {
                Helper.Log("No tasks to perform, returning to the main menu...", LogType.Warning);
                Helper.Log("\n### Press <Enter> to Continue ###", LogType.Info);
                Console.ReadLine(); // halt
                Console.Clear();
                OpenMainMenu();
            }
            
            // Run the preAction
            task.TaskPreAction?.Invoke(task.Title);
            
            // Run subtasks after filters have been applied
            var (successSubtasks, failedSubtasks) = task.SubtasksAction.Invoke(task.Title, subtasks);

            // Handle failure of SubtasksAction
            if (successSubtasks.Count == 0 && failedSubtasks.Count == 0)
            {
                Console.Clear();
                Helper.Log("Returning to the main menu...", LogType.Warning);
                OpenMainMenu();
            }
            // handle success and failed tasks
            if (successSubtasks.Count > 0)
            {
                Helper.Log("");
                Helper.Log($"### Successful Tasks for: {task.Title} ###\n", LogType.Info);
                foreach (var subtask in successSubtasks)
                {
                    Helper.Log($"- {subtask.Msg}");
                }
                Helper.Log("");
            }
            if (failedSubtasks.Count > 0)
            {
                Helper.Log("");
                Helper.Log($"### Failed Tasks for: {task.Title} ###\n", LogType.Info);
                foreach (var subtask in failedSubtasks)
                {
                    Helper.Log($"- {subtask.Msg}", LogType.Error);
                }
                Helper.Log("");
            }
            
            // run postAction
            task.TaskPostAction?.Invoke(task.Title, successSubtasks, failedSubtasks);
            return true;
        }
        catch (Exception e)
        {
            Helper.Log(e.Message, LogType.Error);
            Helper.Log("\n### Press <Enter> to Continue ###", LogType.Info);
            Console.ReadLine();
            Console.Clear();
            OpenMainMenu();
            return false;
        }
    }
    
    public static string[] ParseArgs(string[] args)
    {
        // Convert auto task steps to their respective task steps
        List<string> steps = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value.StartsWith("a="))
            {
                value = value.Replace("a=", "");
                var autoTaskSteps = _settings.AutoTasks.FirstOrDefault(x => x.Id == value)?.Steps;
                foreach (var autoTaskStep in autoTaskSteps)
                {
                    var tokenSteps = autoTaskStep.Split(" ");
                    steps.AddRange(tokenSteps);
                    if (autoTaskSteps.Last() != autoTaskStep)
                    {
                        steps.Add(",");
                    }
                }
            }
            else
            {
                steps.Add(value);
            }
        }
        return steps.ToArray();
    }
    
    public static void HandleTaskArgs(string[] args)
    {
        List<string> steps = new List<string>();
        _settings.BufferedInputs.Clear();
        
        for (int i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (value == ",")
            {
                break;
            }
            if (value == "<enter>")
            {
                value = "";
            }
            steps.Add(value);
        }
        
        args = args.Skip(steps.Count + 1).ToArray();
        var taskId = steps.FirstOrDefault();
        var task = _settings.Tasks.FirstOrDefault(x => x.Id == taskId);
        var taskIndex = (_settings.Tasks.IndexOf(task) + 1).ToString();
        steps = steps.Skip(1).ToList();
        
        if (task != null)
        {
            foreach (var step in steps)
            {
                _settings.BufferedInputs.Enqueue(step);
            }
            if (HandleSelectedTask(taskIndex))
            {
                HandleTaskArgs(args);
            }
        }
    }

    private static void Debug_PrintHelp()
    {
        Helper.Log("\n\n");
        Debug_PrintParsedVariables();
        Debug_PrintTasks();
        Debug_PrintAllScripts();
        Helper.Log("\n\n");
    }
    
    private static void Debug_PrintParsedVariables()
    {
        Helper.Log($"**************************************************************************************", LogType.Info);
        Helper.Log($"\nVARIABLES PARSED FROM utasks.jsonc\n", LogType.Info);
        Helper.Log($"**************************************************************************************", LogType.Info);
        foreach (var (key, values) in _settings.StringVariables)
        {
            Debug_PrintKeyValues(key, values);
        }
    }
    
    private static void Debug_PrintTasks()
    {
        Helper.Log($"**************************************************************************************", LogType.Info);
        Helper.Log($"\nTASKS PARSED FROM utasks.jsonc\n", LogType.Info);
        Helper.Log($"**************************************************************************************", LogType.Info);
        foreach (var task in _settings.Tasks)
        {
            Helper.Log($"`Title` : ", LogType.Info, false);
            Helper.Log($"`{task.Title}`\n", newLine: false);
            Helper.Log($"`TaskPreAction` : ", LogType.Info, false);
            Helper.Log($"`{task.TaskPreAction?.GetType().Name}`\n", newLine: false);
            Helper.Log($"`TaskPostAction` : ", LogType.Info, false);
            Helper.Log($"`{task.TaskPostAction?.GetType().Name}`\n", newLine: false);
            Helper.Log($"`SubtasksAction` : ", LogType.Info, false);
            Helper.Log($"`{task.SubtasksAction?.GetType().Name}`\n", newLine: false);
            Debug_PrintKeyValues("SubtasksFilters", task.SubtaskFilters.Select(x => x.GetType().Name).ToList());
            
            Helper.Log($"`Subtasks` : {task.Subtasks.Count}", LogType.Info);
            Helper.Log("{", LogType.Info);
            foreach (var subtask in task.Subtasks)
            {
                Helper.Log($"   `Msg` : ", LogType.Info, false);
                Helper.Log($"`{subtask.Msg}`\n", newLine: false);
                Helper.Log($"   `Program` : ", LogType.Info, false);
                Helper.Log($"`{subtask.Program}`\n", newLine: false);
                Helper.Log($"   `Args` : ", LogType.Info, false);
                Helper.Log($"`{subtask.Args}`\n", newLine: false);
                if (task.Subtasks.Last() != subtask)
                {
                    Helper.Log("    -----------------------------------------------------------------------------------", LogType.Info);
                }
            }
            Helper.Log("}", LogType.Info);

            Helper.Log($"**************************************************************************************", LogType.Info);
        }
    }

    private static void Debug_PrintAllScripts()
    {
        Helper.Log($"**************************************************************************************", LogType.Info);
        Helper.Log($"\nSCRIPTS AVAILABLE\n", LogType.Info);
        Helper.Log($"**************************************************************************************", LogType.Info);
        
        Debug_PrintKeyValues("TaskPreActions", _taskPreActions.Select(x => x.GetType().Name).ToList());
        Debug_PrintKeyValues("TaskPostActions", _taskPostActions.Select(x => x.GetType().Name).ToList());
        Debug_PrintKeyValues("SubtaskFilters", _subtaskFilters.Select(x => x.GetType().Name).ToList());
        Debug_PrintKeyValues("SubtaskActions", _subtaskActions.Select(x => x.GetType().Name).ToList());
        Debug_PrintKeyValues("GetSubtasks", _getSubtasks.Select(x => x.GetType().Name).ToList());
    }

    private static void Debug_PrintKeyValues(string key, List<string> values)
    {
        Helper.Log($"`{key}` : ", LogType.Info, false);
        
        if (values.Count == 0)
        {
            Helper.Log($"No values", LogType.Warning);
            return;
        }
        
        Helper.Log("[ ", newLine: false);
        List<string> warnings = new();
        foreach (var value in values)
        {
            Helper.Log($"`{value}`", newLine: false);
            if (Helper.IsDirOrFile(value) && !Helper.DoesDirOrFileExist(value))
            {
                warnings.Add($"`{value} appears to be a file or directory, but it doesn't exist`");
            }
            if (values.Last() != value)
            {
                Helper.Log($", ", newLine: false);
            }
        }
        Helper.Log(" ]", newLine: false);
        Helper.Log("");
        foreach (var warning in warnings)
        {
            Helper.Log(warning, LogType.Warning);
        }
    }
}