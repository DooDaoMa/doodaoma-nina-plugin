using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
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
using NINA.Sequencer.SequenceItem.Utility;
using NINA.Sequencer.Trigger.Autofocus;
using NINA.Sequencer.Trigger.Guider;
using NINA.Sequencer.Trigger.MeridianFlip;
using NINA.Sequencer.Trigger.Platesolving;
using NINA.Sequencer.Utility.DateTimeProvider;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
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

        public Task RunStartSequence(CancellationToken cancellationToken) {
            SequentialContainer startMainContainer = new SequentialContainer();

            SequentialContainer startEquipmentCheckContainer = new SequentialContainer();
            startEquipmentCheckContainer.Add(new WaitForTime(dateTimeProviders)); // config params
            startEquipmentCheckContainer.Add(new UnparkScope(telescopeMediator));
            startEquipmentCheckContainer.Add(
                new SetTracking(telescopeMediator) { TrackingMode = TrackingMode.Sidereal }); // config params
            startEquipmentCheckContainer.Add(new CoolCamera(cameraMediator) {
                Temperature = -10, Duration = 5
            }); // config params

            SequentialContainer startSafeAndSyncContainer = new SequentialContainer();
            startSafeAndSyncContainer.Add(new SwitchFilter(profileService,
                filterWheelMediator)); // config params & send list to client
            startSafeAndSyncContainer.Add(new SlewScopeToRaDec(telescopeMediator, guiderMediator) {
                Coordinates = new InputCoordinates(new Coordinates(Angle.ByDegree(AstroUtil.HMSToDegrees("00:00:00")),
                    Angle.ByDegree(AstroUtil.DMSToDegrees("0° 00' 00\"")), Epoch.JNOW))
            }); // config params
            startSafeAndSyncContainer.Add(new RunAutofocus(profileService, imageHistoryVm, cameraMediator,
                filterWheelMediator, focuserMediator, autoFocusVmFactory));
            startSafeAndSyncContainer.Add(new SolveAndSync(profileService, telescopeMediator, rotatorMediator,
                imagingMediator, filterWheelMediator, plateSolverFactory, windowServiceFactory));

            startMainContainer.Add(startEquipmentCheckContainer);
            startMainContainer.Add(startSafeAndSyncContainer);
            return startMainContainer.Execute(this, cancellationToken);
        }

        public Task RunImagingSequence(CancellationToken cancellationToken) {
            DeepSkyObjectContainer targetMainContainer = new DeepSkyObjectContainer(profileService,
                nighttimeCalculator, framingAssistantVm, applicationMediator, planetariumFactory, cameraMediator,
                filterWheelMediator) {
                Target = new InputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude),
                    Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude),
                    profileService.ActiveProfile.AstrometrySettings.Horizon) {
                    TargetName = "Test",
                    Rotation = 0,
                    InputCoordinates = new InputCoordinates(new Coordinates(
                        Angle.ByDegree(AstroUtil.HMSToDegrees("02:03:23")),
                        Angle.ByDegree(AstroUtil.DMSToDegrees("33° 30' 3\"")), Epoch.JNOW))
                }
            };
            targetMainContainer.Add(new MeridianFlipTrigger(profileService, cameraMediator, telescopeMediator,
                focuserMediator, applicationStatusMediator, meridianFlipVmFactory));
            targetMainContainer.Add(new CenterAfterDriftTrigger(profileService, telescopeMediator, filterWheelMediator,
                guiderMediator, imagingMediator, cameraMediator, domeMediator, domeFollower, imageSaveMediator,
                applicationStatusMediator));
            targetMainContainer.Add(new RestoreGuiding(guiderMediator));

            SequentialContainer targetEquipmentCheckContainer = new SequentialContainer();
            targetEquipmentCheckContainer.Add(new WaitForTime(dateTimeProviders)); // require config
            targetEquipmentCheckContainer.Add(new UnparkScope(telescopeMediator));
            targetEquipmentCheckContainer.Add(
                new SetTracking(telescopeMediator) { TrackingMode = TrackingMode.Sidereal }); // require config
            targetEquipmentCheckContainer.Add(new CoolCamera(cameraMediator) {
                Temperature = -10, Duration = 5
            }); // require config

            SequentialContainer targetPrepareContainer = new SequentialContainer();
            targetPrepareContainer.Add(new SwitchFilter(profileService, filterWheelMediator) { }); // require config
            targetPrepareContainer.Add(new Center(profileService, telescopeMediator, imagingMediator,
                filterWheelMediator, guiderMediator, domeMediator, domeFollower, plateSolverFactory,
                windowServiceFactory) { Inherited = true });
            targetPrepareContainer.Add(new StartGuiding(guiderMediator) { ForceCalibration = false }); // require config
            targetPrepareContainer.Add(new RunAutofocus(profileService, imageHistoryVm, cameraMediator,
                filterWheelMediator, focuserMediator, autoFocusVmFactory));

            SequentialContainer imagingContainer = new SequentialContainer();
            imagingContainer.Add(new AutofocusAfterHFRIncreaseTrigger(profileService, imageHistoryVm, cameraMediator,
                filterWheelMediator, focuserMediator, autoFocusVmFactory) {
                Amount = 10, SampleSize = 5
            }); // require config
            imagingContainer.Add(new TimeCondition(dateTimeProviders)); // require config
            imagingContainer.Add(new AboveHorizonCondition(profileService)); // require config
            imagingContainer.Add(new SmartExposure(
                null,
                new SwitchFilter(profileService, filterWheelMediator),
                new TakeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVm) {
                    ExposureTime = 5
                },
                new LoopCondition { Iterations = 3 },
                new DitherAfterExposures(guiderMediator, imageHistoryVm, profileService) { AfterExposures = 0 }
            ));
            imagingContainer.Add(new Dither(guiderMediator, profileService));

            targetMainContainer.Add(targetEquipmentCheckContainer);
            targetMainContainer.Add(new WaitForTime(dateTimeProviders)); // require config
            targetMainContainer.Add(new WaitUntilAboveHorizon(profileService)); // require config
            targetMainContainer.Add(targetPrepareContainer);
            targetMainContainer.Add(imagingContainer);
            targetMainContainer.Add(new StopGuiding(guiderMediator));
            return targetMainContainer.Execute(null, cancellationToken);
        }

        public Task RunEndSequence(CancellationToken cancellationToken) {
            SequentialContainer endMainContainer = new SequentialContainer();

            ParallelContainer endInstructions = new ParallelContainer();
            endInstructions.Add(new WarmCamera(cameraMediator) { Duration = 5 }); // config params
            endInstructions.Add(new ParkScope(telescopeMediator, guiderMediator));

            endMainContainer.Add(endInstructions);
            return endMainContainer.Execute(null, cancellationToken);
        }

        public void Report(ApplicationStatus value) {
            Notification.ShowInformation(value.ToString());
        }
    }
}