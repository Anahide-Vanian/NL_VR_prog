using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


//[ExecuteAlways]//also run in edit mode to see the labels when Play button is not hit
public class BoxLabel : MonoBehaviour
{
    public TMP_Text label;

    void Awake(){//runs once, before STart() because not dependent on other objects state
        label = GetComponentInChildren<TMP_Text>(); //get the tmp text from the object
    }

    // runs after all Update() calls and after animations have been applied for the frame
    //so ensures has the latest position of the box
    void LateUpdate()
    {
        if(name == null) return;
        label.text = name;
        if(Camera.main){
            label.transform.rotation = Camera.main.transform.rotation; //we make sure that the label always rotates to face the camera 
        }
        
    }
}

          