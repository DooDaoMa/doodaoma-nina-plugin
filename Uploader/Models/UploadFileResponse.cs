using Newtonsoft.Json;

namespace Doodaoma.NINA.Doodaoma.Uploader.Models {
    public class UploadFileResponse {
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}