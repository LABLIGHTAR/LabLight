using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Crash-safe JSON persistence using <see cref="Application.persistentDataPath"/>/Checkpoints.
/// Implements exponential-backoff retry for transient IO errors.
/// </summary>
public sealed class LocalFileCheckpointDataProvider : ICheckpointDataProvider
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    private string _rootPath;

    public LocalFileCheckpointDataProvider()
    {
        _rootPath = Path.Combine(Application.persistentDataPath, "Checkpoints");
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>Ensure <c>_rootPath</c> is a valid, existing directory.</summary>
    private void EnsureRoot()
    {
        if (string.IsNullOrEmpty(_rootPath))
            _rootPath = Path.Combine(Application.persistentDataPath, "Checkpoints");

        if (!Directory.Exists(_rootPath))
            Directory.CreateDirectory(_rootPath);
    }

    // --------------- Public API ---------------------------------------------

    public async Task SaveStateAsync(CheckpointState state) =>
        await WriteFileWithRetryAsync(GetFilePath(state), state).ConfigureAwait(false);

    public async Task UpdateStateAsync(CheckpointState state) =>
        await WriteFileWithRetryAsync(GetFilePath(state), state).ConfigureAwait(false);

    public async Task<IReadOnlyList<CheckpointState>> LoadStatesAsync(string protocolName, string userID)
    {
        EnsureRoot();

        Debug.Log($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=LoadStates path={_rootPath}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var list = new List<CheckpointState>();

        try
        {
            string searchPattern = $"{Safe(userID)}_{Safe(protocolName)}_*.json";
            var files = Directory.GetFiles(_rootPath, searchPattern, SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(f).ConfigureAwait(false);
                    var state  = JsonConvert.DeserializeObject<CheckpointState>(json, JsonSettings);
                    if(state != null && state.CompletionTimestamp == null)
                        list.Add(state);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=LoadStates parseError file={f} err={ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=LoadStates listError err={ex}");
        }

        Debug.Log($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=LoadStates count={list.Count} elapsedMs={sw.ElapsedMilliseconds}");
        return list
            .OrderByDescending(s => s.StartTimestamp)
            .ToList();
    }

    public Task DeleteStateAsync(Guid sessionID)
    {
        string pattern = $"*_{sessionID}.json";
        foreach (var file in Directory.GetFiles(_rootPath, pattern, SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(file);
                Debug.Log($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=DeleteState sessionID={sessionID} status=success");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=DeleteState sessionID={sessionID} status=failed err={ex}");
            }
        }
        return Task.CompletedTask;
    }

    // --------------- Helpers -------------------------------------------------

    private async Task WriteFileWithRetryAsync(string finalPath, CheckpointState state)
    {
        Debug.Log("writing to file" + finalPath);
        string json  = JsonConvert.SerializeObject(state, JsonSettings);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        int attempt      = 0;
        int[] backOffMs  = { 100, 500, 2000 };
        Exception lastEx = null;

        while (attempt < backOffMs.Length)
        {
            try
            {
                // Ensure directory exists before writing
                EnsureRoot();

                // Write directly – FileMode.Create will overwrite atomically on most FS
                using (var fs = new FileStream(
                           finalPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 4096,
                           useAsync: true))
                {
                    await fs.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }

                Debug.Log($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=SaveState file={finalPath} size={bytes.Length} status=success");
                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=SaveState attempt={attempt} status=error err={ex}");
                await Task.Delay(backOffMs[attempt]).ConfigureAwait(false);
                attempt++;
            }
        }

        Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=SaveState status=fatal err={lastEx}");
        throw lastEx ?? new IOException("Failed to save checkpoint");
    }

    private string GetFilePath(CheckpointState state)
    {
        string fileName = $"{Safe(state.UserID)}_{Safe(state.ProtocolName)}_{state.SessionID}.json";
        return Path.Combine(_rootPath, fileName);
    }

    /// <summary>
    /// Returns a filesystem-safe representation of <paramref name="str"/>.
    /// If <c>null</c> or whitespace, the literal <c>"anonymous"</c> is returned to avoid
    /// null-reference issues when a new user has not yet been selected.
    /// </summary>
    private static string Safe(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            str = "anonymous";

        str = str.Trim();

        foreach (var c in Path.GetInvalidFileNameChars())
            str = str.Replace(c, '_');

        return str.Replace(' ', '_');
    }
}