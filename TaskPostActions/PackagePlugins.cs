namespace utasks.TaskPostActions;
using System.IO.Compression;

public class PackagePlugins : ITaskPostAction
{
    private readonly USettings _settings;

    public PackagePlugins(USettings settings)
    {
        _settings = settings;
    }
    
    public void Invoke(string taskTitle, List<USubtask> successSubtasks, List<USubtask> failedSubtasks)
    {
        // handle failed subtasks
        foreach (var plugin in failedSubtasks)
        {
            var failedPluginOutputDir = plugin.Metadata as string;
            Helper.RemoveDir(failedPluginOutputDir);
        }
        // handle successtasks
        if (successSubtasks.Count > 0)
        {
            HandleSuccessfullyBuiltPlugins(successSubtasks);
        }
    }
    
    public void HandleSuccessfullyBuiltPlugins(List<USubtask> builtPluginTasks)
    {
        var pluginOutputPath = _settings.GetVariable("{PluginsOutputPath}");
        var workingDir = _settings.GetVariable("{CURRENT_PATH}");
        var zippedPluginsOutputPath = _settings.GetVariable("{ZippedPluginsOutputPath}");
        bool hasUsedBufferedInputs = false;
        
        Helper.Log("");
        Helper.Log("### Choose Actions for the Plugins that were Packaged Successfully ### \n", LogType.Info);
        Helper.Log("1 - Zip Compiled Plugin (Keep `Intermediate` and `Binaries` folder)");
        Helper.Log("2 - Zip Source Files Only (Delete `Intermediate` and `Binaries` folder)");
        Helper.Log("3 - Delete Packaged Plugins in: " + pluginOutputPath);
        Helper.Log("0 - Do all of the above actions");
        Helper.Log("", LogType.Info);
        Helper.Log("(Comma and intervals are supported, e.g. 1,3)", LogType.Info);
        Helper.Log("(Choose an Action or Press <Enter> to continue and exit)", LogType.Info);
        Helper.Log("Action: ", LogType.Info);
        var chooseInput = "";
        if (_settings.BufferedInputs.Count > 0)
        {
            hasUsedBufferedInputs = true;
            chooseInput = _settings.BufferedInputs.Dequeue();
            Console.WriteLine(chooseInput);
        }
        else
        {
            chooseInput = Console.ReadLine();
        }
        Helper.Log("");
        
        var outputFolderToOpen = pluginOutputPath;

        var selectedIndexes = new List<int>();
        if (chooseInput == "0") // do all actions
        {
            selectedIndexes = new List<int> { 0, 1, 2 };
        }
        else if (string.IsNullOrEmpty(chooseInput))
        {
            // Do nothing
        }
        else
        {
            var curIndexes = Helper.GetSelectedIndexesFromInput(chooseInput);
            if (curIndexes.Count == 0) // Rerun if the input is wrong, only exit if the input is empty or succesful
            {
                HandleSuccessfullyBuiltPlugins(builtPluginTasks);
            }

            foreach (var index in curIndexes)
            {
                if (index < 0 || index > 2) // out of supported range index
                {
                    Helper.Log("Invalid input: " + chooseInput, LogType.Error);
                    HandleSuccessfullyBuiltPlugins(builtPluginTasks);
                    return;
                }

                selectedIndexes.Add(index);
            }
        }

        if (selectedIndexes.Count == 0)
        {
            Helper.OpenFolder(pluginOutputPath, workingDir);
            Console.Clear();
            Helper.Log("No actions selected, returning to the main menu...", LogType.Warning);
            return;
        }

        foreach (var pluginTask in builtPluginTasks)
        {
            var packagedPluginPath = pluginTask.Metadata as string;
            
            if (selectedIndexes.Contains(0)) // Task 1 (Zip Compiled)
            {
                var zipFilePath = Path.Combine(zippedPluginsOutputPath, Path.GetFileName(packagedPluginPath) + "_Compiled.zip");
                ZipFile.CreateFromDirectory(packagedPluginPath, zipFilePath);
                outputFolderToOpen = zippedPluginsOutputPath;
            }
            
            if (selectedIndexes.Contains(1)) // Task 2 (Delete Intermediate and Binaries, then Zip)
            {
                Helper.RemoveDir(Path.Combine(packagedPluginPath, "Intermediate"));
                Helper.RemoveDir(Path.Combine(packagedPluginPath, "Binaries"));
                var zipFilePath = Path.Combine(zippedPluginsOutputPath, Path.GetFileName(packagedPluginPath) + ".zip");
                ZipFile.CreateFromDirectory(packagedPluginPath, zipFilePath);
                outputFolderToOpen = zippedPluginsOutputPath;
            }
            
            if (selectedIndexes.Contains(2)) // Task 3 (Delete Packaged Plugins)
            {
                Helper.RemoveDir(packagedPluginPath);
            }
        }
        
        Helper.OpenFolder(outputFolderToOpen, workingDir);
        Helper.Log("Returning to the main menu...", LogType.Warning);
        if (!hasUsedBufferedInputs)
        {
            Console.ReadLine();
            Console.Clear();
        }
    }
}