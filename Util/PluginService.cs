using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;

namespace ApiGateway.Util;

public interface IPluginService
{
    List<PluginInfo> GetPluginNames();
    void LoadPlugins(IOcelotBuilder ocelotBuilder);
}

public class PluginService : IPluginService
{
    private readonly List<PluginInfo> _plugins = new();
    private readonly string _pluginsPath;
    private readonly IServiceProvider _serviceProvider;

    public PluginService(string pluginsPath, IServiceProvider serviceProvider)
    {
        _pluginsPath = pluginsPath;
        _serviceProvider = serviceProvider;
    }

    public List<PluginInfo> GetPluginNames()
    {
        return _plugins;
    }

    public void LoadPlugins(IOcelotBuilder ocelotBuilder)
    {
        if (!Directory.Exists(_pluginsPath))
            return;

        var assemblies = LoadPluginAssemblies(_pluginsPath);
        try
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(IsPluginType);
                foreach (var type in types)
                {
                    if (TryGetPluginMetadata(type, out var isGlobal, out var name))
                    {
                        ocelotBuilder.AddDelegatingHandler(type, isGlobal);
                        _plugins.Add(new PluginInfo()
                        {
                            Name = name,
                            ClassName = type.Name
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        
    }

    private static bool IsPluginType(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;

        if (!typeof(DelegatingHandler).IsAssignableFrom(type))
            return false;

        return HasPluginContract(type);
    }

    private static bool HasPluginContract(Type type)
    {
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        var globalProp = type.GetProperty("IsGlobal", BindingFlags.Public | BindingFlags.Instance);

        if (nameProp == null || nameProp.PropertyType != typeof(string))
            return false;

        if (globalProp == null || globalProp.PropertyType != typeof(bool))
            return false;

        return true;
    }

    private bool TryGetPluginMetadata(Type type, out bool isGlobal, out string name)
    {
        isGlobal = false;
        name = type.Name;

        var instance = ActivatorUtilities.CreateInstance(_serviceProvider, type);
        if (instance == null)
            return false;

        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        var globalProp = type.GetProperty("IsGlobal", BindingFlags.Public | BindingFlags.Instance);

        if (nameProp?.GetValue(instance) is string pluginName && !string.IsNullOrWhiteSpace(pluginName))
            name = pluginName;

        if (globalProp?.GetValue(instance) is bool globalValue)
            isGlobal = globalValue;
        else
            return false;

        return true;
    }

    private IEnumerable<Assembly> LoadPluginAssemblies(string path)
    {
        foreach (var dll in Directory.GetFiles(path, "*.dll"))
        {
            yield return Assembly.LoadFrom(dll);
        }
    }
}
