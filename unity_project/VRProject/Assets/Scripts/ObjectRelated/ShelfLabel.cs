using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[ExecuteAlways]//also run in edit mode to see the labels when Play button is not hit
/*Write teh label of the docking station in the ground, in front of the shelf*/
public class ShelfLabel : MonoBehaviour
{  
    public TMP_Text shelf_label;

    void Awake(){//runs once, before STart() because not dependent on other objects state
        shelf_label = GetComponentInChildren<TMP_Text>(); //get the tmp text from the object
    }

    // runs after all Update() calls and after animations have been applied for the frame
    //so ensures has the latest position of the box
    void LateUpdate()
    {
        if(shelf_label == null) return;
        shelf_label.text = name;
        // if(Camera.main){
        //     shelf_label.transform.rotation = Camera.main.transform.rotation; //we make sure that the label always rotates to face the camera 
        // }
        
    }
}

          