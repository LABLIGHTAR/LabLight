using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

// Unity player settings need to be set to allow HTTP connections since we don't have an HTTPS certificate
//
// Go to Edit > Project Settings > Player
// Select the Other Settings section
// Find 'Allow downloads over HTTP' and set it to allow it, or find 'Block insecure HTTP' and uncheck it
// (depending on your Unity version, at least for 2022.3.2 the first one existed and worked)


public class TCPMJPEGClient : MonoBehaviour
{
	[SerializeField] private string		serverAddress	= "localhost";
	[SerializeField] private int		serverPort		= 8080;
	[SerializeField] private int		timeoutSeconds	= 10;
	[SerializeField] private float		reconnectDelay	= 1.0f;
	[SerializeField] private Material	targetMaterial;

	private bool					isRunning	= false;
	private bool					isConnected	= false;
	private Thread					networkThread;
	private TcpClient				tcpClient;
	private Texture2D				texture;
	private readonly object			textureLock	= new object();
	private ConcurrentQueue<byte[]>	frameQueue	= new ConcurrentQueue<byte[]>();

	private const int BUFFER_SIZE		= 16384;	// 16KB read buffer
	private const int MAX_FRAME_SIZE	= 10485760; // 10MB max frame size


	void Start()
	{
		texture = new Texture2D(2, 2, TextureFormat.RGB24, false);

		// If no target material was specified, try to get the material from the object
		if(targetMaterial == null)
		{
			// Try to get a renderer component (MeshRenderer or SpriteRenderer)
			Renderer renderer = GetComponent<Renderer>();
			if(renderer != null && renderer.material != null)	targetMaterial = renderer.material;
			else												Debug.LogWarning("No target material specified and no Renderer found.");
		}

		if(targetMaterial != null)
			targetMaterial.mainTexture = texture;

		StartClient();
	}


	void OnDisable()
	{
		StopClient();
	}


	void OnDestroy()
	{
		StopClient();
		if(texture != null)
			Destroy(texture);
	}


	void Update()
	{
		// Process any queued frames on the main thread
		while(frameQueue.TryDequeue(out byte[] frameData))
		{
			try
			{
				lock (textureLock)
				{
					if(texture.LoadImage(frameData))
					{
						if(targetMaterial != null)
							targetMaterial.mainTexture = texture;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"Error loading frame: {e.Message}");
			}
		}
	}


	public void StartClient()
	{
		if(isRunning)	return;

		isRunning		= true;
		networkThread	= new Thread(NetworkThreadFunction);
		networkThread.Start();
	}


	public void StopClient()
	{
		isRunning = false;

		if(tcpClient != null)
		{
			tcpClient.Close();
			tcpClient = null;
		}

		if(networkThread != null && networkThread.IsAlive)
		{
			networkThread.Join(1000); // Wait up to 1 second for thread to finish
			if(networkThread.IsAlive)
				networkThread.Abort();
			networkThread = null;
		}
	}


	private void NetworkThreadFunction()
	{
		byte[]		readBuffer			= new byte[BUFFER_SIZE];
		List<byte>	frameBuffer			= new List<byte>();
		bool		inFrame				= false;
		int			expectedFrameSize	= -1;

		while(isRunning)
		{
			try
			{
				if(!isConnected)
				{
					Connect();
					continue;
				}

				NetworkStream stream = tcpClient.GetStream();
				int bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

				if(bytesRead == 0)
				{
					// Connection closed by server
					HandleDisconnect();
					continue;
				}

				// Process the received data
				for (int i = 0; i < bytesRead; i++)
					frameBuffer.Add(readBuffer[i]);

				// Process accumulated data
				while (frameBuffer.Count > 0)
				{
					if(!inFrame)
					{
						// Look for frame boundary
						int boundaryIndex = IndexOf(frameBuffer, "--boundary\r\n");
						if(boundaryIndex == -1)		break;

						// Remove everything before and including boundary
						frameBuffer.RemoveRange(0, boundaryIndex + 12);

						// Look for Content-Length
						int contentLengthIndex = IndexOf(frameBuffer, "Content-Length: ");
						if(contentLengthIndex == -1)	break;

						// Find end of headers
						int headersEndIndex = IndexOf(frameBuffer, "\r\n\r\n");
						if(headersEndIndex == -1)	break;

						// Parse Content-Length
						string headers = Encoding.ASCII.GetString(frameBuffer.GetRange(0, headersEndIndex).ToArray());
						foreach (string line in headers.Split('\n'))
						{
							if (line.StartsWith("Content-Length: "))
							{
								expectedFrameSize = int.Parse(line.Substring(16).Trim());
								break;
							}
						}

						// Remove headers
						frameBuffer.RemoveRange(0, headersEndIndex + 4);
						inFrame = true;
					}

					if(inFrame && frameBuffer.Count >= expectedFrameSize)
					{
						// We have a complete frame
						byte[] frameData = frameBuffer.GetRange(0, expectedFrameSize).ToArray();
						frameQueue.Enqueue(frameData);

						// Remove the frame and trailing \r\n
						frameBuffer.RemoveRange(0, expectedFrameSize + 2);
						inFrame = false;
						expectedFrameSize = -1;
					}
					else if (inFrame)
					{
						// Need more data for complete frame
						break;
					}
				}

				// Prevent buffer from growing too large
				if (frameBuffer.Count > MAX_FRAME_SIZE)
				{
					Debug.LogWarning("Frame buffer overflow, resetting connection");
					HandleDisconnect();
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"Network error: {e.Message}");
				HandleDisconnect();
			}
		}
	}


	private void Connect()
	{
		try
		{
			Debug.Log($"Connecting to {serverAddress}:{serverPort}...");

			tcpClient					= new TcpClient();
			tcpClient.SendTimeout		= timeoutSeconds * 1000;
			tcpClient.ReceiveTimeout	= timeoutSeconds * 1000;

			var result		= tcpClient.BeginConnect(serverAddress, serverPort, null, null);
			bool success	= result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(timeoutSeconds));

			if(!success)
				{
				throw new Exception("Connection attempt timed out");
			}

			tcpClient.EndConnect(result);

			// Send HTTP GET request
			string request =	"GET / HTTP/1.1\r\n" +
								$"Host: {serverAddress}:{serverPort}\r\n" +
								"Connection: keep-alive\r\n" +
								"Accept: multipart/x-mixed-replace;boundary=boundary\r\n" +
								"\r\n";

			byte[] requestData = Encoding.ASCII.GetBytes(request);
			tcpClient.GetStream().Write(requestData, 0, requestData.Length);

			isConnected = true;
			Debug.Log("Connected successfully");
		}
		catch (Exception e)
		{
			Debug.LogWarning($"Connection failed: {e.Message}");
			HandleDisconnect();
		}
	}


	private void HandleDisconnect()
	{
		isConnected = false;
		if (tcpClient != null)
		{
			tcpClient.Close();
			tcpClient = null;
		}

		// Clear any partial data
		frameQueue = new ConcurrentQueue<byte[]>();

		if(isRunning)
		{
			Debug.Log($"Reconnecting in {reconnectDelay} seconds...");
			Thread.Sleep((int)(reconnectDelay * 1000));
		}
	}


	private int IndexOf(List<byte> source, string pattern)
	{
		byte[] patternBytes = Encoding.ASCII.GetBytes(pattern);
		return IndexOf(source, patternBytes);
	}


	private int IndexOf(List<byte> source, byte[] pattern)
	{
		if(source.Count < pattern.Length) return -1;

		for(int i = 0; i <= source.Count - pattern.Length; i++)
		{
			bool found = true;
			for(int j = 0; j < pattern.Length; j++)
			{
				if(source[i + j] != pattern[j])
				{
					found = false;
					break;
				}
			}
			if(found)	return i;
		}
		return -1;
	}
}
