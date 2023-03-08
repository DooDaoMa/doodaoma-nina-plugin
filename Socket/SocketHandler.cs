using Doodaoma.NINA.Doodaoma.Provider;
using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Astrometry;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketHandler {
        private readonly ICameraInfoProvider cameraInfoProvider;
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVm;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly ICameraMediator cameraMediator;

        public event EventHandler<string> UserIdChangeEvent;

        public SocketHandler(ICameraInfoProvider cameraInfoProvider, IDeepSkyObjectSearchVM deepSkyObjectSearchVm,
            ITelescopeMediator telescopeMediator, ICameraMediator cameraMediator) {
            this.cameraInfoProvider = cameraInfoProvider;
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
            this.telescopeMediator = telescopeMediator;
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
                    break;
                }
                case "cancelCapture": {
                    break;
                }
            }
        }
    }
}