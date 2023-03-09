﻿using Doodaoma.NINA.Doodaoma.Socket.Models;
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
        private readonly ICameraMediator cameraMediator;

        public event EventHandler<string> UserIdChangeEvent;
        public event EventHandler UserDisconnectedEvent;
        public event EventHandler<UploadFileEventArgs> UploadFileEvent;

        public SocketHandler(IDeepSkyObjectSearchVM deepSkyObjectSearchVm,
            ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, IProfileService profileService,
            ICameraMediator cameraMediator) {
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
            this.telescopeMediator = telescopeMediator;
            this.imagingMediator = imagingMediator;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
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
                        cameraMediator.RegisterCaptureBlock(this);
                        await cameraMediator.Capture(captureSequence, CancellationToken.None, null);
                        IExposureData exposureData =
                            await imagingMediator.CaptureImage(captureSequence, CancellationToken.None, null);
                        IImageData imageData = await exposureData.ToImageData();
                        FileSaveInfo fileSaveInfo = new FileSaveInfo(profileService);
                        string savePath = await imageData.SaveToDisk(fileSaveInfo);
                        UploadFileEvent?.Invoke(this,
                            new UploadFileEventArgs { ImageData = imageData, Path = savePath });
                    } catch (Exception e) {
                        Notification.ShowError(e.ToString());
                    } finally {
                        cameraMediator.ReleaseCaptureBlock(this);
                    }

                    break;
                }
                case "cancelCapture": {
                    Notification.ShowInformation(cameraMediator.IsFreeToCapture(this).ToString());
                    break;
                }
            }
        }
    }
}