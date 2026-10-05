using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelDampingConf : MonoBehaviour
{

    [Header("Wheel Settings:")]
    public float wheelAngularDamping = 50f;
    public float wheelJointFriction = 10f;

    [Header("Rocker Settings:")]
    public float rockerAngularDamping = 50f;

    // Start is called before the first frame update
    void Start()
    {
       ArticulationBody[] all_ABodies = GetComponentsInChildren<ArticulationBody>(); 

       foreach(ArticulationBody ab in all_ABodies){
            string name = ab.gameObject.name.ToLower();

            if(name.Contains("wheel")){
                //Set damping and forceLimit of the ab
                ab.angularDamping = wheelAngularDamping;
                ab.jointFriction = wheelJointFriction;

                //the motor damping as well
                var drive = ab.xDrive;
                drive.damping = wheelAngularDamping;
                ab.xDrive = drive;
            }
            else if(name.Contains("rocker")){
                ab.angularDamping = rockerAngularDamping;
            }
       }
    }
}
