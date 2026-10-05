using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*

THis script moves the main camera during play. The camera follows and move along with the robot to enable the user to see it in action
To be attached to the main camera and the robot tarnsform should be dragged and dropped in taregt
*/
public class FollowerCamera : MonoBehaviour
{


    public Transform target;//the robot will be the one that we are following
    public float smoothingFraction = 10f;
    public float smoothingFractionRot = 10f;
    public float distanceBehind = 1f;                                                      
    public float heightAbove = 1.5f; 
    public float sideOffset = 1.5f; 

    // Called once per frame, after teh physics update was applied to all the objects in the environment
    void LateUpdate()
    {
        if(target == null) return;

        //tareget.position = robot position
        //target.forward = vector poiting the forward direction
        //target.right = vector pointing the right side of the robot
        //target.above = vector poitning the up part of the robot
        Vector3 desiredPos = target.position - target.forward *distanceBehind + target.right* sideOffset + Vector3.up *heightAbove;


        
        //the camera position will be updated sith interpolation to have a smooth motion.
        //instead of directly going to the target, it will move with a constant fraction value of the remaining distance to reach the target.
        // Example : Lerp(a, b, t) to go from a to b. if t = 0.2 (constant) it means the next data point will be a + (b-a)*0.2 of the way until reaching b
        //since we come closer, b-a diminishes then teh camera slows down.
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothingFraction* Time.deltaTime);//WE multiply by Time.deltaTime ( time it took to display the previous frame) to make it frame rate indeprendent
    
    
        //Quaternion.LookRotation() : rotation to have the camera forward +z = to the direction vector
        // target.position * Vector3.up *1.0f - transform.position : direction vector from the camera (transform.position) to a point 1m above the robot frame (target.position * Vector3.up *1.0f)
        Quaternion desiredRot= Quaternion.LookRotation(target.position + Vector3.up *1.0f - transform.position);
        //camera rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, smoothingFractionRot * Time.deltaTime);
    }

}

