#!/usr/bin/env python3
"""
RTDE Connection Manager

Centralized RTDE connections - initialized once per process.
Usage:
    from rtde_manager import rtde_c, rtde_r
    # Then use rtde_c() and rtde_r() as functions to get connections
"""

import rtde_control
import rtde_receive

# Robot IP
ROBOT_IP = "172.17.0.3"

# Global connection objects - initialized on first use

"""

  1) Multiple RTDEReceiveInterface connections work 
  2) Only behave uses RTDEControlInterface 

"""
_rtde_c = None
_rtde_r = None
_initialized = False

#functions that initializes rtde connections only once to avoid conflicts errors 
def rtde_c():
    global _rtde_c, _initialized
    if _rtde_c is None:
        if not _initialized:
            _initialized = True
        _rtde_c = rtde_control.RTDEControlInterface(ROBOT_IP)
    return _rtde_c

def rtde_r():
    global _rtde_r, _initialized
    if _rtde_r is None:
        if not _initialized:
            _initialized = True
        _rtde_r = rtde_receive.RTDEReceiveInterface(ROBOT_IP)
    return _rtde_r
