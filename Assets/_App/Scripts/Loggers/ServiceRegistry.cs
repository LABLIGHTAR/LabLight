using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Static class for accessing swappable services
/// -logger (or chain of loggers)
/// -Dataprovider
/// </summary>
public class ServiceRegistry
{
    /// </summary>
    private static Dictionary<Type, object> registry =  new Dictionary<Type, object>();
    private static Dictionary<Type, List<object>> multiRegistry = new Dictionary<Type, List<object>>();

    public static void RegisterService<T>(T service)
    {
        Type interfaceType = typeof(T);
        registry[interfaceType] = service;
        Debug.Log("ServiceRegistered" + service + " " + interfaceType);
    }

    public static void RegisterMultiService<T>(T service)
    {
        Type interfaceType = typeof(T);
        if (!multiRegistry.ContainsKey(interfaceType))
        {
            multiRegistry[interfaceType] = new List<object>();
        }
        multiRegistry[interfaceType].Add(service);
        Debug.Log("MultiServiceRegistered" + service + " " + interfaceType);
    }

    public static void UnRegisterService<T>()
    {
        Type interfaceType = typeof(T);
        registry.Remove(interfaceType);
    }

    public static T GetService<T>()
    {
        object service;

        Type interfaceType = typeof(T);
        if (registry.TryGetValue(interfaceType, out service))
        {
            return (T)service;
        }

        return default(T);
    }

    public static List<T> GetServices<T>()
    {
        Type interfaceType = typeof(T);
        if (multiRegistry.TryGetValue(interfaceType, out var services))
        {
            return services.Cast<T>().ToList();
        }

        return new List<T>();
    }

    /// <summary>
    /// Cached logger lookup
    /// </summary>
    private static LoggerImpl _logger;
    public static LoggerImpl Logger
    {
        get
        {
            if (_logger == null)
            {
                _logger = GetService<LoggerImpl>();
            }
            return _logger;
        }
    }
}