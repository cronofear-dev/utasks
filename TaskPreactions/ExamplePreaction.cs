namespace utasks.TaskPreactions;

public class ExamplePreaction : ITaskPreAction
{
    public void Invoke(string taskTitle)
    {
        Helper.Log($"Task '{taskTitle}' is about to start");
    }
}