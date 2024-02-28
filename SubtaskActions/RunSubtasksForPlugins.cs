namespace utasks.SubtaskActions;

public class RunSubtasksForPlugins : ISubtasksAction
{
    private readonly USettings _settings;
    private readonly RunSubtasks _runSubtasks;

    public RunSubtasksForPlugins(USettings settings, RunSubtasks runSubtasks)
    {
        _settings = settings;
        _runSubtasks = runSubtasks;
    }
    
    public (List<USubtask> successSubtasks, List<USubtask> failedSubtasks) Invoke(string taskTitle, List<USubtask> subtasks)
    {
        var pluginOutputPath = _settings.GetVariable("{PluginsOutputPath}");
        
        Helper.Log("The following plugins will be packaged:\n", LogType.Info);
        foreach (var subtask in subtasks)
        {
            Helper.Log("- " + subtask.Msg);
        }
        Helper.Log("");
        Helper.Log("WARNING: This operation will replace the selected plugins in:", LogType.Warning);
        Helper.Log(pluginOutputPath);
        Helper.Log("Do you want to continue?", LogType.Info);
        Helper.Log("(Type <N> to cancel or Press <Enter> to continue): ", LogType.Info);
        Helper.Log("Action: ", LogType.Info);
        var chooseInput = "";
        if (_settings.BufferedInputs.Count > 0)
        {
            chooseInput = _settings.BufferedInputs.Dequeue();
            Console.WriteLine(chooseInput);
        }
        else
        {
            chooseInput = Console.ReadLine();
        }
        Helper.Log("");

        // remove existing plugin folders before continuing
        if (chooseInput.ToLower() != "n")
        {
            foreach (var subtask in subtasks)
            {
                var pluginOutputFolder = subtask.Metadata as string;
                Helper.RemoveDir(pluginOutputFolder);
            }
        }
        else
        {
            // cancel the operation
            return (new List<USubtask>(), subtasks);
        }

        // Run subtasks
        return _runSubtasks.Invoke(taskTitle, subtasks);
    }
}