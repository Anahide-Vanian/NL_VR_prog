using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;


/*
This script SUbscribes to /r100_0000/cmd_vel topic (a TwistStampedMsg)
Read the angular.x and angular.z values from the message
Apply these velocitied to move the robot's ArticulationBody
*/

public class CmdVelSubscriber : MonoBehaviour
{

    //ROS topic we are Subscribing to 
    private const string CmdVelTopic = "/r100_0000/cmd_vel";

    //for watchfog
    private float lastCmdVelTime = -999f;
    public float cmdVelTimeout = 2.0f;

    //need to assign robot ArticulationBody (root articulation body of the entire platform)
    public ArticulationBody robotRoot;

    //Current velocities received from ROS
    private float linearVelocity = 0f;   // ROS x: forward
    private float lateralVelocity = 0f;  //ROS y:omnidirectional                                                                                                           
    private float angularVelocity = 0f;  // ROS z: yaw


    // Start is called before the first frame update
    void Start()
    {
        // At start, we register the subscriber with ROS TCP Connector
        ROSConnection.GetOrCreateInstance().Subscribe<TwistStampedMsg>(CmdVelTopic, OnCmdVelReceived);
        
    }

    void OnCmdVelReceived(TwistStampedMsg msg){
        linearVelocity = (float)msg.twist.linear.x;//according to ROS frame, x robot's forward facing direction
        lateralVelocity = (float)msg.twist.linear.y; //omnidirectional wheels
        angularVelocity = (float)msg.twist.angular.z;//in ROS, z is going up from the robot platform
        lastCmdVelTime = Time.time; //time teh command was received
    }

    // Update is called once per frame
    // FIxed UPdate called every fixed time interval ans is not tied to the frame rate (FPS). THe timestep is defined in UNITY'S PROJECT SETTINGS

    void FixedUpdate()
    {
        if(robotRoot == null) return;

        //If no velocity message arrived within cmdVelTimeout sec, we stop teh robot
        if(Time.time - lastCmdVelTime > cmdVelTimeout){

          linearVelocity = 0f;
          lateralVelocity = 0f;
          angularVelocity = 0f;


        }
        //Applying the velocities to the robot
        //robotRoot.velocity = transform.forward * linearVelocity;//transform refers to the GameObject this script is attached to and transform.forward = forward facing direction of the game object
        robotRoot.velocity = transform.forward * linearVelocity - transform.right * lateralVelocity;//omni
        robotRoot.angularVelocity = new Vector3(0,-angularVelocity,0);// Z in ROS = Y, negative sign to correct the difference in the rotation direction between teh two 
                                                                                                                
                                                                   
        
    }
}
