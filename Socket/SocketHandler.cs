using Doodaoma.NINA.Doodaoma.Provider;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility.Notification;
using NINA.WPF.Base.Interfaces.ViewModel;
using Websocket.Client;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketHandler {
        private readonly WebsocketClient socketClient;
        private readonly ICameraInfoProvider cameraInfoProvider;
        private readonly IDeepSkyObjectSearchVM deepSkyObjectSearchVm;

        public SocketHandler(WebsocketClient socketClient, ICameraInfoProvider cameraInfoProvider,
            IDeepSkyObjectSearchVM deepSkyObjectSearchVm) {
            this.socketClient = socketClient;
            this.cameraInfoProvider = cameraInfoProvider;
            this.deepSkyObjectSearchVm = deepSkyObjectSearchVm;
        }

        public void HandleMessage(string message) {
            Notification.ShowInformation(message);
            JObject parsedMessage = JObject.Parse(message);
            string type = (string)parsedMessage["type"];

            if (type == "setTargetName") {
                deepSkyObjectSearchVm.TargetName = (string)parsedMessage["payload"]?["targetName"];
            } else if (type == "setLimit") {
                deepSkyObjectSearchVm.Limit = (int)parsedMessage["payload"]?["limit"];
            }
        }
    }
}