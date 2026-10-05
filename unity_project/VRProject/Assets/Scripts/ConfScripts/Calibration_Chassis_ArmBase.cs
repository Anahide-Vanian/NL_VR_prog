using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*


HOw to use ?

In the Inspector, create an empty Gane object and in the Inspector, attach the chassis_link and arm_base tarnsforms.

THen read the mount values in the console to see teh transform values from r100 chassis_link to UR5 arm_base_ frame. 
*/
                                                                                                                                                                  
public class Caliibration_Mount_ArmBase : MonoBehaviour {  

    public Transform chassisLink;
    public Transform armBase;
    
    void Start() {
        //we convert a point from world coordinates (armBase) into the local coordinate system of chassisLink
        Vector3 p = chassisLink.InverseTransformPoint(armBase.position);

        //rotation of armBase relative to chassisLink :(R_wold->Chassis)^-1 = R_Chassis->R_wold
        //And R_Chassis->R_wold * R_wold->ArmBAse = R_C->AB
        Quaternion q = Quaternion.Inverse(chassisLink.rotation) * armBase.rotation; 

        //Once we have position + rotation of arm base relative to chassis                                                                                                  
        // Conversion Unity (X-right, Y-up, Z-fwd) -> ROS (X-fwd, Y-left, Z-up)  
        //position transform from chassis to base                                                                                                                 
        float dx =  p.z;                                                                                                                                                              
        float dy = -p.x;                                                                                                                                                              
        float dz =  p.y;                                                                                                                                                            
        float yawDeg = q.eulerAngles.y;   //rotation transform from chassis to base                                                                                                                                           
        if (yawDeg > 180f) yawDeg -= 360f; //bc values only between 0 to -180 or 0 to 180max                                                                                                                                        
        float dyaw = -yawDeg * Mathf.Deg2Rad;   // Y-up -> Z-up flips sign
        Debug.Log($"MOUNT_DX={dx:F4} MOUNT_DY={dy:F4} MOUNT_DZ={dz:F4} MOUNT_DYAW={dyaw:F4}");                                                                                        
    }                                                                                                                                                                                 
}   