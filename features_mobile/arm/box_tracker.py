import time 
import rclpy
from rclpy.node import Node
from tf2_msgs.msg import TFMessage


"""
THis script implemets a ros node, Box tracker that has a subscriber which reads the box positions from box_positions topic

the box positions are the ones defined with respect to the UNity woerld frame
"""

TOPIC_NAME = "/box_positions"

class BoxTracker(Node):

    def __init__(self):
        super().__init__("box_tracker")

        self._poses = {}# empty dico, will contain all the box poses

        #subscription to the topic   
        self.subscription = self.create_subscription(TFMessage, TOPIC_NAME, self._callback, 10)                                             
        

    #Fill the _poses object
    def _callback(self, msg):
        #  each ts has: ts.child_frame_id : box name
        #and ts.transform.translatio   in world position                
        #we store it in our dico as self._poses[name] = (x, y, z)

        for ts in msg.transforms:#msg.tarnsforms = list of transform (includes header filed + child_frame_id + transform fields)
            x = ts.transform.translation.x
            y = ts.transform.translation.y
            z = ts.transform.translation.z
            self._poses[ts.child_frame_id] = (x,y,z)


    #returns teh x,y,z UNity world coordinates of a box given its name
    #Spins for up to timeout_s 
    def get(self, name, timeout_s=1.0):                                                  
                                                                        
        deadline = time.time() + timeout_s                                               
                                                                                        
        while time.time() < deadline: # 1sec spin
            #spin once means that it spins only one time
            # spin = node ket alive, continuously reading new incoming messages, activating callbacks ect.                                                                     
            rclpy.spin_once(self, timeout_sec=0.05)    

            if name in self._poses: 
                return self._poses[name]                   
                                                                
        raise RuntimeError(f"No pose received for box '{name}'")     
                    