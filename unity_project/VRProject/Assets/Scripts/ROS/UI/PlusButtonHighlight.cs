using Unity.Robotics.ROSTCPConnector;                                                    
using UnityEngine;                                                                     
using UnityEngine.UI;                                                                  
using RosMessageTypes.Std;

/*
Tints the + button green when Flask publishes /vr/scenarios_ready = true
(LLM has generated a scenario). Reverts to original color when false                     
(after /vr/execute finishes, or after /vr/reset_context).
*/                                                                                       
public class PlusButtonHighlight : MonoBehaviour
{                                                                                      
    [Header("The + button's Image (drag from ControlPanel)")]
    [SerializeField] private Image plusButtonImage;                                      

    [Header("Tint when scenarios are ready")]                                            
    [SerializeField] private Color readyColor = new Color(0.20f, 0.75f, 0.30f);                                                                                    

    private const string Topic = "/vr/scenarios_ready";                                  
                
    private Color _originalColor;                                                      
    private bool  _colorCached;
    private bool  _pendingReady;
    private bool  _hasPending;
                                                                                        
    void Start()
    {                                                                                    
        if (plusButtonImage != null)
        {                                                                              
            _originalColor = plusButtonImage.color;
            _colorCached = true;
        }
        else
        {
            Debug.LogError("[PlusHighlight] Assign Plus Button Image in the inspector.");
        }                                                                                
        ROSConnection.GetOrCreateInstance().Subscribe<BoolMsg>(Topic, OnScenariosReady);
    }                                                                                    
                
    // ROS callback fires on a background thread; queue the change and apply it in Update.
    private void OnScenariosReady(BoolMsg msg)                                           
    {           
        _pendingReady = msg.data;                                                      
        _hasPending = true;
    }

    void Update()
    {
        if (!_hasPending || !_colorCached) return;
        plusButtonImage.color = _pendingReady ? readyColor : _originalColor;             
        _hasPending = false;
    }                                                                                    
} 