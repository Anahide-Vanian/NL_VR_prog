using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;//Sensor Message Type


/*
EVery 1/publishRate seconds, the LIdar completes a 270deg scan of the room by cating rays from -135deg to 135 deg at a fixed angular increment.
The list of laser scans are compavted and sent in one time as LaserSCan message. 

*/

public class LaserScanPublisher : MonoBehaviour
{

    //ROS Topic to publosh to 
    public string LidarTopic = "/r100_0000/sensors/lidar2d_0/scan";

    //Publishing frequency
    public float publishRate = 50f;//SLAM can't process scan messages fast enough and the queue grows until it breaks so 10f->3f per physics setp 

    // The transform of the lidar sensor (the GameObject representing the lidar)
    public Transform lidarTransform;

    // Lidar parameters (matching the real SICK LMS1xx specs)
    public float angleMin = -2.356f;// -135deg in radians
    public float angleMax = 2.356f;// 135deg
    public float angleIncrement = 0.00872f; // ~0.5 degrees in radians
    public float rangeMin = 0.1f; //minimum valid range in meters
    public float rangeMax = 50f; // max valid range in m

    private ROSConnection ros;
    private float timeElapsed;
    private int numRays;


    // Start is called before the first frame update
    void Start()
    {

        //Ros COnnection
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<LaserScanMsg>(LidarTopic);

        //compute how many rays based on angle and increment
        numRays = Mathf.RoundToInt((angleMax- angleMin)/angleIncrement) +1;
        
    }


    /*
        EXample of a LaserScanMsg:

        header:
            stamp:
                sec: 1710001234
                nanosec: 123456789
            frame_id: "laser_frame"

        angle_min: -1.57        # start angle (radians)
        angle_max: 1.57         # end angle (radians)
        angle_increment: 0.01   # angular step

        time_increment: 0.0     # time between rays
        scan_time: 0.02         # time for full scan

        range_min: 0.1          # minimum valid distance
        range_max: 10.0         # maximum valid distance

        ranges: [1.2, 1.3, 1.1, 0.9, ...]   # distances (meters)
        intensities: [100, 120, 90, ...]    # optional

        */

    // Update is called once per fixedDeltaTime
    void FixedUpdate()
    {
        timeElapsed += Time.fixedDeltaTime;
        if(timeElapsed < 1f / publishRate) return;//as long as timeElapsed is shorter
        timeElapsed = 0f;// reset only if we reached the publshing time and that we DON'T return 

        LaserScanMsg msg = new LaserScanMsg();

        //Header
        msg.header.frame_id = lidarTransform.name; //"lidar2d_0" or "lidar2d_1"


        //time.stamp
        double unixTime = (System.DateTime.UtcNow - new System.DateTime(1970, 1,1,0,0,0,0, System.DateTimeKind.Utc)).TotalSeconds;

        msg.header.stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg{
            sec =
            #if ROS2                                                                                                                                                                              
                (int)
            #else                                                                                                                                                                                 
                (uint)                                                                                                                                                                          
            #endif
                unixTime,
            nanosec = (uint) ((unixTime % 1.0) * 1e9)

        };

        //scan params
        msg.angle_min = angleMin;
        msg.angle_max = angleMax;
        msg.angle_increment = angleIncrement;
        msg.time_increment= 0f;
        msg.scan_time = 1f/ publishRate;
        msg.range_min = rangeMin;
        msg.range_max = rangeMax;

        //Cast rays and fill ranges array
        msg.ranges = new float[numRays];

        for(int i=0; i< numRays; i++){

            //current ray angle in the lidar's local frame
            float angle = angleMin + i * angleIncrement;

            // In Unity: lidar does an horizontal scan (in the XZ plane)
            //angle = 0 is forward and positive angles go left, towards +X
            Vector3 direction = lidarTransform.TransformDirection(
                new Vector3(-Mathf.Sin(angle),0f,Mathf.Cos(angle))// in unity (Z_u = X_ros,  Y_u = Z_ros, X_u = -Y_ros)
            );

            RaycastHit hit;
            //Physics.Raycast shoots an invisible ray from a point (lidarTransform.position) in a direction and returns true if it hitted sthg within the max range
            if(Physics.Raycast(lidarTransform.position, direction, out hit, rangeMax)){//If Raycast succedeed
                msg.ranges[i] = hit.distance;// if it hits something, Unity fills this with info (distance, what was hit, etc..
            }
            else{
                msg.ranges[i] = float.PositiveInfinity; //Ros convention if no hit
            }
        }

        ros.Publish(LidarTopic, msg);


    }
}
