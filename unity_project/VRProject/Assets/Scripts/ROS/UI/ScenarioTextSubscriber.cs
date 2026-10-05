using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class ScenarioTextSubscriber : MonoBehaviour
{
    [SerializeField] private string topic = "/scenario_text";
    [SerializeField] private TMP_Text targetText;

    private string _pendingText;
    private bool _hasPending; 

    // Start is called before the first frame update
    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(topic, OnScenarioReceived);
        
    }

    void OnScenarioReceived(StringMsg msg){
        _pendingText = msg.data;// we put the text in the temporary text holder
        _hasPending = true;//the trigger turns on
    }

    // Update is called once per frame
    void Update()
    {
        if(_hasPending && targetText!=null){
            targetText.text = _pendingText;//we fill the canvas TMP_text
            _hasPending =false;//we turn off the trigger
        }
        
    }
}

