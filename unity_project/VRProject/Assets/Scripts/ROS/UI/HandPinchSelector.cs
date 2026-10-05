using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


/*                                                                                                                                                                                    
Send to the Flask backend the boxes and shelves selected with pinch.                                                                                                                     
                                                                                                                                                                                    
Wire the two hand XR Ray Interactors (left and right) in the inspector.
When the user pinches on a GameObject tagged "TrackedBox" or "Slot", we POST {name, tag, xyz} to /vr/select_target.                                                                                                                                       
*/

public class HandPinchSelector : MonoBehaviour
{

    [Header("Hand ray interactors (drag from XR Origin)")]
    // [SerializeField] private XRRayInteractor leftHandRay;
    // [SerializeField] private XRRayInteractor rightHandRay;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor leftHandRay;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor rightHandRay;


    [Header("Flask backend")]
    [SerializeField] private string flaskBaseUrl = "http://10.158.202.27:5000";

    [Header("Tags")]                                                                                                                                                                  
      [SerializeField] private string boxTag  = "TrackedBox";
      [SerializeField] private string slotTag = "Slot";  

    [Header("Optional debug UI")]
    [SerializeField] private TMP_Text lastSelectedText;

    [Header("Pinch highlight (full-object tint)")]
    [SerializeField] private Color highlightColor = Color.yellow;

    // Shader property IDs for the two common color slots (HDRP/URP + legacy).
    private static readonly int _idBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int _idColor     = Shader.PropertyToID("_Color");
    private static readonly int _idEmission  = Shader.PropertyToID("_EmissionColor");

    // Currently-tinted objects. Value = renderers cached so ClearAllTints
    // can restore them without walking the hierarchy again.
    private readonly Dictionary<Transform, Renderer[]> _tinted = new Dictionary<Transform, Renderer[]>();

    private static readonly HttpClient _http = new HttpClient{
        Timeout = System.TimeSpan.FromSeconds(5)
    };  

    [System.Serializable]                                                                                                                                                             
    private class SelectPayload                                                                                                                                                     
    {
        public string name;
        public string tag;
        public float[] xyz;
    }


    //when the rays are enabled
    void OnEnable()
    {
        if(leftHandRay!=null){
            leftHandRay.selectEntered.AddListener(OnPinchSelect);
        }

        if(rightHandRay!=null){
            rightHandRay.selectEntered.AddListener(OnPinchSelect);
        }
        
    }


    void OnDisable()
    {
        if(leftHandRay!=null){
            leftHandRay.selectEntered.RemoveListener(OnPinchSelect);
        }

        if(rightHandRay!=null){
            rightHandRay.selectEntered.RemoveListener(OnPinchSelect);
        }
        
    }


    private void OnPinchSelect(SelectEnterEventArgs args)
    {
        Transform obj_hit = args.interactableObject.transform;

        //from teh tramnsform we hit, we get the boxlabel component
        string targetTag;

        //we hit a box                                                                                                                                                           
        if (obj_hit.CompareTag(boxTag)){
            targetTag = boxTag;//"TrackedBox"
        }
        //we hit a slot  
        else if (obj_hit.CompareTag(slotTag)){
            targetTag = slotTag;//"Slot"
        }        
        //Pinched something we don't care about like floor, wall,etc.
        else
        {
            return;                                                                                                                                                                   
        }

        string targetName = obj_hit.name;
        Vector3 position  = obj_hit.position;
        SelectPayload payload = new SelectPayload{
            name = targetName,                    
            tag = targetTag,                                                                                                                                                          
            xyz = new float[] { position.x, position.y, position.z }
        }; 

        Debug.Log($"[HandPinch] {targetTag}:{targetName} @ ({position.x:F3}, {position.y:F3}, {position.z:F3})");
          if (lastSelectedText != null)
              lastSelectedText.text = $"{targetTag}: {targetName}";

        ToggleTint(obj_hit);
        StartCoroutine(PostSelection(payload));
    }

    // Toggle yellow tint on the pinched object.
    // - If not currently tinted: apply yellow and remember it.
    // - If already tinted: revert it to original.
    // Called by execute (Y press) via ClearAllTints() to wipe every tint at once.
    private void ToggleTint(Transform target)
    {
        if (target == null) return;

        if (_tinted.TryGetValue(target, out var existing))
        {
            RestoreRenderers(existing);
            _tinted.Remove(target);
            return;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers == null || renderers.Length == 0) return;

        var block = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            try
            {
                r.GetPropertyBlock(block);
                block.SetColor(_idBaseColor, highlightColor);   // HDRP/URP
                block.SetColor(_idColor,     highlightColor);   // legacy Standard
                block.SetColor(_idEmission,  highlightColor);   // glow so it reads in low light
                r.SetPropertyBlock(block);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HandPinch] Tint apply skipped on '{r.name}': {e.Message}");
            }
        }

        _tinted[target] = renderers;
    }

    // Public: called by PushToTalkService when Execute (Y) is pressed.
    public void ClearAllTints()
    {
        foreach (var kv in _tinted)
        {
            RestoreRenderers(kv.Value);
        }
        _tinted.Clear();
    }

    private static void RestoreRenderers(Renderer[] renderers)
    {
        if (renderers == null) return;
        var empty = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            try { r.SetPropertyBlock(empty); }
            catch { /* renderer was destroyed — nothing to do */ }
        }
    }

    private IEnumerator PostSelection(SelectPayload payload){
        string json = JsonUtility.ToJson(payload);
        Task<string> task = PostAsync(json);

        while(!task.IsCompleted){
            yield return null;
        }

        if (task.IsFaulted){
            string err = task.Exception?.GetBaseException()?.Message ?? "unknown";
            Debug.LogError($"[HandPinch] POST failed: {err}");
        }

        else                                                                                                                                                                          
        {                                                                                                                                                                           
            Debug.Log($"[HandPinch] server: {task.Result}");
        }

    }


    private async Task<string> PostAsync(string json){
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{flaskBaseUrl}/vr/select_target", content);
        return await response.Content.ReadAsStringAsync(); 
    }
}
