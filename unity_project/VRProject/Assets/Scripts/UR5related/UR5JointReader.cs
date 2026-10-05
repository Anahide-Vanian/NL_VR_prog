using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JointReader : MonoBehaviour
{
    //Articlations Bodies of the UR5
    ArticulationBody[] ur5UnityABJoints = new ArticulationBody[6];

    private string[] ur5UnityJointNames= {
        "arm_should_link",
        "arm_upper_arm_link",
        "arm_forearm_link",
        "arm_wrist_1_link",
        "arm_wrist_2_link",
        "arm_wrist_3_link"
    };

    // Start is called before the first frame update
    void Start()
    {

        //List of UR5 articulation bodies
        ArticulationBody[] allJoints = GetComponentsInChildren<ArticulationBody>();

        for(int i = 0; i <ur5UnityJointNames.Length; i++){
            foreach(var joint in allJoints){
                if(joint.name == ur5UnityJointNames[i]){
                        ur5UnityABJoints[i] = joint;//we found matching AB
                        break;//we break to save computation ressources
                }
            }
        }
        
    }

    [ContextMenu("Log Joint Positions")] 
    void LogJointPositions()
    {
        float[] radPositions = GetPositionsRad();
        float[] degPositions = GetPositionsDeg();

        //string.Join(", ", array) : converts the array to a comma-separated string. 
        Debug.Log(string.Join(", ", ur5UnityJointNames));
        Debug.Log(string.Join(", ",degPositions));
        Debug.Log(string.Join(", ",radPositions));
        
    }




    public float[] GetPositionsRad(){
        float[] positions = new float[6];

        for(int i = 0;i <ur5UnityABJoints.Length; i++){
            if(ur5UnityABJoints[i] != null){
                //jointPosition[0]; -> actual position of the joint in radian
                positions[i] = ur5UnityABJoints[i].jointPosition[0];

            }
        }
        return positions;
    }

        public float[] GetPositionsDeg(){
        float[] positions = new float[6];

        for(int i = 0;i <ur5UnityABJoints.Length; i++){
            if(ur5UnityABJoints[i] != null){

                positions[i] = ur5UnityABJoints[i].jointPosition[0] * Mathf.Rad2Deg; 

            }
        }
        return positions;
    }
}
