using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using System.Linq;
using UnityEngine.Events;

/// <summary>
/// Creates a whisperstream and start listening for keywords in the dictionary
/// Invokes action in response to found keyword
/// Stops the stream if no keywords left for recognition
/// New dictionaries can be added with the Listen method
/// </summary>
public class SpeechRecognizer : MonoBehaviour
{
    
    public static SpeechRecognizer Instance;

    WhisperManager _whisper;
    WhisperStream _stream;
    MicrophoneRecord _microphoneRecord;

    public Dictionary<string, Action> Keywords;

    // Custom handler for recognizedtext
    private Action<string> recognizedTextHandler;

    public Action<string> RecognizedTextHandler
    {
        get
        {
            return recognizedTextHandler;
        }
        set
        {
            recognizedTextHandler = value;
            CheckStreamRecording();
        }
    }

    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip keywordRecognizedSound;

    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple SpeechRecognizer instances detected. Destroying duplicate (newest).");
            DestroyImmediate(gameObject);
        }

        _whisper = GetComponent<WhisperManager>();
        _microphoneRecord = GetComponent<MicrophoneRecord>();

        Keywords = new Dictionary<string, Action>();
    }

    private async void Start()
    {
        _stream = await _whisper.CreateStream(_microphoneRecord);

        _stream.OnSegmentFinished += Segment =>
        {
            var result = Segment.Result;

            if (recognizedTextHandler == null)
            {
                // Remove special characters and convert to lowercase
                var recognizedWords = Regex.Replace(result, @"[^\w\d\s]","").ToLower().Split(' ');
                Debug.Log("Number of Keywords: " + Keywords.Count);
                //Debug.Log("Number of Keywords: " + Keywords.Count);
                if (result == null || Keywords.Count == 0)
                {
                    Debug.LogWarning("No keywords provided. Cannot listen for nothing.");
                    return;
                }

                Dictionary<string, int> wordCounts = new Dictionary<string, int>();
                //used to play multiple actions in one segment
                
                foreach (var word in recognizedWords)
                {
                    Debug.Log("Word: " + word);
                    // Track word count
                    if (!wordCounts.ContainsKey(word))
                    {
                        wordCounts[word] = 0;
                    }
                    wordCounts[word]++;
                    //first recognized action than breaks than breaks
                    Action action;
                    if(Keywords.TryGetValue(word, out action))
                    {
                        Debug.Log("Keyword recognized: " + word);
                        PlayKeywordRecognizedSound();
                        action.Invoke();
                        break;
                    }
                    //plays every action recognized in segment once
                    /*
                    Action action;
                    if(wordCounts[word] < 2 && Keywords.TryGetValue(word, out action))
                    {
                        Debug.Log("Keyword recognized: " + word);
                        PlayKeywordRecognizedSound();
                        action.Invoke();
                    }*/
                }

            }
            else
            {
                Debug.Log("Raw recognition result");
                recognizedTextHandler?.Invoke(result);
            }
        };
    }

    public Action Listen(Dictionary<string, Action> newKeywords)
    {
        if(_stream == null)
        {
            Debug.LogWarning("WhisperStream not initialized. Cannot listen for keywords.");
            return () =>
            {
            };
        }
        // If no new keywords are provided, return an empty action
        if (newKeywords.Count == 0)
        {
            Debug.LogWarning("No keywords provided. Cannot listen for nothing.");
            return () =>
            {
            };
        }

        // Add new keywords to the list
        foreach(var word in newKeywords.Keys)
        {
            if (!Keywords.ContainsKey(word))
            {
                Keywords.Add(word, newKeywords[word]);
            }
            else
            {
                Debug.LogWarning("Keyword already exists: " + word);
            }
        }

        CheckStreamRecording();
        
        // Return an Action to remove the requested keywords
        return () =>
        {
            foreach(var word in newKeywords.Keys)
            {
                Keywords.Remove(word);
            }

            CheckStreamRecording();
        };
    }

    private void CheckStreamRecording()
    {
        // Start listening if not already listening
        if (!_microphoneRecord.IsRecording && (Keywords.Count > 0 || recognizedTextHandler != null))
        {
            _microphoneRecord.StartRecord();
            _stream.StartStream();
        }
        else if (_microphoneRecord.IsRecording && Keywords.Count == 0 && recognizedTextHandler == null)
        {
            _microphoneRecord.StopRecord();
            _stream.StopStream();
        }
    }

    private void PlayKeywordRecognizedSound()
    {
        if (audioSource != null && keywordRecognizedSound != null)
        {
            audioSource.PlayOneShot(keywordRecognizedSound);
        }
    }

    public void ClearAllKeywords()
    {
        Keywords.Clear();
        RecognizedTextHandler = null;
        CheckStreamRecording();
    }

    public void DisableSpeechRecognition()
    {
       gameObject.SetActive(false);
    }

    public void EnableSpeechRecognition()
    {
        gameObject.SetActive(true);
    }
}
