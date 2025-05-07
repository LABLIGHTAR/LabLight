using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// POCO that mirrors the cross-platform checkpoint JSON schema.
/// </summary>
[Serializable]
public class CheckpointState
{
    // --- Top-level metadata --------------------------------------------------
    public const int CurrentSchemaVersion = 1;

    [JsonProperty("schemaVersion")]        public int SchemaVersion        = CurrentSchemaVersion;
    [JsonProperty("sessionID")]            public Guid SessionID          = Guid.NewGuid();
    [JsonProperty("userID")]               public string UserID;
    [JsonProperty("protocolName")]         public string ProtocolName;
    [JsonProperty("protocolVersion")]      public string ProtocolVersion;
    [JsonProperty("startTimestamp")]       public DateTime StartTimestamp = DateTime.UtcNow;
    [JsonProperty("completionTimestamp")]  public DateTime? CompletionTimestamp;

    // --- Per-step progress ---------------------------------------------------
    [JsonProperty("steps")] public List<StepProgress> Steps = new List<StepProgress>();

    // --- Optional review / audit meta ---------------------------------------
    [JsonProperty("reviewMeta")] public Dictionary<string, object> ReviewMeta = new ();

    // ----------------- Nested types -----------------------------------------
    [Serializable]
    public class StepProgress
    {
        [JsonProperty("stepIndex")]      public int StepIndex;
        [JsonProperty("title")]          public string Title;
        [JsonProperty("startTime")]      public DateTime StartTime;
        [JsonProperty("signoffTime")]    public DateTime? SignoffTime;
        [JsonProperty("signoffUserID")]  public string SignoffUserID;
        [JsonProperty("checkItems")]     public List<CheckItemProgress> CheckItems = new();
    }

    [Serializable]
    public class CheckItemProgress
    {
        [JsonProperty("index")]          public int Index;
        [JsonProperty("text")]           public string Text;
        [JsonProperty("completedTime")]  public DateTime? CompletedTime;
        [JsonProperty("completedBy")]    public string CompletedBy;
    }
}