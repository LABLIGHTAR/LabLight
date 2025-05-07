using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Defines CRUD operations for checkpoint-style save files.
/// All methods are asynchronous and must NEVER touch Unity objects
/// to stay thread-safe.
/// </summary>
public interface ICheckpointDataProvider
{
    /// <summary>Persist a new state file. Overwrites if the same sessionID already exists.</summary>
    Task SaveStateAsync(CheckpointState state);

    /// <summary>Load all unfinished sessions for a user / protocol combination.</summary>
    Task<IReadOnlyList<CheckpointState>> LoadStatesAsync(string protocolName, string userID);

    /// <summary>Delete an existing checkpoint file by sessionID.</summary>
    Task DeleteStateAsync(Guid sessionID);

    /// <summary>Update an existing checkpoint file (helper for incremental writes).</summary>
    Task UpdateStateAsync(CheckpointState state);
}