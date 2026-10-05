using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.UrDashboard
{
    public class SetModeActionResult : ActionResult<SetModeResult>
    {
        public const string k_RosMessageName = "ur_dashboard_msgs/SetModeActionResult";
        public override string RosMessageName => k_RosMessageName;


        public SetModeActionResult() : base()
        {
            this.result = new SetModeResult();
        }

        public SetModeActionResult(HeaderMsg header, GoalStatusMsg status, SetModeResult result) : base(header, status)
        {
            this.result = result;
        }
        public static SetModeActionResult Deserialize(MessageDeserializer deserializer) => new SetModeActionResult(deserializer);

        SetModeActionResult(MessageDeserializer deserializer) : base(deserializer)
        {
            this.result = SetModeResult.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.status);
            serializer.Write(this.result);
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
