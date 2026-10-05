using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.UrDashboard
{
    public class SetModeActionGoal : ActionGoal<SetModeGoal>
    {
        public const string k_RosMessageName = "ur_dashboard_msgs/SetModeActionGoal";
        public override string RosMessageName => k_RosMessageName;


        public SetModeActionGoal() : base()
        {
            this.goal = new SetModeGoal();
        }

        public SetModeActionGoal(HeaderMsg header, GoalIDMsg goal_id, SetModeGoal goal) : base(header, goal_id)
        {
            this.goal = goal;
        }
        public static SetModeActionGoal Deserialize(MessageDeserializer deserializer) => new SetModeActionGoal(deserializer);

        SetModeActionGoal(MessageDeserializer deserializer) : base(deserializer)
        {
            this.goal = SetModeGoal.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.goal_id);
            serializer.Write(this.goal);
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
