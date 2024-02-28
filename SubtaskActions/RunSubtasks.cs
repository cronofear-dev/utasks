namespace utasks.SubtaskActions;

public class RunSubtasks : ISubtasksAction
{
    private readonly USettings _settings;

    public RunSubtasks(USettings settings)
    {
        _settings = settings;
    }
    
    public (List<USubtask> successSubtasks, List<USubtask> failedSubtasks) Invoke(string taskTitle, List<USubtask> subtasks)
    {
        var successSubtasks = new List<USubtask>();
        var failedSubtasks = new List<USubtask>();
        
        string workingDir = _settings.GetVariable("{CURRENT_PATH}");
        
        foreach (var subtask in subtasks)
        {
            if (Helper.RunConsoleCommand(subtask.Program, subtask.Args, subtask.Msg, workingDir))
            {
                successSubtasks.Add(subtask);
            }
            else
            {
                failedSubtasks.Add(subtask);
            }
        }
        return (successSubtasks, failedSubtasks);
    }
}