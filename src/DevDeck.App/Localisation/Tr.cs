using System.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.Localisation
{
    /// <summary>
    ///  What the views bind to for every word on screen.
    /// </summary>
    /// <remarks>
    ///  Generated, one property per string, rather than an indexer. A property is a plain binding
    ///  path, which is the one shape every binding engine handles the same way - an indexer path
    ///  depends on the accessor plugin resolving a string key through reflection, and a binding
    ///  that silently resolves to nothing presents as a blank label rather than as an error.
    ///
    ///  A single instance, held by <see cref="Current"/>, because the language is a property of the
    ///  application and not of any one panel. Changing it raises PropertyChanged for every string,
    ///  which is how the whole window re-reads itself without being rebuilt: the alternative is
    ///  asking the user to restart, and a setting that needs a restart is a setting people do not
    ///  believe worked.
    /// </remarks>
    public sealed class Tr : INotifyPropertyChanged
    {
        /// <summary>The instance every view binds against.</summary>
        public static Tr Current { get; } = new();

        private Tr()
        {
            Strings.Changed += Reread;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        ///  Tells every binding to read its string again.
        /// </summary>
        /// <remarks>
        ///  Named one at a time rather than with the empty name that conventionally means "all of
        ///  them". Both are meant to work; only this one is worth relying on, and the cost is a few
        ///  hundred notifications on an action the user takes once.
        /// </remarks>
        private void Reread()
        {
            foreach (string name in Keys)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private static readonly string[] Keys =
        [
            "AutoNoSteps",
            "AutoNotRunYet",
            "AutoNoFolder",
            "AutoNoCommand",
            "AutoWatching",
            "AutoCannotWatch",
            "AutoChooseWorkspace",
            "AutoDeckPresent",
            "AutoNoSuchCommand",
            "AutoNoSuchCommands",
            "AutoNewChain",
            "AutoNotRunNoCommand",
            "AutoStoppedAfter",
            "AutoStoppedAtStepMissing",
            "AutoStepOf",
            "AutoStoppedAtStepFailed",
            "AutoStoppedAtStep",
            "AutoNothingToRun",
            "AutoFinishedAll",
            "AutoOutputStep",
            "AutoStepExit",
            "AutoStepStopped",
            "AutoStepOff",
            "AutoOutputSkipped",
            "AutoWaitingFor",
            "AutoWhenFilesChange",
            "AutoNothingToImport",
            "AutoDeckEmpty",
            "AutoDeckAllPresent",
            "AutoImportedOne",
            "AutoImportedMany",
            "AutoImportedSome",
            "AutoWroteDeck",
            "AutoCouldNotWrite",
            "CmdHaveAllStarters",
            "CmdAddedOneStarter",
            "CmdAddedStarters",
            "CmdChooseWorkspaceFirst",
            "CmdNothingFound",
            "CmdAllPresent",
            "CmdImportedOne",
            "CmdImportedMany",
            "CmdImportedSome",
            "CmdCopiedLink",
            "CmdNewCommand",
            "CmdInUse",
            "CmdInUseChain",
            "CmdInUseWatch",
            "CmdCreated",
            "CmdLastRan",
            "CmdNeverRun",
            "CmdMoveUp",
            "CmdMoveDown",
            "CmdInUseRow",
            "CmdConverting",
            "CmdConverted",
            "CmdConvertNothing",
            "CmdConvertEdited",
            "CmdConvertFailed",
            "CmdUndoConvert",
            "CmdStopAllCount",
            "CmdStopAll",
            "CmdNotRunYet",
            "CmdUsually",
            "CmdStartedDetached",
            "CmdStartedDetachedTail",
            "CmdExitAfter",
            "CmdStoppedAfter",
            "CmdCouldNotStart",
            "HttpNewRequestName",
            "HttpNoEnvironmentFor",
            "HttpEnvironmentMissing",
            "HttpImportedNote",
            "HttpOneHeader",
            "HttpManyHeaders",
            "HttpNoteCookies",
            "HttpNoteBody",
            "HttpNoteCredentials",
            "HttpFiledUnder",
            "HttpUngrouped",
            "HttpCopySuffix",
            "HttpLastOne",
            "HttpCurlCopied",
            "HttpNotCurl",
            "HttpResponseCopied",
            "HttpMovedToEnvironment",
            "HttpSave",
            "HttpSaveResponse",
            "HttpResponseSaved",
            "HttpResponseNotSaved",
            "HttpImageBroken",
            "HttpMediaHere",
            "ClipsRecordingOn",
            "ClipsRecordingOff",
            "LogCopiedLines",
            "RunContainersVia",
            "RunNoEngine",
            "RunSummary",
            "RunAddedLogs",
            "ParamDefaultsTo",
            "ParamOffending",
            "MemPid",
            "MemDetail",
            "MemCores",
            "MemCleaning",
            "MemCleanLabel",
            "MemNothingTicked",
            "MemFreedSummary",
            "MemCleanFailed",
            "MemRunningStep",
            "MemStepOutcome",
            "MemFreed",
            "MemLessAvailable",
            "MemNoChange",
            "MemStepsRanOne",
            "MemStepsRan",
            "MemUnreadable",
            "MemInUse",
            "MemHideWidget",
            "MemFloatWidget",
            "SetHotkeyHeld",
            "SetStoragePortable",
            "SetStorageConfig",
            "SetSavedAutomatically",
            "SetSecretNeedsBoth",
            "SetSecretSaved",
            "SetSecretRemoved",
            "SetSchemeCan",
            "SetSchemeCannot",
            "SetHotkeyNote",
            "SetHotkeyUnreadable",
            "SetHotkeyTaken",
            "SetHotkeyClaimed",
            "SetHotkeyNone",
            "SetNoRelease",
            "SetUpdateAvailable",
            "SetUpToDate",
            "SetCouldNotCheck",
            "SetSavedAt",
            "PaletteCategoryRun",
            "PaletteCategoryGoTo",
            "PaletteCategoryDeck",
            "PaletteCategoryChain",
            "PaletteCategoryAutomation",
            "PaletteNewCommand",
            "PaletteStopEverything",
            "PaletteImportRunnables",
            "PaletteImportRunnablesHint",
            "PaletteNewChain",
            "PaletteNewChainHint",
            "NavFinished",
            "NavWasStopped",
            "NavFailed",
            "NavRanSummary",
            "PaletteExportDeckHint",
            "PaletteImportDeck",
            "PaletteExportDeck",
            "NavCommands",
            "NavCommandsBlurb",
            "NavAssistant",
            "NavAssistantBlurb",
            "NavHttp",
            "NavHttpBlurb",
            "NavToolbox",
            "NavToolboxBlurb",
            "NavRunning",
            "NavRunningBlurb",
            "NavAutomation",
            "NavAutomationBlurb",
            "NavClipboard",
            "NavClipboardBlurb",
            "NavLog",
            "NavLogBlurb",
            "NavMemory",
            "NavMemoryBlurbWindows",
            "NavMemoryBlurbOther",
            "NavSettings",
            "NavSettingsBlurb",
            "AssistantAssistant",
            "AssistantHistory",
            "Delete",
            "SelectText",
            "SelectTextTip",
            "AssistantSaveAsCommand",
            "AssistantNewChat",
            "AssistantHelp",
            "AiHelpTitle",
            "AiHelpIntro",
            "AiHelpUseExample",
            "AiHelpCommandTitle",
            "AiHelpCommandText",
            "AiHelpCommandExample",
            "AiHelpChainTitle",
            "AiHelpChainText",
            "AiHelpChainExample",
            "AiHelpFailureTitle",
            "AiHelpFailureText",
            "AiHelpFailureExample",
            "AiHelpFilesTitle",
            "AiHelpFilesText",
            "AiHelpFilesExample",
            "AiHelpRefineTitle",
            "AiHelpRefineText",
            "AiHelpRefineExample",
            "AiHelpTipsTitle",
            "AiHelpTip1",
            "AiHelpTip2",
            "AiHelpTip3",
            "AiHelpTip4",
            "AiHelpTip5",
            "AiHelpTip6",
            "AssistantAiSettings",
            "AssistantProvider",
            "AssistantEndpoint",
            "AssistantModel",
            "Refresh",
            "AssistantAPIKey",
            "AssistantCheckAgain",
            "AssistantCopyThisMessage",
            "AssistantCopyWholeConversation",
            "AssistantAddAsCommand",
            "AssistantCreateChain",
            "AssistantRunChain",
            "AssistantAskItSomething",
            "AssistantAnyCodeItWritesGetsA",
            "AssistantRunThisScriptNow",
            "AssistantThisIsAskedOncePerSession",
            "AssistantRunIt",
            "Cancel",
            "AssistantAttachAFile",
            "AssistantPrompts",
            "AssistantKeepThisPrompt",
            "AssistantAskTheModel",
            "Send",
            "Stop",
            "AssistantStopTheScript",
            "AutomationAutomation",
            "AutomationEverythingThatRunsACommandWithout",
            "AutomationChains",
            "AutomationSeveralCommandsOneAfterAnotherEach",
            "AutomationNewChain",
            "AutomationNoChainsYetAChainIs",
            "AutomationBroken",
            "Name",
            "AutomationStepsInOrder",
            "NavSectionBusy",
            "HttpHeaderCount",
            "HttpHeaderCountOne",
            "HttpFilterHeaders",
            "HttpNoHeadersMatch",
            "HttpNoHeadersAtAll",
            "HttpCopyHeader",
            "IconPickerTitle",
            "IconPickerPickOne",
            "IconPickerNone",
            "HttpChooseIcon",
            "HttpGroupIcon",
            "AutomationNoStepsYet",
            "AutomationAddStep",
            "AutomationMoveStepUp",
            "AutomationMoveStepDown",
            "AutomationRemoveStep",
            "AutomationStepIsMissing",
            "AutomationStepOnTip",
            "AutomationSameStepTwiceIsFine",
            "AutomationStopAtTheFirstStepThat",
            "AutomationOffIsForAChainThat",
            "AutomationRunChain",
            "AutomationChainOutput",
            "AutomationChainOutputEmpty",
            "AutomationFold",
            "Save",
            "AutomationWhenFilesChange",
            "AutomationRunACommandWhenSomethingIn",
            "AutomationNewWatch",
            "AutomationAWatchWaitsForTheWriting",
            "AutomationNoWatchesYet",
            "AutomationOn",
            "AutomationCommandToRunByName",
            "AutomationPickACommand",
            "AutomationBrowse",
            "AutomationPickTheFolderToWatch",
            "AutoWatchFired",
            "AutoRunningBecause",
            "AutoSomethingChanged",
            "AutoAndOthers",
            "AutoRanAtFor",
            "AutomationTellMeWhenAWatchFires",
            "AutomationWhatAWatchPassesToItsCommand",
            "AutomationFolderEmptyFollowsTheWorkspace",
            "AutomationFilesSeparatedBySemicolons",
            "AutomationThisProjectSOwnDeck",
            "AutomationADevdeckJsonCommittedBesideThe",
            "AutomationReadingADeckFileNeverRuns",
            "ImportFromThisProject",
            "AutomationExportMyDeckToThisProject",
            "ClipsClipboard",
            "ClipsWhatYouHaveCopiedWhileThis",
            "ClipsRecordWhatICopy",
            "ClipsThisIsOffUntilYouTurn",
            "ClipsSearchWhatYouHaveCopied",
            "Copy",
            "ClipsPin",
            "ClipsClearAll",
            "ClipsOpenThisInTheBrowser",
            "ClipsLink",
            "ClipsNothingRecordedYet",
            "CommandsCommands",
            "CommandsWorkspace",
            "CommandsNoFolderChosenYet",
            "CommandsBrowse",
            "CommandsNewCommand",
            "CommandsReadsPackageJsonMakefileCargoToml",
            "CommandsAddTheStarterCommands",
            "CommandsCopyALinkToThisCommand",
            "CommandsDevdeckRunOpensTheDeckAnd",
            "CommandsNoCommandsYetAddOneAnd",
            "CommandsRunWith",
            "CommandsBody",
            "CommandsWriteNameOrNameDefaultTo",
            "Run",
            "CommandsExplainThisFailure",
            "CommandsRunAndDonTWaitFor",
            "CommandsFollowOutput",
            "CommandsOpenInYourEditor",
            "CommandsOutputAppearsHereOnceYouRun",
            "CommandsChooseAWorkspaceFirst",
            "CommandsEveryCommandRunsInsideThisFolder",
            "CommandsChooseAFolder",
            "PickAttachFiles",
            "PickWorkspace",
            "EnvSecretNote",
            "EnvNewName",
            "EnvUseThisValue",
            "EnvKeepInVault",
            "EnvStoredTypeToReplace",
            "EnvNotSet",
            "EnvName",
            "EnvValue",
            "EnvSecret",
            "HttpAskName",
            "HttpAskGroup",
            "EnvironmentEnvironments",
            "EnvironmentASetOfValuesPerEnvironment",
            "EnvironmentAdd",
            "Remove",
            "EnvironmentEnvironmentName",
            "EnvironmentAddValue",
            "EnvironmentDone",
            "HttpRename",
            "HttpNameItAfterTheURL",
            "HttpDuplicate",
            "HttpFlag",
            "HttpNoFlag",
            "HttpRed",
            "HttpAmber",
            "HttpGreen",
            "HttpBlue",
            "HttpPurple",
            "HttpGrey",
            "HttpMoveToGroup",
            "HttpNoGroup",
            "HttpNewGroup",
            "HttpExisting",
            "HttpCopyAsCurl",
            "HttpDeleteThisRequest",
            "HttpNewRequest",
            "HttpPasteACurlCommand",
            "HttpEnvironment",
            "HttpNoneSendAsWritten",
            "Clear",
            "HttpSendRequestsExactlyAsTheyAre",
            "HttpEdit",
            "HttpAddEnvironmentsAndTheValuesName",
            "HttpLocalhost5000HealthOrPasteA",
            "HttpACurlCommandPastedHereIs",
            "HttpMore",
            "HttpHeaders",
            "HttpAdd",
            "HttpSendThisHeader",
            "HttpValue",
            "HttpBodyJSONIsDetectedFromThe",
            "LogLog",
            "LogEverythingTheAppItselfHasDone",
            "LogFilter",
            "LogProblemsOnly",
            "LogCopyAll",
            "LogNothingToReport",
            "MainWindowDismiss",
            "MemoryMemoryAndProcessor",
            "MemoryMemory",
            "MemoryProcessor",
            "MemoryAcrossAllCores",
            "MemorySampledEveryTwoSeconds",
            "MemoryDisk",
            "MemoryGpu",
            "MemoryDiskActive",
            "MemoryGpu3d",
            "MemDiskTotal",
            "MemDiskSpace",
            "MemNotAvailable",
            "MemWidgetGpuDisk",
            "MemoryLargestProcesses",
            "MemoryProcess",
            "MemoryShareOfTheHeaviest",
            "MemoryMemory2",
            "MemoryCleanupSteps",
            "MemorySomeTickedStepsNeedAnAdministrator",
            "MemoryAdministrator",
            "MemoryWidgetDevDeckMemory",
            "MemoryWidgetShowDevDeck",
            "MemoryWidgetCleanMemoryNow",
            "MemoryWidgetHideWidget",
            "MemoryWidgetQuitDevDeck",
            "MemoryWidgetClickToClean",
            "MemoryWidgetFreed",
            "NamePromptRename",
            "PaletteRunACommand",
            "PaletteTypeACommandAPanelOr",
            "PaletteNothingMatchesThat",
            "PaletteEnterRunsItArrowsMoveEsc",
            "ParameterPromptValuesForThisRun",
            "ParameterPromptThisCommandAsksForValuesBefore",
            "RunTilePorts",
            "RunTileContainers",
            "RunTileDeck",
            "RunTileHealth",
            "RunContainersUp",
            "RunDeckRunning",
            "RunHealthUp",
            "RunUnknown",
            "RunningFilter",
            "RunningAutoRefresh",
            "RunningAutoRefreshTip",
            "RunningPid",
            "RunningCopyUrl",
            "RunningHealth",
            "RunningHealthBlurb",
            "RunningHealthPlaceholder",
            "RunningHealthNone",
            "RunningCheckNow",
            "RunHealthWaiting",
            "RunHealthMs",
            "RunHealthNoAnswer",
            "RunHealthBadUrl",
            "RunningPortCheck",
            "RunningPortPlaceholder",
            "RunningCheck",
            "RunPortFree",
            "RunPortTaken",
            "RunPortTakenUnknown",
            "RunPortInvalid",
            "RunningDeckActivity",
            "RunningDeckIdle",
            "RunningTopProcesses",
            "Add",
            "SettingsEnvVars",
            "SettingsAddVariable",
            "SettingsEnvVarsNote",
            "CommandsParameters",
            "CommandsAddParameter",
            "CommandsParameterName",
            "CommandsParameterValue",
            "CommandsParameterOnTip",
            "CommandsParametersHelp",
            "RunningKill",
            "RunningKillTip",
            "RunKilled",
            "RunKillFailed",
            "RunningRunning",
            "RunningOnlyLikelyPorts",
            "RunningHidesSystemServicesAndEphemeralPorts",
            "RunningListening",
            "RunningOpen",
            "RunningStopWhatHasIt",
            "RunningContainers",
            "RunningStart",
            "RunningRestart",
            "RunningLogs",
            "RunningOpenInABrowser",
            "RunningFollowItsLogInTheDeck",
            "RunningNothingBelowUntilAnEngineIs",
            "RunningClose",
            "SettingsSettings",
            "SettingsAppearance",
            "SettingsTheme",
            "SettingsFollowTheSystemTracksTheDesktop",
            "SettingsLanguage",
            "SettingsTheLanguageChangesTheMomentYou",
            "SettingsAi",
            "SettingsAiNote",
            "SettingsBehaviour",
            "SettingsKeepThisMachineAwakeWhileDevDeck",
            "SettingsAlsoStopItLockingItselfAnd",
            "SettingsAskBeforePowerActions",
            "SettingsFollowOutputAsItArrives",
            "SettingsKeepAwakeTip",
            "SettingsStayAvailableTip",
            "SettingsConfirmPowerTip",
            "SettingsFollowOutputTip",
            "SettingsAutoStartTip",
            "SettingsNotSleepingAndNotLockingAre",
            "SettingsWhenALongCommandFinishes",
            "SettingsTellMeWhenItIsDone",
            "SettingsOnlyIfItRanForAt",
            "SettingsSeconds",
            "SettingsADesktopNotificationWhereTheSystem",
            "SettingsSecrets",
            "SettingsASecretIsStoredWrappedIn",
            "SettingsValue",
            "SettingsStore",
            "SettingsFromATerminalAndFromA",
            "SettingsASavedCommandCanBeStarted",
            "SettingsWriteTheTerminalLauncher",
            "SettingsRemoveIt",
            "SettingsRegisterDevdeck",
            "SettingsAKeyThatWorksFromAnywhere",
            "SettingsOneCombinationThatBringsTheDeck",
            "SettingsClaimIt",
            "SettingsGiveItBack",
            "SettingsThisBuild",
            "SettingsSettingsFile",
            "SettingsUpdates",
            "SettingsAsksTheReleasePageWhetherThere",
            "SettingsCheckNow",
            "SettingsOpenTheReleasePage",
            "SetWhatsNew",
            "SetAutoStart",
            "SetAutoStartNote",
            "SetAutoStartCannot",
            "SetAutoStartOn",
            "SetAutoStartOff",
            "SetAutoStartFailed",
            "NavChangelog",
            "NavChangelogBlurb",
            "ChangelogThisVersion",
            "ChangelogNotInstalled",
            "ChangelogReleased",
            "SplashTagline",
            "SplashVersion",
            "SplashLoading",
            "ChangelogCheck",
            "ChangelogAsking",
            "ChangelogEmpty",
            "ConfirmTitle",
            "SettingsStartOver",
            "SettingsResetExplains",
            "SettingsResetKeepsSecrets",
            "SettingsResetNow",
            "SettingsResetAsk",
            "SettingsResetDetail",
            "AutomationDragToResize",
            "SettingsResetRestarting",
            "SettingsResetDone",
            "SettingsResetFailed",
            "ToolboxEverythingHereRunsOnThisMachine",
            "ToolboxAgain",
            "ToolboxPaste",
            "ToolboxThePattern",
            "AiWhoYou",
            "AiWhoAssistant",
            "AiWhoOutput",
            "AiWhoProblem",
            "AiReady",
            "AiLookingForModel",
            "AiChecking",
            "AiModelOn",
            "AiUsing",
            "AiSetUpLabel",
            "AiNothingAnswered",
            "AiNoEndpoint",
            "AiSettingUp",
            "AiLocalReady",
            "AiNowAsking",
            "AiAskingEndpoint",
            "AiModelsAvailableOne",
            "AiModelsAvailable",
            "AiNoModels",
            "AiCouldNotReach",
            "AiQuestionReady",
            "AiAsking",
            "AiStopped",
            "AiFailed",
            "AiNoAnswer",
            "AiSavedNewOne",
            "AiCleared",
            "AiAlreadyAttached",
            "AiAttachedNote",
            "AiCountAttached",
            "AiSentWithOne",
            "AiSentWithMany",
            "AiOpened",
            "AiDeleted",
            "AiLoadedPrompt",
            "AiNothingToSave",
            "AiSavedPrompt",
            "AiSpendSession",
            "AiContextWindow",
            "AiContextTight",
            "AiNoCommandInAnswer",
            "AiNoChainInAnswer",
            "AiChainCreated",
            "AiChainMissing",
            "AiChainHeader",
            "AiAskWhy",
            "AiAskWhyTip",
            "AiRemoveOutputTip",
            "AiChainRenamed",
            "AiConfirmBlurb",
            "AiNotRun",
            "AiNeedWorkspace",
            "AiExitCode",
            "AiRanCleanly",
            "AiExited",
            "AiStoppedAfter",
            "AiCouldNotRunLine",
            "AiCouldNotRun",
            "ToolCopied",
            "ToolGroupData",
            "ToolGroupEncoding",
            "ToolGroupInspect",
            "ToolGroupGenerate",
            "ToolGroupText",
            "ToolJsonFormat",
            "ToolJsonFormatHint",
            "ToolJsonMinify",
            "ToolXmlFormat",
            "ToolBase64Encode",
            "ToolBase64Decode",
            "ToolBase64DecodeHint",
            "ToolUrlEncode",
            "ToolUrlDecode",
            "ToolHtmlEscape",
            "ToolHtmlUnescape",
            "ToolJwtDecode",
            "ToolJwtDecodeHint",
            "ToolHash",
            "ToolTimestamp",
            "ToolTimestampHint",
            "ToolUuid",
            "ToolUuidHint",
            "ToolCase",
            "ToolSortLines",
            "ToolSortLinesHint",
            "ToolRegex",
            "ToolRegexHint",
            "ToolboxPasteHere",
            "ToolboxUseAsInput",
        ];

        /// <summary>No steps yet.</summary>
        public string AutoNoSteps => Strings.Text("AutoNoSteps");

        /// <summary>Not run yet.</summary>
        public string AutoNotRunYet => Strings.Text("AutoNotRunYet");

        /// <summary>No folder to watch.</summary>
        public string AutoNoFolder => Strings.Text("AutoNoFolder");

        /// <summary>No command to run.</summary>
        public string AutoNoCommand => Strings.Text("AutoNoCommand");

        /// <summary>Watching {0} in {1}</summary>
        public string AutoWatching => Strings.Text("AutoWatching");

        /// <summary>Cannot watch that folder: {0}</summary>
        public string AutoCannotWatch => Strings.Text("AutoCannotWatch");

        /// <summary>Choose a workspace on the Commands page first.</summary>
        public string AutoChooseWorkspace => Strings.Text("AutoChooseWorkspace");

        /// <summary>This project ships a deck. Import brings its commands in - nothing run</summary>
        public string AutoDeckPresent => Strings.Text("AutoDeckPresent");

        /// <summary>No command called "{0}".</summary>
        public string AutoNoSuchCommand => Strings.Text("AutoNoSuchCommand");

        /// <summary>No command called {0}.</summary>
        public string AutoNoSuchCommands => Strings.Text("AutoNoSuchCommands");

        /// <summary>New chain</summary>
        public string AutoNewChain => Strings.Text("AutoNewChain");

        /// <summary>Not run - no command called "{0}".</summary>
        public string AutoNotRunNoCommand => Strings.Text("AutoNotRunNoCommand");

        /// <summary>Stopped after {0} of {1}.</summary>
        public string AutoStoppedAfter => Strings.Text("AutoStoppedAfter");

        /// <summary>Stopped at step {0} - no command called "{1}".</summary>
        public string AutoStoppedAtStepMissing => Strings.Text("AutoStoppedAtStepMissing");

        /// <summary>Step {0} of {1}: {2}</summary>
        public string AutoStepOf => Strings.Text("AutoStepOf");

        /// <summary>Stopped at step {0} of {1}: {2} failed.</summary>
        public string AutoStoppedAtStepFailed => Strings.Text("AutoStoppedAtStepFailed");

        /// <summary>Stopped at step {0} of {1}.</summary>
        public string AutoStoppedAtStep => Strings.Text("AutoStoppedAtStep");

        /// <summary>Nothing to run - this chain has no steps.</summary>
        public string AutoNothingToRun => Strings.Text("AutoNothingToRun");

        /// <summary>Finished all {0} steps.</summary>
        public string AutoFinishedAll => Strings.Text("AutoFinishedAll");

        /// <summary>Step {0} of {1} · {2}</summary>
        public string AutoOutputStep => Strings.Text("AutoOutputStep");

        /// <summary>exit code {0}</summary>
        public string AutoStepExit => Strings.Text("AutoStepExit");

        /// <summary>stopped</summary>
        public string AutoStepStopped => Strings.Text("AutoStepStopped");

        /// <summary>switched off</summary>
        public string AutoStepOff => Strings.Text("AutoStepOff");

        /// <summary>Skipped - no command called "{0}".</summary>
        public string AutoOutputSkipped => Strings.Text("AutoOutputSkipped");

        /// <summary>Step {0} of {1}: waiting for {2}, which is already running.</summary>
        public string AutoWaitingFor => Strings.Text("AutoWaitingFor");

        /// <summary>When files change</summary>
        public string AutoWhenFilesChange => Strings.Text("AutoWhenFilesChange");

        /// <summary>Nothing to import - this project has no deck file, or it could not be </summary>
        public string AutoNothingToImport => Strings.Text("AutoNothingToImport");

        /// <summary>That deck file lists nothing runnable.</summary>
        public string AutoDeckEmpty => Strings.Text("AutoDeckEmpty");

        /// <summary>Everything in that deck file is already in your deck.</summary>
        public string AutoDeckAllPresent => Strings.Text("AutoDeckAllPresent");

        /// <summary>Imported 1 command.</summary>
        public string AutoImportedOne => Strings.Text("AutoImportedOne");

        /// <summary>Imported {0} commands.</summary>
        public string AutoImportedMany => Strings.Text("AutoImportedMany");

        /// <summary>Imported {0}, and {1} were already here.</summary>
        public string AutoImportedSome => Strings.Text("AutoImportedSome");

        /// <summary>Wrote {0} commands to {1}. Commit it and the next person has your deck</summary>
        public string AutoWroteDeck => Strings.Text("AutoWroteDeck");

        /// <summary>Could not write it: {0}</summary>
        public string AutoCouldNotWrite => Strings.Text("AutoCouldNotWrite");

        /// <summary>You already have all of the starter commands.</summary>
        public string CmdHaveAllStarters => Strings.Text("CmdHaveAllStarters");

        /// <summary>Added 1 starter command.</summary>
        public string CmdAddedOneStarter => Strings.Text("CmdAddedOneStarter");

        /// <summary>Added {0} starter commands.</summary>
        public string CmdAddedStarters => Strings.Text("CmdAddedStarters");

        /// <summary>Choose a workspace first - there is nothing to read yet.</summary>
        public string CmdChooseWorkspaceFirst => Strings.Text("CmdChooseWorkspaceFirst");

        /// <summary>Nothing found. This project declares no scripts this knows how to read</summary>
        public string CmdNothingFound => Strings.Text("CmdNothingFound");

        /// <summary>Everything this project declares is already in your deck.</summary>
        public string CmdAllPresent => Strings.Text("CmdAllPresent");

        /// <summary>Imported 1 command.</summary>
        public string CmdImportedOne => Strings.Text("CmdImportedOne");

        /// <summary>Imported {0} commands.</summary>
        public string CmdImportedMany => Strings.Text("CmdImportedMany");

        /// <summary>Imported {0}, and {1} were already here.</summary>
        public string CmdImportedSome => Strings.Text("CmdImportedSome");

        /// <summary>Copied a link that runs "{0}".</summary>
        public string CmdCopiedLink => Strings.Text("CmdCopiedLink");

        /// <summary>New command</summary>
        public string CmdNewCommand => Strings.Text("CmdNewCommand");

        /// <summary>Used by {0}. Take it out of there first, then it can be deleted.</summary>
        public string CmdInUse => Strings.Text("CmdInUse");

        /// <summary>the chain "{0}"</summary>
        public string CmdInUseChain => Strings.Text("CmdInUseChain");

        /// <summary>the watch "{0}"</summary>
        public string CmdInUseWatch => Strings.Text("CmdInUseWatch");

        /// <summary>created {0}</summary>
        public string CmdCreated => Strings.Text("CmdCreated");

        /// <summary>ran {0}</summary>
        public string CmdLastRan => Strings.Text("CmdLastRan");

        /// <summary>never run</summary>
        public string CmdNeverRun => Strings.Text("CmdNeverRun");

        /// <summary>Move up (Alt+Up)</summary>
        public string CmdMoveUp => Strings.Text("CmdMoveUp");

        /// <summary>Move down (Alt+Down)</summary>
        public string CmdMoveDown => Strings.Text("CmdMoveDown");

        /// <summary>Used by {0}, so it cannot be selected or deleted. Take it out of there</summary>
        public string CmdInUseRow => Strings.Text("CmdInUseRow");

        /// <summary>Converting to {0} with AI…</summary>
        public string CmdConverting => Strings.Text("CmdConverting");

        /// <summary>Converted from {0} to {1} by AI. Read it before you run it.</summary>
        public string CmdConverted => Strings.Text("CmdConverted");

        /// <summary>The AI did not send back a script, so the body was left as it was.</summary>
        public string CmdConvertNothing => Strings.Text("CmdConvertNothing");

        /// <summary>The script was edited while it was being converted, so your version wa</summary>
        public string CmdConvertEdited => Strings.Text("CmdConvertEdited");

        /// <summary>Could not convert with AI: {0}</summary>
        public string CmdConvertFailed => Strings.Text("CmdConvertFailed");

        /// <summary>Undo</summary>
        public string CmdUndoConvert => Strings.Text("CmdUndoConvert");

        /// <summary>Stop all ({0})</summary>
        public string CmdStopAllCount => Strings.Text("CmdStopAllCount");

        /// <summary>Stop all</summary>
        public string CmdStopAll => Strings.Text("CmdStopAll");

        /// <summary>Not run yet.</summary>
        public string CmdNotRunYet => Strings.Text("CmdNotRunYet");

        /// <summary>usually {0}</summary>
        public string CmdUsually => Strings.Text("CmdUsually");

        /// <summary>Started. Not waiting for it to finish.</summary>
        public string CmdStartedDetached => Strings.Text("CmdStartedDetached");

        /// <summary>Started detached. Nothing further will be reported.</summary>
        public string CmdStartedDetachedTail => Strings.Text("CmdStartedDetachedTail");

        /// <summary>Exit code {0} after {1}</summary>
        public string CmdExitAfter => Strings.Text("CmdExitAfter");

        /// <summary>Stopped after {0}</summary>
        public string CmdStoppedAfter => Strings.Text("CmdStoppedAfter");

        /// <summary>Could not start: {0}</summary>
        public string CmdCouldNotStart => Strings.Text("CmdCouldNotStart");

        /// <summary>New request</summary>
        public string HttpNewRequestName => Strings.Text("HttpNewRequestName");

        /// <summary>No environment chosen, so {0} will be sent as written.</summary>
        public string HttpNoEnvironmentFor => Strings.Text("HttpNoEnvironmentFor");

        /// <summary>{0} does not define {1}.</summary>
        public string HttpEnvironmentMissing => Strings.Text("HttpEnvironmentMissing");

        /// <summary>Imported {0} · </summary>
        public string HttpImportedNote => Strings.Text("HttpImportedNote");

        /// <summary>1 header</summary>
        public string HttpOneHeader => Strings.Text("HttpOneHeader");

        /// <summary>{0} headers</summary>
        public string HttpManyHeaders => Strings.Text("HttpManyHeaders");

        /// <summary> · cookies</summary>
        public string HttpNoteCookies => Strings.Text("HttpNoteCookies");

        /// <summary> · body</summary>
        public string HttpNoteBody => Strings.Text("HttpNoteBody");

        /// <summary>. Credentials came with it - saved as typed.</summary>
        public string HttpNoteCredentials => Strings.Text("HttpNoteCredentials");

        /// <summary>Filed under {0}.</summary>
        public string HttpFiledUnder => Strings.Text("HttpFiledUnder");

        /// <summary>Ungrouped.</summary>
        public string HttpUngrouped => Strings.Text("HttpUngrouped");

        /// <summary>{0} copy</summary>
        public string HttpCopySuffix => Strings.Text("HttpCopySuffix");

        /// <summary>That is the last one - clear it instead.</summary>
        public string HttpLastOne => Strings.Text("HttpLastOne");

        /// <summary>curl command copied.</summary>
        public string HttpCurlCopied => Strings.Text("HttpCurlCopied");

        /// <summary>That did not look like a curl command.</summary>
        public string HttpNotCurl => Strings.Text("HttpNotCurl");

        /// <summary>Response copied.</summary>
        public string HttpResponseCopied => Strings.Text("HttpResponseCopied");

        /// <summary> {0} moved into {1}.</summary>
        public string HttpMovedToEnvironment => Strings.Text("HttpMovedToEnvironment");

        /// <summary>Save</summary>
        public string HttpSave => Strings.Text("HttpSave");

        /// <summary>Save the response to a file</summary>
        public string HttpSaveResponse => Strings.Text("HttpSaveResponse");

        /// <summary>Saved as {0}.</summary>
        public string HttpResponseSaved => Strings.Text("HttpResponseSaved");

        /// <summary>Could not save: {0}</summary>
        public string HttpResponseNotSaved => Strings.Text("HttpResponseNotSaved");

        /// <summary>This image could not be decoded. Save it and open it elsewhere.</summary>
        public string HttpImageBroken => Strings.Text("HttpImageBroken");

        /// <summary>Audio and video are not played here. Save the file and open it in a pl</summary>
        public string HttpMediaHere => Strings.Text("HttpMediaHere");

        /// <summary>Recording what you copy. Anything that looks like a credential is skip</summary>
        public string ClipsRecordingOn => Strings.Text("ClipsRecordingOn");

        /// <summary>Stopped. What was already recorded is still here until you clear it.</summary>
        public string ClipsRecordingOff => Strings.Text("ClipsRecordingOff");

        /// <summary>Copied {0} lines.</summary>
        public string LogCopiedLines => Strings.Text("LogCopiedLines");

        /// <summary>Containers, through {0}.</summary>
        public string RunContainersVia => Strings.Text("RunContainersVia");

        /// <summary>No container engine found. Install Docker or Podman and restart to see</summary>
        public string RunNoEngine => Strings.Text("RunNoEngine");

        /// <summary>{0} listening · {1} containers · checked {2}</summary>
        public string RunSummary => Strings.Text("RunSummary");

        /// <summary>Added "logs: {0}" to the deck.</summary>
        public string RunAddedLogs => Strings.Text("RunAddedLogs");

        /// <summary>Defaults to {0}</summary>
        public string ParamDefaultsTo => Strings.Text("ParamDefaultsTo");

        /// <summary>This contains {0}, so it would run as more than one command.</summary>
        public string ParamOffending => Strings.Text("ParamOffending");

        /// <summary>pid {0}</summary>
        public string MemPid => Strings.Text("MemPid");

        /// <summary>{0} · pid {1} · {2} · {3}% of physical memory</summary>
        public string MemDetail => Strings.Text("MemDetail");

        /// <summary>{0} logical cores</summary>
        public string MemCores => Strings.Text("MemCores");

        /// <summary>Cleaning…</summary>
        public string MemCleaning => Strings.Text("MemCleaning");

        /// <summary>Clean memory</summary>
        public string MemCleanLabel => Strings.Text("MemCleanLabel");

        /// <summary>No steps are ticked, so there was nothing to run.</summary>
        public string MemNothingTicked => Strings.Text("MemNothingTicked");

        /// <summary>{0} · {1}</summary>
        public string MemFreedSummary => Strings.Text("MemFreedSummary");

        /// <summary>The clean failed: {0}</summary>
        public string MemCleanFailed => Strings.Text("MemCleanFailed");

        /// <summary>Running: {0}…</summary>
        public string MemRunningStep => Strings.Text("MemRunningStep");

        /// <summary>{0} — {1}</summary>
        public string MemStepOutcome => Strings.Text("MemStepOutcome");

        /// <summary>Freed {0}</summary>
        public string MemFreed => Strings.Text("MemFreed");

        /// <summary>{0} less available</summary>
        public string MemLessAvailable => Strings.Text("MemLessAvailable");

        /// <summary>No change</summary>
        public string MemNoChange => Strings.Text("MemNoChange");

        /// <summary>{0} of {1} step ran</summary>
        public string MemStepsRanOne => Strings.Text("MemStepsRanOne");

        /// <summary>{0} of {1} steps ran</summary>
        public string MemStepsRan => Strings.Text("MemStepsRan");

        /// <summary>Could not read memory on this system.</summary>
        public string MemUnreadable => Strings.Text("MemUnreadable");

        /// <summary>{0} of {1} in use · processor {2}</summary>
        public string MemInUse => Strings.Text("MemInUse");

        /// <summary>Hide widget</summary>
        public string MemHideWidget => Strings.Text("MemHideWidget");

        /// <summary>Float widget</summary>
        public string MemFloatWidget => Strings.Text("MemFloatWidget");

        /// <summary>{0} brings the deck up from anywhere.</summary>
        public string SetHotkeyHeld => Strings.Text("SetHotkeyHeld");

        /// <summary>Beside the executable, so a copied folder carries your deck with it.</summary>
        public string SetStoragePortable => Strings.Text("SetStoragePortable");

        /// <summary>In your own config folder, which is where a signed or system-installed</summary>
        public string SetStorageConfig => Strings.Text("SetStorageConfig");

        /// <summary>Saved automatically.</summary>
        public string SetSavedAutomatically => Strings.Text("SetSavedAutomatically");

        /// <summary>A secret needs both a name and a value.</summary>
        public string SetSecretNeedsBoth => Strings.Text("SetSecretNeedsBoth");

        /// <summary>Saved. Use it in a command as {{{{secret:{0}}}}}.</summary>
        public string SetSecretSaved => Strings.Text("SetSecretSaved");

        /// <summary>Removed {0}. Any command using it will now run with it left as typed.</summary>
        public string SetSecretRemoved => Strings.Text("SetSecretRemoved");

        /// <summary>Registers devdeck:// for your user only, so a link in a README or a CI</summary>
        public string SetSchemeCan => Strings.Text("SetSchemeCan");

        /// <summary>On macOS this belongs to an app bundle's Info.plist and cannot be clai</summary>
        public string SetSchemeCannot => Strings.Text("SetSchemeCannot");

        /// <summary>Works while you are in another application. A modifier is required - C</summary>
        public string SetHotkeyNote => Strings.Text("SetHotkeyNote");

        /// <summary>"{0}" is not a combination I can read. Try something like Ctrl+Shift+D</summary>
        public string SetHotkeyUnreadable => Strings.Text("SetHotkeyUnreadable");

        /// <summary>Could not claim {0} - something else on this machine already has it. T</summary>
        public string SetHotkeyTaken => Strings.Text("SetHotkeyTaken");

        /// <summary>{0} now brings the deck up from anywhere.</summary>
        public string SetHotkeyClaimed => Strings.Text("SetHotkeyClaimed");

        /// <summary>No key is claimed.</summary>
        public string SetHotkeyNone => Strings.Text("SetHotkeyNone");

        /// <summary>Nothing published yet at the release page.</summary>
        public string SetNoRelease => Strings.Text("SetNoRelease");

        /// <summary>{0} is available. You have {1}.</summary>
        public string SetUpdateAvailable => Strings.Text("SetUpdateAvailable");

        /// <summary>You have the newest build ({0}).</summary>
        public string SetUpToDate => Strings.Text("SetUpToDate");

        /// <summary>Could not check: {0}</summary>
        public string SetCouldNotCheck => Strings.Text("SetCouldNotCheck");

        /// <summary>Saved at {0}.</summary>
        public string SetSavedAt => Strings.Text("SetSavedAt");

        /// <summary>Run</summary>
        public string PaletteCategoryRun => Strings.Text("PaletteCategoryRun");

        /// <summary>Go to</summary>
        public string PaletteCategoryGoTo => Strings.Text("PaletteCategoryGoTo");

        /// <summary>Deck</summary>
        public string PaletteCategoryDeck => Strings.Text("PaletteCategoryDeck");

        /// <summary>Chain</summary>
        public string PaletteCategoryChain => Strings.Text("PaletteCategoryChain");

        /// <summary>Automation</summary>
        public string PaletteCategoryAutomation => Strings.Text("PaletteCategoryAutomation");

        /// <summary>New command</summary>
        public string PaletteNewCommand => Strings.Text("PaletteNewCommand");

        /// <summary>Stop everything that is running</summary>
        public string PaletteStopEverything => Strings.Text("PaletteStopEverything");

        /// <summary>Import runnables from this project</summary>
        public string PaletteImportRunnables => Strings.Text("PaletteImportRunnables");

        /// <summary>package.json, Makefile, Cargo.toml and the rest</summary>
        public string PaletteImportRunnablesHint => Strings.Text("PaletteImportRunnablesHint");

        /// <summary>New chain</summary>
        public string PaletteNewChain => Strings.Text("PaletteNewChain");

        /// <summary>Several commands, run one after another</summary>
        public string PaletteNewChainHint => Strings.Text("PaletteNewChainHint");

        /// <summary>finished</summary>
        public string NavFinished => Strings.Text("NavFinished");

        /// <summary>was stopped</summary>
        public string NavWasStopped => Strings.Text("NavWasStopped");

        /// <summary>failed</summary>
        public string NavFailed => Strings.Text("NavFailed");

        /// <summary>{0}s · exit code {1}</summary>
        public string NavRanSummary => Strings.Text("NavRanSummary");

        /// <summary>Writes {0} for the next person to clone it</summary>
        public string PaletteExportDeckHint => Strings.Text("PaletteExportDeckHint");

        /// <summary>Import this project's deck file</summary>
        public string PaletteImportDeck => Strings.Text("PaletteImportDeck");

        /// <summary>Export deck to this project</summary>
        public string PaletteExportDeck => Strings.Text("PaletteExportDeck");

        /// <summary>Commands</summary>
        public string NavCommands => Strings.Text("NavCommands");

        /// <summary>Your saved commands and scripts.</summary>
        public string NavCommandsBlurb => Strings.Text("NavCommandsBlurb");

        /// <summary>Assistant</summary>
        public string NavAssistant => Strings.Text("NavAssistant");

        /// <summary>A local or hosted model, and its code straight into the deck.</summary>
        public string NavAssistantBlurb => Strings.Text("NavAssistantBlurb");

        /// <summary>HTTP</summary>
        public string NavHttp => Strings.Text("NavHttp");

        /// <summary>Send a request. Paste a curl command and it becomes one.</summary>
        public string NavHttpBlurb => Strings.Text("NavHttpBlurb");

        /// <summary>Toolbox</summary>
        public string NavToolbox => Strings.Text("NavToolbox");

        /// <summary>Encode, decode, format and hash - all of it on this machine.</summary>
        public string NavToolboxBlurb => Strings.Text("NavToolboxBlurb");

        /// <summary>Running</summary>
        public string NavRunning => Strings.Text("NavRunning");

        /// <summary>What is listening, and what is containerised.</summary>
        public string NavRunningBlurb => Strings.Text("NavRunningBlurb");

        /// <summary>Automation</summary>
        public string NavAutomation => Strings.Text("NavAutomation");

        /// <summary>Chains, file watches and the deck a project commits.</summary>
        public string NavAutomationBlurb => Strings.Text("NavAutomationBlurb");

        /// <summary>Clipboard</summary>
        public string NavClipboard => Strings.Text("NavClipboard");

        /// <summary>What you have copied, once you switch it on.</summary>
        public string NavClipboardBlurb => Strings.Text("NavClipboardBlurb");

        /// <summary>Log</summary>
        public string NavLog => Strings.Text("NavLog");

        /// <summary>Everything the app itself has done this session.</summary>
        public string NavLogBlurb => Strings.Text("NavLogBlurb");

        /// <summary>Memory/CPU</summary>
        public string NavMemory => Strings.Text("NavMemory");

        /// <summary>Memory and processor load, working set trimming and the standby cache.</summary>
        public string NavMemoryBlurbWindows => Strings.Text("NavMemoryBlurbWindows");

        /// <summary>Memory and processor load. Only the page cache can be dropped on this </summary>
        public string NavMemoryBlurbOther => Strings.Text("NavMemoryBlurbOther");

        /// <summary>Settings</summary>
        public string NavSettings => Strings.Text("NavSettings");

        /// <summary>Theme, behaviour and where this build keeps its files.</summary>
        public string NavSettingsBlurb => Strings.Text("NavSettingsBlurb");

        /// <summary>Assistant</summary>
        public string AssistantAssistant => Strings.Text("AssistantAssistant");

        /// <summary>History</summary>
        public string AssistantHistory => Strings.Text("AssistantHistory");

        /// <summary>Delete</summary>
        public string Delete => Strings.Text("Delete");

        /// <summary>Select text</summary>
        public string SelectText => Strings.Text("SelectText");

        /// <summary>Shows the output as plain text that can be selected across lines and c</summary>
        public string SelectTextTip => Strings.Text("SelectTextTip");

        /// <summary>Save as command</summary>
        public string AssistantSaveAsCommand => Strings.Text("AssistantSaveAsCommand");

        /// <summary>New chat</summary>
        public string AssistantNewChat => Strings.Text("AssistantNewChat");

        /// <summary>Help</summary>
        public string AssistantHelp => Strings.Text("AssistantHelp");

        /// <summary>What the assistant can do</summary>
        public string AiHelpTitle => Strings.Text("AiHelpTitle");

        /// <summary>Ask in plain words. Code it writes comes back with buttons to run it o</summary>
        public string AiHelpIntro => Strings.Text("AiHelpIntro");

        /// <summary>Puts this in the box below, ready to send or change.</summary>
        public string AiHelpUseExample => Strings.Text("AiHelpUseExample");

        /// <summary>Write a command</summary>
        public string AiHelpCommandTitle => Strings.Text("AiHelpCommandTitle");

        /// <summary>A one-liner or a whole script, in a language this machine runs. Run it</summary>
        public string AiHelpCommandText => Strings.Text("AiHelpCommandText");

        /// <summary>Write a script that lists the 10 largest files under this folder</summary>
        public string AiHelpCommandExample => Strings.Text("AiHelpCommandExample");

        /// <summary>Build a chain</summary>
        public string AiHelpChainTitle => Strings.Text("AiHelpChainTitle");

        /// <summary>Ask for several commands that work together. Each comes back as its ow</summary>
        public string AiHelpChainText => Strings.Text("AiHelpChainText");

        /// <summary>Make three commands that count the files, count the TODOs and show the</summary>
        public string AiHelpChainExample => Strings.Text("AiHelpChainExample");

        /// <summary>Explain a failure</summary>
        public string AiHelpFailureTitle => Strings.Text("AiHelpFailureTitle");

        /// <summary>When a command fails on the Commands page, Explain this failure sends </summary>
        public string AiHelpFailureText => Strings.Text("AiHelpFailureText");

        /// <summary>Why would 'dotnet build' fail with error NETSDK1045, and how do I fix </summary>
        public string AiHelpFailureExample => Strings.Text("AiHelpFailureExample");

        /// <summary>Ask about your files</summary>
        public string AiHelpFilesTitle => Strings.Text("AiHelpFilesTitle");

        /// <summary>Attach files, or drag them onto the chat, and ask about them. It also </summary>
        public string AiHelpFilesText => Strings.Text("AiHelpFilesText");

        /// <summary>Explain what the attached script does, and what could go wrong when it</summary>
        public string AiHelpFilesExample => Strings.Text("AiHelpFilesExample");

        /// <summary>Change what it wrote</summary>
        public string AiHelpRefineTitle => Strings.Text("AiHelpRefineTitle");

        /// <summary>Follow up on the last answer instead of starting again: it remembers t</summary>
        public string AiHelpRefineText => Strings.Text("AiHelpRefineText");

        /// <summary>Make the last script skip the bin, obj and node_modules folders</summary>
        public string AiHelpRefineExample => Strings.Text("AiHelpRefineExample");

        /// <summary>Getting good answers</summary>
        public string AiHelpTipsTitle => Strings.Text("AiHelpTipsTitle");

        /// <summary>Say what you want to end up with, not just the tool: "the branches alr</summary>
        public string AiHelpTip1 => Strings.Text("AiHelpTip1");

        /// <summary>Name the language when it matters: PowerShell, batch or bash.</summary>
        public string AiHelpTip2 => Strings.Text("AiHelpTip2");

        /// <summary>For a chain, say what each step should produce and what the last step </summary>
        public string AiHelpTip3 => Strings.Text("AiHelpTip3");

        /// <summary>Read code before you run it, above all anything that deletes, moves or</summary>
        public string AiHelpTip4 => Strings.Text("AiHelpTip4");

        /// <summary>Keep a prompt you reuse with Keep this prompt and find it under Prompt</summary>
        public string AiHelpTip5 => Strings.Text("AiHelpTip5");

        /// <summary>Choose the provider and model under Settings > AI. Larger models follo</summary>
        public string AiHelpTip6 => Strings.Text("AiHelpTip6");

        /// <summary>AI settings</summary>
        public string AssistantAiSettings => Strings.Text("AssistantAiSettings");

        /// <summary>Provider</summary>
        public string AssistantProvider => Strings.Text("AssistantProvider");

        /// <summary>Endpoint</summary>
        public string AssistantEndpoint => Strings.Text("AssistantEndpoint");

        /// <summary>Model</summary>
        public string AssistantModel => Strings.Text("AssistantModel");

        /// <summary>Refresh</summary>
        public string Refresh => Strings.Text("Refresh");

        /// <summary>API key</summary>
        public string AssistantAPIKey => Strings.Text("AssistantAPIKey");

        /// <summary>Check again</summary>
        public string AssistantCheckAgain => Strings.Text("AssistantCheckAgain");

        /// <summary>Copy this message</summary>
        public string AssistantCopyThisMessage => Strings.Text("AssistantCopyThisMessage");

        /// <summary>Copy whole conversation</summary>
        public string AssistantCopyWholeConversation => Strings.Text("AssistantCopyWholeConversation");

        /// <summary>Add as command</summary>
        public string AssistantAddAsCommand => Strings.Text("AssistantAddAsCommand");

        /// <summary>Create chain</summary>
        public string AssistantCreateChain => Strings.Text("AssistantCreateChain");

        /// <summary>Run chain</summary>
        public string AssistantRunChain => Strings.Text("AssistantRunChain");

        /// <summary>Ask it something.</summary>
        public string AssistantAskItSomething => Strings.Text("AssistantAskItSomething");

        /// <summary>Any code it writes gets a Run button and an Add as command button. Run</summary>
        public string AssistantAnyCodeItWritesGetsA => Strings.Text("AssistantAnyCodeItWritesGetsA");

        /// <summary>Run this script now?</summary>
        public string AssistantRunThisScriptNow => Strings.Text("AssistantRunThisScriptNow");

        /// <summary>This is asked once per session.</summary>
        public string AssistantThisIsAskedOncePerSession => Strings.Text("AssistantThisIsAskedOncePerSession");

        /// <summary>Run it</summary>
        public string AssistantRunIt => Strings.Text("AssistantRunIt");

        /// <summary>Cancel</summary>
        public string Cancel => Strings.Text("Cancel");

        /// <summary>Attach a file</summary>
        public string AssistantAttachAFile => Strings.Text("AssistantAttachAFile");

        /// <summary>Prompts</summary>
        public string AssistantPrompts => Strings.Text("AssistantPrompts");

        /// <summary>Keep this prompt</summary>
        public string AssistantKeepThisPrompt => Strings.Text("AssistantKeepThisPrompt");

        /// <summary>Ask the model…</summary>
        public string AssistantAskTheModel => Strings.Text("AssistantAskTheModel");

        /// <summary>Send</summary>
        public string Send => Strings.Text("Send");

        /// <summary>Stop</summary>
        public string Stop => Strings.Text("Stop");

        /// <summary>Stop the script</summary>
        public string AssistantStopTheScript => Strings.Text("AssistantStopTheScript");

        /// <summary>Automation</summary>
        public string AutomationAutomation => Strings.Text("AutomationAutomation");

        /// <summary>Everything that runs a command without you pressing Run.</summary>
        public string AutomationEverythingThatRunsACommandWithout => Strings.Text("AutomationEverythingThatRunsACommandWithout");

        /// <summary>Chains</summary>
        public string AutomationChains => Strings.Text("AutomationChains");

        /// <summary>Several commands, one after another. Each step waits for the last.</summary>
        public string AutomationSeveralCommandsOneAfterAnotherEach => Strings.Text("AutomationSeveralCommandsOneAfterAnotherEach");

        /// <summary>New chain</summary>
        public string AutomationNewChain => Strings.Text("AutomationNewChain");

        /// <summary>No chains yet. A chain is a list of command names - pull, build, test </summary>
        public string AutomationNoChainsYetAChainIs => Strings.Text("AutomationNoChainsYetAChainIs");

        /// <summary>broken</summary>
        public string AutomationBroken => Strings.Text("AutomationBroken");

        /// <summary>Name</summary>
        public string Name => Strings.Text("Name");

        /// <summary>Steps, in order</summary>
        public string AutomationStepsInOrder => Strings.Text("AutomationStepsInOrder");

        /// <summary>Running</summary>
        public string NavSectionBusy => Strings.Text("NavSectionBusy");

        /// <summary>{0} headers</summary>
        public string HttpHeaderCount => Strings.Text("HttpHeaderCount");

        /// <summary>1 header</summary>
        public string HttpHeaderCountOne => Strings.Text("HttpHeaderCountOne");

        /// <summary>Filter</summary>
        public string HttpFilterHeaders => Strings.Text("HttpFilterHeaders");

        /// <summary>No header matches that.</summary>
        public string HttpNoHeadersMatch => Strings.Text("HttpNoHeadersMatch");

        /// <summary>The reply carried no headers.</summary>
        public string HttpNoHeadersAtAll => Strings.Text("HttpNoHeadersAtAll");

        /// <summary>Copy this header</summary>
        public string HttpCopyHeader => Strings.Text("HttpCopyHeader");

        /// <summary>Choose an icon</summary>
        public string IconPickerTitle => Strings.Text("IconPickerTitle");

        /// <summary>Pick one to mark this in the list. Hover a tile to see the name it is </summary>
        public string IconPickerPickOne => Strings.Text("IconPickerPickOne");

        /// <summary>No icon</summary>
        public string IconPickerNone => Strings.Text("IconPickerNone");

        /// <summary>Icon...</summary>
        public string HttpChooseIcon => Strings.Text("HttpChooseIcon");

        /// <summary>Group icon...</summary>
        public string HttpGroupIcon => Strings.Text("HttpGroupIcon");

        /// <summary>No steps yet. Pick a command below and add it.</summary>
        public string AutomationNoStepsYet => Strings.Text("AutomationNoStepsYet");

        /// <summary>Add step</summary>
        public string AutomationAddStep => Strings.Text("AutomationAddStep");

        /// <summary>Move this step earlier</summary>
        public string AutomationMoveStepUp => Strings.Text("AutomationMoveStepUp");

        /// <summary>Move this step later</summary>
        public string AutomationMoveStepDown => Strings.Text("AutomationMoveStepDown");

        /// <summary>Remove this step</summary>
        public string AutomationRemoveStep => Strings.Text("AutomationRemoveStep");

        /// <summary>No command by this name</summary>
        public string AutomationStepIsMissing => Strings.Text("AutomationStepIsMissing");

        /// <summary>Run this step. Untick it to skip the step without taking it out of the</summary>
        public string AutomationStepOnTip => Strings.Text("AutomationStepOnTip");

        /// <summary>A command may appear more than once - build, test, build is a real cha</summary>
        public string AutomationSameStepTwiceIsFine => Strings.Text("AutomationSameStepTwiceIsFine");

        /// <summary>Stop at the first step that fails</summary>
        public string AutomationStopAtTheFirstStepThat => Strings.Text("AutomationStopAtTheFirstStepThat");

        /// <summary>Off is for a chain that is a list of chores rather than a pipeline - o</summary>
        public string AutomationOffIsForAChainThat => Strings.Text("AutomationOffIsForAChainThat");

        /// <summary>Run chain</summary>
        public string AutomationRunChain => Strings.Text("AutomationRunChain");

        /// <summary>Output</summary>
        public string AutomationChainOutput => Strings.Text("AutomationChainOutput");

        /// <summary>Run the chain and every step's output appears here, one after another.</summary>
        public string AutomationChainOutputEmpty => Strings.Text("AutomationChainOutputEmpty");

        /// <summary>Show or hide this section</summary>
        public string AutomationFold => Strings.Text("AutomationFold");

        /// <summary>Save</summary>
        public string Save => Strings.Text("Save");

        /// <summary>When files change</summary>
        public string AutomationWhenFilesChange => Strings.Text("AutomationWhenFilesChange");

        /// <summary>Run a command when something in a folder is saved.</summary>
        public string AutomationRunACommandWhenSomethingIn => Strings.Text("AutomationRunACommandWhenSomethingIn");

        /// <summary>New watch</summary>
        public string AutomationNewWatch => Strings.Text("AutomationNewWatch");

        /// <summary>A watch waits for the writing to stop before it runs anything, ignores</summary>
        public string AutomationAWatchWaitsForTheWriting => Strings.Text("AutomationAWatchWaitsForTheWriting");

        /// <summary>No watches yet.</summary>
        public string AutomationNoWatchesYet => Strings.Text("AutomationNoWatchesYet");

        /// <summary>On</summary>
        public string AutomationOn => Strings.Text("AutomationOn");

        /// <summary>Command to run</summary>
        public string AutomationCommandToRunByName => Strings.Text("AutomationCommandToRunByName");

        /// <summary>Pick a command</summary>
        public string AutomationPickACommand => Strings.Text("AutomationPickACommand");

        /// <summary>Browse...</summary>
        public string AutomationBrowse => Strings.Text("AutomationBrowse");

        /// <summary>Pick the folder to watch</summary>
        public string AutomationPickTheFolderToWatch => Strings.Text("AutomationPickTheFolderToWatch");

        /// <summary>{0} fired</summary>
        public string AutoWatchFired => Strings.Text("AutoWatchFired");

        /// <summary>{0} changed - running {1}</summary>
        public string AutoRunningBecause => Strings.Text("AutoRunningBecause");

        /// <summary>something</summary>
        public string AutoSomethingChanged => Strings.Text("AutoSomethingChanged");

        /// <summary>{0} and {1} more</summary>
        public string AutoAndOthers => Strings.Text("AutoAndOthers");

        /// <summary>Ran at {0} for {1} · {2} so far</summary>
        public string AutoRanAtFor => Strings.Text("AutoRanAtFor");

        /// <summary>Tell me when a watch fires</summary>
        public string AutomationTellMeWhenAWatchFires => Strings.Text("AutomationTellMeWhenAWatchFires");

        /// <summary>A watch passes what changed to its command as environment variables: D</summary>
        public string AutomationWhatAWatchPassesToItsCommand => Strings.Text("AutomationWhatAWatchPassesToItsCommand");

        /// <summary>Folder - empty follows the workspace</summary>
        public string AutomationFolderEmptyFollowsTheWorkspace => Strings.Text("AutomationFolderEmptyFollowsTheWorkspace");

        /// <summary>Files, separated by semicolons</summary>
        public string AutomationFilesSeparatedBySemicolons => Strings.Text("AutomationFilesSeparatedBySemicolons");

        /// <summary>This project's own deck</summary>
        public string AutomationThisProjectSOwnDeck => Strings.Text("AutomationThisProjectSOwnDeck");

        /// <summary>A .devdeck.json committed beside the code, so the commands arrive with</summary>
        public string AutomationADevdeckJsonCommittedBesideThe => Strings.Text("AutomationADevdeckJsonCommittedBesideThe");

        /// <summary>Reading a deck file never runs anything. Importing adds its commands t</summary>
        public string AutomationReadingADeckFileNeverRuns => Strings.Text("AutomationReadingADeckFileNeverRuns");

        /// <summary>Import from this project</summary>
        public string ImportFromThisProject => Strings.Text("ImportFromThisProject");

        /// <summary>Export my deck to this project</summary>
        public string AutomationExportMyDeckToThisProject => Strings.Text("AutomationExportMyDeckToThisProject");

        /// <summary>Clipboard</summary>
        public string ClipsClipboard => Strings.Text("ClipsClipboard");

        /// <summary>What you have copied, while this panel was open.</summary>
        public string ClipsWhatYouHaveCopiedWhileThis => Strings.Text("ClipsWhatYouHaveCopiedWhileThis");

        /// <summary>Record what I copy</summary>
        public string ClipsRecordWhatICopy => Strings.Text("ClipsRecordWhatICopy");

        /// <summary>This is off until you turn it on, and it only runs while you are on th</summary>
        public string ClipsThisIsOffUntilYouTurn => Strings.Text("ClipsThisIsOffUntilYouTurn");

        /// <summary>Search what you have copied</summary>
        public string ClipsSearchWhatYouHaveCopied => Strings.Text("ClipsSearchWhatYouHaveCopied");

        /// <summary>Copy</summary>
        public string Copy => Strings.Text("Copy");

        /// <summary>Pin</summary>
        public string ClipsPin => Strings.Text("ClipsPin");

        /// <summary>Clear all</summary>
        public string ClipsClearAll => Strings.Text("ClipsClearAll");

        /// <summary>Open this in the browser</summary>
        public string ClipsOpenThisInTheBrowser => Strings.Text("ClipsOpenThisInTheBrowser");

        /// <summary>link</summary>
        public string ClipsLink => Strings.Text("ClipsLink");

        /// <summary>Nothing recorded yet.</summary>
        public string ClipsNothingRecordedYet => Strings.Text("ClipsNothingRecordedYet");

        /// <summary>Commands</summary>
        public string CommandsCommands => Strings.Text("CommandsCommands");

        /// <summary>Workspace</summary>
        public string CommandsWorkspace => Strings.Text("CommandsWorkspace");

        /// <summary>No folder chosen yet</summary>
        public string CommandsNoFolderChosenYet => Strings.Text("CommandsNoFolderChosenYet");

        /// <summary>Browse</summary>
        public string CommandsBrowse => Strings.Text("CommandsBrowse");

        /// <summary>New command</summary>
        public string CommandsNewCommand => Strings.Text("CommandsNewCommand");

        /// <summary>Reads package.json, Makefile, Cargo.toml, compose files and the rest</summary>
        public string CommandsReadsPackageJsonMakefileCargoToml => Strings.Text("CommandsReadsPackageJsonMakefileCargoToml");

        /// <summary>Add the starter commands</summary>
        public string CommandsAddTheStarterCommands => Strings.Text("CommandsAddTheStarterCommands");

        /// <summary>Copy a link to this command</summary>
        public string CommandsCopyALinkToThisCommand => Strings.Text("CommandsCopyALinkToThisCommand");

        /// <summary>devdeck://run/… - opens the deck and runs it</summary>
        public string CommandsDevdeckRunOpensTheDeckAnd => Strings.Text("CommandsDevdeckRunOpensTheDeckAnd");

        /// <summary>No commands yet. Add one and it is saved with your settings, body and </summary>
        public string CommandsNoCommandsYetAddOneAnd => Strings.Text("CommandsNoCommandsYetAddOneAnd");

        /// <summary>Run with</summary>
        public string CommandsRunWith => Strings.Text("CommandsRunWith");

        /// <summary>Body</summary>
        public string CommandsBody => Strings.Text("CommandsBody");

        /// <summary>Write {{name}} or {{name:default}} to be asked for a value before this</summary>
        public string CommandsWriteNameOrNameDefaultTo => Strings.Text("CommandsWriteNameOrNameDefaultTo");

        /// <summary>Run</summary>
        public string Run => Strings.Text("Run");

        /// <summary>Explain this failure</summary>
        public string CommandsExplainThisFailure => Strings.Text("CommandsExplainThisFailure");

        /// <summary>Run and don't wait for it to finish</summary>
        public string CommandsRunAndDonTWaitFor => Strings.Text("CommandsRunAndDonTWaitFor");

        /// <summary>Follow output</summary>
        public string CommandsFollowOutput => Strings.Text("CommandsFollowOutput");

        /// <summary>Open in your editor</summary>
        public string CommandsOpenInYourEditor => Strings.Text("CommandsOpenInYourEditor");

        /// <summary>Output appears here once you run this command.</summary>
        public string CommandsOutputAppearsHereOnceYouRun => Strings.Text("CommandsOutputAppearsHereOnceYouRun");

        /// <summary>Choose a workspace first</summary>
        public string CommandsChooseAWorkspaceFirst => Strings.Text("CommandsChooseAWorkspaceFirst");

        /// <summary>Every command runs inside this folder, so there is nothing sensible to</summary>
        public string CommandsEveryCommandRunsInsideThisFolder => Strings.Text("CommandsEveryCommandRunsInsideThisFolder");

        /// <summary>Choose a folder</summary>
        public string CommandsChooseAFolder => Strings.Text("CommandsChooseAFolder");

        /// <summary>Attach files to the conversation</summary>
        public string PickAttachFiles => Strings.Text("PickAttachFiles");

        /// <summary>Choose the folder commands run in</summary>
        public string PickWorkspace => Strings.Text("PickWorkspace");

        /// <summary>A value marked secret is kept in this machine's vault, not in settings</summary>
        public string EnvSecretNote => Strings.Text("EnvSecretNote");

        /// <summary>New environment</summary>
        public string EnvNewName => Strings.Text("EnvNewName");

        /// <summary>Use this value</summary>
        public string EnvUseThisValue => Strings.Text("EnvUseThisValue");

        /// <summary>Keep this value in the vault rather than in settings.json</summary>
        public string EnvKeepInVault => Strings.Text("EnvKeepInVault");

        /// <summary>stored - type to replace</summary>
        public string EnvStoredTypeToReplace => Strings.Text("EnvStoredTypeToReplace");

        /// <summary>not set</summary>
        public string EnvNotSet => Strings.Text("EnvNotSet");

        /// <summary>name</summary>
        public string EnvName => Strings.Text("EnvName");

        /// <summary>value</summary>
        public string EnvValue => Strings.Text("EnvValue");

        /// <summary>secret</summary>
        public string EnvSecret => Strings.Text("EnvSecret");

        /// <summary>What should this request be called?</summary>
        public string HttpAskName => Strings.Text("HttpAskName");

        /// <summary>Which group should this be filed under?</summary>
        public string HttpAskGroup => Strings.Text("HttpAskGroup");

        /// <summary>Environments</summary>
        public string EnvironmentEnvironments => Strings.Text("EnvironmentEnvironments");

        /// <summary>A set of values per environment. Write {{name}} in a URL, a header or </summary>
        public string EnvironmentASetOfValuesPerEnvironment => Strings.Text("EnvironmentASetOfValuesPerEnvironment");

        /// <summary>Add</summary>
        public string EnvironmentAdd => Strings.Text("EnvironmentAdd");

        /// <summary>Remove</summary>
        public string Remove => Strings.Text("Remove");

        /// <summary>Environment name</summary>
        public string EnvironmentEnvironmentName => Strings.Text("EnvironmentEnvironmentName");

        /// <summary>+ Add value</summary>
        public string EnvironmentAddValue => Strings.Text("EnvironmentAddValue");

        /// <summary>Done</summary>
        public string EnvironmentDone => Strings.Text("EnvironmentDone");

        /// <summary>Rename…</summary>
        public string HttpRename => Strings.Text("HttpRename");

        /// <summary>Name it after the URL</summary>
        public string HttpNameItAfterTheURL => Strings.Text("HttpNameItAfterTheURL");

        /// <summary>Duplicate</summary>
        public string HttpDuplicate => Strings.Text("HttpDuplicate");

        /// <summary>Flag</summary>
        public string HttpFlag => Strings.Text("HttpFlag");

        /// <summary>No flag</summary>
        public string HttpNoFlag => Strings.Text("HttpNoFlag");

        /// <summary>Red</summary>
        public string HttpRed => Strings.Text("HttpRed");

        /// <summary>Amber</summary>
        public string HttpAmber => Strings.Text("HttpAmber");

        /// <summary>Green</summary>
        public string HttpGreen => Strings.Text("HttpGreen");

        /// <summary>Blue</summary>
        public string HttpBlue => Strings.Text("HttpBlue");

        /// <summary>Purple</summary>
        public string HttpPurple => Strings.Text("HttpPurple");

        /// <summary>Grey</summary>
        public string HttpGrey => Strings.Text("HttpGrey");

        /// <summary>Move to group</summary>
        public string HttpMoveToGroup => Strings.Text("HttpMoveToGroup");

        /// <summary>No group</summary>
        public string HttpNoGroup => Strings.Text("HttpNoGroup");

        /// <summary>New group…</summary>
        public string HttpNewGroup => Strings.Text("HttpNewGroup");

        /// <summary>Existing</summary>
        public string HttpExisting => Strings.Text("HttpExisting");

        /// <summary>Copy as curl</summary>
        public string HttpCopyAsCurl => Strings.Text("HttpCopyAsCurl");

        /// <summary>Delete this request</summary>
        public string HttpDeleteThisRequest => Strings.Text("HttpDeleteThisRequest");

        /// <summary>New request</summary>
        public string HttpNewRequest => Strings.Text("HttpNewRequest");

        /// <summary>Paste a curl command</summary>
        public string HttpPasteACurlCommand => Strings.Text("HttpPasteACurlCommand");

        /// <summary>Environment</summary>
        public string HttpEnvironment => Strings.Text("HttpEnvironment");

        /// <summary>None - send as written</summary>
        public string HttpNoneSendAsWritten => Strings.Text("HttpNoneSendAsWritten");

        /// <summary>Clear</summary>
        public string Clear => Strings.Text("Clear");

        /// <summary>Send requests exactly as they are written</summary>
        public string HttpSendRequestsExactlyAsTheyAre => Strings.Text("HttpSendRequestsExactlyAsTheyAre");

        /// <summary>Edit…</summary>
        public string HttpEdit => Strings.Text("HttpEdit");

        /// <summary>Add environments and the values {{name}} resolves from</summary>
        public string HttpAddEnvironmentsAndTheValuesName => Strings.Text("HttpAddEnvironmentsAndTheValuesName");

        /// <summary>localhost:5000/health — or paste a whole curl command here</summary>
        public string HttpLocalhost5000HealthOrPasteA => Strings.Text("HttpLocalhost5000HealthOrPasteA");

        /// <summary>A curl command pasted here is read into this request: method, URL, hea</summary>
        public string HttpACurlCommandPastedHereIs => Strings.Text("HttpACurlCommandPastedHereIs");

        /// <summary>More</summary>
        public string HttpMore => Strings.Text("HttpMore");

        /// <summary>Headers</summary>
        public string HttpHeaders => Strings.Text("HttpHeaders");

        /// <summary>+ Add</summary>
        public string HttpAdd => Strings.Text("HttpAdd");

        /// <summary>Send this header</summary>
        public string HttpSendThisHeader => Strings.Text("HttpSendThisHeader");

        /// <summary>Value</summary>
        public string HttpValue => Strings.Text("HttpValue");

        /// <summary>Body — JSON is detected from the first character</summary>
        public string HttpBodyJSONIsDetectedFromThe => Strings.Text("HttpBodyJSONIsDetectedFromThe");

        /// <summary>Log</summary>
        public string LogLog => Strings.Text("LogLog");

        /// <summary>Everything the app itself has done this session - not the output of wh</summary>
        public string LogEverythingTheAppItselfHasDone => Strings.Text("LogEverythingTheAppItselfHasDone");

        /// <summary>Filter</summary>
        public string LogFilter => Strings.Text("LogFilter");

        /// <summary>Problems only</summary>
        public string LogProblemsOnly => Strings.Text("LogProblemsOnly");

        /// <summary>Copy all</summary>
        public string LogCopyAll => Strings.Text("LogCopyAll");

        /// <summary>Nothing to report.</summary>
        public string LogNothingToReport => Strings.Text("LogNothingToReport");

        /// <summary>dismiss</summary>
        public string MainWindowDismiss => Strings.Text("MainWindowDismiss");

        /// <summary>Memory and processor</summary>
        public string MemoryMemoryAndProcessor => Strings.Text("MemoryMemoryAndProcessor");

        /// <summary>memory</summary>
        public string MemoryMemory => Strings.Text("MemoryMemory");

        /// <summary>processor</summary>
        public string MemoryProcessor => Strings.Text("MemoryProcessor");

        /// <summary>across all cores</summary>
        public string MemoryAcrossAllCores => Strings.Text("MemoryAcrossAllCores");

        /// <summary>sampled every two seconds</summary>
        public string MemorySampledEveryTwoSeconds => Strings.Text("MemorySampledEveryTwoSeconds");

        /// <summary>disk</summary>
        public string MemoryDisk => Strings.Text("MemoryDisk");

        /// <summary>GPU</summary>
        public string MemoryGpu => Strings.Text("MemoryGpu");

        /// <summary>active time, all disks</summary>
        public string MemoryDiskActive => Strings.Text("MemoryDiskActive");

        /// <summary>3D engine, all processes</summary>
        public string MemoryGpu3d => Strings.Text("MemoryGpu3d");

        /// <summary>{0} · {1} total</summary>
        public string MemDiskTotal => Strings.Text("MemDiskTotal");

        /// <summary>{0} used · {1} free</summary>
        public string MemDiskSpace => Strings.Text("MemDiskSpace");

        /// <summary>not available on this machine</summary>
        public string MemNotAvailable => Strings.Text("MemNotAvailable");

        /// <summary>GPU {0} · Disk {1}</summary>
        public string MemWidgetGpuDisk => Strings.Text("MemWidgetGpuDisk");

        /// <summary>Largest processes</summary>
        public string MemoryLargestProcesses => Strings.Text("MemoryLargestProcesses");

        /// <summary>Process</summary>
        public string MemoryProcess => Strings.Text("MemoryProcess");

        /// <summary>Share of the heaviest</summary>
        public string MemoryShareOfTheHeaviest => Strings.Text("MemoryShareOfTheHeaviest");

        /// <summary>Memory</summary>
        public string MemoryMemory2 => Strings.Text("MemoryMemory2");

        /// <summary>Cleanup steps</summary>
        public string MemoryCleanupSteps => Strings.Text("MemoryCleanupSteps");

        /// <summary>Some ticked steps need an administrator. Windows will ask once when yo</summary>
        public string MemorySomeTickedStepsNeedAnAdministrator => Strings.Text("MemorySomeTickedStepsNeedAnAdministrator");

        /// <summary>administrator</summary>
        public string MemoryAdministrator => Strings.Text("MemoryAdministrator");

        /// <summary>DevDeck memory</summary>
        public string MemoryWidgetDevDeckMemory => Strings.Text("MemoryWidgetDevDeckMemory");

        /// <summary>Show DevDeck</summary>
        public string MemoryWidgetShowDevDeck => Strings.Text("MemoryWidgetShowDevDeck");

        /// <summary>Clean memory now</summary>
        public string MemoryWidgetCleanMemoryNow => Strings.Text("MemoryWidgetCleanMemoryNow");

        /// <summary>Hide widget</summary>
        public string MemoryWidgetHideWidget => Strings.Text("MemoryWidgetHideWidget");

        /// <summary>Quit DevDeck</summary>
        public string MemoryWidgetQuitDevDeck => Strings.Text("MemoryWidgetQuitDevDeck");

        /// <summary>click to clean</summary>
        public string MemoryWidgetClickToClean => Strings.Text("MemoryWidgetClickToClean");

        /// <summary>freed</summary>
        public string MemoryWidgetFreed => Strings.Text("MemoryWidgetFreed");

        /// <summary>Rename</summary>
        public string NamePromptRename => Strings.Text("NamePromptRename");

        /// <summary>Run a command</summary>
        public string PaletteRunACommand => Strings.Text("PaletteRunACommand");

        /// <summary>Type a command, a panel or a tool</summary>
        public string PaletteTypeACommandAPanelOr => Strings.Text("PaletteTypeACommandAPanelOr");

        /// <summary>Nothing matches that.</summary>
        public string PaletteNothingMatchesThat => Strings.Text("PaletteNothingMatchesThat");

        /// <summary>Enter runs it · arrows move · Esc closes</summary>
        public string PaletteEnterRunsItArrowsMoveEsc => Strings.Text("PaletteEnterRunsItArrowsMoveEsc");

        /// <summary>Values for this run</summary>
        public string ParameterPromptValuesForThisRun => Strings.Text("ParameterPromptValuesForThisRun");

        /// <summary>This command asks for values before it runs.</summary>
        public string ParameterPromptThisCommandAsksForValuesBefore => Strings.Text("ParameterPromptThisCommandAsksForValuesBefore");

        /// <summary>Listening</summary>
        public string RunTilePorts => Strings.Text("RunTilePorts");

        /// <summary>Containers</summary>
        public string RunTileContainers => Strings.Text("RunTileContainers");

        /// <summary>Deck</summary>
        public string RunTileDeck => Strings.Text("RunTileDeck");

        /// <summary>Health</summary>
        public string RunTileHealth => Strings.Text("RunTileHealth");

        /// <summary>{0} of {1} up</summary>
        public string RunContainersUp => Strings.Text("RunContainersUp");

        /// <summary>{0} running</summary>
        public string RunDeckRunning => Strings.Text("RunDeckRunning");

        /// <summary>{0} of {1} up</summary>
        public string RunHealthUp => Strings.Text("RunHealthUp");

        /// <summary>-</summary>
        public string RunUnknown => Strings.Text("RunUnknown");

        /// <summary>Filter ports and containers</summary>
        public string RunningFilter => Strings.Text("RunningFilter");

        /// <summary>Auto-refresh</summary>
        public string RunningAutoRefresh => Strings.Text("RunningAutoRefresh");

        /// <summary>Re-reads ports, containers and health checks every twelve seconds whil</summary>
        public string RunningAutoRefreshTip => Strings.Text("RunningAutoRefreshTip");

        /// <summary>PID {0}</summary>
        public string RunningPid => Strings.Text("RunningPid");

        /// <summary>Copy URL</summary>
        public string RunningCopyUrl => Strings.Text("RunningCopyUrl");

        /// <summary>Health checks</summary>
        public string RunningHealth => Strings.Text("RunningHealth");

        /// <summary>URLs asked on every refresh - status code and response time.</summary>
        public string RunningHealthBlurb => Strings.Text("RunningHealthBlurb");

        /// <summary>http://localhost:5000/health</summary>
        public string RunningHealthPlaceholder => Strings.Text("RunningHealthPlaceholder");

        /// <summary>No checks yet. Add a URL one of your services answers on.</summary>
        public string RunningHealthNone => Strings.Text("RunningHealthNone");

        /// <summary>Check now</summary>
        public string RunningCheckNow => Strings.Text("RunningCheckNow");

        /// <summary>not checked yet</summary>
        public string RunHealthWaiting => Strings.Text("RunHealthWaiting");

        /// <summary>{0} · {1} ms</summary>
        public string RunHealthMs => Strings.Text("RunHealthMs");

        /// <summary>no answer - {0}</summary>
        public string RunHealthNoAnswer => Strings.Text("RunHealthNoAnswer");

        /// <summary>Not a URL: {0}</summary>
        public string RunHealthBadUrl => Strings.Text("RunHealthBadUrl");

        /// <summary>Is a port free?</summary>
        public string RunningPortCheck => Strings.Text("RunningPortCheck");

        /// <summary>Port, e.g. 5173</summary>
        public string RunningPortPlaceholder => Strings.Text("RunningPortPlaceholder");

        /// <summary>Check</summary>
        public string RunningCheck => Strings.Text("RunningCheck");

        /// <summary>{0} is free.</summary>
        public string RunPortFree => Strings.Text("RunPortFree");

        /// <summary>{0} is taken by {1} (PID {2}).</summary>
        public string RunPortTaken => Strings.Text("RunPortTaken");

        /// <summary>{0} is in use.</summary>
        public string RunPortTakenUnknown => Strings.Text("RunPortTakenUnknown");

        /// <summary>Type a port between 1 and 65535.</summary>
        public string RunPortInvalid => Strings.Text("RunPortInvalid");

        /// <summary>Running from the deck</summary>
        public string RunningDeckActivity => Strings.Text("RunningDeckActivity");

        /// <summary>Nothing from the deck is running.</summary>
        public string RunningDeckIdle => Strings.Text("RunningDeckIdle");

        /// <summary>Heaviest processes</summary>
        public string RunningTopProcesses => Strings.Text("RunningTopProcesses");

        /// <summary>Add</summary>
        public string Add => Strings.Text("Add");

        /// <summary>Environment variables</summary>
        public string SettingsEnvVars => Strings.Text("SettingsEnvVars");

        /// <summary>Add variable</summary>
        public string SettingsAddVariable => Strings.Text("SettingsAddVariable");

        /// <summary>Set on every command the deck runs - read them as $env:name in PowerSh</summary>
        public string SettingsEnvVarsNote => Strings.Text("SettingsEnvVarsNote");

        /// <summary>Parameters</summary>
        public string CommandsParameters => Strings.Text("CommandsParameters");

        /// <summary>Add parameter</summary>
        public string CommandsAddParameter => Strings.Text("CommandsAddParameter");

        /// <summary>name</summary>
        public string CommandsParameterName => Strings.Text("CommandsParameterName");

        /// <summary>value</summary>
        public string CommandsParameterValue => Strings.Text("CommandsParameterValue");

        /// <summary>Pass this one. Off keeps it in the list without sending it.</summary>
        public string CommandsParameterOnTip => Strings.Text("CommandsParameterOnTip");

        /// <summary>Passed on every run. PowerShell gets -name "value" (use param() in the</summary>
        public string CommandsParametersHelp => Strings.Text("CommandsParametersHelp");

        /// <summary>End</summary>
        public string RunningKill => Strings.Text("RunningKill");

        /// <summary>End this process and everything it started. Unsaved work in it is lost</summary>
        public string RunningKillTip => Strings.Text("RunningKillTip");

        /// <summary>Ended {0} (PID {1}).</summary>
        public string RunKilled => Strings.Text("RunKilled");

        /// <summary>Could not end {0}: {1}</summary>
        public string RunKillFailed => Strings.Text("RunKillFailed");

        /// <summary>Running</summary>
        public string RunningRunning => Strings.Text("RunningRunning");

        /// <summary>Only likely ports</summary>
        public string RunningOnlyLikelyPorts => Strings.Text("RunningOnlyLikelyPorts");

        /// <summary>Hides system services and ephemeral ports.</summary>
        public string RunningHidesSystemServicesAndEphemeralPorts => Strings.Text("RunningHidesSystemServicesAndEphemeralPorts");

        /// <summary>Listening</summary>
        public string RunningListening => Strings.Text("RunningListening");

        /// <summary>Open</summary>
        public string RunningOpen => Strings.Text("RunningOpen");

        /// <summary>Stop what has it</summary>
        public string RunningStopWhatHasIt => Strings.Text("RunningStopWhatHasIt");

        /// <summary>Containers</summary>
        public string RunningContainers => Strings.Text("RunningContainers");

        /// <summary>Start</summary>
        public string RunningStart => Strings.Text("RunningStart");

        /// <summary>Restart</summary>
        public string RunningRestart => Strings.Text("RunningRestart");

        /// <summary>Logs</summary>
        public string RunningLogs => Strings.Text("RunningLogs");

        /// <summary>Open in a browser</summary>
        public string RunningOpenInABrowser => Strings.Text("RunningOpenInABrowser");

        /// <summary>Follow its log in the deck</summary>
        public string RunningFollowItsLogInTheDeck => Strings.Text("RunningFollowItsLogInTheDeck");

        /// <summary>Nothing below until an engine is installed.</summary>
        public string RunningNothingBelowUntilAnEngineIs => Strings.Text("RunningNothingBelowUntilAnEngineIs");

        /// <summary>Close</summary>
        public string RunningClose => Strings.Text("RunningClose");

        /// <summary>Settings</summary>
        public string SettingsSettings => Strings.Text("SettingsSettings");

        /// <summary>Appearance</summary>
        public string SettingsAppearance => Strings.Text("SettingsAppearance");

        /// <summary>Theme</summary>
        public string SettingsTheme => Strings.Text("SettingsTheme");

        /// <summary>Follow the system tracks the desktop. A theme changes the whole window</summary>
        public string SettingsFollowTheSystemTracksTheDesktop => Strings.Text("SettingsFollowTheSystemTracksTheDesktop");

        /// <summary>Language</summary>
        public string SettingsLanguage => Strings.Text("SettingsLanguage");

        /// <summary>The language changes the moment you choose it, without a restart. Each</summary>
        public string SettingsTheLanguageChangesTheMomentYou => Strings.Text("SettingsTheLanguageChangesTheMomentYou");

        /// <summary>AI</summary>
        public string SettingsAi => Strings.Text("SettingsAi");

        /// <summary>The assistant asks whatever is chosen here. A key is kept per provider</summary>
        public string SettingsAiNote => Strings.Text("SettingsAiNote");

        /// <summary>Behaviour</summary>
        public string SettingsBehaviour => Strings.Text("SettingsBehaviour");

        /// <summary>Keep this machine awake while DevDeck is open</summary>
        public string SettingsKeepThisMachineAwakeWhileDevDeck => Strings.Text("SettingsKeepThisMachineAwakeWhileDevDeck");

        /// <summary>Also stop it locking itself, and stay available in chat apps</summary>
        public string SettingsAlsoStopItLockingItselfAnd => Strings.Text("SettingsAlsoStopItLockingItselfAnd");

        /// <summary>Ask before power actions</summary>
        public string SettingsAskBeforePowerActions => Strings.Text("SettingsAskBeforePowerActions");

        /// <summary>Follow output as it arrives</summary>
        public string SettingsFollowOutputAsItArrives => Strings.Text("SettingsFollowOutputAsItArrives");

        /// <summary>Stops the computer going to sleep while DevDeck is open, so a long bui</summary>
        public string SettingsKeepAwakeTip => Strings.Text("SettingsKeepAwakeTip");

        /// <summary>Also keeps the screen from locking and your status in Teams or Slack f</summary>
        public string SettingsStayAvailableTip => Strings.Text("SettingsStayAvailableTip");

        /// <summary>Asks for confirmation before DevDeck locks, signs out, restarts or shu</summary>
        public string SettingsConfirmPowerTip => Strings.Text("SettingsConfirmPowerTip");

        /// <summary>Scrolls a command's output to the newest line as it is written. Turn i</summary>
        public string SettingsFollowOutputTip => Strings.Text("SettingsFollowOutputTip");

        /// <summary>Opens DevDeck each time you sign in to this computer, minimised to the</summary>
        public string SettingsAutoStartTip => Strings.Text("SettingsAutoStartTip");

        /// <summary>Not sleeping and not locking are two different settings on most machin</summary>
        public string SettingsNotSleepingAndNotLockingAre => Strings.Text("SettingsNotSleepingAndNotLockingAre");

        /// <summary>When a long command finishes</summary>
        public string SettingsWhenALongCommandFinishes => Strings.Text("SettingsWhenALongCommandFinishes");

        /// <summary>Tell me when it is done</summary>
        public string SettingsTellMeWhenItIsDone => Strings.Text("SettingsTellMeWhenItIsDone");

        /// <summary>Only if it ran for at least</summary>
        public string SettingsOnlyIfItRanForAt => Strings.Text("SettingsOnlyIfItRanForAt");

        /// <summary>seconds</summary>
        public string SettingsSeconds => Strings.Text("SettingsSeconds");

        /// <summary>A desktop notification where the system has one - macOS and Linux. On </summary>
        public string SettingsADesktopNotificationWhereTheSystem => Strings.Text("SettingsADesktopNotificationWhereTheSystem");

        /// <summary>Secrets</summary>
        public string SettingsSecrets => Strings.Text("SettingsSecrets");

        /// <summary>A secret is stored wrapped, in its own file, and never written into a </summary>
        public string SettingsASecretIsStoredWrappedIn => Strings.Text("SettingsASecretIsStoredWrappedIn");

        /// <summary>value</summary>
        public string SettingsValue => Strings.Text("SettingsValue");

        /// <summary>Store</summary>
        public string SettingsStore => Strings.Text("SettingsStore");

        /// <summary>From a terminal, and from a link</summary>
        public string SettingsFromATerminalAndFromA => Strings.Text("SettingsFromATerminalAndFromA");

        /// <summary>A saved command can be started from outside the window: devdeck run "B</summary>
        public string SettingsASavedCommandCanBeStarted => Strings.Text("SettingsASavedCommandCanBeStarted");

        /// <summary>Write the terminal launcher</summary>
        public string SettingsWriteTheTerminalLauncher => Strings.Text("SettingsWriteTheTerminalLauncher");

        /// <summary>Remove it</summary>
        public string SettingsRemoveIt => Strings.Text("SettingsRemoveIt");

        /// <summary>Register devdeck://</summary>
        public string SettingsRegisterDevdeck => Strings.Text("SettingsRegisterDevdeck");

        /// <summary>A key that works from anywhere</summary>
        public string SettingsAKeyThatWorksFromAnywhere => Strings.Text("SettingsAKeyThatWorksFromAnywhere");

        /// <summary>One combination that brings the deck up whatever you are looking at - </summary>
        public string SettingsOneCombinationThatBringsTheDeck => Strings.Text("SettingsOneCombinationThatBringsTheDeck");

        /// <summary>Claim it</summary>
        public string SettingsClaimIt => Strings.Text("SettingsClaimIt");

        /// <summary>Give it back</summary>
        public string SettingsGiveItBack => Strings.Text("SettingsGiveItBack");

        /// <summary>This build</summary>
        public string SettingsThisBuild => Strings.Text("SettingsThisBuild");

        /// <summary>Settings file</summary>
        public string SettingsSettingsFile => Strings.Text("SettingsSettingsFile");

        /// <summary>Updates</summary>
        public string SettingsUpdates => Strings.Text("SettingsUpdates");

        /// <summary>Asks the release page whether there is a newer build. It will not inst</summary>
        public string SettingsAsksTheReleasePageWhetherThere => Strings.Text("SettingsAsksTheReleasePageWhetherThere");

        /// <summary>Check now</summary>
        public string SettingsCheckNow => Strings.Text("SettingsCheckNow");

        /// <summary>Open the release page</summary>
        public string SettingsOpenTheReleasePage => Strings.Text("SettingsOpenTheReleasePage");

        /// <summary>What's new</summary>
        public string SetWhatsNew => Strings.Text("SetWhatsNew");

        /// <summary>Start DevDeck when I sign in</summary>
        public string SetAutoStart => Strings.Text("SetAutoStart");

        /// <summary>For your user only. It opens minimised, so it is ready on the taskbar </summary>
        public string SetAutoStartNote => Strings.Text("SetAutoStartNote");

        /// <summary>On macOS a login item belongs to an app bundle. Add DevDeck under Syst</summary>
        public string SetAutoStartCannot => Strings.Text("SetAutoStartCannot");

        /// <summary>DevDeck will start the next time you sign in.</summary>
        public string SetAutoStartOn => Strings.Text("SetAutoStartOn");

        /// <summary>DevDeck will no longer start when you sign in.</summary>
        public string SetAutoStartOff => Strings.Text("SetAutoStartOff");

        /// <summary>Could not change the startup entry: {0}</summary>
        public string SetAutoStartFailed => Strings.Text("SetAutoStartFailed");

        /// <summary>Changelog</summary>
        public string NavChangelog => Strings.Text("NavChangelog");

        /// <summary>What each version brought, and what is waiting in a newer one.</summary>
        public string NavChangelogBlurb => Strings.Text("NavChangelogBlurb");

        /// <summary>This version</summary>
        public string ChangelogThisVersion => Strings.Text("ChangelogThisVersion");

        /// <summary>Not installed yet</summary>
        public string ChangelogNotInstalled => Strings.Text("ChangelogNotInstalled");

        /// <summary>Released {0}</summary>
        public string ChangelogReleased => Strings.Text("ChangelogReleased");

        /// <summary>Your commands, chains and tools in one deck.</summary>
        public string SplashTagline => Strings.Text("SplashTagline");

        /// <summary>Version {0}</summary>
        public string SplashVersion => Strings.Text("SplashVersion");

        /// <summary>Getting your deck ready…</summary>
        public string SplashLoading => Strings.Text("SplashLoading");

        /// <summary>Check for newer versions</summary>
        public string ChangelogCheck => Strings.Text("ChangelogCheck");

        /// <summary>Asking the release page…</summary>
        public string ChangelogAsking => Strings.Text("ChangelogAsking");

        /// <summary>This build carries no changelog.</summary>
        public string ChangelogEmpty => Strings.Text("ChangelogEmpty");

        /// <summary>Are you sure?</summary>
        public string ConfirmTitle => Strings.Text("ConfirmTitle");

        /// <summary>Start over</summary>
        public string SettingsStartOver => Strings.Text("SettingsStartOver");

        /// <summary>Throws away every setting, saved command, chain, watch and environment</summary>
        public string SettingsResetExplains => Strings.Text("SettingsResetExplains");

        /// <summary>Stored secrets are left alone. They live in the system keychain rather</summary>
        public string SettingsResetKeepsSecrets => Strings.Text("SettingsResetKeepsSecrets");

        /// <summary>Reset everything</summary>
        public string SettingsResetNow => Strings.Text("SettingsResetNow");

        /// <summary>Reset every setting to the defaults?</summary>
        public string SettingsResetAsk => Strings.Text("SettingsResetAsk");

        /// <summary>Your commands, chains, watches, environments and preferences are repla</summary>
        public string SettingsResetDetail => Strings.Text("SettingsResetDetail");

        /// <summary>Drag to resize</summary>
        public string AutomationDragToResize => Strings.Text("AutomationDragToResize");

        /// <summary>Settings reset. Restarting DevDeck...</summary>
        public string SettingsResetRestarting => Strings.Text("SettingsResetRestarting");

        /// <summary>Settings reset. Close and open DevDeck to see them - what is on screen</summary>
        public string SettingsResetDone => Strings.Text("SettingsResetDone");

        /// <summary>Could not reset: {0}</summary>
        public string SettingsResetFailed => Strings.Text("SettingsResetFailed");

        /// <summary>Everything here runs on this machine. Nothing you paste is sent anywhe</summary>
        public string ToolboxEverythingHereRunsOnThisMachine => Strings.Text("ToolboxEverythingHereRunsOnThisMachine");

        /// <summary>Again</summary>
        public string ToolboxAgain => Strings.Text("ToolboxAgain");

        /// <summary>Paste</summary>
        public string ToolboxPaste => Strings.Text("ToolboxPaste");

        /// <summary>The pattern</summary>
        public string ToolboxThePattern => Strings.Text("ToolboxThePattern");

        /// <summary>You</summary>
        public string AiWhoYou => Strings.Text("AiWhoYou");

        /// <summary>Assistant</summary>
        public string AiWhoAssistant => Strings.Text("AiWhoAssistant");

        /// <summary>Output</summary>
        public string AiWhoOutput => Strings.Text("AiWhoOutput");

        /// <summary>Problem</summary>
        public string AiWhoProblem => Strings.Text("AiWhoProblem");

        /// <summary>Ready.</summary>
        public string AiReady => Strings.Text("AiReady");

        /// <summary>Looking for the model…</summary>
        public string AiLookingForModel => Strings.Text("AiLookingForModel");

        /// <summary>Checking {0}…</summary>
        public string AiChecking => Strings.Text("AiChecking");

        /// <summary>{0} on {1}</summary>
        public string AiModelOn => Strings.Text("AiModelOn");

        /// <summary>Using {0} from {1}</summary>
        public string AiUsing => Strings.Text("AiUsing");

        /// <summary>Set up {0}</summary>
        public string AiSetUpLabel => Strings.Text("AiSetUpLabel");

        /// <summary>Nothing answered at {0} — {1}</summary>
        public string AiNothingAnswered => Strings.Text("AiNothingAnswered");

        /// <summary>No endpoint.</summary>
        public string AiNoEndpoint => Strings.Text("AiNoEndpoint");

        /// <summary>Setting up {0}. This can take a few minutes.</summary>
        public string AiSettingUp => Strings.Text("AiSettingUp");

        /// <summary>{0} is ready.</summary>
        public string AiLocalReady => Strings.Text("AiLocalReady");

        /// <summary>Now asking {0}.</summary>
        public string AiNowAsking => Strings.Text("AiNowAsking");

        /// <summary>Asking the endpoint what it has…</summary>
        public string AiAskingEndpoint => Strings.Text("AiAskingEndpoint");

        /// <summary>{0} model available.</summary>
        public string AiModelsAvailableOne => Strings.Text("AiModelsAvailableOne");

        /// <summary>{0} models available.</summary>
        public string AiModelsAvailable => Strings.Text("AiModelsAvailable");

        /// <summary>The endpoint answered, but offered no models.</summary>
        public string AiNoModels => Strings.Text("AiNoModels");

        /// <summary>Could not reach {0}: {1}</summary>
        public string AiCouldNotReach => Strings.Text("AiCouldNotReach");

        /// <summary>The question is ready - the model is not reachable yet.</summary>
        public string AiQuestionReady => Strings.Text("AiQuestionReady");

        /// <summary>Asking {0}…</summary>
        public string AiAsking => Strings.Text("AiAsking");

        /// <summary>Stopped.</summary>
        public string AiStopped => Strings.Text("AiStopped");

        /// <summary>Failed.</summary>
        public string AiFailed => Strings.Text("AiFailed");

        /// <summary>_Could not get an answer: {0}_</summary>
        public string AiNoAnswer => Strings.Text("AiNoAnswer");

        /// <summary>Saved, and started a new one.</summary>
        public string AiSavedNewOne => Strings.Text("AiSavedNewOne");

        /// <summary>Cleared.</summary>
        public string AiCleared => Strings.Text("AiCleared");

        /// <summary>{0} is already attached.</summary>
        public string AiAlreadyAttached => Strings.Text("AiAlreadyAttached");

        /// <summary>{0} - sent with your next question.</summary>
        public string AiAttachedNote => Strings.Text("AiAttachedNote");

        /// <summary>{0} attached.</summary>
        public string AiCountAttached => Strings.Text("AiCountAttached");

        /// <summary>Sent with {0}.</summary>
        public string AiSentWithOne => Strings.Text("AiSentWithOne");

        /// <summary>Sent with {0} files: {1}.</summary>
        public string AiSentWithMany => Strings.Text("AiSentWithMany");

        /// <summary>Opened "{0}".</summary>
        public string AiOpened => Strings.Text("AiOpened");

        /// <summary>Deleted "{0}".</summary>
        public string AiDeleted => Strings.Text("AiDeleted");

        /// <summary>Loaded "{0}" - fill in the placeholders and send.</summary>
        public string AiLoadedPrompt => Strings.Text("AiLoadedPrompt");

        /// <summary>There is nothing in the box to save.</summary>
        public string AiNothingToSave => Strings.Text("AiNothingToSave");

        /// <summary>Saved "{0}".</summary>
        public string AiSavedPrompt => Strings.Text("AiSavedPrompt");

        /// <summary>{0} · {1} USD this session</summary>
        public string AiSpendSession => Strings.Text("AiSpendSession");

        /// <summary>{0}% of a {1}k window</summary>
        public string AiContextWindow => Strings.Text("AiContextWindow");

        /// <summary> - clear the chat soon, or it will start forgetting</summary>
        public string AiContextTight => Strings.Text("AiContextTight");

        /// <summary>That answer did not contain a command to save.</summary>
        public string AiNoCommandInAnswer => Strings.Text("AiNoCommandInAnswer");

        /// <summary>There is no chain in this answer to create.</summary>
        public string AiNoChainInAnswer => Strings.Text("AiNoChainInAnswer");

        /// <summary>Created the chain "{0}" with {1} steps, and added {2} new commands to </summary>
        public string AiChainCreated => Strings.Text("AiChainCreated");

        /// <summary>These steps name commands the deck does not have: {0}. The chain marks</summary>
        public string AiChainMissing => Strings.Text("AiChainMissing");

        /// <summary>Chain "{0}"</summary>
        public string AiChainHeader => Strings.Text("AiChainHeader");

        /// <summary>Ask AI why</summary>
        public string AiAskWhy => Strings.Text("AiAskWhy");

        /// <summary>Sends what ran and what it printed to the model, and asks why it behav</summary>
        public string AiAskWhyTip => Strings.Text("AiAskWhyTip");

        /// <summary>Takes this output out of the conversation.</summary>
        public string AiRemoveOutputTip => Strings.Text("AiRemoveOutputTip");

        /// <summary>"{0}" is already in the deck with a different script, so this one was </summary>
        public string AiChainRenamed => Strings.Text("AiChainRenamed");

        /// <summary>This was written by a model. It runs in {0} with your account - read i</summary>
        public string AiConfirmBlurb => Strings.Text("AiConfirmBlurb");

        /// <summary>Not run.</summary>
        public string AiNotRun => Strings.Text("AiNotRun");

        /// <summary>Choose a workspace folder first - that is where it would run.</summary>
        public string AiNeedWorkspace => Strings.Text("AiNeedWorkspace");

        /// <summary>— exit code {0} after {1}</summary>
        public string AiExitCode => Strings.Text("AiExitCode");

        /// <summary>That ran cleanly.</summary>
        public string AiRanCleanly => Strings.Text("AiRanCleanly");

        /// <summary>That exited {0}.</summary>
        public string AiExited => Strings.Text("AiExited");

        /// <summary>— stopped after {0}</summary>
        public string AiStoppedAfter => Strings.Text("AiStoppedAfter");

        /// <summary>— could not run it: {0}</summary>
        public string AiCouldNotRunLine => Strings.Text("AiCouldNotRunLine");

        /// <summary>Could not run it: {0}</summary>
        public string AiCouldNotRun => Strings.Text("AiCouldNotRun");

        /// <summary>Copied.</summary>
        public string ToolCopied => Strings.Text("ToolCopied");

        /// <summary>Data</summary>
        public string ToolGroupData => Strings.Text("ToolGroupData");

        /// <summary>Encoding</summary>
        public string ToolGroupEncoding => Strings.Text("ToolGroupEncoding");

        /// <summary>Inspect</summary>
        public string ToolGroupInspect => Strings.Text("ToolGroupInspect");

        /// <summary>Generate</summary>
        public string ToolGroupGenerate => Strings.Text("ToolGroupGenerate");

        /// <summary>Text</summary>
        public string ToolGroupText => Strings.Text("ToolGroupText");

        /// <summary>JSON: format</summary>
        public string ToolJsonFormat => Strings.Text("ToolJsonFormat");

        /// <summary>Comments and trailing commas are accepted.</summary>
        public string ToolJsonFormatHint => Strings.Text("ToolJsonFormatHint");

        /// <summary>JSON: minify</summary>
        public string ToolJsonMinify => Strings.Text("ToolJsonMinify");

        /// <summary>XML: format</summary>
        public string ToolXmlFormat => Strings.Text("ToolXmlFormat");

        /// <summary>Base64: encode</summary>
        public string ToolBase64Encode => Strings.Text("ToolBase64Encode");

        /// <summary>Base64: decode</summary>
        public string ToolBase64Decode => Strings.Text("ToolBase64Decode");

        /// <summary>URL-safe input and missing padding are both fine.</summary>
        public string ToolBase64DecodeHint => Strings.Text("ToolBase64DecodeHint");

        /// <summary>URL: encode</summary>
        public string ToolUrlEncode => Strings.Text("ToolUrlEncode");

        /// <summary>URL: decode</summary>
        public string ToolUrlDecode => Strings.Text("ToolUrlDecode");

        /// <summary>HTML: escape</summary>
        public string ToolHtmlEscape => Strings.Text("ToolHtmlEscape");

        /// <summary>HTML: unescape</summary>
        public string ToolHtmlUnescape => Strings.Text("ToolHtmlUnescape");

        /// <summary>JWT: decode</summary>
        public string ToolJwtDecode => Strings.Text("ToolJwtDecode");

        /// <summary>The signature is not checked. Decoding a token does not verify it.</summary>
        public string ToolJwtDecodeHint => Strings.Text("ToolJwtDecodeHint");

        /// <summary>Hash</summary>
        public string ToolHash => Strings.Text("ToolHash");

        /// <summary>Timestamp</summary>
        public string ToolTimestamp => Strings.Text("ToolTimestamp");

        /// <summary>Unix seconds or milliseconds, or a date to turn into one.</summary>
        public string ToolTimestampHint => Strings.Text("ToolTimestampHint");

        /// <summary>UUID</summary>
        public string ToolUuid => Strings.Text("ToolUuid");

        /// <summary>A new one each time you press Again.</summary>
        public string ToolUuidHint => Strings.Text("ToolUuidHint");

        /// <summary>Case</summary>
        public string ToolCase => Strings.Text("ToolCase");

        /// <summary>Sort lines</summary>
        public string ToolSortLines => Strings.Text("ToolSortLines");

        /// <summary>Sorted and de-duplicated.</summary>
        public string ToolSortLinesHint => Strings.Text("ToolSortLinesHint");

        /// <summary>Regex</summary>
        public string ToolRegex => Strings.Text("ToolRegex");

        /// <summary>The pattern goes above, the text to search below.</summary>
        public string ToolRegexHint => Strings.Text("ToolRegexHint");

        /// <summary>Paste here</summary>
        public string ToolboxPasteHere => Strings.Text("ToolboxPasteHere");

        /// <summary>Use as input</summary>
        public string ToolboxUseAsInput => Strings.Text("ToolboxUseAsInput");
    }
}
