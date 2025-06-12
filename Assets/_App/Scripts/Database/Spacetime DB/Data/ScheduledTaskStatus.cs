/// <summary>
/// Represents the status of a scheduled protocol task, mirroring the server-side enum.
/// </summary>
public enum ScheduledTaskStatus
{
    Pending,
    InProgress,
    Completed,
    Overdue,
    Cancelled
} 