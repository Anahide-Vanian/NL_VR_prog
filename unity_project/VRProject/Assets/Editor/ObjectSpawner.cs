using UnityEditor;
using UnityEngine;

/*

HOw it works? 

Takes the prefab object in prefabPath and assings it as a child to the parentGameObj.
INstantiates several times the children to fill the parent object
*/

public class ObjectSpawner : Editor
{

    //static string prefabPath = "Assets/Warehouse/MyPrefabs/Screw_08_Aluminium_High.prefab";// change teh prefab path to whatever object 
    //static string prefabPath = "Assets/Warehouse/MyPrefabs/Washer_01_Copper_High.prefab";
    //static string prefabPath = "Assets/Warehouse/MyPrefabs/Screw_10_Copper_High.prefab";
    static string prefabPath = "Assets/Warehouse/MyPrefabs/Washer_01_Aluminium_High.prefab";

    [MenuItem("Tools/Spawn Objects In Selected")]
    static void SpawnObjects(){
        GameObject parentGameObj = Selection.activeGameObject;//get the game object we clicked on
        Vector3 dims;

        if (parentGameObj == null){
            Debug.LogWarning("GameObject not found");
            return;
        }

        //Get prefab of object that will become child
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);  
                    
        if (prefab == null)                                                   
        {                                                                     
            Debug.LogError("Prefab not found");             
            return;
        }  

        //Computing object dimensions ( L and W needed) using its bottom Collider
        // dims = col.bounds.size; // Before we used this because we used a one BOx Collider for the box. 
        // But we removed to manually create several colliders and have a convex object and be able to fill the screws
        // col.bounds.size this gives the dimensions using the world frame X/Z
        //we should use the X/Z form the local frame of the box to define the dimensions of the bottom box frame:
        BoxCollider[] boxCols = parentGameObj.GetComponents<BoxCollider>();

        if(boxCols.Length > 0){
            BoxCollider bottomFaceCol = boxCols[0];
            //we try to find the BoxCollider corresponding to the bottom face of the box
            //We will use the dimenions of the bottom face of the box to fill the box with screws
            foreach(BoxCollider bc in boxCols){
                if(bc.size.y < bottomFaceCol.size.y) bottomFaceCol = bc;
            }
            //dims = bottomFaceCol.size;// we multiply by lossyScale because 

            //BoxCollider.size stays constant and always in local space in UNity (never scaled up/down even if parent was)
            //So : bottomFaceCol.size = (1, 1, 1) -> original collider size (local), it the orginal size of the object, original prefab size
            //lossyScale = (0.1, 0.1, 0.1) -> scale applied to teh object in UNity world and how we see it in the scene
            // What the object looks like in the world : bounds.size = 0.1
            //SO what we are doing below is : (0.1,0.1,0.1) = (1,1,1) × (0.1,0.1,0.1) 

            //The screws were overflowing the walls, so we just add a padding factor
            dims = Vector3.Scale(bottomFaceCol.size, parentGameObj.transform.lossyScale)* 0.85f;

        }
        else{
            //try renderer : we use renderer because collider might not work in Editor mode (until pLay is not pressed) because Physics are not applied yet
            Renderer ren = parentGameObj.GetComponent<Renderer>();

            if(ren == null){
                Debug.LogWarning("GameObject without Renderer!");
                return;
            }
            else{
                dims = ren.bounds.size;  //full sizes of the collider/renderer bounds
            }


        }
        

        //Settings
        //float childScale  = 0.05f;//need to scale down the downloaded/imported prefab from the Asset store
        
        float spacingX = 0.02f;//0.5cm increments                                               
        float spacingZ = 0.02f;  
        int objs_nb_z = Mathf.FloorToInt((dims.z - spacingZ) /spacingZ); // nb of objects along z - 0.5cm for padding
        // we multiplied by the 0.8 because the colider width is wider than the flat surface width so teh screws were going out (its empirical)
        int objs_nb_x = Mathf.FloorToInt((dims.x - spacingX) /spacingX); // nb of objects along x- 0.5cm     + *0.8f is needed because the dimensions of the collider are bigger than the object itslef 
        
        //along Y axis:
        float offsetY  = 0.04f; // height above the parent pivot, we want the object to be a bit higher than the object flat surface
        float spacingY = 0.03f; // we will pile up the child objects with spacingY as disantce incerements
        int objs_nb_y = 3;//nb of layers along Y 

        Debug.Log("dims: " + dims);                                                   
        Debug.Log("objs_nb_x: " + objs_nb_x + "  objs_nb_z: " + objs_nb_z); 

        //COmpute the X/Z lengths of the grid 

        float totalX = (objs_nb_x - 1)* spacingX;//distance from the first to the last object (-1 because we dont count first or last object otherwise we will add spacing one more time)
        float totalZ = (objs_nb_z - 1)* spacingZ;

        //we start from the left from the middle of the object's pivot center and not at (0,0) that is centered in the middle                                                
        float startX = -totalX / 2f;            
        float startZ = -totalZ / 2f;

        //SInce the prefab because the child of the parents, it deforms so we have to remove teh scale parameters of the child after assigning it in the loop below
        //We create an in-between GAme object that will be the parent of all the child objects so :   KLT Box (parent) -> Screws (container) -> [Screw_01, Screw_02 .., Screw_0N] 
        GameObject container = new GameObject("Screws");                                                                                                                                      
        Undo.RegisterCreatedObjectUndo(container, "Screws Container");                                                                                                                        
        container.transform.SetParent(parentGameObj.transform, false);                                                                                                                        
        Vector3 ps = parentGameObj.transform.lossyScale;// get the transform of teh parent
        container.transform.localScale = new Vector3(1f/ps.x, 1f/ps.y, 1f/ps.z); //correcting scale - removing parent scale effect                        

        for(int y = 0; y <objs_nb_y; y++){//add object along y as well
            for(int x = 0; x <objs_nb_x; x++){
                for(int z = 0; z <objs_nb_z; z++){

                    Vector3 localPos= new Vector3(startX + x * spacingX, offsetY + y * spacingY, startZ + z*spacingZ);//evolving by increments
                    GameObject childGameObj= (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);//passing prefab to container.transform make them child automatically
                    Undo.RegisterCreatedObjectUndo(childGameObj, "Child Object");
                    childGameObj.transform.localPosition = localPos;
                    childGameObj.transform.localRotation = Random.rotation;//addd a random rotation
                }
            }
        }





    }
}
 

