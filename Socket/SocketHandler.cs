using Doodaoma.NINA.Doodaoma.Provider;
using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.FileFormat;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Threading;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketHandler {
        private readonly ICameraInfoProvider cameraInfoProvider;
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVm;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IProfileService profileService;

        public event EventHandler<string> UserIdChangeEvent;
        public event EventHandler UserDisconnectedEvent; 

        public SocketHandler(ICameraInfoProvider cameraInfoProvider, IDeepSkyObjectSearchVM deepSkyObjectSearchVm,
            ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, IProfileService profileService) {
            this.cameraInfoProvider = cameraInfoProvider;
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
            this.telescopeMediator = telescopeMediator;
            this.imagingMediator = imagingMediator;
            this.profileService = profileService;
        }

        public async void HandleMessage(string message) {
            Notification.ShowInformation(message);
            JObject parsedMessage = JObject.Parse(message);
            string type = (string)parsedMessage["type"];

            switch (type) {
                case "setUserId": {
                    SetUserIdPayload payload = parsedMessage["payload"]?.ToObject<SetUserIdPayload>();
                    UserIdChangeEvent?.Invoke(this, payload?.UserId);
                    break;
                }
                case "resetUserId": {
                    UserDisconnectedEvent?.Invoke(this, EventArgs.Empty);
                    break;
                }
                case "setTargetName": {
                    SetTargetNamePayload payload = parsedMessage["payload"]?.ToObject<SetTargetNamePayload>();
                    deepSkyObjectSearchVm.TargetName = payload?.TargetName;
                    break;
                }
                case "setLimit": {
                    SetLimitPayload payload = parsedMessage["payload"]?.ToObject<SetLimitPayload>();
                    deepSkyObjectSearchVm.Limit = payload?.Limit ?? 10;
                    break;
                }
                case "slewTelescope": {
                    break;
                }
                case "capture": {
                    CapturePayload payload = parsedMessage["payload"]?.ToObject<CapturePayload>();
                    CaptureSequence captureSequence = new CaptureSequence {
                        ExposureTime = payload?.ExposureTime ?? 1.0
                    };
                    try {
                        IExposureData exposureData = await imagingMediator.CaptureImage(captureSequence, CancellationToken.None, null);
                        IImageData imageData = await exposureData.ToImageData();
                        FileSaveInfo fileSaveInfo = new FileSaveInfo(profileService);
                        string savePath = await imageData.SaveToDisk(fileSaveInfo);
                        Notification.ShowInformation("Save to path " + savePath);
                    } catch (Exception e) {
                        Notification.ShowError(e.ToString());
                    }
                    break;
                }
                case "cancelCapture": {
                    break;
                }
            }
        }
    }
}