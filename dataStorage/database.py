import sqlite3
from datetime import datetime
import os
import json 


"""

- Three tables created by dataStorage/database.py:init_db():                  
- Users — one row per participant (auto-generated username like             
participant_95898677)                                                         
- Scenarios — every generated or manually edited scenario, with prompt,     
scenario_text (JSON), run_status (success/failed/stopped/NULL), source        
(llm_generated/manually_edited), and a session_id                           
- ChatMessages — the raw chat log (role = user or assistant, plus content)


If Flask crashes mid-session on P03 and you want to keep writing to that same P03 row: PARTICIPANT_ID=P03 python3 web_app.py. 
"""


# Use absolute path relative to this file's location
DB_DIR = os.path.join(os.path.dirname(__file__), 'database')
DB_PATH = os.path.join(DB_DIR, 'experiments.db')

# Ensure the database directory exists
os.makedirs(DB_DIR, exist_ok=True)


def init_db():

    #create a live connection with the database to interact with it
    conn = sqlite3.connect(DB_PATH)
    #we need a "control handle" to execute SQL commands through the connection.
    cursor = conn.cursor()

    #create user table
    #WE send SQL query to the database
    cursor.execute('''
            CREATE TABLE IF NOT EXISTS Users(
            user_id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT UNIQUE NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
    
    #create scenarios
    #run_status can have 3 values ("success" , "failed", "stopped")
    #"llm_generated" or "manually_edited"

    cursor.execute('''
        CREATE TABLE IF NOT EXISTS Scenarios (
            scenario_id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            session_id TEXT,
            prompt TEXT NOT NULL,
            scenario_text TEXT NOT NULL,
            run_status TEXT DEFAULT NULL,
            source TEXT DEFAULT "llm_generated",
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (user_id) REFERENCES Users(user_id)
            )
        ''')
    
    #we create a message/chat history table
    cursor.execute('''CREATE TABLE IF NOT EXISTS ChatMessages (
                  message_id INTEGER PRIMARY KEY AUTOINCREMENT,
                  user_id INTEGER NOT NULL,
                  session_id TEXT,
                  role TEXT NOT NULL, 
                  content TEXT NOT NULL, 
                  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                  FOREIGN KEY (user_id) REFERENCES Users(user_id))
                  

        

    ''')


    conn.commit()
    conn.close()
    print("Database initialized")

#Add a new user and return their user_id
def add_user(username):

    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    try:
        cursor.execute("""
            INSERT INTO Users (username)
            VALUES (?)
        """, (username,))

        conn.commit()
        user_id = cursor.lastrowid
        print(f"User '{username}' added with ID: {user_id}")
        return user_id
    
    except sqlite3.IntegrityError:
        # User already exists, get their ID to print error message
        cursor.execute('SELECT user_id FROM Users WHERE username = ?', (username,))
        user_id = cursor.fetchone()[0]
        print(f"User '{username}' already exists with ID: {user_id}")
        return user_id
    
    finally:
        conn.close()

    # INSERT INTO table_name (column1, column2, column3, ...) VALUES (value1, value2, value3, ...);
    # INSERT INTO Users (username)
    # VALUES ('Alice');


def save_scenario(user_id, prompt, scenario_text, session_id=None, source="llm_generated"):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute("""
                 INSERT INTO Scenarios (user_id, session_id, prompt, scenario_text, source)
                 VALUES (?,?,?,?,?)
                 """, (user_id, session_id, prompt, scenario_text, source))
    

    conn.commit()
    scenario_id = cursor.lastrowid
    conn.close()
    #print(f"Scenario {scenario_id} saved for the user {user_id}")
    return scenario_id


#This will be used by web_app.py to feed the LLM with the conversation and scenario generation history
def get_user_scenarios(user_id):
    """
    Retrieve all scenarios for a given user_id, ordered by creation time.
    It returns a list of tuples (user_prompt, scenario_text, created_at)
    """

    # SELECT * FROM Users;                -- get all columns from all users
    # SELECT username FROM Users;         -- get only usernames
    # SELECT * FROM Users WHERE user_id=1;-- get one specific user
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()


    cursor.execute('''
        SELECT scenario_id, prompt, scenario_text, created_at, session_id
        FROM Scenarios
        WHERE user_id = ?
        ORDER BY created_at
    ''', (user_id,))

    scenarios = cursor.fetchall()
    conn.close()
    return scenarios #returns a tuple with : scenario_id (int), prompt (str), scenario_text(str, JSON text), scenario_created_at (str), session_id (str)



#returns username knowing user_id
def get_username(user_id):

    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()


    cursor.execute('''
        SELECT username
        FROM Users
        WHERE user_id = ?
    ''', (user_id,))

    username = cursor.fetchone()
    conn.close()
    return username


def get_all_data():
    """Get all users and their scenarios for analysis"""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
        SELECT u.username, s.scenario_id, s.prompt, s.scenario_text, 
s.created_at, s.session_id
        FROM Users u
        LEFT JOIN Scenarios s ON u.user_id = s.user_id
        ORDER BY u.user_id, s.created_at
    ''')

    data = cursor.fetchall()
    conn.close()
    return data


#saves a user or LLM message to teh chat history
def save_chat_message(user_id, session_id, role, content):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
                   INSERT INTO ChatMessages (user_id, session_id, role, content)
                   VALUES(?,?,?,?)
                   ''', (user_id, session_id, role, content))
    conn.commit()
    message_id = cursor.lastrowid
    conn.close()
    return message_id


#returs teh chat history for a user as a list or role, content dicts
def get_chat_history(user_id, session_id):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
                    SELECT role, content
                    FROM ChatMessages
                    WHERE user_id = ? AND session_id = ?
                    ORDER BY created_at
                   ''', (user_id, session_id))
    rows = cursor.fetchall()#all teh corresponding rows
    conn.close()
    messages = []#empty list
    for role, content in rows:
        messages.append({'role' : role, 'content' : content})
    return messages

def update_scenario_run_status(scenario_id, run_status):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
                    UPDATE Scenarios SET run_status = ?
                    WHERE scenario_id = ?
                   ''', (run_status, scenario_id))
    conn.commit()#commit changes
    conn.close()#close connection


#all scenarios that have been run ( for LLM state awareness)
def get_executed_scenarios(user_id):

    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
                    SELECT scenario_id, prompt, scenario_text, created_at, run_status
                    FROM Scenarios
                    WHERE user_id = ? AND run_status IS NOT NULL 
                    ORDER BY created_at
                   ''', (user_id,))
    
    scenarios = cursor.fetchall()
    conn.close()
    return scenarios

#get the most recent scenario for this session (to restore UI on page refresh)

def get_last_scenario_for_session(user_id, session_id):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    cursor.execute('''
                    SELECT scenario_id, prompt, scenario_text, created_at, run_status, source
                    FROM Scenarios
                    WHERE user_id = ? and session_id = ?
                   
                    ORDER BY created_at DESC
                    LIMIT 1
                   ''', (user_id, session_id))
    row = cursor.fetchone() # gets the newest as we sorting in descending order
    conn.close()
    if row:
        return {
            "scenario_id" : row [0],
            "prompt" :row[1],
              'scenario_text': row[2],
              'created_at': row[3],
              'run_status': row[4],
              'source': row[5]
          }
    return None
    

def get_next_participant_id():
    """Return the next participant id in P01, P02, P03... form.
    Scans Users.username values matching 'P<digits>' and picks max(N)+1.
    Returns 'P01' if none exist yet."""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    cursor.execute("SELECT username FROM Users WHERE username LIKE 'P%'")
    rows = cursor.fetchall()
    conn.close()
    max_n = 0
    for (name,) in rows:
        if len(name) >= 2 and name[0] == 'P' and name[1:].isdigit():
            n = int(name[1:])
            if n > max_n:
                max_n = n
    return f"P{max_n + 1:02d}"


if __name__ == "__main__":
    init_db()#init database
    #print("RPEVIOUS", get_user_scenarios(1))


