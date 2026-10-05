import json
import os

class FeatureGenerator:
    def __init__(self, features_dir="features_mobile"):
        self.features_dir = features_dir
        os.makedirs(self.features_dir, exist_ok=True)
    
    def parse_llm_response(self, llm_response):
        try:
            # Try to parse as JSON
            scenario_data = json.loads(llm_response)
            return scenario_data
        except json.JSONDecodeError:
            # If not valid JSON, try to extract JSON part
            import re
            json_match = re.search(r'\{[\s\S]*\}', llm_response)
            if json_match:
                try:
                    scenario_data = json.loads(json_match.group(0))
                    print("Error: System response doesn't contain scenario")
                    return scenario_data
                except:
                    pass
            
            # Return error information
            #return {"error": "Could not parse LLM response as JSON", "raw_response": llm_response}
            #Here the LLM reponse doesnt contain a scenario but another message so we couldn't parse LLM response as JSON
            return {"error": "The system couldn't create a scenario", "raw_response": llm_response}

    # FUnction generates feature content without writing to a file
    def generate_feature_content(self, scenario_data):
       
        if "error" in scenario_data:
            return scenario_data["error"]
        
        feature_name = scenario_data.get("feature", "Unnamed Feature")
        feature_desc = scenario_data.get("description", "")
        scenarios = scenario_data.get("scenarios", [])
        
        # Generate the feature file content
        content = f"Feature: {feature_name}\n"
        if feature_desc:
            content += f"  {feature_desc}\n"
        content += "\n"
        
        # Add scenarios
        for scenario in scenarios:
            content += f"  Scenario: {scenario.get('name', 'Unnamed Scenario')}\n"
            
            # Add steps
            for step in scenario.get('steps', []):
                step_type = step.get('type', '').capitalize()
                step_text = step.get('text', '')
                
                # Skip the step type prefix if it's already in the text
                if step_text.lower().startswith(step_type.lower()):
                    content += f"    {step_text}\n"
                else:
                    content += f"    {step_type} {step_text}\n"
            
            content += "\n"
        
        return content
    
    def generate_feature_file(self, scenario_data, output_file=None):
        if "error" in scenario_data:
            return scenario_data["error"]

        if not output_file:
            # Create a filename from the feature name
            output_file = "features_mobile/robot_movement.feature"
        
        # Generate the feature file content
        content = self.generate_feature_content(scenario_data)
        
        # Write to file
        os.makedirs(os.path.dirname(output_file), exist_ok=True)
        with open(output_file, 'w') as f:
            f.write(content)
        
        return output_file
