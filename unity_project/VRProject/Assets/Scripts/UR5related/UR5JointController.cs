using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

//AB = Articluation Bodies
public class RobotJointController : MonoBehaviour
{
    private ArticulationBody[] ur5ABJoints = new ArticulationBody[6];

    private string[] ur5UnityABJointNames = {
        "arm_shoulder_link",
        "arm_upper_arm_link",
        "arm_forearm_link",
        "arm_wrist_1_link",
        "arm_wrist_2_link",
        "arm_wrist_3_link"
    };

    private string[] rosJointNames = {
        "shoulder_pan_joint",                                                                               
        "shoulder_lift_joint",                                                                              
        "elbow_joint",                                                                                      
        "wrist_1_joint",                                                                                    
        "wrist_2_joint",                                                                                    
        "wrist_3_joint"                                                                                     
    };                                                                                                      
       

    // Start is called before the first frame update
    void Start()
    {
        // Gets all the articulation bodies of the robot
        ArticulationBody[] allUnityABJoints = GetComponentsInChildren<ArticulationBody>();

        //Gather the ArticlautionBodies of the ur5 only in unity 
        for(int i=0; i< ur5UnityABJointNames.Length; i++){
            foreach(var joint in allUnityABJoints){
                if(joint.name == ur5UnityABJointNames[i]){
                    ur5ABJoints[i] = joint;//Found UR5 joint
                    break;
                }
            }
        }

        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>("/joint_states",OnJointCommand);
        
        Debug.Log($"RobotJointController ready. Found {ur5ABJoints.Length} joints."); 
    }

    void OnJointCommand(JointStateMsg msg)
    {
        //For checking the correct joint names:
        // Debug.Log($"Received joint command with {msg.position.Length} positions");

        // Debug.Log($"Names count: {msg.name.Length}, Positions count: {msg.position.Length}");                   
        // for (int i = 0; i < msg.name.Length; i++)                                                               
        // {                                                                                                       
        //     Debug.Log($"  Name[{i}]: '{msg.name[i]}'");                                                         
        // } 
        //Debug.Log($"GOT MESSAGE: pos[0]={msg.position[0]}");  // ADD THIS

        for(int i = 0; i < msg.name.Length && i<msg.position.Length; i++){
            //Find 
            for(int j=0; j< ur5ABJoints.Length; j++){
                if (msg.name[i] == rosJointNames[j] && ur5ABJoints[j] != null){
                    var drive = ur5ABJoints[j].xDrive;
                    drive.target = (float)(msg.position[i]* Mathf.Rad2Deg);
                    ur5ABJoints[j].xDrive = drive;

                    //I am checking the motors, if they are actually set                                                        
                    //Debug.Log($"Setting {rosJointNames[j]} to {drive.target} deg. Actual drive.target:{ur5ABJoints[j].xDrive.target}");  
                }
            }
            
        }

    }
}
