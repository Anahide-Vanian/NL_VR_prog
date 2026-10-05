using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;


namespace RosMessageTypes.UrDashboard
{
    public class SetModeAction : Action<SetModeActionGoal, SetModeActionResult, SetModeActionFeedback, SetModeGoal, SetModeResult, SetModeFeedback>
    {
        public const string k_RosMessageName = "ur_dashboard_msgs/SetModeAction";
        public override string RosMessageName => k_RosMessageName;


        public SetModeAction() : base()
        {
            this.action_goal = new SetModeActionGoal();
            this.action_result = new SetModeActionResult();
            this.action_feedback = new SetModeActionFeedback();
        }

        public static SetModeAction Deserialize(MessageDeserializer deserializer) => new SetModeAction(deserializer);

        SetModeAction(MessageDeserializer deserializer)
        {
            this.action_goal = SetModeActionGoal.Deserialize(deserializer);
            this.action_result = SetModeActionResult.Deserialize(deserializer);
            this.action_feedback = SetModeActionFeedback.Deserialize(deserializer);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.action_goal);
            serializer.Write(this.action_result);
            serializer.Write(this.action_feedback);
        }

    }
}
