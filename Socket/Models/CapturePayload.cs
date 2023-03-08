using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class CapturePayload {
        [JsonProperty(PropertyName = "exposureTime")]
        public double ExposureTime { get; set; }
    }
}