namespace utasks.GetSubtasks;

public class GetPluginSubtasks : IGetSubtasks
{
    private readonly USettings _settings;

    public GetPluginSubtasks(USettings settings)
    {
        _settings = settings;
    }
    
    public List<USubtask> Invoke()
    {
        var result = new List<USubtask>();
        foreach (var file in GetPluginFiles())
        {
            var dir = Path.GetDirectoryName(file); // C:/Path/To/Plugin
            var fileName = Path.GetFileNameWithoutExtension(file); // CSPluginName
            var pluginFolderName = Directory.GetParent(file).Name; // Parent folder where CSPluginName.uplugin lives
            var pluginOutputFolder = Path.Combine(_settings.GetVariable("{PluginsOutputPath}"), pluginFolderName);

            // Make command for all plugins
            var msg = $"{fileName} ({pluginFolderName})";
            var program = _settings.GetVariable("{RunUAT_bat}");
            var args = $"BuildPlugin -Plugin=\"{file}\" -Package=\"{pluginOutputFolder}\" {_settings.GetVariable("{PluginPackageArgs}")}";
            
            result.Add(new USubtask
            {
                Msg = msg,
                Program = program,
                Args = args,
                Metadata = pluginOutputFolder
            });
        }
        return result;
    }
    
    public List<string> GetPluginFiles()
    {
        // pluginsFiles are in relative path from Project/Plugins (by default)
        return _settings.GetVariableList("{PluginNames}");
    }
}