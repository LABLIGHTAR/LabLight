using UnityEngine;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine.UI;

// Point a webbrowser to http://localhost:8080
public class MJPEGStreamer : MonoBehaviour
{
	[Header("Stream Settings")]
	[SerializeField] private int port		= 8080;
	[SerializeField] private int quality	= 75;
	
	[Header("Source")]

	// private IFrameProvider frameProvider;	
	private HttpListener										httpListener;
	private ConcurrentDictionary<string, HttpListenerContext>	activeClients;
	private CancellationTokenSource								cancellationTokenSource;
	private bool												isStreaming = false;

	private void Start()
	{
		activeClients = new ConcurrentDictionary<string, HttpListenerContext>();
		StartServer();

		// frameProvider = ServiceRegistry.GetService<IFrameProvider>();
   		// frameProvider.OnFrameReceived += OnFrameReceived;
	}

	void OnDestroy()
	{
		/*
		if (frameProvider != null)
		{
			frameProvider.OnFrameReceived -= OnFrameReceived;
		}
		*/

		if (readableTexture != null)
		{
			Destroy(readableTexture);
		}
	}

	private void OnFrameReceived(Texture2D texture)
	{
		/*
		if (!isStreaming)
	 		return;

		SendFrame(texture);
		*/	
	}

	private void StartServer()
	{
		try
		{
			httpListener = new HttpListener();
			httpListener.Prefixes.Add($"http://*:{port}/");
			httpListener.Start();
			
			Debug.Log($"MJPEG server started on port {port}");
			Debug.Log($"Access the stream at: http://localhost:{port}/");
			
			cancellationTokenSource = new CancellationTokenSource();
			ListenForClientsAsync();
			isStreaming = true;
		}
		catch(System.Exception e)
		{
			Debug.LogError($"Failed to start MJPEG server: {e.Message}");
		}
	}

	private async void ListenForClientsAsync()
	{
		while (	cancellationTokenSource != null &&
				cancellationTokenSource.Token != null &&
				!cancellationTokenSource.Token.IsCancellationRequested)
		{
			try
			{
				var context		= await httpListener.GetContextAsync();
				var clientId	= context.Request.RemoteEndPoint.ToString();
				
				activeClients.TryAdd(clientId, context);
				
				// Send HTTP headers
				var response			= context.Response;
				response.ContentType	= "multipart/x-mixed-replace; boundary=frame";
				response.Headers.Add("Access-Control-Allow-Origin", "*");
				response.Headers.Add("Connection", "keep-alive");
				
				Debug.Log($"New client connected: {clientId}");
			}
			catch(System.Exception e)
			{
				if (cancellationTokenSource != null &&
					cancellationTokenSource.Token != null &&
					!cancellationTokenSource.Token.IsCancellationRequested)
				{
					Debug.LogError($"Error accepting client: {e.Message}");
				}
			}
		}
	}

	Texture2D readableTexture = null;
	RenderTexture _renderTexture = null;	

	private void SendFrame(Texture2D texture)
	{
		/*
		if (activeClients.Count == 0 || texture == null)
			return;

		byte[] jpegBytes = null;
		
		if (texture.isReadable && (texture.format == TextureFormat.RGBA32 || texture.format == TextureFormat.RGB24))
		{
			jpegBytes = texture.EncodeToJPG(quality);
		}
		else
		{
			// Webcam texture 
			// EncodeToJPG kan direct hierop worden aangeroepen
			// Texture format: RGBA32 
			// graphics format R8G8B8A8_SRGB 
			// width: 1920 
			// height: 1080 
			// readable: True 
			// mipmap: 11 
			// filterMode: Bilinear 
			// wrapMode: Repeat

			// Vision OS Incoming texture 
			// Texture format: BGRA32 
			// graphics format B8G8R8A8_SRGB 
			// width: 1920 height: 1080 
			// readable: True 
			// mipmap: 1 
			// filterMode: Bilinear 
			// wrapMode: Repeat

			// print texture info that is relevatn for conversion to jpg
			// Debug.Log("Texture format: " + texture.format + 
			// 	" graphics format " + texture.graphicsFormat +
			// 	" width: " + texture.width + 
			// 	" height: " + texture.height + 
			// 	" readable: " + texture.isReadable + 
			// 	" mipmap: " + texture.mipmapCount + 
			// 	" filterMode: " + texture.filterMode +
			// 	" wrapMode: " + texture.wrapMode );			

			if (_renderTexture == null)
			{
				_renderTexture = new RenderTexture(texture.width, texture.height, 1, RenderTextureFormat.ARGB32);
				_renderTexture.enableRandomWrite = true;
				_renderTexture.Create();
			}

       		// Blit the external texture to the RenderTexture
			Graphics.Blit(  texture, 
							_renderTexture, 
							new Vector2(frameProvider.IsFlippedHorizontally ? -1.0f: 1.0f, frameProvider.IsFlippedVertically ? -1.0f : 1.0f), 
							new Vector2(frameProvider.IsFlippedHorizontally ? 1.0f : 0.0f, frameProvider.IsFlippedVertically ? 1.0f : 0.0f));

			if (readableTexture == null)
			{
				readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
			}

			// Read rendertarget into CPU texture
			RenderTexture.active = _renderTexture;
			readableTexture.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
			// Note: no Apply needed since we are only using readableTexture to encode to JPG
			// Encode texture into JPG
        	jpegBytes = readableTexture.EncodeToJPG(quality);
		}

		if (jpegBytes != null)
		{
			foreach(var client in activeClients)
			{
				try
				{
					var response = client.Value.Response;
					var headers = $"\r\n--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {jpegBytes.Length}\r\n\r\n";
					var headerBytes = System.Text.Encoding.ASCII.GetBytes(headers);
					
					response.OutputStream.Write(headerBytes, 0, headerBytes.Length);
					response.OutputStream.Write(jpegBytes, 0, jpegBytes.Length);
					response.OutputStream.Flush();
				}
				catch
				{
					activeClients.TryRemove(client.Key, out _);
					try { client.Value.Response.Close(); } catch { }
					Debug.Log($"Client disconnected: {client.Key}");
				}
			}
		}
		*/
	}

	private void OnDisable()
	{
		StopServer();
	}

	private void StopServer()
	{
		isStreaming = false;
		
		if (cancellationTokenSource != null)
		{
			// Stop listening for new clients
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
			cancellationTokenSource = null;
		}

		foreach (var client in activeClients)
		{
			try { client.Value.Response.Close(); } catch { }
		}
		activeClients.Clear();

		if (httpListener != null)
		{
			httpListener.Stop();
			httpListener.Close();
			httpListener = null;
		}
	}
}
