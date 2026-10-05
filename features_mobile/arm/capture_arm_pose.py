"""                                                                                                                                                                                 
SCript to fill the  slot_configurations.json.
                                        
how to use ?
1) Drive the chassis to a docking position (example  A1) using pose_navigator.py script                                                                                                           
2. Use URSim's free-drive to position the arm so the gripper hovers ~10 cm
    above slot N, pointing down.                                                                                                                                                       
3. Run this script to capture the joint angles.                                                                                                                                     
                                            
to run :                                                                                                                                                                             
python3 capture_arm_pose.py   
python3 capture_arm_pose.py AD 3  -> to paste directly paste                                                                                                                 
                                                                                                                                                                                    
URSim should be running + RTDE configured in rtde_manager.py (ROBOT_IP)                                                                                                               
"""                                                                                                                                                                                   
                                                                                                                                                                                    
                                                                                                                                                                   
import os, sys, json                                                                                                                                                                          
                                                                                                                                                                                    
                                                                                                                                                                                    
#rtde_manager should be importable (rtde_manager.py lives at root)                                                                                                                                                                                                                                                                                                          
project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))                                                                                           
sys.path.insert(0, project_root)                                                                                                                                                
                                                                                                                                                                                    
                                            
#import rtde_manager                                                                                                                                                         
import rtde_manager  as rm                                                                                                                                                                
                                                                                                                                                                                    

def main():                                                                                                                                                                           
                                                                                                                                                                                                                                                                                                                                                           
    shelf_type = sys.argv[1] if len(sys.argv) > 1 else None#"AD"                                                                                                                               
    slot= sys.argv[2] if len(sys.argv) > 2 else None#"3"                                                                                                                                                             
                                                                                                                                                                                                                                                                                                                                                                     
    # read the joint angles                      
    q = rm.rtde_r().getActualQ()                                                                                                                                                                           
                                                                                                                                                                                    
                                                                                                                                                                                    
    #round to 4 decimals so the JSON file stays readable                                                                                                                                                                            
    rounded_q = [round(val, 4) for val in q]                    
                                                                                                                                                                                    
                                                                                                                                                                                    
    #print                                                                                                                                         
    if shelf_type is not None and slot is not None:                                                                                                                                                                           
        print(f'    "{slot}": {json.dumps(rounded_q)},')#json.dumps formats to string
                                                                                                                                                                  
    else:                                                                                                                                                                           
        print(json.dumps(rounded_q))                                                                                                                                                                     
                                                                                                                                                                                    

if __name__ == "__main__":                                                                                                                                                            
    main()
