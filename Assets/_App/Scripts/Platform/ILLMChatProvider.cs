using UnityEngine;
using UnityEngine.Events;
using System.Threading.Tasks;

public class StringEvent : UnityEvent<string> { }
public interface ILLMChatProvider
{
    StringEvent OnResponse {get; }

    Task QueryAsync(string message);
}
