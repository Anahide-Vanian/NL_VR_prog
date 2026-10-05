from behave import given, when, then
import rtde_manager
import time
import environment as env
import rclpy
from std_msgs.msg import Float32
from geometry_msgs.msg import TwistStamped
import math
import json
from pathlib import Path
from arm import arm_helpers


# Get RTDE connections
rtde_c = env.rtde_c
rtde_r = env.rtde_r

# Simple ROS2 publisher for gripper commands
if not rclpy.ok():
    rclpy.init()
import rclpy.node

#robot_steps.py becomes a ROS2 Node
_rb_steps_node = rclpy.node.Node('robot_steps_node')

#Publisher for Gripper commands 
_rb_steps_grip_pub = _rb_steps_node.create_publisher(Float32, '/gripper_position/cmd', 10)

# Let publisher and client establish
time.sleep(0.1)  


#OBJ

obj_length = 0.12

#------------- ur5 positions 

#warehouse position labels (A1..D5) once at import time. 
SLOT_CONFS_FILE = Path(__file__).parent.parent / "arm" / "slot_configurations.json"

with open(SLOT_CONFS_FILE) as f:
    #returns first level python dictionnary
    SLOT_CONFIGS = json.load(f)# returns {"AD": {"1": [...], ...}, "BC": {"1": [...], ...}}


UR5_SAFE_CONG = [-1.5708, -2.618, 1.7453, -2.2689, -1.5708, 0.0] # in between conf to avoid collisions with shelves


#---------------------------------------


#------------- Ridgeback docking positions 

#warehouse position labels (A1..D5) once at import time. 
WAREHOUSE_POSITIONS_FILE = Path(__file__).parent.parent / "nav" / "warehouse_positions.json"

with open(WAREHOUSE_POSITIONS_FILE) as f:
    WAREHOUSE_LABELS = set(json.load(f).keys())#we create a set containing all position labels




# ------------- Helper Functions


#checks if the platform is where it is supposed to be (matching context.current_dock) within "tolerance" meters. Raises error message otherwise.
def verify_robot_at_dock(context, tolerance=0.15):

    if not hasattr(context, "current_dock") or context.current_dock is None:
        raise AssertionError("The robot platform has no assigned dock position. Move it first.")

    expected = context.navigator._positions.get(context.current_dock)#load the x,y,yaw values form the _positions dico
    if expected is None:
        raise AssertionError(f"Unknown dock label. '{context.current_dock}' does not exist")

    if not context.navigator.wait_for_current_pose(timeout=2.0):#Ask unity for the platform position
        raise AssertionError("No current robot pose available from Unity. Check if the odom topic publishing.")

    cur_x, cur_y, _ = context.navigator._current_pose
    distance = math.hypot(expected["x"] - cur_x, expected["y"] - cur_y)

    #We check if the current position of the robot in UNity is matchinh the context.current_dock (the latest dock label set from a enhave step)
    if distance > tolerance:
        raise AssertionError(
            f"The robot is not at {context.current_dock}. "
            f"It is {distance:.2f}m away from the expected docking position. "
            f"Please make sure the robot navigates to {context.current_dock} first."
        )


#This returns the shelf type : AD or BC from context.current_dock
#For A1..A5 / D1..D5 returns "AD"
#For B1..B5 / C1..C5 returns "BC"
def shelf_type_from_label(dock_label):
    first_letter = dock_label[0].upper()

    if(first_letter == "A" or first_letter == "D"):
        return "AD"
    elif(first_letter == "B" or first_letter == "C"):
        return "BC"
    return None



# --------------------------------Gripper Functions

#Publish gripper close command
def gripper_close():
    msg = Float32()
    msg.data = float(0.0)  # Closed=0.00radians (fingers together)
    #time.sleep(2)
    _rb_steps_grip_pub.publish(msg)
    time.sleep(3)  # Wait for gripper to physically close (needs time for all joints)


#Publish gripper open command
def gripper_open():

    msg = Float32()
    msg.data = float(0.0)
    time.sleep(2)
    _rb_steps_grip_pub.publish(msg)
    time.sleep(3)  # Wait for gripper to physically open (needs time in Mujoco)

#Publish gripper, more to a specific value
#val closer to 0.85, more we close
def gripper_close_val(value=None):
    msg = Float32()
    if value == None:
        msg.data = float(0.0)  # Closed=0.00radians (fingers together)
    else:
        msg.data = float(value)  # Open=0.85 rad is the max(fingers apart)
    time.sleep(2)
    _rb_steps_grip_pub.publish(msg)
    time.sleep(3)  # Wait for gripper to physically open (needs time in Mujoco)




# -------------UR5 : Helper Functions


#Get current TCP  pose in Cartesian space.
def get_current_tcp_pose():
    return rtde_r.getActualTCPPose()

#Get current TCP pose of the URSIM robot in Cartesian space.
def get_current_q():
    return rtde_r.getActualQ()



#Translate the robot end-effector along the ROS X-axis using joint space movement.
def translate_y(y_offset, speed=0.5, acceleration=0.5):


    # Get current TCP pose
    current_pose = get_current_tcp_pose()

    # Create target pose with Z offset
    target_pose = current_pose.copy()
    target_pose[1] += y_offset  


    # Move to new pose using joint movement (IK calculated by robot)
    rtde_c.moveL(target_pose, speed=0.25, acceleration=1.2, asynchronous=False)



#Translate the robot end-effector along the ROS X-axis using joint space movement.
def translate_z(z_offset, speed=0.5, acceleration=0.5):


    # Get current TCP pose
    current_pose = get_current_tcp_pose()

    # Create target pose with Z offset
    target_pose = current_pose.copy()
    target_pose[2] += z_offset  

    # Move to new pose using joint movement (IK calculated by robot)
    rtde_c.moveL(target_pose, speed=0.25, acceleration=1.2, asynchronous=False)
    


def move_to_q_pose(target_q, speed=0.5, acc=0.5):
    rtde_c.moveJ(target_q, speed, acc)

    
#Get only the position part (x, y, z) of current TCP pose.
def get_current_tcp_position():    

    pose = rtde_r.getActualTCPPose()
    return pose[:3]  # Return only x, y, z

#Get only the rotation part (rx, ry, rz) of current TCP pose.
def get_current_tcp_rotation():
    pose = rtde_r.getActualTCPPose()
    return pose[3:]  # Return only rx, ry, rz

#print current TCP pose
def print_current_pose():
    pose = get_current_tcp_pose()
    print(f"Current TCP Pose:")
    print(f"  Position: x={pose[0]:.4f}m, y={pose[1]:.4f}m, z={pose[2]:.4f}m")
    print(f"  Rotation: rx={pose[3]:.4f}, ry={pose[4]:.4f}, rz={pose[5]:.4f}")
    return pose





#function compute the entire pick sequence
#put robot in approach conf defined in json file
#move robot to a better approach pose, aligned with the object but with clareance 5cm away
#move linearly to object 
#close gripper
#retract
def pick_box(context, box_name, slot_name, obj_length=obj_length, approach_clearance=0.10):

    #first we check that the platform is at the right dock 
    verify_robot_at_dock(context, tolerance=0.15) 

    #--------MOVE ARM TO APPROACH

    #MOVE ARM TO in BETWEEN CONF TO AVOID COLLISON 
    target_q = UR5_SAFE_CONG
    #move the robot to approx conf
    move_to_q_pose(target_q, speed=0.5, acc=0.5)


    #--------MOVE ARM TO BETTER APPROACH POSE (ALIGNED with object)
    #Remap the box pose defined in respect with the Unity world frame to the URSIM world frame
    box_unity_pose = context.box_tracker.get(box_name)#box_pose_unity

    #wait to ask bavigator to change r100_current_pose = None to a value if possible (wait for an update)
    if not context.navigator.wait_for_current_pose(timeout=2.0):
      raise AssertionError("No current r100 pose available")#current pose is still None after spining the nvaigatpr Node
    
    r100_current_pose = context.navigator._current_pose

    r100_world= r100_current_pose[:2]
    r100_yaw = math.radians(r100_current_pose[2])

    [x,y,z]= arm_helpers.world_to_ursim_base(box_unity_pose, r100_world, r100_yaw)
    q_near = get_current_q()
    
    offset = obj_length + approach_clearance#approach clearance need otherwize teh arm colides with the shelve will descending
    # print(f"current pose:{q_near}")
    # print(f"tcp:{get_current_tcp_pose()}")
    # print(f"target:{[x,y-approach_clearance  ,z]}")
    try:
        target_q = arm_helpers.q_from_cartes(x, y-offset, z+0.02, q_near)#z+0.02 offset because it goes to the box bottom 
    except RuntimeError:
        raise AssertionError(
            f"The robot can't reach box '{box_name}' from its current position at dock '{context.current_dock}'. "
            f"The box is too far or at an awkward angle for the arm. "
            f"Please make sure the robot is properly docked in front of the shelf and try again."
        ) from None
    move_to_q_pose(target_q, speed=0.5, acc=0.5)
    #----------------

    # Translate towards  object 
    translate_y(offset, speed=0.5, acceleration=0.5)
        
    
    # Close gripper
    gripper_close_val(0.39)

    # Translate up a bit to not collide the box with th esurface of the shelve
    translate_z(0.05, speed=0.5, acceleration=0.5)# up a bit to not collide with shelve surface

    #when going back, we have to take into account the entire size of the object
    # Translate back to avoid hitting the ceiling of the slot

    offset2 = approach_clearance + 0.12#cardbox length
    translate_y(-offset2, speed=0.1, acceleration=0.1)

    # to avoid singularitues insetad of  translate_y(-0.5, speed=0.5, acceleration=0.5)    
    move_to_q_pose(UR5_SAFE_CONG, speed=0.5, acc=0.5)
    print("done pick")




def place_box(context, slot_name, obj_length=obj_length, approach_clearance=0.27):


    #--------MOVE ARM TO APPROACH
    #check if the r100 dock position global variable exist and has been filled
    #if context doesnt have teh variable or it is none:
    if not hasattr(context, "current_dock") or context.current_dock is None:
        print("current dock value", context.current_dock)
        raise AssertionError("The r100 dock position is missing")
    
    #Move the UR5 to approach conf
    shelf_type = shelf_type_from_label(context.current_dock)#returns "AD" or "BC"

    if(shelf_type == "AD" or shelf_type == "BC"):

        #--------MOVE ARM TO APPROACH POSE
        #get configuration form shelf type + slot
        #ITs a nested dictionnary, so first we have the first key for shelf type
        #second key for slot, then config
        target_q = SLOT_CONFIGS.get(shelf_type, {}).get(slot_name)#name2=slot name 

        if target_q is None:                                                                                                                                                                  
            raise AssertionError(f"No configuration for {shelf_type} slot {slot_name}")

        #move the robot to approx conf
        move_to_q_pose(target_q, speed=0.5, acc=0.5)

        #--------MOVE ARM TO BETTER APPROACH POSE (ALIGNED with object)
        #Remap the box pose defined in respect with the Unity world frame to the URSIM world frame
        # obj_length=obj_length

        # Translate closer to empty slot by approach clearanec + take into account the hole object length
        translate_y(approach_clearance, speed=0.5, acceleration=0.5)

        # Translate down for better placement
        translate_z(-0.03 , speed=0.5, acceleration=0.1)

        # Close gripper
        gripper_open()

        #no need to translate up again, just go back

        # Translate back to avoid hitting the ceiling of the slot
        translate_y(-approach_clearance, speed=0.5, acceleration=0.5)

        # translate_y(-approach_clearance -offset, speed=1.05, acceleration=0.5)  
        #Instead of translate back, UR5 safe conf to avoid singularities :
        move_to_q_pose(UR5_SAFE_CONG, speed=0.5, acc=0.5)





#---------------------------------- STEP DEFINITIONS

#-----------------------------------GIVEN 

#the property (position/speed/color) of the object/robot "{name}" is {value}
@given('the property {property_name} of the {entity} "{name}" is {value}')
def step_given_property_equal_to(context, property_name, entity, name, value):
    #ROBOT
    if entity.lower() == "robot":
        if property_name.lower() == "position":
            
            #Check if robot is at expected position
            #WE first save the label into context
            context.current_dock = value.upper()    

            # Then we verify if the robot is actually where it should be by asking Unity
            verify_robot_at_dock(context, tolerance=0.15)



@given('the {entity} "{name}" is {state}')
def step_given_entity_is_state(context, entity, name, state):

    # # Matches "robot", "Robot", "ROBOT", etc.
    # if entity.lower() == "robot":  
    #     #In ready and done states, the robot has to be at home_pos position
    #     if state.lower() == "ready" or state.lower() == "done":
    #         # Check if robot is at home_pos position
    #         check_robot_expected_position("home")
    #     else:
    #         #raise AssertionError(f"Robot {name} is not at home_pos. Current: {get_current_q()}, Expected: {POSITIONS['home']}")
    #         raise AssertionError(f"Robot {name} is not at HOME.")
    pass


#------------------------------------------WHEN

#When the robot "Bob" picks the box "redBox" from the slot "1"
@when('the {entity} "{name}" {action} the {entity1} "{name1}" from the {entity2} "{name2}"')
def when_entity_action_entity(context, entity, name, action, entity1, name1, entity2, name2):
    #ROBOT + OBJECT +SLOT
    if entity.lower() == "robot" and entity1.lower() == "box" and entity2.lower() == "slot":

        #PICKS
        if action.lower() == "picks":
            pick_box(context, name1, name2)



#When the robot "Bob" releases the box "redBox" to the slot "1"
@when('the {entity} "{name}" {action} the {entity1} "{name1}" to the {entity2} "{name2}"')
def when_entity_action_entity(context, entity, name, action, entity1, name1, entity2, name2):
    #ROBOT + OBJECT +SLOT
    if entity.lower() == "robot" and entity1.lower() == "box" and entity2.lower() == "slot":

        #RELEASES
        if action.lower() == "releases":

            place_box(context, name2)



#When the robot moves to position VALUE
@when('the {entity} "{name}" {action} to position {value}')
def when_entity_action_to_position(context, entity, name, action, value):
    """Helper for: the {entity} "{name}" {action} to position {value}"""

    #ROBOT + MOVES
    if entity.lower() == "robot" and action.lower() == "moves":

        #ONLY ROBOT R100 MOVES!!
        if value.upper() in WAREHOUSE_LABELS:
            success = context.navigator.navigate_to(value.upper())
            context.current_dock = value.upper()#stores the current docking position of the platform
            # print("current dock value", context.current_dock)
        else:
            raise AssertionError("Positions %s unknown", value)

       


#----------------------------------------------------------------THEN--------(SAME AS GIVEN)

#the property (position/speed/color) of the object/robot "{name}" is equal to {value}
@then('the property {property_name} of the {entity} "{name}" is {value}')
def step_then_property_equal_to(context, property_name, entity, name, value):
    
    #ROBOT
    if entity.lower() == "robot":
        if property_name.lower() == "position":
            
            #Check if robot is at expected position
            #WE first save the label into context
            context.current_dock = value.upper()    

            # Then we verify if the robot is actually where it should be by asking Unity
            verify_robot_at_dock(context, tolerance=0.15)

    # #OBJECT
    # if entity.lower() == "object":
    #     if property_name.lower() == "position":
    #         #Check if robot is at expected position
    #         check_object_position(name, value)
    pass





@then('the {entity} "{name}" is {state}')
def step_then_entity_is_state(context, entity, name, state):

    # # Matches "robot", "Robot", "ROBOT", etc.
    # if entity.lower() == "robot": 
    #     #In ready and done states, the robot has to be at home_pos position
    #     if state.lower() == "ready" or state.lower() == "done":
    #         # Check if robot is at home_pos position
    #         check_robot_expected_position("home", tol=0.01)
    #     else:
    #         #raise AssertionError(f"Robot {name} is not at HOME. Current: {get_current_q()}, Expected: {POSITIONS['home']}")
    #         raise AssertionError(f"Robot {name} is not at HOME.")
    pass
      




# Cleanup function to disconnect from robot
def cleanup_robot_connection():
    """Call this to properly disconnect from the robot"""
    global rtde_c, rtde_r
    if rtde_c:
        rtde_c.disconnect()
    if rtde_r:
        rtde_r.disconnect()
    rtde_c = None
    rtde_r = None







































































