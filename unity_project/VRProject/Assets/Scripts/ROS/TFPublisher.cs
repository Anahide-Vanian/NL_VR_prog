using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;


/* 

Warning ROS: base_footprint is the robot's ground position.
But in Unity, the equivalent of that is base_link's world position.

In Unity's physics system:                                                    
parent : base_footprint  -> parent GameObject, FIXED and never moves              
child: base_link -> ArticulationBody, moves via physics 

SO we will publish in "/r100_0000/tf" the position of teh base_link frame and not base_footprint because the first moves the other is static.
BUt the trick is tha we will puclish this data as : publish odom -> base_footprint. ROS needs that, base_footprint moves according to ROS 

HOw to use, trang and drop the Transform of the base_link only, keep teh one of base_footprint empty, not needed


*/

public class TFPublisher : MonoBehaviour
{

    private const string TFTopic = "/r100_0000/tf";

    public float publishRate= 100f;

    //The robot root transform (base_footprint in ROS = the GameOBject at groundlevel)
    public Transform baseFootprint; // Transform = GameObject HERE, leave empty
    public Transform baseLink;//HERE, fill with teh TRansform of base_link

    private ROSConnection ros;
    private float timeElapsed;

    // Start is called before the first frame update
    void Start()
    {

        //create ROS Publisher
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TFMessageMsg>(TFTopic);
        
    }

    // FixedUpdate to ensure one will not be missed
    void FixedUpdate()
    {
        timeElapsed = timeElapsed + Time.fixedDeltaTime; //Time.fixedDeltaTime; = time between each FixedUpdate() call
        if(timeElapsed < 1f / publishRate) return;//as long as timeElapsed is shorter
        timeElapsed = 0f;// reset only if we reached the publshing time and that we DON'T return 

        var tfMsg = new TFMessageMsg();//create a message type of TFMessage
        tfMsg.transforms = new TransformStampedMsg[] {
            BuildOdomToBaseFootprint(),//Transform Matrix from odom to base link
            //BuildBaseFootprintToBaseLink()//Transform Matrix from base link to base footprint
 
        };

        //Publsih
        ros.Publish(TFTopic, tfMsg);

    }


    /* Function below cerates a  transform object because TF message contains a list of transforms:

        transforms:
            - header:
                stamp:
                sec: 1710001234
                nanosec: 123456789
            frame_id: "odom"
            child_frame_id: "base_link"
            transform:
                translation:
                x: 1.0
                y: 0.5
                z: 0.0
                rotation:
                x: 0.0
                y: 0.0
                z: 0.0
                w: 1.0
    */


    //This method creates the transofrmation  matrix from odom to basefootprint
    private TransformStampedMsg BuildOdomToBaseFootprint(){
        var ts = new TransformStampedMsg();//stamped because values are valid for the robot only at an exact time 

        ts.header.frame_id = "odom";
        ts.child_frame_id = "base_footprint";// in reality we send the position of base_link but SLAM should think that it gets odom -> base_footprint, otherwise he will compute the wrong TF chain. 

        //add timestamp so not equal to 0 
        //my computer Unix time ( April 9th 2026, 10:30:00) - seconds since Jan 1 1970
        //.TotalSeconds converts that duration to a plain number of seconds
        double unixTime = (System.DateTime.UtcNow - new System.DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc)).TotalSeconds;

        ts.header.stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg{
           sec =
            #if ROS2                                                                                                                                                                              
                (int)
            #else                                                                                                                                                                                 
                (uint)                                                                                                                                                                          
            #endif
                unixTime,
            nanosec = (uint)((unixTime % 1.0) *1e9)

        };

        //add position and rotation by converting to ROS coordinates
        //Unity : X = left, Y up , Z forward while ROS : x= forward, y= right and z= up 
        ts.transform.translation.x = baseLink.position.z;//baseFootprint.position.z; we changed to baseLink
        ts.transform.translation.y = -baseLink.position.x;
        ts.transform.translation.z = baseLink.position.y;

        ts.transform.rotation.x = baseLink.rotation.z;
        ts.transform.rotation.y = -baseLink.rotation.x;
        ts.transform.rotation.z = -baseLink.rotation.y;
        ts.transform.rotation.w = baseLink.rotation.w;
        
        return ts;

    }



//     private TransformStampedMsg BuildBaseFootprintToBaseLink(){
//         var ts = new TransformStampedMsg();//stamped because values are valid for the robot only at an exact time 

//         ts.header.frame_id = "base_footprint";
//         ts.child_frame_id = "base_link";

//         //add timestamp so not equal to 0 
//         //my computer Unix time ( April 9th 2026, 10:30:00) - seconds since Jan 1 1970
//         //.TotalSeconds converts that duration to a plain number of seconds
//         double unixTime = (System.DateTime.UtcNow - new System.DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc)).TotalSeconds;

//         ts.header.stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg{
//             sec  = (int)unixTime, 
//             nanosec = (uint)((unixTime % 1.0) *1e9)

//         };

//         //add position and rotation by converting to ROS coordinates
//         //Unity : X = left, Y up , Z forward while ROS : x= forward, y= right and z= up 
//         ts.transform.translation.x = baseFootprint.position.z;
//         ts.transform.translation.y = -baseFootprint.position.x;
//         ts.transform.translation.z = baseFootprint.position.y;

//         ts.transform.rotation.x = baseFootprint.rotation.z;
//         ts.transform.rotation.y = -baseFootprint.rotation.x;
//         ts.transform.rotation.z = baseFootprint.rotation.y;
//         ts.transform.rotation.w = baseFootprint.rotation.w;
        
//         return ts;
//     }
}
