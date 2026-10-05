import time
import sys
import os


#include root path 
project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, project_root)

#Add steps directory to path (it will work for both container and local computer)
steps_dir = os.path.join(os.path.dirname(__file__), 'steps')
sys.path.append(steps_dir)# Looks here too to resolve imports


#Add nav directory to path to add the Navigator r100 controller
nav_dir = os.path.join(os.path.dirname(__file__), 'nav')
sys.path.append(nav_dir)


#Add nav directory to path to add the box tracker to subscrite to the bos poses
arm_dir = os.path.join(os.path.dirname(__file__), 'arm')
sys.path.append(arm_dir)


import robot_steps
import rtde_manager

#import controller ROS2 Node + BOxTRacker
import rclpy
from pose_navigator import PoseNavigator
from box_tracker import BoxTracker

# Use shared RTDE connections from rtde_manager (lazy init)
rtde_c = rtde_manager.rtde_c()
rtde_r = rtde_manager.rtde_r()


def before_all(context):

    if not rclpy.ok():
        rclpy.init()#initializes the rclpy runtime, once per process, used when this process wants to participate in the ROS 2 network
    context.navigator = PoseNavigator()#We use its publisher to command the r100
    context.box_tracker = BoxTracker()

    context.current_dock = None #global variable to save the position of the r100

    #TCP offset -> SUPER IMPORTANT because every IK calculation sill fail otherwise ( no solution found)
    rtde_c.setTcp([0.0, 0.0, 0.215, 0.0, 0.0, 0.0]) # lenth of the 2f-140 gripper along z                                                                                                                           
    print(f"TCP offset set to: {rtde_c.getTCPOffset()}") 

    #HOME CONF + shoulder offset adjusting URDF to Unity 
    context.home = robot_steps.UR5_SAFE_CONG#[-1.5708, -1.5708, 1.5708, -1.5708, -1.5708, 0.0175]


    #move robot to home position 
    rtde_c.moveJ(context.home, 3.14, 1.4)
    #open gripper


       

def after_step(context, step):
    time.sleep(2)
    pass


def before_step(context, step):
    time.sleep(2)
    pass


def after_all(context):
    if hasattr(context, "navigator"):#if has attribute navigator still alive
        context.navigator.shutdown()
    #we don't do rclpy.shutdown()  because robor_steps share the same context
    #pass