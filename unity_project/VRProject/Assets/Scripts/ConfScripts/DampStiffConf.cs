using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*

Script to configure STiffness and Damping in teh articulation bodies of the robot.
Needs to be on a parent of all the arm joints : arm_base_link 
*/

public class DampStiffConf : MonoBehaviour
{
    [Header("Drive Settings")]
    public float stiffness = 2000f;
    public float damping = 500f;
    public float angularDamping = 10f;

    [Header("Gripper settings")]
    public float gripperStiffness = 10000f;//10000f
    public float gripperDamping = 200f; //200f;                                                    




    //Start() to DampStiffConf.cs so it runs automatically when Play begins we dont need to hit "Configure All Joints" in inspector
    void Start(){
        ConfigAllJoints();
    }


    [ContextMenu("Configure All Joints")] 
    public void ConfigAllJoints(){
        ArticulationBody[] ur5_grip_AB = GetComponentsInChildren<ArticulationBody>();
        int count = 0;

        foreach(ArticulationBody ab in ur5_grip_AB){
            if(ab.jointType == ArticulationJointType.FixedJoint)
                continue;

            //If it has not a Fixed JOint
            ab.angularDamping = angularDamping;

            //We create a virtual motor entity
            ArticulationDrive drive = ab.xDrive;

            //LOwer values for gripper joints
            if(ab.name.ToLower().Contains("finger")|| ab.name.ToLower().Contains("gripper")|| ab.name.ToLower().Contains("robotiq")){

                    drive.stiffness = gripperStiffness;
                    drive.damping = gripperDamping;
                    drive.forceLimit = 300f;
            }
            else{// setiing xDRive for teh ab belonging to the ur5 arm
                drive.stiffness = stiffness;
                drive.damping = damping;
                drive.forceLimit = 2000f;
            }

            ab.xDrive = drive;
            count++;
        }
        //Debug.Log($"Configured Stiffness and Damping for {count} joints");


    }



}
