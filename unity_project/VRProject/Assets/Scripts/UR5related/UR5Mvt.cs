using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UR5Mvt : MonoBehaviour
{
    //Articlations Bodies of the UR5
    ArticulationBody[] ur5ABJoints = new ArticulationBody[6];//AB related to teh UR5 robot
    
    private string[] ur5UnityJointNames= {
        "arm_shoulder_link",
        "arm_upper_arm_link",
        "arm_forearm_link",
        "arm_wrist_1_link",
        "arm_wrist_2_link",
        "arm_wrist_3_link"
    };

    [Header("Joint Targets (degrees)")]
    [Range(-180,180)] public float shoulder = 0f;
    [Range(-180,180)] public float upperArm = -90f;
    [Range(-180,180)] public float forearm = 0f;
    [Range(-180,180)] public float wrist1 = -90f;
    [Range(-180,180)] public float wrist2 = 0f;
    [Range(-180,180)] public float wrist3 = 0f;
    
    // Start is called before the first frame update
    void Start()
    {

        float[] initialTargets = {shoulder, upperArm, forearm, wrist1, wrist2, wrist3};
        ArticulationBody[] allJoints = GetComponentsInChildren<ArticulationBody>();

        for(int i=0; i<ur5UnityJointNames.Length; i++){
            foreach(var joint in allJoints){
                if(joint.name == ur5UnityJointNames[i]){
                    ur5ABJoints[i] = joint;
                    var drive = joint.xDrive;
                    drive.target = initialTargets[i];
                    joint.xDrive = drive;
                    break;

                    //we have to add stiffness and damping to enable the join to move, otherwise it will not
                    //we cerate a drive object, we define its stiffness and damping
                    //Then, we add it to the joint
                }
            }
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        float[] targets = {shoulder, upperArm, forearm, wrist1, wrist2, wrist3};
        for(int i = 0; i<ur5ABJoints.Length; i++){
            if(ur5ABJoints[i] != null){
                var drive = ur5ABJoints[i].xDrive;
                //drive.target = targets[i]; teleports the arm which generates abrupt, jerky mvts 
                drive.target = Mathf.MoveTowards(drive.target, targets[i], Time.deltaTime * 30f); //moves to drive.target with the speed of 30deg a second, gemtle forces
                ur5ABJoints[i].xDrive = drive;

            }

        }
        
    }
}
