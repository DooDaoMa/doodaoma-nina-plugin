using Newtonsoft.Json;
using System.Collections.Generic;

namespace Doodaoma.NINA.Doodaoma.Socket.Models {
    public class RunImagingSequencePayload {
        [JsonProperty(PropertyName = "startSequence")]
        public StartSequence StartSequence { get; set; }

        [JsonProperty(PropertyName = "imagingSequence")]
        public ImagingSequence ImagingSequence { get; set; }

        [JsonProperty(PropertyName = "endSequence")]
        public EndSequence EndSequence { get; set; }
    }

    public class StartSequence {
        [JsonProperty(PropertyName = "cooling")]
        public Cooling Cooling { get; set; }
    }

    public class Cooling {
        [JsonProperty(PropertyName = "temperature")]
        public double Temperature { get; set; }

        [JsonProperty(PropertyName = "duration")]
        public double Duration { get; set; }
    }

    public class ImagingSequence {
        [JsonProperty(PropertyName = "target")]
        public Target Target { get; set; }

        [JsonProperty(PropertyName = "tracking")]
        public Tracking Tracking { get; set; }

        [JsonProperty(PropertyName = "guiding")]
        public Guiding Guiding { get; set; }

        [JsonProperty(PropertyName = "exposure")]
        public Exposure Exposure { get; set; }
    }

    public class Target {
        [JsonProperty(PropertyName = "name")] public string Name { get; set; }

        [JsonProperty(PropertyName = "rotation")]
        public double Rotation { get; set; }

        [JsonProperty(PropertyName = "ra")] public Ra Ra { get; set; }

        [JsonProperty(PropertyName = "dec")] public Dec Dec { get; set; }
    }

    public class Ra {
        [JsonProperty(PropertyName = "hours")] public string Hours { get; set; }

        [JsonProperty(PropertyName = "minutes")]
        public string Minutes { get; set; }

        [JsonProperty(PropertyName = "seconds")]
        public string Seconds { get; set; }
    }

    public class Dec {
        [JsonProperty(PropertyName = "degrees")]
        public string Degrees { get; set; }

        [JsonProperty(PropertyName = "minutes")]
        public string Minutes { get; set; }

        [JsonProperty(PropertyName = "seconds")]
        public string Seconds { get; set; }
    }

    public class Tracking {
        [JsonProperty(PropertyName = "mode")] public int Mode { get; set; }
    }

    public class Guiding {
        [JsonProperty(PropertyName = "forceCalibration")]
        public bool IsForceCalibration { get; set; }
    }

    public class Exposure {
        [JsonProperty(PropertyName = "gain")] public int Gain { get; set; }

        [JsonProperty(PropertyName = "time")] public double Time { get; set; }

        // [JsonProperty(PropertyName = "amount")]
        // public int Amount { get; set; }

        [JsonProperty(PropertyName = "binning")]
        public string Binning { get; set; }

        [JsonProperty(PropertyName = "imageType")]
        public string ImageType { get; set; }

        // [JsonProperty(PropertyName = "filterPosition")]
        // public int FilterPosition { get; set; }
    }

    public class EndSequence {
        [JsonProperty(PropertyName = "warming")]
        public Warming Warming { get; set; }
    }

    public class Warming {
        [JsonProperty(PropertyName = "duration")]
        public double Duration { get; set; }
    }
}