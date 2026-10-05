using System.Collections;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif


using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;           

               


/*
Records audio from the Quest microphone when the MIC button is pressed.
Press MIC once -> starts recording.
Press MIC again -> stops, encodes as WAV, POSTs to Flask /transcribe,
                   displays the transcript text.
*/

public class MicButtonRecorder : MonoBehaviour
{
    [Header("Flask backend")]
    [SerializeField] private string flaskBaseUrl = "http://10.158.202.27:5000";

    [Header("Recording settings")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordingSeconds = 30;

    [Header("UI feedback (all optional)")]
    [SerializeField] private TMP_Text statusText;       // small "Recording..." indicator
    [SerializeField] private TMP_Text transcriptText;   // shows the returned text
    [SerializeField] private UnityEngine.UI.Image micButtonImage;  // drag the MicButton's Image here
    [SerializeField] private Color recordingColor = Color.green;                                                                                                                            
    [SerializeField] private Color processingColor = new Color(1f, 0.6f, 0f); // orange  when processing
    [SerializeField] private GameObject transcriptionPanel;   // panel that appears on MIC press, collapses on SEND
    private Color _originalColor = Color.white;
    private bool _colorCached = false;


    private const string RosScenariosReadyTopic = "/vr/scenarios_ready";
    private bool _pendingReadyReset;   // ROS thread → main thread flag   

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = System.TimeSpan.FromSeconds(30)
    };

    private AudioClip _clip;
    private bool _isRecording = false;
    private string _micName;

    //-----------before when transcription was shown
    // Wired to MicButton.onClick in the Inspector
    public void OnMicPressed()
    {
        if (!_isRecording)
            StartRecording();
        else
            StopAndTranscribe();
    }


    //Wired to SendButton.onClick in the Inspector
    public void OnSendPressed()
    {
        if (transcriptText == null || string.IsNullOrWhiteSpace(transcriptText.text))
        {
            SetStatus("Nothing to send — record something first");
            return;
        }
        string message = transcriptText.text;
        StartCoroutine(SendMessage(message));
    }

    //----------------------------

    // Wired via EventTrigger: PointerDown -> OnMicHoldStart
    // public void OnMicHoldStart()                            
    // {                                       
    //     if (_isRecording) return;       
    //     StartRecording();        
    // }                                                                                    
    
    // // Wired via EventTrigger: PointerUp -> OnMicHoldEnd                                 
    // public void OnMicHoldEnd()              
    // {                                                                                  
    //     if (!_isRecording) return;
    //     StopAndTranscribe();                                                             
    // }


    [System.Serializable] private class ChatMessagePayload { public string message; }

    private IEnumerator SendMessage(string message)
    {
        SetStatus("Sending...");
        Task<string> task = PostMessageAsync(message);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            string err = task.Exception?.GetBaseException()?.Message ?? "unknown";
            Debug.LogError($"[MicRecorder] Send FAILED: {err}");
            SetStatus($"Failed: {err}");
        }
        else
        {
            Debug.Log($"[MicRecorder] Sent, response: {task.Result}");
            // Fallback path: LLM replied with plain chat (no scenarios) so no ROS
            // scenarios_ready=true will fire. Reset the mic UI here instead.
            // If scenarios WERE generated, the ROS handler will also fire — harmless (idempotent).
            ResetMicUi();
        }
    }

    private async Task<string> PostMessageAsync(string message)
    {
        string json = JsonUtility.ToJson(new ChatMessagePayload { message = message });
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{flaskBaseUrl}/chat/message", content);
        return await response.Content.ReadAsStringAsync();
    }

    private void StartRecording()
    {
        #if UNITY_ANDROID
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    Permission.RequestUserPermission(Permission.Microphone);
                    SetStatus("Grant mic permission and press MIC again");
                    return;
                }
        #endif
                if (Microphone.devices.Length == 0)
                {
                    SetStatus("No microphone found");
                    return;
                }

                _micName = Microphone.devices[0];
                _clip = Microphone.Start(_micName, false, maxRecordingSeconds, sampleRate);
                _isRecording = true;
                if (transcriptionPanel != null) transcriptionPanel.SetActive(true);
                SetButtonColor(recordingColor);
                SetStatus("Recording...");
    }

    private void StopAndTranscribe()
    {
        int recordedSamples = Microphone.GetPosition(_micName);
        Microphone.End(_micName);
        _isRecording = false;                                                                                                                                                                 
        SetButtonColor(processingColor);// we set to orange color = "working on it"


        if (_clip == null || recordedSamples <= 0)
        {
            SetStatus("No audio captured");
            return;
        }

        float[] samples = new float[recordedSamples * _clip.channels];
        _clip.GetData(samples, 0);

        byte[] wavBytes = EncodeAsWav(samples, _clip.channels, sampleRate);
        SetStatus("Sending...");//Transcribing... is more accurate here but user doesnt care
        StartCoroutine(SendToTranscribe(wavBytes));
    }

    private IEnumerator SendToTranscribe(byte[] wavBytes)
    {
        Task<string> task = PostAudioAsync(wavBytes);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            string err = task.Exception?.GetBaseException()?.Message ?? "unknown";
            Debug.LogError($"[MicRecorder] FAILED: {err}");
            SetStatus($"Failed: {err}");
        }
        //before not sent automaticaaly
        // else
        // {
        //     string transcript = ExtractText(task.Result);
        //     Debug.Log($"[MicRecorder] Transcript: {transcript}");
        //     SetStatus("");                 // clear status first so it doesn't overwrite
        //     SetTranscript(transcript);      // then show the transcript last
        // }

        //now we send automaticaaly via Coroutine
        else
        {   
            string transcript = ExtractText(task.Result);
            Debug.Log($"[MicRecorder] Transcript: {transcript}");
            if (string.IsNullOrWhiteSpace(transcript))                                
            {                                         
                SetStatus("No speech detected"); 
                SetButtonColor(_originalColor);                                     
                yield break;                    
            }                                                                       
            SetStatus("Sending...");
            yield return StartCoroutine(SendMessage(transcript));
            // Mic UI reset is handled by either ResetMicUi() in SendMessage (chat-only fallback)
            // or the /vr/scenarios_ready ROS handler (scenario-generated primary path).
        }
    }

    private async Task<string> PostAudioAsync(byte[] wavBytes)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(wavBytes);
        audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "audio", "recording.wav");

        var response = await _http.PostAsync($"{flaskBaseUrl}/transcribe", form);
        return await response.Content.ReadAsStringAsync();
    }

    // Minimal JSON parse — response is {"text": "...", "language": "..."}
    private string ExtractText(string json)
    {
        int idx = json.IndexOf("\"text\"");
        if (idx < 0) return json;
        int colon = json.IndexOf(':', idx);
        int qs = json.IndexOf('"', colon + 1);
        int qe = json.IndexOf('"', qs + 1);
        if (qs < 0 || qe <= qs) return json;
        return json.Substring(qs + 1, qe - qs - 1);
    }

    // 16-bit PCM WAV encoder
    private byte[] EncodeAsWav(float[] samples, int channels, int sr)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        int byteRate = sr * channels * 2;
        int dataSize = samples.Length * 2;

        w.Write(new char[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataSize);
        w.Write(new char[] { 'W', 'A', 'V', 'E' });

        w.Write(new char[] { 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(sr);
        w.Write(byteRate);
        w.Write((short)(channels * 2));
        w.Write((short)16);

        w.Write(new char[] { 'd', 'a', 't', 'a' });
        w.Write(dataSize);

        foreach (float s in samples)
        {
            short pcm = (short)Mathf.Clamp(s * 32767f, -32768f, 32767f);
            w.Write(pcm);
        }

        return stream.ToArray();
    }

    private void SetButtonColor(Color c)
    {
        if (micButtonImage == null) return;
        if (!_colorCached)
        {
            _originalColor = micButtonImage.color;
            _colorCached = true;
        }
        micButtonImage.color = c;
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
        Debug.Log($"[MicRecorder] {text}");
    }

    private void SetTranscript(string text)
    {
        if (transcriptText != null) transcriptText.text = text;
    }

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<BoolMsg>(RosScenariosReadyTopic, OnScenariosReady);
    }

    // ROS callback runs on a background thread — just queue.
    private void OnScenariosReady(BoolMsg msg)
    {
        if (msg.data) _pendingReadyReset = true;
    }

    void Update()
    {
        if (!_pendingReadyReset) return;
        _pendingReadyReset = false;
        ResetMicUi();
    }

    // Shared cleanup: hide panel, clear text, restore button color.
    private void ResetMicUi()
    {
        SetStatus("");
        SetTranscript("");
        if (transcriptionPanel != null) transcriptionPanel.SetActive(false);
        SetButtonColor(_originalColor);
    }
}