from flask import Flask, render_template, request, redirect, url_for, flash, session, jsonify
import time
import os
import json
import re
import ast
import tempfile
import threading#for parrallel precess running
import subprocess #library to run bash command from code
import traceback
from dotenv import load_dotenv
from openai import OpenAI
from feature_generator import FeatureGenerator
#--- To reinitialze MUjoco env, we create a ros2 client node
import rclpy
import rclpy.node
#-------
from std_srvs.srv import Trigger
# Load environment variables from .env file
load_dotenv()

#--transcription
from whisper_service import WhisperService

#--------- for database creation
from dataStorage.database import (init_db, add_user, save_scenario, get_user_scenarios, save_chat_message, get_chat_history, update_scenario_run_status, get_executed_scenarios, get_last_scenario_for_session, get_next_participant_id)
import uuid  # for generating session IDs

#--------------ROS Client Node to call reset services
if not rclpy.ok():
    rclpy.init()#node constantly alive
_flask_ros_node = rclpy.node.Node("flask_web_app_node")


# --- #------------To send the generated/edited scenarios to unity
                                     
from std_msgs.msg import String as StringMsg, Bool as BoolMsg
from rclpy.qos import QoSProfile, DurabilityPolicy, HistoryPolicy 

#create generak QoS profile
_scenario_qos = QoSProfile(depth=1, durability=DurabilityPolicy.TRANSIENT_LOCAL, history=HistoryPolicy.KEEP_LAST)#last subscriber have the last scenrio                                                                             
_scenario_pub = _flask_ros_node.create_publisher(StringMsg, '/scenario_text', _scenario_qos)
_scenarios_ready_pub = _flask_ros_node.create_publisher(BoolMsg, '/vr/scenarios_ready', _scenario_qos)                                                                

#callback funtion              
def _publish_scenario_text(text: str):                                        
    if text:
        _scenario_pub.publish(StringMsg(data=text))

#to tell unity that the scnearios have been generated and user will know he can execute
def _publish_scenarios_ready(ready: bool):
    _scenarios_ready_pub.publish(BoolMsg(data=ready))

# VR piinch slected queued by UNity, they are gathered by /chat/message

_vr_selections = [] # its a list of {"name", "tag", "xyz", "ts"}

_vr_selections_lock = threading.Lock()




#------------To send the generated/edited scenarios to unity


# Tracks the DB id of whichever scenario is currently on disk.
# Updated by chat_message + chat_edit_scenario, read by /vr/execute.
_current_scenario_id = None             


#--------------

app = Flask(__name__)
# app.secret_key = os.urandom(24)
app.secret_key = os.environ.get('FLASK_SECRET_KEY', 'saduko-dev-secret')

# Initialize database when Flask starts
init_db()

# --- Ensure there is one experiment participant per Flask process lifetime.
# Priority: PARTICIPANT_ID env var (lets you re-attach to an existing P## after a crash),
# else auto-increment P01, P02, P03... based on what is already in the DB.
_participant_username = os.environ.get('PARTICIPANT_ID') or get_next_participant_id()
_participant_user_id  = add_user(_participant_username)
_participant_session_id = f'session_{uuid.uuid4().hex[:8]}'
print(f"[Experiment] Participant: {_participant_username} "
    f"(user_id={_participant_user_id}, session_id={_participant_session_id})")

@app.before_request                                                                                                                                                                   
def ensure_session():                                                                                                                                                               
    # Always bind the request to participant                                                                                                                       
    session['user_id']    = _participant_user_id                                                                                                                                      
    session['session_id'] = _participant_session_id


#Reads and provides access to the complete DSL grammar rules from the Xtext grammar file (dsl_garmmar.xtext).
#This class extracts the formal grammar definition to create BDD-based scenarios.
class DSLGrammarReader:

    def __init__(self, grammar_file_path):
        self.grammar_file_path = grammar_file_path
    
    def read_grammar_rules(self):
        # Read the complete DSL grammar from the .xtext file
        try:
            with open(self.grammar_file_path, 'r') as f:
                return f.read()
        except Exception as e:
            print(f"Error reading DSL grammar: {e}")
            return "DSL Grammar file not found."



# Extracts step definitions (decoratiors) from robot_steps.py file to understand available BDD steps.
# This class parses the python file containing @given, @when, @then decorators.
# IT alo better formats them for use in the LLM prompt.
# This ensures generated scenarios use only implemented step patterns.

class StepDefinitionReader: 

    def __init__(self, steps_file_path):
        self.steps_file_path = steps_file_path
    
    def extract_step_definitions(self):
        #Extract step definitions from the robot_steps.py file
        try:
            with open(self.steps_file_path, 'r') as f:
                content = f.read()
            
            step_definitions = {'given': [], 'when': [], 'then': []}
            
            # Find all @given, @when, @then decorators and their function definitions
            #The regex patterns below match a single-quoted string, but only capture what’s inside the quotes.
            # + = one or more(everything)   ^ = NOT    \'= single quote 
            #[^\']+ = Take everything but not single quotes
            patterns = [
                (r'@given\(\'([^\']+)\'\)', 'given'),
                (r'@when\(\'([^\']+)\'\)', 'when'),
                (r'@then\(\'([^\']+)\'\)', 'then')
            ]
            
            for pattern, step_type in patterns:
                matches = re.findall(pattern, content)
                step_definitions[step_type].extend(matches)

            print("step_definitions", step_definitions)  
                    
            """
            step_definitions looks like :


            step_definitions 
            {
            'given': ['the property {property_name} of the {entity} "{name}" is {value}', 'the {entity} "{name}" is {state}'], 
            'when': ['the {entity} "{name}" {action} the {entity1} "{name1}"', 'the {entity} "{name}" {action} to position {value}'], 
            'then': ['the property {property_name} of the {entity} "{name}" is {value}', 'the {entity} "{name}" is {state}']
            }

            """  
            
            return step_definitions

            
        except Exception as e:
            print(f"Error reading step definitions: {e}")
            return {'given': [], 'when': [], 'then': []}#stores all the steps in a dictionnary by keyword sorting 
    

    #Format step definitions for use in the LLM prompt
    def format_step_definitions_for_prompt(self):
        
        step_definitions = self.extract_step_definitions()
        
        if not any(step_definitions.values()):
            return "No step definitions found."
        
        formatted_steps = []
        for step_type, patterns in step_definitions.items():
            if patterns:  # Only add section if there are patterns
                formatted_steps.append(f"\n{step_type.upper()} Steps:")
                for pattern in patterns:
                    formatted_steps.append(f"- {pattern}")
        
        steps_formatted = "\n".join(formatted_steps)#sends the steps by formatting them
        print("\n Steps formatted", steps_formatted)

        """
         Steps formatted 
            GIVEN Steps:
            - the property {property_name} of the {entity} "{name}" is {value}
            - the {entity} "{name}" is {state}

            WHEN Steps:
            - the {entity} "{name}" {action} the {entity1} "{name1}"
            - the {entity} "{name}" {action} to position {value}

            THEN Steps:
            - the property {property_name} of the {entity} "{name}" is {value}
            - the {entity} "{name}" is {state}
        """

        return steps_formatted

class OpenAIService:

    #init function
    def __init__(self):
        # Get API key from .env file
        api_key = os.getenv("OPENAI_API_KEY")
        if not api_key:
            raise ValueError("OPENAI_API_KEY not found in .env file")
        
        self.client = OpenAI(api_key=api_key)
        self.model = "gpt-5"  #
        
        # Initialize step definition reader
        #Path to the step definition file
        steps_file_path = os.path.join(os.path.dirname(__file__), 'features_mobile', 'steps', 'robot_steps.py') 
        #creates the class that will read and process robot_steps.py to only retain the decorators
        self.step_reader = StepDefinitionReader(steps_file_path)
        
        # Initialize DSL grammar reader
        grammar_file_path = os.path.join(os.path.dirname(__file__), 'dsl_grammar.xtext')
        self.grammar_reader = DSLGrammarReader(grammar_file_path)



    #-----helper function to feed the LLM with the prompt/scenarios history of a user (user_id)
    #Format all previous scenarios of a user to inlcude it in into the LLM context
    
    def format_conversation_history(self, user_id):

        previous_scenarios = get_user_scenarios(user_id)

        #if previous scenarios is empty because this is the first time generation/login
        if not previous_scenarios or len(previous_scenarios) == 0:
            return "This is the user's first interaction."

        history = f"The user has previously generated {len(previous_scenarios)} scenario(s):\n\n"

        for scenario_id, prompt, scenario_text, created_at, session_id in previous_scenarios:
            history += f"Scenario {scenario_id}, created at {created_at}"
            history += f"User requested : '{prompt}'"
            history += f"Feature file generated: '{scenario_text}'"
            history += "-" * 50 + "\n\n"

        print("history", history)
        return history



    #Generate scnarios using the full conversation history.
    #chat_history is a list of {role, content} dicts obtained form DB
    def generate_scenario_chat(self, chat_history, execution_context = ""):
        available_steps = self.step_reader.format_step_definitions_for_prompt()
        dsl_grammar = self.grammar_reader.read_grammar_rules()

        system_message = f"""

        **Robot + Warehouse description:**
        A mobile manipulator arm consisting of a UR5 arm placed an a ridgeback r100 with a gripper is in a warehouse.
        The warehouse is divided into two wings: 
        The west wing, the production area, and the east wing, which corresponds to the storage area. 
        A mobile robot, the r100, navigates between the storage and the production area.

        1) The storage area contains two shelving units A and C. Each unit consists of three aligned shelves, and each shelf contains two slots.
        In the first unit (labeled A), the slots contain boxes ready for picking and delivery to the production area. 
        Each shelf has a dedicated docking position for the r100 robot, labeled A1 to A3.
        In the second unit (C), the slots contain empty boxes returned by the robot from the production area. 
        The docking positions in front of these shelves are labeled C1 to C3.

        2) The production area has one shelving unit (B), and each shelf contains two slots.
        The shelves may contain either full or empty boxes.
        The docking positions are labeled B1 to B3.


        The users will prompt messages to ask the robot to perform logistics tasks: pick and place boxes or move them.

        PRODUCTION AREA			STORAGE AREA
                        
                        C:  [s] [s] [s]	
                            C1  C2  C3
                            
        B:  [s] B3				[s] A1	:A		            		     
            [s] B2			    [s] A2
            [s] B1				[s] A3

                                

        **Robot r100 and UR5 Positions:**
        - HOME: The r100 and UR5 robots are at HOME at the beginning of the experiment. 
        - A1, A2, ..., C3: Docking positions for the robot
        - Slot names: "1" (lower slot of a shelf), "2" (upper slot of a shelf)
        - Example: box "A1-2-box" lives in unit A, dock A1, slot "2". To pick it: the robot moves to A1, then picks the box "A1-2-box" from the slot "2".

        {dsl_grammar}

        {available_steps}

        ## ENTITY REFERENCE

        **robot**:
        - Actions: moves, picks, releases                                                                                                                                                     
        - Properties: position (values: HOME, A1, A2, A3, B1, B2, B3, C1, C2, C3)                                                                                                           
        - States: (none)                                                                                                                                                               
        
        **box**:                                                                                                                                                                              
        - Actions: (none )                                                                                                                                   
        - Properties: (none)                                                                                                                                                       
        - States: (none)                                                                                                                                                                    
                                                                                                                                                                                                
        **slot**:
        - Actions: (none)                                                                                                                                                                     
        - Properties: (none)                                                                                                                                                                
        - States: (none)

        **Usage rules:**
            - Use default names for the robot and boxes:
                - Robot: "Bob"
                - Boxes: "A1-1-box", "A1-2-box", "B1-1-box", etc.


        {execution_context}

        ## OUTPUT FORMAT
        Return valid JSON with this structure (generate one or more scenarios as needed):
        {{
        "chat_reply": "A short friendly response to the user (1-2 sentences)",
        "feature": "Robot movement",
        "description": "Relevant description",
        "scenarios": [
            {{
            "name": "Descriptive scenario name",
            "steps": [
                {{"type": "given", "text": "step text"}},
                {{"type": "and", "text": "additional given step"}},
                {{"type": "when", "text": "step text"}},
                {{"type": "and", "text": "additional when step"}},
                {{"type": "then", "text": "step text"}},
                {{"type": "and", "text": "additional then step"}}
            ]
            }},
            ...
        ]
        }}
        If you cannot generate a scenario (need clarification), return:
        {{
            "chat_reply": "Your question or explanation"
        }}

        **Important guidelines:**
        - Don't include objects NOT related to the task
        - Consider breaking complex tasks into multiple focused scenarios for better testability.

        """

        #Creation of the messages list : system prompt and full conversation history
        messages = [{"role" : "system", "content" : system_message}] #create list with the system message at the beginning

        #fill it with user/assistant chat history
        for msg in chat_history:
            if msg['role'] in ('user', 'assistant'):
                messages.append({"role" : msg['role'], "content" : msg['content']})

        response = self.client.chat.completions.create(model =self.model, messages =messages, stream = False)

        print("Chat response:", response.choices[0].message.content)

        return response.choices[0].message.content
    



#create the trancription class
whisper_service = WhisperService()

# Initialize the LLM service
llm_service = OpenAIService()
feature_generator = FeatureGenerator(features_dir='features_mobile')

# To store running processes (key: session ID, value: process object)
running_processes = {}



#Parse the LLM JSON response and to extract the chat_reply and scenario data.
#Retuns scenario_data or NOne and char_reply

def parse_chat_response(llm_response):
    cleaned = llm_response.strip()

    #STrip markdown code by keeping everything between : ```json ... ```

    if cleaned.startswith('```'):
        lines = cleaned.split('\n')
        lines = lines[1:] #remve first line (```json)
        if lines and lines[-1].strip() == '```':
            lines = lines[:-1] #remove last line (```)
        cleaned = '\n'.join(lines)

    try : 
        data = json.loads(cleaned)
        #Here are your scenarios! in case LLM doesnt repLy
        chat_reply = data.get('chat_reply', 'Here are your scenarios!')

        #if scenarios not empty 
        if 'scenarios' in data and data['scenarios']:
            scenario_data = {"feature": data.get('feature', 'Robot Movement'),
                             "description": data.get("description", ''),
                             "scenarios": data["scenarios"]
                            }
            return scenario_data, chat_reply
    
        else:#LLM replied without scenario
            return None, chat_reply

    except json.JSONDecodeError:
        #LLM return plain text or didnt return the JSON format
        return None, llm_response.strip()



#format execution context to feed the LLM and make it stateaware
#We feed all teh scenarios that have been executed, and whether they were sucessfull or not
# The LLM will then know the current physical state of the robot and environment, knowing which scenarios ran
def format_execution_context(user_id):

    executed= get_executed_scenarios(user_id)

    if not executed:
        return "No scenario shave been executed yet."
    
    context = "The following scenarios have been executed,\n"
    for scenario_id, prompt, scenario_text, created_at, run_status in executed :
        status_label = run_status.upper()
        context += f" - [{status_label}] '{prompt}' (at {created_at})\n"

        context += f" The scenarios executed are: {scenario_text}\n"

    context += "\nInfer the current boxes/robot positions from the sucessfully executed scenarios above."
    
    return context
    


@app.route('/', methods=['GET'])
def index():
    return render_template('chat.html')


@app.route('/validate/<path:output_file>')
def validate_scenario(output_file):
    try:
        # OLD SESSION-BASED APPROACH (COMMENTED OUT, UNRELIABLE):
        # Get the scenario data from session
        # Problem : THe session data can be lost and that causes redirect to index.html
        # REsaon: Flask sessions are cookie-based. If session expires or data too large, it's lost.
        # WHen did it faield last time : User clicked "Run" -> test fails -> I fixed python code -> clicked "Run" again -> it redirected to index
        # scenario_data = json.loads(session.get('scenario_data', '{}'))
        # original_prompt = session.get('original_prompt', '')

        # New file based approach:
        # Try to load scenario data from the JSON file (more reliable than session approach)
        # We create the scenario JSON filename by using the output_file name
        scenario_json_file = output_file.replace('.feature', '_scenario.json')

        print(f"We search for scenario data in: {scenario_json_file}")

        scenario_data = None
        original_prompt = ''

        # Try to read from file
        if os.path.exists(scenario_json_file):
            try:
                with open(scenario_json_file, 'r') as f:
                    saved_data = json.load(f)#we load teh saved file
                    scenario_data = saved_data.get('scenario_data')
                    original_prompt = saved_data.get('original_prompt', '')
                print(f"Successfully loaded scenario data from file")
            except Exception as e:
                print(f"Error loading scenario data from file: {e}")
        else:
            print(f"Scenario JSON file not found: {scenario_json_file}")

        if not scenario_data:
            flash("No scenario data available", 'error')
            return redirect(url_for('index'))
        
        # Generate and save the feature file
        feature_generator.generate_feature_file(scenario_data, output_file)
        #flash(f"Feature file validated and saved: {output_file}", 'success')
        #flash(f"Running", 'success')

        # Read the content for display
        with open(output_file, 'r') as f:
            feature_content = f.read()

        # We don't run behave here yet: we let javascript to trigger it via streaming endpoint
        return render_template('result.html',
                             feature_content=feature_content,
                             output_file=output_file,
                             prompt=original_prompt,
                             run_behave=True)  # Signal to javascript to start streaming
    
    except Exception as e:
        #flash(f"An error occurred during validation: {str(e)}", 'error')
        flash(f"An error occurred: {str(e)}", 'error')
        return redirect(url_for('index'))


@app.route('/stream_behave/<path:output_file>')
def stream_behave(output_file):
    """Stream behave output in real-time using Server-Sent Events"""
    # Capture session ID BEFORE generator (within request context)
    session_id = request.cookies.get('session', 'default')

    def generate():
        try:
            # Start behave process
            process = subprocess.Popen(
                ['behave', output_file, '--stop'],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1  # Line buffered
            )

            # Store process in global dict
            running_processes[session_id] = process

            # Flag to track if we've started displaying output
            #used to remove all the unecessary print text above FEature : "title"
            display_started = False


            # Stream output line by line
            for line in process.stdout:
                if "Feature:" in line.rstrip():
                    display_started = True

                if display_started == True:
                    # Send each line as SSE event
                    yield f"data: {line.rstrip()}\n\n"

            # Wait for process to complete
            process.wait()

            # Remove from running processes
            if session_id in running_processes:
                del running_processes[session_id]

            # Send completion status
            if process.returncode == 0:
                yield f"data: [COMPLETE:SUCCESS]\n\n"
            elif process.returncode == -15 or process.returncode == -2:  # SIGTERM or SIGINT
                yield f"data: [COMPLETE:STOPPED]\n\n"
            else:
                yield f"data: [COMPLETE:FAILED:{process.returncode}]\n\n"

        except Exception as e:
            yield f"data: [ERROR] {str(e)}\n\n"

    return app.response_class(
        generate(),
        mimetype='text/event-stream',
        headers={
            'Cache-Control': 'no-cache',
            'X-Accel-Buffering': 'no'  # Disable nginx buffering
        }
    )


@app.route('/stop_behave', methods=['POST'])
def stop_behave():
    """Stop the running behave process"""
    try:
        session_id = request.cookies.get('session', 'default')

        if session_id in running_processes:
            process = running_processes[session_id]
            process.terminate()  # Send SIGTERM (like Ctrl+C)
            del running_processes[session_id]
            return jsonify({'success': True, 'message': 'Tests stopped'})
        else:
            return jsonify({'success': False, 'message': 'No running tests found'})
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)}), 500



#-----------New routes for chatbot

#FOr new singla page chat interface
@app.route('/chat', methods=['GET'])
def chat():
    return render_template('chat.html')


#Return existing chat messages and teh last scenario to restore the page
@app.route('/chat/history', methods=['GET'])
def chat_history_endpoint():
    if 'user_id' not in session:
        return jsonify({'messages':[], 'last_scenario': None})
    
    user_id = session['user_id']
    session_id = session['session_id']

    messages = get_chat_history(user_id, session_id)
    last_scenario = get_last_scenario_for_session(user_id, session_id)

    last_scenario_content = None
    last_scenario_id = None
    output_file = None

    if last_scenario:
        last_scenario_id = last_scenario['scenario_id']
        output_file = 'features_mobile/robot_movement.feature'

        if last_scenario['source'] == "manually_edited":
            last_scenario_content = last_scenario['scenario_text']#not in JSON format
        else:#LLM edited so it is a JSON file
            try:
                scenario_data = json.loads(last_scenario['scenario_text'])
                last_scenario_content = feature_generator.generate_feature_content(scenario_data)
            except Exception:
                last_scenario_content = last_scenario['scenario_text']
    return jsonify({
        'messages':messages,
        'last_scenario_content' : last_scenario_content,
        'last_scenario_id' : last_scenario_id,
        'output_file' :output_file
    })


#Handles teh user chat message, it calls teh LLM and return the scenario and reply
@app.route('/chat/message', methods=['POST'])
def chat_message():
    data = request.get_json()# WHat is request ? 
    user_message  = data.get('message', '').strip()

    if not user_message:
        return jsonify({'error' : 'Empty message'}), 400
    
    #if exist
    user_id = session['user_id']
    session_id = session['session_id']

    #1) We save the user message to the chat history (ChatMesssages DB) 
    save_chat_message(user_id, session_id, 'user', user_message)

    #2) WE get the full history, including the message just saved
    chat_history = get_chat_history(user_id, session_id)

    #3) we create the execution context for the LLM stateawareness
    execution_context = format_execution_context(user_id)


    #3_new) We collect all the pinch selected objects in the VR and appemd them to the LLM so he can disambiguate "this object" and "that shelf"
    with _vr_selections_lock: # we lock the dico so nobody modified it

        vr_latest_selections = list(_vr_selections)
        _vr_selections.clear()#we empty 

        if vr_latest_selections:
            lines_for_LLM = ["\n\nThe user pinched the following objects in VR (in order of selection):"]

            for index, s in enumerate(vr_latest_selections, 1):
                x,y,z = s["xyz"]
                lines_for_LLM.append(f" {index}. {s["name"]} (tag={s["tag"]}) at world position [{x:.3f}, {y:.3f}, {z:.3f}]")

            lines_for_LLM.append("Use these positions, names and their order to resolve deictic references like 'this','that', 'there' in the user's message.")

            execution_context = execution_context + "\n".join(lines_for_LLM)

    #4) Cakk the LLM
    try:
        llm_response = llm_service.generate_scenario_chat(chat_history,execution_context)#we give the chat history and the executed scenarios
    except Exception as e:#ifthe LLM crashed, or the request was malformed.
        traceback.print_exc()
        return jsonify({'error': f'LLM error : {str(e)}'}),500
    
    #5) We parse the response containing the scnario_data and the chat_reply
    scenario_data, chat_reply = parse_chat_response(llm_response)

    #6) Save the assistant message to the chat_history
        #Note : llm_response is the raw unprocessed string straight from the API. No formatting has been done to it.
    save_chat_message(user_id, session_id, 'assistant', llm_response)

    if scenario_data:
        feature_content = feature_generator.generate_feature_content(scenario_data)
        output_file = 'features_mobile/robot_movement.feature'

        #save scenarios to path
        feature_generator.generate_feature_file(scenario_data, output_file)

        #SAve to scenarios_table
        scenario_id = save_scenario(user_id, user_message, json.dumps(scenario_data), session_id, source ='llm_generated')

        #we ensure when we save the scenario, we update the scenario id with the latest id                                                                                                                             
        global _current_scenario_id                                                                                                                                                         
        _current_scenario_id = scenario_id       

        # We publish the scenario text to display it in UNity        
        _publish_scenario_text(feature_content)  

        #WE also tell the VR panel that it should turn green and that the scenarios are ready for execution and that the user can say the "Execute" keyword
        _publish_scenarios_ready(True)


        return jsonify({
            'success' :True,
            'chat_reply' : chat_reply,
            'scenario_content' : feature_content,
            'scenario_id' : scenario_id,
            'output_file' : output_file
        })
    else:
        #LLM replied with text only
        return jsonify({
            'success' :True,# also th erequest was succesffully pocessed
            'chat_reply': chat_reply,
            'scenario_content' : None

        })
    
#Save manually the edited scenario to DB and disk    
@app.route('/chat/edit_scenario', methods = ['POST'])
def chat_edit_scenario():

    data= request.get_json()
    scenario_content = data.get('scenario_content', '')
    scenario_id = data.get('scenario_id')
    output_file = data.get('output_file', 'features_mobile/robot_movement.feature')
    
    user_id = session['user_id']
    session_id = session['session_id']

    #We write the edited content to feature file on disk
    # When user edits, its a raw Gherkin text and not a JSON dict so we dont use feature_generator
    with open(output_file, 'w') as f:
        f.write(scenario_content)

    #Save the scnario in DB as manual edit
    #we give a new id in DB to the edited scenario
    new_scenario_id = save_scenario(user_id,f'[Manual esit of scenario {scenario_id}]', scenario_content, session_id, source='manually_edited')

    #we update the scenario ID of the new created scenario
    global _current_scenario_id
    _current_scenario_id = new_scenario_id  


    # we puclish the edited scenrio to UNity to update teh floating panel         
    _publish_scenario_text(scenario_content)

    return jsonify({'success': True, 'scenario_id': new_scenario_id})


#We write the cuurent running scenario to disk and return the SSE stream URL    
@app.route('/chat/run_scenario', methods = ['POST'])
def chat_run_scenario():

    data= request.get_json()
    scenario_content = data.get('scenario_content', '')
    output_file = data.get('output_file', 'features_mobile/robot_movement.feature')

    #We write the current scneario content to feature file on disk
    with open(output_file, 'w') as f:
        f.write(scenario_content)

    stream_url = url_for('stream_behave', output_file=output_file)
    return jsonify({'success': True, 'stream_url': stream_url})


#We update the run result of a scenario after behave finishes     
@app.route('/chat/mark_run_result', methods = ['POST'])
def chat_mark_run_result():
    data = request.get_json()#get the json object of the request object
    scenario_id = data.get('scenario_id')
    status = data.get('status') #success or failed or stopped

    if not scenario_id or not status:
        return jsonify({'error':'Missing scenario_id or status'}), 400
    
    #udpate the status of the scenario in DB Scenarios
    update_scenario_run_status(scenario_id, status)
    return jsonify({'success' :True})


#We created this route so UNity post here every slecetd object with pinch 
#Payload : {"name", "tag", "xyz":[x,y,z]}
@app.route('/vr/select_target', methods=['POST'])
def vr_select_target():
    data = request.get_json() or {}

    name = data.get("name")
    tag = data.get("tag")
    xyz = data.get("xyz")

    if not name or not tag or not xyz or len(xyz)!=3:
        return jsonify({"error":"Expected {name, tag, xyz:[x,y,z]}"}), 400
    
    #we create the dico 
    entry = {"name" : name, "tag" : tag, "xyz" : [float(val) for val in xyz], "ts": time.time()}


    #theard lock
    with _vr_selections_lock:

        _vr_selections.append(entry)
        count = len(_vr_selections)

    print(f"[vr_select_target] {tag}:{name} @ {xyz} (total selections : {count})")

    return jsonify({'success' : True, "count" : count})


#Called by Unity after the word "Hey RObot" fires agian 
#It clears the queued pinch selected objects and tells the panel + is no longer green for execution 
@app.route("/vr/reset_context", methods=["POST"])
def vr_reset_context():

    with _vr_selections_lock:
        _vr_selections.clear()

        _publish_scenarios_ready(False)#we tell unity we cant execute scenarios 

        print("[vr_reset_context] slectiosn are cleared ans scenarios_ready= False")

        return jsonify({"success": True})

#Wehn we execute the scenario form VR
#  /vr/execute doesn't need to know the user, it just updates run_status on _current_scenario_id, and that scenario is already tagged with _participant_user_id from when chat_message saved it. 
@app.route('/vr/execute', methods=['POST'])
def vr_execute():
    output_file = 'features_mobile/robot_movement.feature'
    scenario_id_snapshot = _current_scenario_id  # remember the current scenario id      
                                          
    # This function will run in the background, Flask doesn't wait for it            
    def _run():                                                                          
        process = subprocess.Popen(['behave', output_file, '--stop'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)                              
        process.wait()# this waits, but only inside the background helper 

        if process.returncode == 0:                                                      
            status = 'success'    

        elif process.returncode in (-15, -2):                                            
            status = 'stopped'  

        else:                                                                          
            status = 'failed'

        if scenario_id_snapshot is not None: 

            update_scenario_run_status(scenario_id_snapshot, status)

        #THe scenarios has been consumed ( executed), we reste the + button back to non-green in UNity
        _publish_scenarios_ready(False)

        print(f"[vr_execute] scenario {scenario_id_snapshot} finished: {status}")        
                                        
    # Hire the background helper and let it work                                       
    threading.Thread(target=_run, daemon=True).start()                                   

    # Return to the Quest immediately - Channel 1 closes in ~50ms                        
    return jsonify({'success': True, 'started': True, 'scenario_id':
scenario_id_snapshot}) 


#for the microphone
@app.route('/transcribe', methods=['POST'])                                                                                                                                           
def transcribe():                          
    try:                                                                                                                                                                              
        if 'audio' not in request.files:                                                                                                                                            
            return jsonify({'error': 'No audio file provided'}), 400
                                                                    
        audio_file = request.files['audio']                                                                                                                                           
                                            
        with tempfile.NamedTemporaryFile(delete=False, suffix='.webm') as temp_audio:                                                                                                 
            audio_file.save(temp_audio.name)                                                                                                                                          
            temp_filename = temp_audio.name 
                                                                                                                                                                                    
        try:                                                                                                                                                                        
            result = whisper_service.transcribe(temp_filename)
            os.unlink(temp_filename)                          
            return jsonify({'text': result['text'], 'language': result['language']})
        except Exception as e:                                                                                                                                                        
            if os.path.exists(temp_filename):
                os.unlink(temp_filename)                                                                                                                                              
            raise e                                                                                                                                                                 
    except Exception as e:                                                                                                                                                            
        return jsonify({'error': str(e)}), 500



if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False) 
