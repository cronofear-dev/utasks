namespace utasks.SubtaskFilters;

public class FilterByInputSingleOption : ISubtaskFilter
{
    private readonly USettings _settings;

    public FilterByInputSingleOption(USettings settings)
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
        
        Helper.Log($"### Choose Action to Perform for: {taskTitle} ### \n", LogType.Info);
        for (int idx = 0; idx < subtasks.Count; idx++)
        {
            var subtask = subtasks[idx];
            Helper.Log($"{idx + 1} - {subtask.Msg}");
        }
        Helper.Log("", LogType.Info);
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
        
        var selectedIndexes = Helper.GetSelectedIndexesFromInput(chooseInput);

        
        // Apply the filter using the indexes obtained by the input
        try
        {
            if (selectedIndexes.Count == 1)
            {
                var index = selectedIndexes.First();
                var subtask = subtasks[index];
                result.Add(subtask);
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