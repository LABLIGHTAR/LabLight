using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Interface for accessing protocol summary and the actual protocol definition
/// </summary>
public interface IProtocolDataProvider
{
    /// <summary>
    /// Retrieve list of available protocols as short ProtocolItem
    /// </summary>
    /// <returns></returns>
    public Task<List<ProtocolDefinition>> GetProtocolList();

    //void DeleteProtocolDefinition(string protocolName);
}