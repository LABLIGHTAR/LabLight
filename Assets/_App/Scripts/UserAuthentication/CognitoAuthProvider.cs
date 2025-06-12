using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class CognitoAuthProvider : IUserAuthProvider
{
    private string apiUrl => $"https://cognito-idp.{region}.amazonaws.com/";
    private string userPoolId = "us-east-1_0UOUdY1FA";
    private string clientId = "2u744jkvo6ca9sia15v3b0e4hm";
    private string region = "us-east-1";

    private AuthenticationResult authResult;

    public async Task<bool> TryAuthenticateUser(string username, string password)
    {
        // Manually create the JSON request body
        string jsonBody = $@"{{
            ""AuthFlow"": ""USER_PASSWORD_AUTH"",
            ""ClientId"": ""{clientId}"",
            ""AuthParameters"": {{
                ""USERNAME"": ""{username}"",
                ""PASSWORD"": ""{password}""
            }}
        }}";

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            // Set the headers
            request.SetRequestHeader("Content-Type", "application/x-amz-json-1.1");
            request.SetRequestHeader("X-Amz-Target", "AWSCognitoIdentityProviderService.InitiateAuth");

            // Add the JSON body to the request
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Send the request
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;

                var authResponse = JsonConvert.DeserializeObject<AuthenticationResponse>(jsonResponse);

                if (authResponse != null && authResponse.AuthenticationResult != null)
                {
                    authResult = authResponse.AuthenticationResult;
                    Debug.Log("User authenticated successfully.");
                    return true;
                }
                else
                {
                    Debug.Log("AuthResult is null, deserialization failed.");
                    return false;
                }
            }
            else
            {
                Debug.Log("Error: " + request.error);
                Debug.Log("Response: " + request.downloadHandler.text);
                return false;
            }
        }
    }

    public bool IsAuthenticated()
    {
        return authResult != null;
    }

    public string TryGetIdToken()
    {
        if(!IsAuthenticated())
        {
            Debug.LogError("User is not authenticated.");
            return null;
        }
        return authResult.IdToken;
    }

    public string TryGetAccessToken()
    {
        if(!IsAuthenticated())
        {
            Debug.LogError("User is not authenticated.");
            return null;
        }
        return authResult.AccessToken;
    }

    public string TryGetRefreshToken()
    {
        if(!IsAuthenticated())
        {
            Debug.LogError("User is not authenticated.");
            return null;
        }
        return authResult.RefreshToken;
    }

    public class AuthenticationResponse
    {
        [JsonProperty("AuthenticationResult")]
        public AuthenticationResult AuthenticationResult { get; set; }
    }

    public class AuthenticationResult
    {
        [JsonProperty("AccessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("ExpiresIn")]
        public int ExpiresIn { get; set; }

        [JsonProperty("IdToken")]
        public string IdToken { get; set; }

        [JsonProperty("RefreshToken")]
        public string RefreshToken { get; set; }

        [JsonProperty("TokenType")]
        public string TokenType { get; set; }
    }
}