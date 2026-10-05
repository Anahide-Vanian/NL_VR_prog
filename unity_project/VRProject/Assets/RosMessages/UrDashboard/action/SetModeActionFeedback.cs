using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.UrDashboard
{
    public class SetModeActionFeedback : ActionFeedback<SetModeFeedback>
    {
        public const string k_RosMessageName = "ur_dashboard_msgs/SetModeActionFeedback";
        public override string RosMessageName => k_RosMessageName;


        public SetModeActionFeedback() : base()
        {
            this.feedback = new SetModeFeedback();
        }

        public SetModeActionFeedback(HeaderMsg header, GoalStatusMsg status, SetModeFeedback feedback) : base(header, status)
        {
            this.feedback = feedback;
        }
        public static SetModeActionFeedback Deserialize(MessageDeserializer deserializer) => new SetModeActionFeedback(deserializer);

        SetModeActionFeedback(MessageDeserializer deserializer) : base(deserializer)
        {
            this.feedback = SetModeFeedback.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.status);
            serializer.Write(this.feedback);
        }


#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }
}
