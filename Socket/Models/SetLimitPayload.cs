using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class SetLimitPayload {
        [JsonProperty(PropertyName = "limit")]
        public int Limit { get; set; }
    }
}