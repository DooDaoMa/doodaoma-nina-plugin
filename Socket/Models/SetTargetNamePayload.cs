using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class SetTargetNamePayload {
        [JsonProperty(PropertyName = "targetName")]
        public string TargetName { get; set; }
    }
}