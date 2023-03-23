using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class SlewTelescopePayload {
        [JsonProperty(PropertyName = "ra")]
        public string Ra { get; set; }
        
        [JsonProperty(PropertyName = "dec")]
        public string Dec { get; set; }
    }
}