namespace utasks.SubtaskFilters;

public class FilterByInput : ISubtaskFilter
{
    private readonly USettings _settings;

    public FilterByInput(USettings settings)
    {
        _settings = settings;
    }
    
    public List<USubtask> Invoke(string taskTitle, List<USubtask> subtasks)
    {
        List<USubtask> result = new();
        
        if (subtasks.Count == 0)
        {
            Helper.Log($"No subtasks available for: `{this.GetType().Name}`", LogType.Warning);
            return subtasks; // skip filter if there are no subtasks
        }
        
        Helper.Log($"### Choose Actions to Perform for: {taskTitle} ### \n", LogType.Info);
        for (int idx = 0; idx < subtasks.Count; idx++)
        {
            var subtask = subtasks[idx];
            Helper.Log($"{idx + 1} - {subtask.Msg}");
        }
        Helper.Log("0 - Do all of the above actions");
        Helper.Log("", LogType.Info);
        Helper.LogIf(subtasks.Count > 2, "(Comma and intervals are supported, e.g. 1,3-5,7)", LogType.Info);
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
        Helper.Log("", LogType.Info);
        
        // do all actions if input is `0`
        if (chooseInput == "0")
        {
            return subtasks; // return all subtasks (skip filter)
        }
        
        var selectedIndexes = Helper.GetSelectedIndexesFromInput(chooseInput);
        
        // Apply the filter using the indexes obtained by the input
        try
        {
            foreach (var index in selectedIndexes)
            {
                // only add unique tasks
                var subtask = subtasks[index];
                if (!result.Contains(subtask))
                {
                    result.Add(subtask);
                }
            }
        }
        catch (Exception e)
        {
            Helper.Log($"Invalid input: {chooseInput}", LogType.Error);
            Helper.Log(e.Message, LogType.Error);
            result = new();
        }
        
        return result;
    }
}