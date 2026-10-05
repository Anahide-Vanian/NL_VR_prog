using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetObjectDimensions : MonoBehaviour
{


    // Start is called before the first frame update
    void Start()
    {
        //the dimensions to return
        Vector3 dimensions = Vector3.zero;

        Renderer[] objRend = GetComponentsInChildren<Renderer>();

        //If the object doesnt have a renderer, lets use its coliderr
        if (objRend.Length>0){
            Bounds combined = objRend[0].bounds;
            for (int i = 1; i < objRend.Length; i++) {                                                                                                                                          
                combined.Encapsulate(objRend[i].bounds);                                                                                                                                      
            }                                                                                                                                                                                 
            dimensions = combined.size;//realworld size in Unity UNits
        }else {
                        
            Collider col = GetComponentInChildren<Collider>();

            if(col == null){
                Debug.LogWarning("No Renderer or Collider found on " + gameObject.name); //gameObject is a general csharp variable
                return;//if not colider
            } 

            dimensions = col.bounds.size;//realworld size in Unity UNits

        }

        
        Debug.Log("The object length is: " + dimensions.x + " width is: " + dimensions.z + " height is: " + dimensions.y);

        //note: bounds.size gives you the actual world dimensions in meters/units, already accounting for the scale and the mesh geometry

        
    }


}

