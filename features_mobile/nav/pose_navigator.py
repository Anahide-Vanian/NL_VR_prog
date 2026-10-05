"""
This rcply features_mobile.nav.pose_navigator script contain the controller class to move the robot sending command to /r100_000/cmd_vel
COntroller class that drives the ridgeback to labbelled docking positions ( A1,...D5)

No Nav2, no SLAM, just proportional go-to-pose controller using gound truth odom dorm UNity

"""
import json
import math
import time
from pathlib import Path
from tf_transformations import euler_from_quaternion

import rclpy
from rclpy.node import Node
from nav_msgs.msg import Odometry
from geometry_msgs.msg import TwistStamped

CMD_VEL_TOPIC = "/r100_0000/cmd_vel"
ODOM_TOPIC = "/r100_0000/odom"
POSITIONS_FILE = Path(__file__).parent / "warehouse_positions.json"


class PoseNavigator(Node):

    def __init__(self):
        super().__init__("pose_navigator")

        #State: latest odom pose (x, y, yaw_rad). It is None until first odom arrives.
        self._current_pose = None

        #We load the warehouse positions from JSON
        with open(POSITIONS_FILE) as f:
            self._positions = json.load(f)


        #ROS related
        self._odom_sub = self.create_subscription(Odometry, ODOM_TOPIC, self._on_odom, 10)

        self._cmd_vel_pub = self.create_publisher(TwistStamped, CMD_VEL_TOPIC, 10)


    def _on_odom(self, msg:Odometry):
        #extract x, y, yaw from msg.pose.pose and store in self._current_pose
        x = msg.pose.pose.position.x
        y  = msg.pose.pose.position.y

        q = msg.pose.pose.orientation
        quat = [q.x, q.y, q.z, q.w]
        roll, pitch, yaw = euler_from_quaternion(quat)
        self._current_pose = [x,y,math.degrees(yaw)]


    #This function spins the Node just once to update self._current_pose
    # it is needed to know the last position of the platform, without necessarily calling on odom (moving the platform)
    # EVen if the platform fif not move, in some situations, we need the latest position of the platform 
    def wait_for_current_pose(self, timeout: float = 2.0) -> bool:
      start = time.time()
      while self._current_pose is None:
          rclpy.spin_once(self, timeout_sec=0.05)
          if time.time() - start > timeout:
              return False
      # drain one extra spin so pose is fresh
      rclpy.spin_once(self, timeout_sec=0.05)
      return True

    
    #  Method need becasue dyaw = target_yaw_deg - current_yaw_deg can give values outside [-180, 180] 
    #  like: target=170° and current=-170° ->  diff = 340° but the short path is -20°                                                                              
    def _wrap_deg(self, d):
        return (d + 180.0) % 360.0 - 180.0 

    def navigate_to(self, label: str, tolerance: float = 0.08, yaw_tolerance_deg : float = 5.0, timeout: float = 30.0) -> bool:                  
        """                             
        Drive the robot to the labeled docking position.                                 
        it blocks until the psoition is reached or timeout. Returns True on success, False on timeout.                       
        """  

        if label not in self._positions:
            self.get_logger().error(f"UNknown label: {label}")
            return False
        
        target = self._positions[label] # x,y and yaw (deg)

                                                                  
        # spin_once to get latest odom
        # compute dx, dy, dyaw between target and current pose                         
        # if within tolerance, publish zero twist and return True
        # otherwise compute omni cmd_vel and publish                                   
        # sleep one control cycle ( approx 50 ms) 

        start = time.time()
        last_log = 0.0
        log_interval= 1.0

        iter_count = 0  
        while True:
            #spin once so the subscriber update latest position 
            rclpy.spin_once(self, timeout_sec=0.05)
            if self._current_pose is None:       

                if time.time() - start > timeout:                          
                    self.get_logger().error(f"[{label}] timeout waiting for odom")                                                                    
                    self._cmd_vel_pub.publish(TwistStamped()) 
                    return False                                                     
                continue   


            cur_x, cur_y, cur_yaw_deg = self._current_pose 
            [dx, dy, dyaw] = [target["x"] - cur_x , target["y"] - cur_y, self._wrap_deg(target["yaw_deg"] - cur_yaw_deg)]
            distance = math.hypot(dx, dy)#distance, same in amy frame, its a scalar

            # Compute lateral (perpendicular to shelf) and forward (toward shelf) errors             
            target_yaw_rad = math.radians(target["yaw_deg"])                                         
            tc, ts = math.cos(target_yaw_rad), math.sin(target_yaw_rad)                            
            lateral_error = -dx*ts + dy*tc                                                           
            forward_error =  dx*tc + dy*ts                                                           
                                                                                                    
            x_align_tolerance = 0.05     # 5cm lateral                                               
            safety_margin    = 0.10      # pre-dock 10cm short of shelf                              
                                                                                                    
            # ARRIVAL CHECK on true errors (must be aligned in all three)                            
            if (abs(lateral_error) < x_align_tolerance
                and abs(forward_error) < tolerance                                                   
                and abs(dyaw) < yaw_tolerance_deg):                                                  
                msg = TwistStamped()                                                               
                msg.header.frame_id = "base_link"                                                    
                self._cmd_vel_pub.publish(msg)      
                return True                                                                          
                                                                                                    
            # Phase 1: lateral not aligned → move sideways only                                    
            if abs(lateral_error) > x_align_tolerance:
                forward_partial = 0.25 * max(0.0, forward_error - safety_margin)                     
                dx = -lateral_error * ts + forward_partial * tc                                      
                dy =  lateral_error * tc + forward_partial * ts                                    
                distance = math.hypot(dx, dy)    
                                                                                                    
            # Phase 2: lateral aligned, still farther than safety_margin → approach pre-dock
            elif forward_error > safety_margin:                                                    
                waypoint_x = target["x"] - safety_margin * tc                                        
                waypoint_y = target["y"] - safety_margin * ts
                dx = waypoint_x - cur_x                                                              
                dy = waypoint_y - cur_y
                distance = math.hypot(dx, dy)  


            now = time.time()
            #got odom but could not reach goal
            if now - start > timeout:                                     
                # self.get_logger().error(                                  
                #     f"[{label}] timeout after {timeout:.1f}s — "          
                #     f"last dist={distance:.2f} dyaw={dyaw:.1f}deg")       
                self._cmd_vel_pub.publish(TwistStamped())# no command anymore, we reached time limit             
                return False                


            #we convert world frame error into to robot frame, take into accound teh yaw angle :fix the direction issue
            cur_yaw_rad = math.radians(cur_yaw_deg)
            c, s = math.cos(cur_yaw_rad), math.sin(cur_yaw_rad)                                  
            vx_robot =  c*dx + s*dy             
            vy_robot = -s*dx + c*dy 

            #compute linear speed
            high_speed = 0.8
            low_speed = 0.45# to avoid the stady state issue of the controler and ensure the robot velocity calculated as K_p * error is never 0
            linear_gain = 1.5 #to ramp up faster as we approach goal (Proportional term)
            approach_threshold = 0.5

            # print("distance:", distance)
            #if we are too close, we don't use min_speed because might be too big and robot might overshoot the P controller takes over
            #CLOSE
            if distance < approach_threshold:
                speed = max(low_speed, linear_gain*distance)# iwhen we are close the speed is proportional to the distance
                #iter_count += 1                                                                          
                #if iter_count % 20 == 0:   # print every 20 iterations (~1 per second at 20 Hz loop)
                    #print(f"dist={distance:.3f}m  dyaw={dyaw:.1f}deg  cur=({cur_x:.2f},{cur_y:.2f}) target=({target['x']:.2f},{target['y']:.2f})  speed={speed:.3f}", flush=True)

                
            # else:
            #we apply a minimum speed of 0.2 when we are far away from target
            else:             
                speed = min(high_speed, linear_gain*distance)# NO too quick                                                
                #iter_count += 1                                                                          
                #if iter_count % 20 == 0:   # print every 20 iterations (~1 per second at 20 Hz loop)
                    #print(f"CHANGE dist={distance:.3f}m  dyaw={dyaw:.1f}deg  cur=({cur_x:.2f},{cur_y:.2f}) target=({target['x']:.2f},{target['y']:.2f})  speed={speed:.3f}", flush=True)


            scale = speed / max(distance, 1e-6)#scale =  desired_speed / current_speed                                            
            vx_robot = vx_robot * scale
            vy_robot = vy_robot * scale  

            

            #yaw block : ensures robot angle is facing the shelve
            max_yaw_rate = 1.5                                                                       
            angular_gain = 3.0                                                                       
            turn_start_distance = 100.0   # always rotate — get heading right ASAP                 
                                                                                                    
            if distance < turn_start_distance:                                                       
                wz_raw = angular_gain * math.radians(dyaw)
                wz = max(-max_yaw_rate, min(max_yaw_rate, wz_raw))                                   
            else:                                       
                wz = 0.0  


            #omni cmd_vel from (dx, dy, dyaw) and publish
            msg= TwistStamped()
            msg.header.frame_id = "base_link"

            msg.twist.linear.x = vx_robot
            msg.twist.linear.y = vy_robot
            msg.twist.angular.z = wz
            self._cmd_vel_pub.publish(msg)   

    def shutdown(self):                                                                
        stop = TwistStamped()
        stop.header.frame_id = "base_link"  
        self._cmd_vel_pub.publish(stop) 
        self.destroy_node()
                                         
                                                                        
                                                                                                

# standalone test(to run it without behave)
def main():                                                                              
    rclpy.init()
    nav = PoseNavigator()                                                              
    try:
        # nav.navigate_to("A1")                  
        # nav.navigate_to("A2")
        nav.navigate_to("B2")   
        nav.navigate_to("C1")                  
        # nav.navigate_to("B2")
        # nav.navigate_to("B3")
        #nav.navigate_to("C1")                  
        # nav.navigate_to("C2")
        # nav.navigate_to("C3") 
        # nav.navigate_to("D1")                  
        # nav.navigate_to("D2")             
    finally:
        nav.shutdown()                                                                   
        rclpy.shutdown()                    
                                                                                        
                
if __name__ == "__main__":                                                               
    main()
                
            