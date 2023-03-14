using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Astrometry;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.FileFormat;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Threading;
using System.Windows.Media.Media3D;

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

        public event EventHandler<UploadFileEventArgs> UploadFileEvent;
        public event EventHandler UserDisconnectedEvent;
        public event EventHandler CapturingEvent;

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
                    SlewTelescopePayload payload = parsedMessage["payload"]?.ToObject<SlewTelescopePayload>();
                    if (payload?.RA == null || payload?.Dec == null) {
                        return;
                    }

                    if (!AstroUtil.IsHMS(payload.RA) || AstroUtil.IsDMS(payload.Dec)) {
                        return;
                    }

                    try {
                        Angle ra = Angle.ByDegree(AstroUtil.HMSToDegrees(payload.RA));
                        Angle dec = Angle.ByDegree(AstroUtil.DMSToDegrees(payload.Dec));
                        Notification.ShowInformation(ra.ToString());
                        Notification.ShowInformation(dec.ToString());
                        Coordinates coordinates = new Coordinates(ra, dec, Epoch.JNOW);
                        Notification.ShowInformation(coordinates.ToString());
                        bool isSuccess =
                            await telescopeMediator.SlewToCoordinatesAsync(coordinates, CancellationToken.None);
                        Notification.ShowInformation(isSuccess.ToString());
                    } catch (Exception exception) {
                        Notification.ShowError(exception.ToString());
                    }

                    break;
                }
                case "capture": {
                    if (captureCancelTokenSource != null) {
                        CapturingEvent?.Invoke(this, EventArgs.Empty);
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
                        ClearCaptureCancelTokenSource();
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

                    ClearCaptureCancelTokenSource();
                    break;
                }
            }
        }

        public void ClearCaptureCancelTokenSource() {
            captureCancelTokenSource.Cancel();
            captureCancelTokenSource.Dispose();
            captureCancelTokenSource = null;
        }
    }
}