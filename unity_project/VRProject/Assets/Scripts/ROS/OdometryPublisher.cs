using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;


/*

THis script send the pose and velocity of the bas_link frame with respect to the odom frame (drifting world frame).
HOw to use? 

FRag and place the bas_link TRansform in robotTRanform so the origin of the robot base /odom is equal to base_link. Before, it was base_footprint but that was removes since 
base_footprint is static in Unity
*/

public class OdometryPublisher : MonoBehaviour
{

    //ROS Topic to publish to (TopicNAme = OdomTopic)
    private const string OdomTopic = "/r100_0000/odom";

    //Publishing frequency
    public float publishRate = 20f;

    //The robot root transform object to tarck its position and velocity 
    public Transform robotTransform; 

    private ROSConnection ros;
    private float timeElapsed;
    private Vector3 lastPosition;
    private Quaternion lastRotation;


    // Start is called before the first frame update
    void Start()
    {

        ros= ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(OdomTopic);

        //tha last pos/rot is at the beginning
        lastPosition = robotTransform.position;
        lastRotation = robotTransform.rotation;
        
    }

    // We call FIxed UPadte instead of Update for stability. The physic engien will always call this function at the defined timestep.
    //FOr Update, even if we have a fixed frame rate we might display one frame more or less example 29 FPS, 30FPS or 31FPS because software buggs
    void FixedUpdate()
    {
        //THis part is to define the publishing frequency of the odom values
        //We want to publish the teh odom values every 0.05 sec.
        //IF TimeSetp in UNity settings is 50Hz, the frame update will be every 0.02s (quicker)
        timeElapsed = timeElapsed + Time.fixedDeltaTime;

        if(timeElapsed < 1f /publishRate) return;//dont publish if still under the publishing rate

        //otherwise
        timeElapsed= 0f; //start over

        //Calculate velocities from position change
        Vector3 deltaPos = robotTransform.position - lastPosition;//change in psoition eg: (0.3,0.2,0.1)
        float dt = 1 / publishRate; //time elapsed

        OdometryMsg msg = new OdometryMsg();

        //Header


        //time stamp
        double unixTime = (System.DateTime.UtcNow - new System.DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc)).TotalSeconds;
        msg.header.stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg{
            sec =
            #if ROS2                                                                                                                                                                              
                (int)
            #else                                                                                                                                                                                 
                (uint)                                                                                                                                                                          
            #endif
                unixTime,
            nanosec = (uint)((unixTime % 1.0)*1e9)
        };


        msg.header.frame_id = "odom";
        //msg.child_frame_id = "base_footprint";// base_footprint never moves in Unity physics.
        msg.child_frame_id = "base_link";// moves 

                



        //POsition and Rotation: convert Unity coordinates to ROS coordinates
        //Unity (left-handed, Y-up) while ROS (right-handed, Z-up) 
        msg.pose.pose.position.x = robotTransform.position.z;
        msg.pose.pose.position.y = -robotTransform.position.x;
        msg.pose.pose.position.z = robotTransform.position.y;

        // quaternion transformation
        msg.pose.pose.orientation.x = robotTransform.rotation.z;
        msg.pose.pose.orientation.y = -robotTransform.rotation.x;
        msg.pose.pose.orientation.z = -robotTransform.rotation.y;
        msg.pose.pose.orientation.w = robotTransform.rotation.w;


        //Velocity

        msg.twist.twist.linear.x = Vector3.Dot(deltaPos, transform.forward) / dt;//transform.forward = (0,0,1) because X_ROS = Z_UNITY
        //We take the vector representing the change in position comapred to teh last position
        //We project it onto the robot's forward vector ( the unit vector pointing in the robot's forward direction)
        //TH result will give  us the amount of change in the FORWARD direction ( only the chaneg for teh forward component)

        //LAteral mvt: Ridgeback R100 has mecanum wheels : it can move sideways
        msg.twist.twist.linear.y = -Vector3.Dot(deltaPos, transform.right)/dt;//transform.right = unit vector pointing to the robot's right side
        //msg.twist.twist.angular.z= -(robotTransform.rotation.eulerAngles.y - lastRotation.eulerAngles.y) /dt;//nb : Y_(unity) = Z_(ROS)
        float yawDeltaDeg = Mathf.DeltaAngle(lastRotation.eulerAngles.y, robotTransform.rotation.eulerAngles.y); // Mathf.DeltaAngle handles 1 − 359 = −358° instead of 2°
        msg.twist.twist.angular.z = -yawDeltaDeg * Mathf.Deg2Rad /dt; //ROS needs rads and not dgrees

        //we publish
        ros.Publish(OdomTopic, msg);

        //update position, rotation
        lastPosition = robotTransform.position;
        lastRotation = robotTransform.rotation;
        
    }
}
