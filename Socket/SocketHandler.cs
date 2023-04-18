using Doodaoma.NINA.Doodaoma.Manager;
using Doodaoma.NINA.Doodaoma.Socket.Models;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility.Notification;
using System;
using System.Threading;

namespace Doodaoma.NINA.Doodaoma.Socket {
    internal class SocketHandler {
        private readonly SequenceManager sequenceManager;
        private CancellationTokenSource captureCancelTokenSource;

        public event EventHandler<string> UserIdChangeEvent;
        public event EventHandler<bool> UpdateIsBusyEvent;
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
                case "runImagingSequence": {
                    if (GetIsBusy()) {
                        return;
                    }

                    captureCancelTokenSource = new CancellationTokenSource();
                    CancellationToken token = captureCancelTokenSource.Token;
                    UpdateIsBusy(true);
                    try {
                        RunImagingSequencePayload payload =
                            parsedMessage["payload"]?.ToObject<RunImagingSequencePayload>();
                        Ra ra = payload?.ImagingSequence.Target.Ra ??
                                new Ra { Hours = "00", Minutes = "00", Seconds = "00" };
                        Dec dec = payload?.ImagingSequence.Target.Dec ??
                                  new Dec { Degrees = "90", Minutes = "00", Seconds = "00" };
                        
                        await sequenceManager.RunStartSequence(
                            new SequenceManager.StartSequenceParams {
                                Temperature = payload?.StartSequence.Cooling.Temperature ?? 0,
                                Duration = payload?.StartSequence.Cooling.Duration ?? 0
                            },
                            token);
                        await sequenceManager.RunImagingSequence(
                            new SequenceManager.ImagingSequenceParams {
                                Name = payload?.ImagingSequence.Target.Name ?? "",
                                Rotation = payload?.ImagingSequence.Target.Rotation ?? 0,
                                Ra = $"{ra.Hours}:{ra.Minutes}:{ra.Seconds}",
                                Dec = $"{dec.Degrees}° {dec.Minutes}' {dec.Seconds}\"",
                                TrackingMode = payload?.ImagingSequence.Tracking.Mode ?? 0,
                                IsForceCalibration = payload?.ImagingSequence.Guiding.IsForceCalibration ?? false,
                                ExposureItem = new SequenceManager.ExposureItem {
                                    Time = payload?.ImagingSequence.Exposure.Time ?? 6,
                                    Gain = payload?.ImagingSequence.Exposure.Gain ?? 100,
                                    ImageType = payload?.ImagingSequence.Exposure.ImageType ?? "LIGHT",
                                    Binning = payload?.ImagingSequence.Exposure.Binning ?? "1x1",
                                }
                            },
                            token);
                        await sequenceManager.RunEndSequence(
                            new SequenceManager.EndSequenceParams {
                                Duration = payload?.EndSequence.Warming.Duration ?? 0
                            },
                            token);
                        ClearCaptureCancelTokenSource();
                    } catch (Exception e) {
                        if (!(e is OperationCanceledException)) {
                            Notification.ShowError(e.ToString());
                            ClearCaptureCancelTokenSource();
                        }
                    } finally {
                        UpdateIsBusy(false);
                    }

                    break;
                }
                case "cancelRunningSequence": {
                    if (!GetIsBusy()) {
                        return;
                    }

                    ClearCaptureCancelTokenSource();
                    break;
                }
                case "getIsBusy": {
                    UpdateIsBusy(GetIsBusy());
                    break;
                }
            }
        }

        public void ClearCaptureCancelTokenSource() {
            captureCancelTokenSource.Cancel();
            captureCancelTokenSource.Dispose();
            captureCancelTokenSource = null;
        }

        private void UpdateIsBusy(bool isBusy) {
            UpdateIsBusyEvent?.Invoke(this, isBusy);
        }

        private bool GetIsBusy() {
            return captureCancelTokenSource != null;
        }
    }
}