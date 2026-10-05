"""
THis script is there to have extra functions for the UR5 arm functionning
"""

import math                           
import os, sys
                                                                                                                                                                 
#rtde_manager should be importable (rtde_manager.py lives at root)                                                                                                                                                                                                                                                                                                          
project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))                                                                                           
sys.path.insert(0, project_root)  
                                                   
from rtde_manager import rtde_r, rtde_c                                                                                                                                             
            
                                        
# --- Mount calibration (measure once)
MOUNT_DX   = 0.0                                                                         
MOUNT_DY   = 0.0                            
MOUNT_DZ   = 0.295#0.295                                                                        
MOUNT_DYAW = -math.pi / 2                                                                                         

#returns the cartesian x,y, z of an object in the URSIM world from the unity coordinates box_world                                  
#in URSIM, world frame = UR5 base_frame
# in UNity, world is seperated
# WHen IK is computed in URSIM, teh (x,y,z) is considered to be with respect to the base_link of the arm
#  So we need to map the UNity world to the UR5 base_link in URSIM
# IN unity, the r100 orientation ahs to be taken into consideration and is changing the orientation of the arm_base_link
#teh frambe arm_base_link is supposed to be fixed on teh chassis
def world_to_ursim_base(box_world, r100_world, r100_yaw):                                   
    #box_world : x, y, z from BoxTracker                                              
    #r100_world : x, y from PoseNavigator's _current_pose[:2] or odom
    #r100_yaw   : radians, from PoseNavigator's _current_pose[2] converted from deg      
                                              
    #Returns ursim_x, ut_sim_y, ursim_z in URSim base frame

    #1 - convert box coordinate in world to box coordinate in chassis frame ( r100 might be rotated and not aligned with world frame)
    dx = box_world[0] - r100_world[0] #error in x of the box expressed in world frame     
    dy = box_world[1] - r100_world[1] #error in y of the box expressed in world frame        

    # - 2 express error in r100 frame
    # we apply r100 rotation (yaw) to express the error in the chassis frame
    #-r100_yaw because we want to express the world coordinates in the chassis view, so we have to remove its rotation to align its x to the world x 
    c, s = math.cos(-r100_yaw), math.sin(-r100_yaw)
    d_r100_x = c*dx - s*dy
    d_r100_y = s*dx + c*dy
    d_r100_z = box_world[2]#because chassis is at z=0 all the time, not displacement possible

    #3 convert r100 frame to URSIM world frame = base frame

    #translation error
    d_mount_base_x = d_r100_x - MOUNT_DX
    d_mount_base_y = d_r100_y - MOUNT_DY
    d_mount_base_z = d_r100_z - MOUNT_DZ

    #rotate error by MOUNT DYAW because mount frame and base link frame might have rotation difference
    c_m_base, s_m_base = math.cos(-MOUNT_DYAW), math.sin(-MOUNT_DYAW)

    ursim_x = c_m_base * d_mount_base_x - s_m_base* d_mount_base_y
    ursim_y = s_m_base * d_mount_base_x + c_m_base* d_mount_base_y
    ursim_z = d_mount_base_z

    return ursim_x, ursim_y, ursim_z


                  
                        
                                                                                        
                                                                                        

#x,y,z in URSim base frame and provides corresponding joint configuration  
#function provies the q joint configuration of the robot to place the TCP at x,y,z
#SInce IK has 8 solutions, q_near is used to select the one closest to the latest confuration, q_near
def q_from_cartes(x,y,z, q_near=None):

    #WE create a TCP pose with the same orientationa as the current one (rx, ry, rz same)
    target_pose = rtde_r().getActualTCPPose()
    target_pose[0] = x
    target_pose[1] = y
    target_pose[2] = z

    #cehck robot current conf if not defined
    if(q_near is None):
        q_near = rtde_r().getActualQ()

    if not rtde_c().getInverseKinematicsHasSolution(target_pose, q_near): 
        raise RuntimeError(f"No IK solution for pose {target_pose}")

    #return the pose 
    return rtde_c().getInverseKinematics(target_pose, q_near)
