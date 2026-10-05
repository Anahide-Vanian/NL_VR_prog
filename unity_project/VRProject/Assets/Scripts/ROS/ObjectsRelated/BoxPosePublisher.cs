using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;


/*
This script publishes /box_positions as several tf2_msgs/TFMessage.
THe goeal is to publish pose in real-world of the gameObject.
THe idea is to send its transform matrix with resepect to the world farme "map" 
Each box pose will become a TRansformStamped object if with "TrackedBOx" as a tag.
child_frame_id = GameOBject.name.


How to use :

1) In Unity, we have to create a Tag called TrackedBox (in Projects SEtiings, Tags and Layers, create the new Tag)
2) Apply that tag to each box wanted
3) Create one empty GameObject,a manager like, BoxesPublisherNode and attach this script



*/

public class BoxPosePublisher : MonoBehaviour
{

    public string boxTag = "TrackedBox";//if blank, Game object default name will be used
    public float publishRate = 5f;//10Hz

    private string topic = "/box_positions";
    private GameObject[] boxes;//list of boxes we want to getthe position
    private ROSConnection ros;//need to create a ros connection object to create a publisher
    private float timeElapsed;



    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();//create ROS Connection
        ros.RegisterPublisher<TFMessageMsg>(topic);

        //fill the list with the boxes having the tracking tag
        boxes = GameObject.FindGameObjectsWithTag(boxTag);
        
    }

    // Update is called once per fixed timestep, every 1/50 seconds (50 HZ)
    void FixedUpdate()
    {

        //To puclish every 5 Hz and not 50 Hz
        timeElapsed = timeElapsed + Time.fixedDeltaTime; // time.fixedDeltaTime = 1/50
        if(timeElapsed < 1f/publishRate) return; //as long as we didnt reach 5 Hz, we return

        //put back to 0
        timeElapsed =0f;

        var msg = new TFMessageMsg();//contains an array of transforms (array of TransformSTamped)
        msg.transforms = new TransformStampedMsg[boxes.Length];//array size = nb of boxes found with tag

        //unix time
        double unixTime = (System.DateTime.UtcNow - new System.DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc)).TotalSeconds;

        var stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg{
            sec =
            #if ROS2                                                                                                                                                                              
                (int)
            #else                                                                                                                                                                                 
                (uint)                                                                                                                                                                          
            #endif
                unixTime,
            nanosec = (uint) ((unixTime %1.0) * 1e9) 
        };

        for(int i=0; i< boxes.Length; i++){
            var ts = new TransformStampedMsg();
            ts.header.stamp = stamp;
            ts.header.frame_id = "map";//world frame
            ts.child_frame_id = boxes[i].name;

            //the transform matrix
            //position
            ts.transform.translation.x = boxes[i].transform.position.z; //xUnity = zRos
            ts.transform.translation.y = -boxes[i].transform.position.x;//yUnity = -xRos
            ts.transform.translation.z = boxes[i].transform.position.y;

            //orientation - quaternion
            ts.transform.rotation.x = boxes[i].transform.rotation.z; //xUnity = zRos
            ts.transform.rotation.y = -boxes[i].transform.rotation.x;//yUnity = -xRos
            ts.transform.rotation.z = boxes[i].transform.rotation.y;
            ts.transform.rotation.w = boxes[i].transform.rotation.w;

            msg.transforms[i] = ts;

        }

        ros.Publish(topic, msg);
        
    }
}
