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
    internal struct UploadFileEventArgs {
        public IImageData ImageData { get; set; }
        public string Path { get; set; }
    }

    internal class SocketHandler {
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVm;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IProfileService profileService;
        private CancellationTokenSource captureCancelTokenSource;

        public event EventHandler<string> UserIdChangeEvent;
        public event EventHandler UserDisconnectedEvent;
        public event EventHandler<UploadFileEventArgs> UploadFileEvent;

        public SocketHandler(IDeepSkyObjectSearchVM deepSkyObjectSearchVm,
            ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, IProfileService profileService) {
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
                    if (captureCancelTokenSource != null) {
                        return;
                    }
                    CapturePayload payload = parsedMessage["payload"]?.ToObject<CapturePayload>();
                    CaptureSequence captureSequence = new CaptureSequence {
                        ExposureTime = payload?.ExposureTime ?? 1.0
                    };
                    captureCancelTokenSource = new CancellationTokenSource();
                    try {
                        IExposureData exposureData =
                            await imagingMediator.CaptureImage(captureSequence, captureCancelTokenSource.Token, null);
                        IImageData imageData = await exposureData.ToImageData();
                        FileSaveInfo fileSaveInfo = new FileSaveInfo(profileService);
                        string savePath = await imageData.SaveToDisk(fileSaveInfo);
                        UploadFileEvent?.Invoke(this,
                            new UploadFileEventArgs { ImageData = imageData, Path = savePath });
                    } catch (Exception e) {
                        if (e is OperationCanceledException) {
                            Notification.ShowInformation("Cancel capturing");
                        } else {
                            Notification.ShowError(e.ToString());
                        }
                    }

                    break;
                }
                case "cancelCapture": {
                    if (captureCancelTokenSource == null) {
                        return;
                    }

                    DisposeCaptureCancelTokenSource();
                    break;
                }
            }
        }

        public void DisposeCaptureCancelTokenSource() {
            captureCancelTokenSource.Cancel();
            captureCancelTokenSource.Dispose();
            captureCancelTokenSource = null;
        }
    }
}