using UnityEngine;
using UnityEditor;

/*

How to use ? 

1) In the Hierarchy, select boxes ( Ctrl+A  and Ctrl+click rest)                                                     
2) then top menu -> Tools -> Set KLT Dimensions

*/

public class SetObjectDimensions : Editor
{
    [MenuItem("Tools/Set Box Dimensions")]
    // Start is called before the first frame update
    static void SetDimensions()
    {

       

        // // 30110 Shelf Bin yellow
        // float targetX = 0.2953f; // length
        // float targetZ = 0.0699f; // width
        // float targetY = 0.1016f; // height

     

        // // 30124 Shelf Bin vert
        // float targetX = 0.6001f; // length
        // float targetZ = 0.1048f; // width
        // float targetY = 0.1016f; // height

        // 30128 Shelf Bin rouge
        float targetX = 0.4540f; // length
        float targetZ = 0.1048f; // width
        float targetY = 0.1016f; // height

        

        // //SEt the target dimensions 29.53 × 10.48 × 10.16 cm
        // //30120 Shelf Bin blue
        // float targetX = 0.2953f; //length
        // float targetZ = 0.1048f; //width
        // float targetY = 0.1016f; //height


        GameObject[] selectedGameObjs = Selection.gameObjects;

        foreach (GameObject gameObj in selectedGameObjs){
            Vector3 objCurrentSize;

            Renderer ren = gameObj.GetComponent<Renderer>();
            if (ren != null){// skip if renderer not found
                objCurrentSize  = ren.bounds.size;  
            }
            else{
                Collider col = gameObj.GetComponent<Collider>();
                if (col == null){                                                                     
                    Debug.LogWarning("Skipping " + gameObj.name + ": no Renderer or Collider found.");                          
                    continue;
                }  
                objCurrentSize = col.bounds.size;
            }

            //Compute new scale
            //transform = reserved C# keyword
            float scaleX = gameObj.transform.localScale.x * targetX / objCurrentSize.x;
            float scaleZ = gameObj.transform.localScale.z * targetZ / objCurrentSize.z;
            float scaleY = gameObj.transform.localScale.y * targetY / objCurrentSize.y;

            //Save old dimensions to enable undo ( ctrl+z)
            Undo.RecordObject(gameObj.transform, "Set Box Dimensions");

            //Assign
            gameObj.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

        }

        // Confirm result                                                     
        Debug.Log("Applied dimensions");
        
    }

}
