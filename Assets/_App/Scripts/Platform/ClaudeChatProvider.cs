using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using Newtonsoft.Json;

public class ClaudeChatProvider : ILLMChatProvider
{
    private string apiUrl = "https://2fdv197i13.execute-api.us-east-1.amazonaws.com/dev/message";

    private readonly StringEvent _onResponse = new StringEvent();
    public StringEvent OnResponse => _onResponse;

    // Method to query Claude with a string prompt
    public async Task QueryAsync(string query)
    {
        if(!ServiceRegistry.GetService<IUserAuthProvider>().IsAuthenticated())
        {
            Debug.LogError("User is not authenticated. Cannot query Claude.");
            return;
        }
        await SendQueryToClaudeAsync(query);
    }

    // Coroutine to send the query to Claude API
    private async Task SendQueryToClaudeAsync(string query)
    {
        string jsonBody = $@"{{
            ""query"": ""{query}""
        }}";

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            // Add the JSON body to the request
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("auth-token", ServiceRegistry.GetService<IUserAuthProvider>().TryGetIdToken()); // Include the Cognito ID token in the request

            var operation = request.SendWebRequest();

            while(!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Parse the outer JSON response
                var outerResponse = JsonUtility.FromJson<OuterResponse>(request.downloadHandler.text);

                // Parse the body (which is a JSON string) to extract the message
                var bodyJson = JsonUtility.FromJson<InnerBody>(outerResponse.body);
                _onResponse.Invoke(bodyJson.message);
            }
            else
            {
                Debug.LogError("Error: " + request.error);
                Debug.Log("Response: " + request.downloadHandler.text);
            }
        }
    }

    // Define classes to match the structure of the JSON response
    [System.Serializable]
    public class OuterResponse
    {
        public int statusCode;
        public string body; // This is a stringified JSON
    }

    [System.Serializable]
    public class InnerBody
    {
        public string message;
    }
}
