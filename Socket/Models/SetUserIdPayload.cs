using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class SetUserIdPayload {
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
    }
}