// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using RosMessageTypes.Std;
// using Unity.Robotics.ROSTCPConnector;



// /*

// This script subscribes to /gripper_position/cmd (Float32)
// It will set the robotiq gripper xDrive targets to the commanded value

// Needs to be attchaged ti the gripper GameoBJ
// */


// public class GripperSubscriber : MonoBehaviour
// {

//     private const string topic = "/gripper_position/cmd";
//     private List<ArticulationBody> fingerJoints = new List<ArticulationBody>();//all teh gripper ab that will be commended
//     private float targetRad = 0f; // 0 = open and 0.85 close 
//     private ROSConnection ros;

//     void Start()
//     {
//         //cpllect all Articulation Bodies that contain finger or knuckle
//         ArticulationBody[] artBodies = GetComponentsInChildren<ArticulationBody>();
//         foreach(var ab in artBodies){
//             string name = ab.gameObject.name.ToLower();
//             if(name.Contains("finger") || name.Contains("knuckle")){
//                 fingerJoints.Add(ab);//we foudn + add gripper ab to be actusated
//             }
//         }
        

//         //Ros COnnection
//         ros = ROSConnection.GetOrCreateInstance();//instantiate the ros connection
//         ros.Subscribe<Float32Msg>(topic, MoveGripperCallBack);//create a subscriber to the topic gripper cmd
            
//     }

//     // Update is once every fixed timestep defined in teh project settings fixedDeltaTime
//     void FixedUpdate()
//     {
//         foreach(var ab in fingerJoints){

//             if(ab == null) continue;
//             var drive =  ab.xDrive;//create a virtual motor
//             drive.target = targetRad * Mathf.Rad2Deg;
//             ab.xDrive = drive;
//         }

        
//     }

//     //called when everytime we get a new message arrives on the topic, event driven
//     void MoveGripperCallBack(Float32Msg msg){

//         targetRad = msg.data;//the cmd value
         

//     }
// }


using System.Collections.Generic;
using UnityEngine;                                                                                                                                                                    
using RosMessageTypes.Std;                                                                                                                                                            
using Unity.Robotics.ROSTCPConnector;                                                                                                                                                 
                                                                                                                                                                                    
/*                                                                                                                                                                                  
Subscribes to /gripper_position/cmd (Float32, radians, 0=open .. 0.7=closed)                                                                                                          
Drives the Robotiq 2F-140 master joint and applies the URDF mimic multipliers                                                                                                         
to the 5 follower joints. Attach to the gripper root GameObject.                                                                                                                      
*/                                                                                                                                                                                    

/*HOw the grab/release logic works:

  1. Gripper commanded to close (targetRad > 0.3) AND we're not already holding anything -> TryGrab() fires.                                                                         
  2. Draw an invisible sphere of radius 20cm centered on the gripper GameObject's position.
   Physics engine returns every collider inside that sphere (walls, floor, boxes, robot parts, whatever).                       
  3. For each collider hit, walk up to its Rigidbody root. If the collider is on SM_box_2 (untagged child), the Rigidbody is on C1-1-box (parent).So attachedRigidbody.gameObject returns C1-1-box.                                                                     
  4. Check if that root has the TrackedBox tag. Walls/floor/robot -> no tag -> skip. Real boxes -> tagged -> gotcha.                                                                 
  5. On the first tagged box found:                                                      
    - Save its Rigidbody reference.                                                        
    - Freeze physics (isKinematic = true) so gravity/collisions don't fight the parent transform.                                                                             
    - Make it a child of the gripper GameObject -> from now on, it moves rigidly with the arm.                                        
    - Log [Gripper] Grabbed <name>.                                                        
  6. Return. Only one box grabbed per close.
*/

public class GripperSubscriber : MonoBehaviour                                                                                                                                        
{                                                                                                                                                                                   
    private const string topic = "/gripper_position/cmd";
    private const float MIN_RAD = 0f;                                                                                                                                                 
    private const float MAX_RAD = 0.7f;
                                                                                                                                                                                    
    //link name -> mimic multiplier relative to gripper_finger_joint                                                                                                   
    private static readonly Dictionary<string, float> mimicMap = new Dictionary<string, float>                                                                                        
    {                                                                                                                                                                                 
        { "gripper_left_outer_knuckle",  +1f }, //main actuator(gripper_finger_joint = gripper_left_outer_knuckle)
        { "gripper_left_inner_knuckle",  -1f },                                                                                                                                       
        { "gripper_left_inner_finger",   +1f },                                                                                                                                     
        { "gripper_right_outer_knuckle", -1f },                                                                                                                                       
        { "gripper_right_inner_knuckle", -1f },                                                                                                                                       
        { "gripper_right_inner_finger",  +1f },       
                                                                                                                                
    };
                                                                                                                                                                                    
    private struct DrivenJoint                                                                                                                                                        
    {
        public ArticulationBody ab;                                                                                                                                                   
        public float multiplier;                                                                                                                                                    
    }

    private readonly List<DrivenJoint> fingerJoints = new List<DrivenJoint>();
    private float targetRad = 0f;
    private ROSConnection ros;

    [Header("Grab settings")]
    [SerializeField] private float grabRadius = 0.2f;      // 20cm sphere around gripper root
    [SerializeField] private float grabThreshold = 0.3f;    // targetRad above this = try to grab
    private GameObject _heldBox;
    private Rigidbody _heldBoxRb;
    private bool _heldBoxWasKinematic;
                                                                                                                                                                                    
    void Start()
    {
        ArticulationBody[] artBodies = GetComponentsInChildren<ArticulationBody>();
        foreach (var ab in artBodies)                                                                                                                                                 
        {
            string name = ab.gameObject.name.ToLower();  
            //check if ab name is in the list of mimicJoints                                                                                                                             
            if (mimicMap.TryGetValue(name, out float mult))                                                                                                                           
            {
                //create new driven joint struct
                DrivenJoint drivenJoint = new DrivenJoint {ab = ab, multiplier = mult}; 
                fingerJoints.Add(drivenJoint);//we add teh struct in the list                                                                                                     
            }                                                                                                                                                                         
        }
                                                                                                                                                                                    
        if (fingerJoints.Count != mimicMap.Count)                                                                                                                                   
        {
            Debug.LogWarning($"Expected {mimicMap.Count} joints but we found {fingerJoints.Count}");
        }                                                                                                                                                                             

        ros = ROSConnection.GetOrCreateInstance();                                                                                                                                    
        ros.Subscribe<Float32Msg>(topic, MoveGripperCallBack);                                                                                                                      
    }                                                                                                                                                                                 
                                                                                                                                                                                    
    void FixedUpdate()
    {
        foreach (var j in fingerJoints)
        {
            if (j.ab == null) continue;
            var drive = j.ab.xDrive;
            drive.target = targetRad * j.multiplier * Mathf.Rad2Deg;
            j.ab.xDrive = drive;
        }

        // Grab / release based on gripper command
        if (targetRad > grabThreshold && _heldBox == null)
            TryGrab();
        else if (targetRad <= grabThreshold && _heldBox != null)
            Release();
    }

    void MoveGripperCallBack(Float32Msg msg)
    {
        targetRad = Mathf.Clamp(msg.data, MIN_RAD, MAX_RAD);
    }

    void TryGrab()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, grabRadius);
        foreach (var c in hits)
        {
            // Collider may be on a child (e.g. SM_box_2) while the tag + Rigidbody
            // live on the parent (e.g. C1-1-box). Walk to the Rigidbody root and check there.
            GameObject boxRoot = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
            if (!boxRoot.CompareTag("TrackedBox")) continue;

            _heldBox = boxRoot;
            _heldBoxRb = boxRoot.GetComponent<Rigidbody>();
            if (_heldBoxRb != null)
            {
                _heldBoxWasKinematic = _heldBoxRb.isKinematic;
                _heldBoxRb.isKinematic = true;
            }
            _heldBox.transform.SetParent(transform, true);
            Debug.Log($"[Gripper] Grabbed {_heldBox.name}");
            return;
        }
    }

    void Release()
    {
        if (_heldBox == null) return;

        // Preserve world position, snap rotation to upright, zero velocities
        Vector3 pos = _heldBox.transform.position;
        _heldBox.transform.SetParent(null, true);
        _heldBox.transform.position = pos;
        _heldBox.transform.rotation = Quaternion.identity;

        if (_heldBoxRb != null)
        {
            _heldBoxRb.isKinematic = _heldBoxWasKinematic;
            _heldBoxRb.velocity = Vector3.zero;
            _heldBoxRb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[Gripper] Released {_heldBox.name}");
        _heldBox = null;
        _heldBoxRb = null;
    }
}
