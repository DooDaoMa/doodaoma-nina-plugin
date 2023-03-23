using   NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.SequenceItem.Platesolving;
using NINA.Sequencer.SequenceItem.Telescope;
using NINA.Sequencer.Trigger.Guider;
using NINA.Sequencer.Trigger.MeridianFlip;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Doodaoma.NINA.Doodaoma.Manager {
    public class SequenceManager : IProgress<ApplicationStatus> {
        private readonly IList<IDateTimeProvider> dateTimeProviders;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly ICameraMediator cameraMediator;
        private readonly IProfileService profileService;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly IImageHistoryVM imageHistoryVm;
        private readonly IFocuserMediator focuserMediator;
        private readonly IAutoFocusVMFactory autoFocusVmFactory;
        private readonly IRotatorMediator rotatorMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IPlateSolverFactory plateSolverFactory;
        private readonly IWindowServiceFactory windowServiceFactory;
        private readonly INighttimeCalculator nighttimeCalculator;
        private readonly IFramingAssistantVM framingAssistantVm;
        private readonly IApplicationMediator applicationMediator;
        private readonly IPlanetariumFactory planetariumFactory;
        private readonly IMeridianFlipVMFactory meridianFlipVmFactory;
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IDomeFollower domeFollower;
        private readonly IImageSaveMediator imageSaveMediator;

        public SequenceManager(IList<IDateTimeProvider> dateTimeProviders, ITelescopeMediator telescopeMediator,
            ICameraMediator cameraMediator, IProfileService profileService, IFilterWheelMediator filterWheelMediator,
            IGuiderMediator guiderMediator, IImageHistoryVM imageHistoryVm, IFocuserMediator focuserMediator,
            IAutoFocusVMFactory autoFocusVmFactory, IRotatorMediator rotatorMediator, IImagingMediator imagingMediator,
            IPlateSolverFactory plateSolverFactory, IWindowServiceFactory windowServiceFactory,
            INighttimeCalculator nighttimeCalculator, IFramingAssistantVM framingAssistantVm,
            IApplicationMediator applicationMediator, IPlanetariumFactory planetariumFactory,
            IMeridianFlipVMFactory meridianFlipVmFactory, IApplicationStatusMediator applicationStatusMediator,
            IDomeMediator domeMediator, IDomeFollower domeFollower, IImageSaveMediator imageSaveMediator) {
            this.dateTimeProviders = dateTimeProviders;
            this.telescopeMediator = telescopeMediator;
            this.cameraMediator = cameraMediator;
            this.profileService = profileService;
            this.filterWheelMediator = filterWheelMediator;
            this.guiderMediator = guiderMediator;
            this.imageHistoryVm = imageHistoryVm;
            this.focuserMediator = focuserMediator;
            this.autoFocusVmFactory = autoFocusVmFactory;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.nighttimeCalculator = nighttimeCalculator;
            this.framingAssistantVm = framingAssistantVm;
            this.applicationMediator = applicationMediator;
            this.planetariumFactory = planetariumFactory;
            this.meridianFlipVmFactory = meridianFlipVmFactory;
            this.applicationStatusMediator = applicationStatusMediator;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            this.imageSaveMediator = imageSaveMediator;
        }

        public Task RunStartSequence(StartSequenceParams sequenceParams, CancellationToken cancellationToken) {
            SequentialContainer startMainContainer = new SequentialContainer();

            SequentialContainer startEquipmentCheckContainer = new SequentialContainer();
            startEquipmentCheckContainer.Add(new UnparkScope(telescopeMediator));
            startEquipmentCheckContainer.Add(new CoolCamera(cameraMediator) {
                Temperature = sequenceParams.Temperature, Duration = sequenceParams.Duration
            }); // config params

            startMainContainer.Add(startEquipmentCheckContainer);
            return startMainContainer.Execute(this, cancellationToken);
        }

        public Task RunImagingSequence(ImagingSequenceParams sequenceParams, CancellationToken cancellationToken) {
            DeepSkyObjectContainer targetMainContainer = new DeepSkyObjectContainer(profileService,
                nighttimeCalculator, framingAssistantVm, applicationMediator, planetariumFactory, cameraMediator,
                filterWheelMediator) {
                Target = new InputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude),
                    Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude),
                    profileService.ActiveProfile.AstrometrySettings.Horizon) {
                    TargetName = sequenceParams.Name,
                    Rotation = sequenceParams.Rotation,
                    InputCoordinates = new InputCoordinates(new Coordinates(
                        Angle.ByDegree(AstroUtil.HMSToDegrees(sequenceParams.Ra)),
                        Angle.ByDegree(AstroUtil.DMSToDegrees(sequenceParams.Dec)), Epoch.JNOW))
                }
            };
            targetMainContainer.Add(new MeridianFlipTrigger(profileService, cameraMediator, telescopeMediator,
                focuserMediator, applicationStatusMediator, meridianFlipVmFactory));
            targetMainContainer.Add(new RestoreGuiding(guiderMediator));

            SequentialContainer targetEquipmentCheckContainer = new SequentialContainer();
            targetEquipmentCheckContainer.Add(new UnparkScope(telescopeMediator));
            targetEquipmentCheckContainer.Add(
                new SetTracking(telescopeMediator) { TrackingMode = TrackingMode.Sidereal }); // require config

            SequentialContainer targetPrepareContainer = new SequentialContainer();
            targetPrepareContainer.Add(new Center(profileService, telescopeMediator, imagingMediator,
                filterWheelMediator, guiderMediator, domeMediator, domeFollower, plateSolverFactory,
                windowServiceFactory));
            targetPrepareContainer.Add(
                new StartGuiding(guiderMediator) {
                    ForceCalibration = sequenceParams.IsForceCalibration
                }); // require config
            targetPrepareContainer.Add(new RunAutofocus(profileService, imageHistoryVm, cameraMediator,
                filterWheelMediator, focuserMediator, autoFocusVmFactory));

            SequentialContainer imagingContainer = new SequentialContainer();
            imagingContainer.Add(new AboveHorizonCondition(profileService));
            foreach (ExposureItem exposureItem in sequenceParams.ExposureItems) {
                BinningMode.TryParse(exposureItem.Binning, out BinningMode mode);
                FilterInfo filterInfo = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList()
                    .Find(filter => filter.Position == exposureItem.FilterPosition);
                imagingContainer.Add(new SmartExposure(
                    null,
                    new SwitchFilter(profileService, filterWheelMediator) { Filter = filterInfo },
                    new TakeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator,
                        imageHistoryVm) {
                        ExposureTime = exposureItem.Time,
                        Gain = exposureItem.Gain,
                        Binning = mode,
                        ImageType = exposureItem.ImageType
                    },
                    new LoopCondition { Iterations = exposureItem.Amount },
                    new DitherAfterExposures(guiderMediator, imageHistoryVm, profileService) { AfterExposures = 0 }
                )); // require config   
            }

            imagingContainer.Add(new Dither(guiderMediator, profileService));

            targetMainContainer.Add(targetEquipmentCheckContainer);
            targetMainContainer.Add(targetPrepareContainer);
            targetMainContainer.Add(imagingContainer);
            targetMainContainer.Add(new StopGuiding(guiderMediator));
            return targetMainContainer.Execute(null, cancellationToken);
        }

        public Task RunEndSequence(EndSequenceParams sequenceParams, CancellationToken cancellationToken) {
            SequentialContainer endMainContainer = new SequentialContainer();

            ParallelContainer endInstructions = new ParallelContainer();
            endInstructions.Add(new WarmCamera(cameraMediator) { Duration = sequenceParams.Duration }); // config params
            endInstructions.Add(new ParkScope(telescopeMediator, guiderMediator));

            endMainContainer.Add(endInstructions);
            return endMainContainer.Execute(null, cancellationToken);
        }

        public void Report(ApplicationStatus value) {
            Notification.ShowInformation(value.ToString());
        }

        public struct StartSequenceParams {
            public double Temperature { get; set; }
            public double Duration { get; set; }
        }

        public struct ImagingSequenceParams {
            public string Name { get; set; }
            public double Rotation { get; set; }
            public string Ra { get; set; }
            public string Dec { get; set; }
            public int TrackingMode { get; set; }
            public bool IsForceCalibration { get; set; }
            public IList<ExposureItem> ExposureItems { get; set; }
        }

        public struct ExposureItem {
            public int Gain { get; set; }
            public double Time { get; set; }
            public int Amount { get; set; }
            public string Binning { get; set; }
            public string ImageType { get; set; }
            public int FilterPosition { get; set; }
        }

        public struct EndSequenceParams {
            public double Duration { get; set; }
        }
    }
}