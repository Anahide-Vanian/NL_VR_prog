#!/usr/bin/env python3
"""
Bridge Node for connecting URSim to ROS2 

This script creates a bridge between URSim (Universal Robots Simulator) which has the real robot data  (will be repplaced by real robot in the future)
and ROS2 to have a real-time 3D visualization of robot movements in simulation env..

Flow:
    URSim (IP = 172.17.0.2) -> ur_rtde -> This Bridge -> ROS2 Topics -> simulation env. 3D Display

Data Flow:
1) Robot movements (controlled by BDD tests) are executed in URSim 
2) This bridge (script) reads joint positions from URSim via ur_rtde
3) Bridge converts joint data to JointState messages (JointState messages = message type in ROS2 )
4) BRidge publishes data to /joint_states topic
5) simulation env. subscribes to /joint_states topic and displays 3D robot
6) Real-time synchronization: URSim movement = simulation env. animation

    URSim -> reads joint angles -> publishes to /joint_states topic
"""

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Header, Int32, Float32
import rtde_manager
import time
import threading
from typing import List, Optional
import subprocess
import os

class URSimBridge(Node):
    """
    ROS2 Node that bridges URSim robot simulator with ROS2 ecosystem
    
    This node:
    - Connects to URSim via ur_rtde library
    - Reads real-time joint positions from the robot
    - Publishes joint states to ROS2 topics for visualization
    - Handles connection errors and reconnection logic
    """
    
    def __init__(self):
        """
        Initialize the URSim Bridge Node
        
        Sets up:
        - ROS2 node with name 'ursim_bridge'
        - Connection to URSim robot at 172.17.0.2
        - Publisher for joint state messages
        - Timer for periodic joint state updates
        """
        super().__init__('ursim_bridge')
        
        #URSim connection parameters
        self.robot_ip = "172.17.0.3"  #URSim Docker container IP
        self.update_rate = 10.0  #Hz - how often to read/publish joint states

        #Use shared RTDE connection from rtde_manager
        self.rtde_r = rtde_manager.rtde_r()
        
        #ROS2 Publisher for joint states
        #Topic: /joint_states (standard ROS2 topic for robot joint positions)
        #Message Type: sensor_msgs/JointState (standard ROS2 message for joint data)
        #Queue Size: 10 (buffer size for messages)
        self.joint_state_publisher = self.create_publisher(JointState, '/joint_states', 10)
        
        #UR5e joint names in correct order
        self.joint_names = [
            'shoulder_pan_joint',
            'shoulder_lift_joint',
            'elbow_joint',
            'wrist_1_joint',
            'wrist_2_joint',
            'wrist_3_joint'
        ]

        #Gripper joints (simple - just two driver joints)
        self.gripper_joint_names = ['right_driver_joint', 'left_driver_joint']
        self.gripper_actuator_name= ['fingers_actuator']


        #Gripper position (0 = open, 255 = closed)
        #Initialize to 0 (open) to ensure gripper starts in open position
        self.gripper_position = 0.0

        #Subscribe to gripper commands from robot_steps.py
        self.gripper_sub = self.create_subscription(Float32, '/gripper_position/cmd', self.gripper_callback, 10)#INt32

        #Publish initial gripper state immediately to set starting position
        #This ensures MuJoCo starts with gripper open (0 radians)
        #self.initial_state_published = False

        #Connection status
        self.is_connected = True

        #Start robot_state_publisher for TF transforms
        #self.start_robot_state_publisher()
        
        #Create timer for periodic joint state updates (every 1/update_rate seconds)
        #Callback function : self.publish_joint_states 
        self.timer = self.create_timer(1.0 / self.update_rate,  self.publish_joint_states)
        
        #Log initialization
        self.get_logger().info(f"URSim Bridge initialized")
        self.get_logger().info(f"Robot IP: {self.robot_ip}")
        self.get_logger().info(f"Subscribing to: /gripper_position/cmd (from robot_steps.py)")
        self.get_logger().info(f"Publishing to: /joint_states (for mujoco_bridge.py)")
    
    
    def gripper_callback(self, msg):
        """Simple callback - just save the gripper position"""
        self.gripper_position = msg.data
        self.get_logger().info(f"Gripper position updated: {self.gripper_position}")

    def publish_joint_states(self) -> None:
        """
        Read joint positions from URSim and publish to ROS2 topic

        This function is called periodically by the ROS2 timer.

        Process:
        1. Check if connected to URSim
        2. Read current joint positions from robot
        3. Create ROS2 JointState message
        4. Populate message with joint data
        5. Publish message to /joint_states topic
        6. Handle any connection errors
        """

        #Publish initial gripper state on first call to ensure clean start
        #if not self.initial_state_published:
        #    self.initial_state_published = True
        #    self.get_logger().info(f"Initial gripper position set to: {self.gripper_position} (0 = open)")

        #Check connection status
        if not self.is_connected or self.rtde_r is None:
            self.get_logger().warn("Not connected to URSim")
            return
        
        try:
            #Read joint positions from URSim
            #getActualQ() returns current joint angles in radians
            joint_positions = self.rtde_r.getActualQ()

            #Validate joint data
            if joint_positions is None:
                self.get_logger().warn("Received None joint positions from URSim")
                return

            if len(joint_positions) != 6:
                self.get_logger().warn(f"Expected 6 joints, got {len(joint_positions)}")
                return

            #Create ROS2 JointState message
            joint_state_msg = JointState()

            #Set message header with current timestamp
            joint_state_msg.header = Header()
            joint_state_msg.header.stamp = self.get_clock().now().to_msg()
            joint_state_msg.header.frame_id = "base_link"  #Robot base frame

            ##Convert gripper: 0-255 -> 0-0.85 radians
            #gripper_rad = 0.85 * (1.0 - (self.gripper_position / 255.0))

            #Populate joint data (robot + gripper)
            joint_state_msg.name = self.joint_names + self.gripper_joint_names
            #joint_state_msg.position = list(joint_positions) + [gripper_rad, gripper_rad]
            joint_state_msg.position = list(joint_positions) + [self.gripper_position, self.gripper_position]

            #Optional: Add velocity and effort data if available
            #joint_state_msg.velocity = self.rtde_r.getActualQd()  #Joint velocities
            #joint_state_msg.effort = []  #Joint torques (not available in URSim)

            #Publish the message to ROS2 topic
            self.joint_state_publisher.publish(joint_state_msg)
            
            #Log periodic status (every 50 messages = ~5 seconds at 10Hz)
            if hasattr(self, 'message_count'):
                self.message_count += 1
            else:
                self.message_count = 1
                
            if self.message_count % 50 == 0:
                self.get_logger().info(f"Gripper position {self.gripper_position} ")
                #self.get_logger().info(f"Published {self.message_count} joint state messages")
                self.get_logger().info(f"Current joints: {[f'{j:.3f}' for j in joint_positions]}")
                self.get_logger().info("-" * 50)

        except Exception as e:
            #Handle connection errors
            self.get_logger().error(f"Error reading joint states from URSim: {e}")
    
    def destroy_node(self) -> None:
        """
        Clean up when node is shut down

        Note: RTDE connections are managed by rtde_manager and will be
        cleaned up separately. We only clean up ROS2 resources here.
        """
        self.get_logger().info("Shutting down URSim Bridge")

        #Stop robot_state_publisher process
        if hasattr(self, 'robot_state_publisher_process') and self.robot_state_publisher_process:
            try:
                self.robot_state_publisher_process.terminate()
                self.robot_state_publisher_process.wait(timeout=5)
                self.get_logger().info("robot_state_publisher stopped")
            except Exception as e:
                self.get_logger().error(f"Error stopping robot_state_publisher: {e}")

        #Call parent class cleanup
        super().destroy_node()


def main(args=None):
    """
    Main function to run the URSim Bridge Node
    
    Usage:
        python3 ur_bridge.py
    
    This function:
    1) Initializes ROS2
    2) Creates the URSim Bridge node
    3) Spins the node to keep it running
    4) Handles shutdown 
    """
    
    print("Starting URSim to ROS2 Bridge")
    #This bridge connects URSim robot simulator to ROS2

    
    #Initialize ROS2
    rclpy.init(args=args)
    
    try:
        #Create and run the bridge node
        bridge_node = URSimBridge()
        
        print("Bridge node created successfully !")
        print("Publishing joint states to /joint_states topic")
        print()
        
        #Keep the node running
        rclpy.spin(bridge_node)
        
    except KeyboardInterrupt:
        print("\nKeyboard interrupt received, shutting down")
        
    except Exception as e:
        print(f"\nUnexpected error: {e}")
        
    finally:
        #Clean up
        if 'bridge_node' in locals():
            bridge_node.destroy_node()
        rclpy.shutdown()
        print("URSim Bridge shut down successfully!")


if __name__ == '__main__':
    main()