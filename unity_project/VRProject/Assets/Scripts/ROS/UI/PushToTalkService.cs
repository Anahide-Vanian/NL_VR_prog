using System.Collections;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RosMessageTypes.Std;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/*
 * Push-to-talk voice command service (toggle mode).
 *
 * Talk button PRESS #1 -> Microphone.Start
 * Talk button PRESS #2 -> Microphone.End, POST /transcribe, POST /chat/message
 * Execute button PRESS -> POST /vr/execute (only if /vr/scenarios_ready == true)
 *
 * VR pinch-selections from HandPinchSelector are drained into the LLM prompt
 * inside Flask (on /chat/message), so no reset call is needed here.
 */

public class PushToTalkService : MonoBehaviour
{
    [Header("Flask backend")]
    [SerializeField] private string flaskBaseUrl = "http://10.158.202.27:5000";

    [Header("Input")]
    [Tooltip("Toggle-to-record: first press starts, second press stops + sends.")]
    [SerializeField] private InputActionProperty talkAction;

    [Tooltip("Bind to <XRController>{RightHand}/primaryButton (single-press execute).")]
    [SerializeField] private InputActionProperty executeAction;

    [Header("Recording")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordingSeconds = 30;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text statusText;

    [Header("State indicator (optional — drag your Canvas Image here)")]
    [SerializeField] private Image stateIndicator;
    [SerializeField] private Color idleColor      = Color.white;
    [SerializeField] private Color recordingColor = Color.red;
    [SerializeField] private Color sendingColor   = new Color(1f, 0.65f, 0f); // orange
    [SerializeField] private Color readyColor     = Color.green;
    [Tooltip("Seconds to hold the Ready color before returning to Idle.")]
    [SerializeField] private float readyHoldSeconds = 1.5f;

    private const string RosScenariosReadyTopic = "/vr/scenarios_ready";

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = System.TimeSpan.FromSeconds(30)
    };

    private AudioClip _clip;
    private string _micDevice;
    private bool _isRecording;
    private bool _scenariosReady;
    private Coroutine _readyResetCo;
    private HandPinchSelector _pinchSelector;

    private void OnEnable()
    {
        // Toggle: first press starts recording, second press stops + sends.
        talkAction.action.started += OnTalkToggled;
        executeAction.action.performed += OnExecutePressed;

        talkAction.action.Enable();
        executeAction.action.Enable();
    }

    private void OnDisable()
    {
        talkAction.action.started -= OnTalkToggled;
        executeAction.action.performed -= OnExecutePressed;

        talkAction.action.Disable();
        executeAction.action.Disable();
    }

    private void Start()
    {
        _pinchSelector = FindFirstObjectByType<HandPinchSelector>();

        ROSConnection.GetOrCreateInstance().Subscribe<BoolMsg>(
            RosScenariosReadyTopic,
            OnScenariosReady
        );

        SetStatus("Hold Left X to speak, press Right A to execute");
        SetIndicatorColor(idleColor);
    }

    private void OnScenariosReady(BoolMsg msg)
    {
        _scenariosReady = msg.data;

        if (_scenariosReady)
        {
            SetIndicatorColor(readyColor);
            if (_readyResetCo != null) StopCoroutine(_readyResetCo);
            _readyResetCo = StartCoroutine(ReturnToIdleAfter(readyHoldSeconds));
        }

        Debug.Log($"[PTT] scenariosReady = {_scenariosReady}");
    }

    private void OnTalkToggled(InputAction.CallbackContext ctx)
    {
        // Second press: stop recording and send.
        if (_isRecording)
        {
            StopAndSend();
            return;
        }

        // First press: start recording.
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            SetStatus("Grant mic permission and try again");
            return;
        }
#endif

        if (Microphone.devices.Length == 0)
        {
            SetStatus("No microphone found");
            return;
        }

        _micDevice = Microphone.devices[0];

        _clip = Microphone.Start(
            _micDevice,
            false,
            maxRecordingSeconds,
            sampleRate
        );

        _isRecording = true;

        SetIndicatorColor(recordingColor);
        SetStatus("Recording — press B again to stop");
    }

    private void StopAndSend()
    {
        int recordedSamples = Microphone.GetPosition(_micDevice);

        Microphone.End(_micDevice);

        _isRecording = false;

        if (_clip == null || recordedSamples <= 0)
        {
            SetIndicatorColor(idleColor);
            SetStatus("No audio captured");
            return;
        }

        float[] samples = new float[recordedSamples * _clip.channels];

        _clip.GetData(samples, 0);

        byte[] wav = EncodeAsWav(
            samples,
            _clip.channels,
            sampleRate
        );

        SetIndicatorColor(sendingColor);
        SetStatus("Transcribing...");

        StartCoroutine(TranscribeAndChat(wav));
    }

    private void OnExecutePressed(InputAction.CallbackContext ctx)
    {
        if (_isRecording)
            return;

        if (!_scenariosReady)
        {
            SetStatus("Nothing to execute yet");
            return;
        }

        SetStatus("Running on robot...");

        // Restore every pinched box/slot back to its original color
        // now that the user is committing the selection.
        _pinchSelector?.ClearAllTints();

        StartCoroutine(
            PostJson(
                $"{flaskBaseUrl}/vr/execute",
                ""
            )
        );
    }

    private IEnumerator TranscribeAndChat(byte[] wavBytes)
    {
        Task<string> t = PostMultipart(
            $"{flaskBaseUrl}/transcribe",
            wavBytes
        );

        while (!t.IsCompleted)
            yield return null;

        if (t.IsFaulted)
        {
            SetIndicatorColor(idleColor);
            SetStatus(
                $"Transcribe failed: {t.Exception?.GetBaseException()?.Message}"
            );

            yield break;
        }

        string transcript = ExtractText(t.Result);

        Debug.Log($"[PTT] transcript: {transcript}");

        if (string.IsNullOrWhiteSpace(transcript))
        {
            SetIndicatorColor(idleColor);
            SetStatus("(empty transcript)");
            yield break;
        }

        SetStatus($"You said: {transcript}");

        string json = $"{{\"message\":{JsonEncode(transcript)}}}";
        Task<string> c = PostJsonAsync($"{flaskBaseUrl}/chat/message", json);  
                  
        while (!c.IsCompleted) 
          yield return null;  

        if (c.IsFaulted)
        {
            SetIndicatorColor(idleColor);
            SetStatus(
                $"Chat failed: {c.Exception?.GetBaseException()?.Message}"
            );

            yield break;
        }

        SetStatus(
            "LLM done — press Right A to execute when + is green"
        );
    }

    private IEnumerator PostJson(string url, string json)
    {
        Task<string> t = PostJsonAsync(url, json);

        while (!t.IsCompleted)
            yield return null;

        if (t.IsFaulted)
        {
            Debug.LogError(
                $"[PTT] POST {url} failed: {t.Exception?.GetBaseException()?.Message}"
            );
        }
        else
        {
            Debug.Log(
                $"[PTT] POST {url} -> {t.Result}"
            );
        }
    }

    private async Task<string> PostJsonAsync(string url, string json)
    {
        using var content = new StringContent(
            json,
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await _http.PostAsync(url, content);

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostMultipart(
        string url,
        byte[] wavBytes
    )
    {
        using var form = new MultipartFormDataContent();

        var audio = new ByteArrayContent(wavBytes);

        audio.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

        form.Add(
            audio,
            "audio",
            "voice.wav"
        );

        var response = await _http.PostAsync(url, form);

        return await response.Content.ReadAsStringAsync();
    }

    private string ExtractText(string json)
    {
        int idx = json.IndexOf("\"text\"");

        if (idx < 0)
            return json;

        int colon = json.IndexOf(':', idx);

        int qs = json.IndexOf('"', colon + 1);
        int qe = json.IndexOf('"', qs + 1);

        if (qs < 0 || qe <= qs)
            return json;

        return json.Substring(
            qs + 1,
            qe - qs - 1
        );
    }

    private string JsonEncode(string s)
    {
        return "\"" +
               s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"") +
               "\"";
    }

    private byte[] EncodeAsWav(
        float[] samples,
        int channels,
        int sr
    )
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
            short pcm = (short)Mathf.Clamp(
                s * 32767f,
                -32768f,
                32767f
            );

            w.Write(pcm);
        }

        return stream.ToArray();
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;

        Debug.Log($"[PTT] {text}");
    }

    private void SetIndicatorColor(Color c)
    {
        if (stateIndicator != null)
            stateIndicator.color = c;
    }

    private IEnumerator ReturnToIdleAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetIndicatorColor(idleColor);
        _readyResetCo = null;
    }
}