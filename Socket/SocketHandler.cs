using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Astrometry;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.FileFormat;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ICameraMediator cameraMediator;
        private readonly IImageSaveMediator imageSaveMediator;
        private readonly IImageHistoryVM imageHistoryVm;
        private CancellationTokenSource captureCancelTokenSource;

        public event EventHandler<string> UserIdChangeEvent;

        public event EventHandler<UploadFileEventArgs> UploadFileEvent;
        public event EventHandler UserDisconnectedEvent;
        public event EventHandler CapturingEvent;

        public SocketHandler(IDeepSkyObjectSearchVM deepSkyObjectSearchVm,
            ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, IProfileService profileService,
            ICameraMediator cameraMediator, IImageSaveMediator imageSaveMediator, IImageHistoryVM imageHistoryVm) {
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
            this.telescopeMediator = telescopeMediator;
            this.imagingMediator = imagingMediator;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVm = imageHistoryVm;
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
                        Coordinates coordinates = new Coordinates(ra, dec, Epoch.JNOW);
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
                    captureCancelTokenSource = new CancellationTokenSource();

                    Task captureTask = Task.Run(async () => {
                        await new CoolCamera(cameraMediator) {
                            Temperature = -5, Duration = TimeSpan.FromMinutes(1).Minutes
                        }.Run(null, CancellationToken.None);
                        await new TakeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator,
                                imageHistoryVm) { ExposureTime = payload?.ExposureTime ?? 1 }
                            .Run(null, CancellationToken.None);
                        ClearCaptureCancelTokenSource();
                    }, captureCancelTokenSource.Token);
                    
                    try {
                        await captureTask;
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