using UnityEngine;
                                                                                        
// Attach to Main Camera. WASD = move, mouse (with right button held)=look,            
// QE = down/up, Shift = faster. Only active in Play mode.
public class FlyCamera : MonoBehaviour                                                   
{               
    public float moveSpeed = 5f;                                                         
    public float fastMultiplier = 3f;
    public float lookSensitivity = 2f;                                                   

    float yaw;                                                                           
    float pitch;
                                                                                        
    void Start()
    {
        var e = transform.eulerAngles;
        yaw = e.y;                                                                       
        pitch = e.x;
    }                                                                                    
                
    void Update()                                                                      
    {
        // Look: hold right mouse button
        if (Input.GetMouseButton(1))
        {                                                                                
            yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;                         
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0);                          
        }       
                                                                                        
        // Move 
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier :  1f);                                                                                     
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += transform.forward;                           
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;                             
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.E)) dir += transform.up;                                
        if (Input.GetKey(KeyCode.Q)) dir -= transform.up;
        transform.position += dir.normalized * speed * Time.deltaTime;                   
    }                                                                                    
} 