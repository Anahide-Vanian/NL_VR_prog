using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class ControlPanelActions : MonoBehaviour
{
    [Header("Scenarios panel to toggle")]
    [SerializeField] private GameObject scenariosPanel;

    [Header("Flask backend")]
    [SerializeField] private string flaskBaseUrl = "http://10.158.202.27:5000";

    [Header("Status display (optional)")]
    [SerializeField] private TMP_Text statusText;

    [Header("Help panel")]                                           
    [SerializeField] private GameObject HelpPanel;                         
                                                                                

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = System.TimeSpan.FromSeconds(10)
    };

    private bool _isRunning = false;
    private string _pendingResult;
    private bool _hasPendingResult;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>("/vr/run_result", OnRunResult);
    }

    void Update()
    {
        if (_hasPendingResult)
        {
            SetStatus(CapitalizeFirst(_pendingResult));
            _hasPendingResult = false;
            _isRunning = false;   // free up Execute button for next run
        }
    }

    private void OnRunResult(StringMsg msg)
    {
        _pendingResult = msg.data;
        _hasPendingResult = true;
    }

    private string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    public void OnPlusPressed()
    {
        if (scenariosPanel != null)
            scenariosPanel.SetActive(!scenariosPanel.activeSelf);
    }

    public void OnExecutePressed()
    {
        if (_isRunning) return;
        StartCoroutine(RunExecute());
    }

      public void OnHelpPressed()                                                
    {                                                                             
        if (HelpPanel != null)                                             
            HelpPanel.SetActive(!HelpPanel.activeSelf);             
    }   

    private IEnumerator RunExecute()
    {
        _isRunning = true;
        SetStatus("Starting...");
        string url = $"{flaskBaseUrl}/vr/execute";

        Task<string> task = PostAsync(url);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            string err = task.Exception?.GetBaseException()?.Message ?? "unknown";
            Debug.LogError($"[CPA] FAILED: {err}");
            SetStatus($"Failed: {err}");
        }
        else
        {
            Debug.Log($"[CPA] Started: {task.Result}");
            SetStatus("Running on robot...");
        }

        _isRunning = false;
    }

    private async Task<string> PostAsync(string url)
    {
        using var content = new StringContent("");
        var response = await _http.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }

    private string ParseStatus(string json)
    {
        if (json.Contains("\"status\": \"success\"")) return "Success";
        if (json.Contains("\"status\": \"failed\""))  return "Failed";
        if (json.Contains("\"status\": \"stopped\"")) return "Stopped";
        return "Done";
    }

    private void SetStatus(string text)
    {
        Color color = ColorForStatus(text);

        if (statusText != null)
        {
            statusText.text = text;
            statusText.color = color;
        }
    }

    private Color ColorForStatus(string text)
    {
        if (string.IsNullOrEmpty(text)) return Color.white;
        if (text.Contains("Success") || text.Contains("Done"))  return new Color(0.20f, 0.75f, 0.30f); // green
        if (text.Contains("Failed") || text.Contains("Error") || text.Contains("Stopped")) return new Color(0.85f, 0.20f, 0.20f); // red
        if (text.Contains("Starting") || text.Contains("Running")) return new Color(1.00f, 0.65f, 0.00f); // orange
        return Color.white;
    }
}
