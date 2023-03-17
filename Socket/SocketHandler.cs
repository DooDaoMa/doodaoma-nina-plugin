using Doodaoma.NINA.Doodaoma.Manager;
using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Astrometry;
using NINA.Core.Utility.Notification;
using System;
using System.Threading;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketHandler {
        private readonly SequenceManager sequenceManager;
        private CancellationTokenSource captureCancelTokenSource;

        public event EventHandler<string> UserIdChangeEvent;
        public event EventHandler UserDisconnectedEvent;

        public SocketHandler(SequenceManager sequenceManager) {
            this.sequenceManager = sequenceManager;
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
                case "runStartSequence": {
                    try {
                        await sequenceManager.RunStartSequence(CancellationToken.None);
                    } catch (Exception e) {
                        Notification.ShowError(e.ToString());
                    }

                    break;
                }
                case "runImagingSequence": {
                    try {
                        await sequenceManager.RunImagingSequence(CancellationToken.None);
                    } catch (Exception e) {
                        Notification.ShowError(e.ToString());
                    }

                    break;
                }
                case "runEndSequence": {
                    try {
                        await sequenceManager.RunEndSequence(CancellationToken.None);
                    } catch (Exception e) {
                        Notification.ShowError(e.ToString());
                    }

                    break;
                }
                case "cancelRunningSequence": {
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