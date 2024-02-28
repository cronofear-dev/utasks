
namespace utasks;

public enum LogType : int
{
    Default = 15, // White
    Info = 11, // Cyan
    Warning = 14, // Yellow
    Error = 12 // Red
}

// what to do before the subtasks are executed
public interface ITaskPreAction
{
    void Invoke(string taskTitle);
}

// what to do after the subtasks have been executed, accept a list of subtasks that were successful and a list of subtasks that failed
public interface ITaskPostAction
{
    void Invoke(string taskTitle, List<USubtask> successSubtasks, List<USubtask> failedSubtasks);
}

// Filter to perform to a list of subtasks, the result is a filtered list of subtasks
public interface ISubtaskFilter
{
    List<USubtask> Invoke(string taskTitle, List<USubtask> subtasks);
}

// what to do with the subtasks after they have been filtered, the result is a list of subtasks that were successful and a list of subtasks that failed
public interface ISubtasksAction
{
    (List<USubtask> successSubtasks, List<USubtask> failedSubtasks) Invoke(string taskTitle, List<USubtask> subtasks);
}

public interface IGetSubtasks
{
    List<USubtask> Invoke();
}

public class USubtask
{
    public string Msg { get; set; }
    public string Program { get; set; }
    public string Args { get; set; }
    public object Metadata { get; set; }
}

public class UTask
{
    public string Id { get; set; }
    public string Title { get; set; }
    public ITaskPreAction? TaskPreAction { get; set; }
    public ITaskPostAction? TaskPostAction { get; set; }
    
    public List<USubtask> Subtasks { get; set; } = new List<USubtask>();
    public List<ISubtaskFilter?> SubtaskFilters { get; set; } = new List<ISubtaskFilter?>();
    public ISubtasksAction? SubtasksAction { get; set; }
    
    public object Metadata { get; set; }
}

public class UAutoTask
{
    public string Id { get; set; }
    public string Title { get; set; }
    public List<string> StepsDescriptions { get; set; } = new List<string>();
    public List<string> Steps { get; set; } = new List<string>();
}

public class USettings
{
    public Dictionary<string, List<string>> StringVariables { get; set; } = new Dictionary<string, List<string>>();
    public List<UTask> Tasks { get; set; } = new List<UTask>();
    public List<UAutoTask> AutoTasks { get; set; } = new List<UAutoTask>();
    
    public Queue<string> BufferedInputs = new Queue<string>();
    
    public string? GetVariable(string key)
    {
        if (StringVariables.ContainsKey(key))
        {
            return StringVariables[key].FirstOrDefault();
        }
        return null;
    }
    
    public List<string> GetVariableList(string key)
    {
        if (StringVariables.ContainsKey(key))
        {
            return StringVariables[key];
        }
        return new List<string>();
    }
    
    public void SetVariable(string key, string value)
    {
        if (StringVariables.ContainsKey(key))
        {
            StringVariables[key].Clear();
            StringVariables[key].Add(value);
        }
        else
        {
            StringVariables.Add(key, new List<string> { value });
        }
    }
    
    public void SetVariableList(string key, List<string> value)
    {
        if (StringVariables.ContainsKey(key))
        {
            StringVariables[key].Clear();
            StringVariables[key].AddRange(value);
        }
        else
        {
            StringVariables.Add(key, value);
        }
    }
    
    public void AddVariable(string key, string value)
    {
        if (StringVariables.ContainsKey(key))
        {
            StringVariables[key].Add(value);
        }
        else
        {
            StringVariables.Add(key, new List<string> { value });
        }
    }
    
    public void AddVariableList(string key, List<string> value)
    {
        if (StringVariables.ContainsKey(key))
        {
            StringVariables[key].AddRange(value);
        }
        else
        {
            StringVariables.Add(key, value);
        }
    }
    
    public void ClearSettings()
    {
        StringVariables.Clear();
        Tasks.Clear();
        AutoTasks.Clear();
        BufferedInputs.Clear();
    }
}
