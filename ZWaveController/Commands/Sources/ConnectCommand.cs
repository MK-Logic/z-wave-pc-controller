/// SPDX-License-Identifier: BSD-3-Clause
/// SPDX-FileCopyrightText: Silicon Laboratories Inc. https://www.silabs.com
﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Utils;
using ZWave.Enums;
using ZWaveController.Enums;
using ZWaveController.Interfaces;
using ZWaveController.Services;

namespace ZWaveController.Commands
{
    public class ConnectCommand : SourcesCommandBase
    {
        private List<IDataSource> _dataSources { get; set; }
        private LogBaseService _logger { get; set; }
        public ISenderHistoryService SenderHistoryService { get; set; }
        public IPredefinedPayloadsService PredefinedPayloadsService { get; set; }
        public ConnectCommand(IApplicationModel applicationModel, List<IDataSource> dataSources, LogBaseService logger) : base(applicationModel)
        {
            _dataSources = dataSources;
            _logger = logger;
            SenderHistoryService = new SenderHistoryService(ApplicationModel);
            PredefinedPayloadsService = new PredefinedPayloadsService(ApplicationModel);
        }

        /// <summary>Overrides default so the overlay shows what is running during Connect (device open, init, NLS sync, etc.).</summary>
        protected override string BusyMessage => "Connecting to Source";

        protected override void ExecuteInner(object param)
        {
            ApplicationModel.TraceCapture.TraceCaptureSettingsModel.WriteDefault();
            ApplicationModel.LastCommandExecutionResult = CommandExecutionResult.Failed;

            var selectedDataSource = param as IDataSource;
            if (selectedDataSource == null)
            {
                if (ApplicationModel.AppSettings.SourceOnStartup != null)
                {
                    selectedDataSource = ApplicationModel.AppSettings.SourceOnStartup;
                    ApplicationModel.AppSettings.SourceOnStartup = null;
                }
                else
                {
                    return;
                }
            }

            ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage("Connecting to " + selectedDataSource.SourceName + " ..."));

            if (ApplicationModel.DataSource != null &&
                ControllerSessionsContainer.ControllerSessions.ContainsKey(ApplicationModel.DataSource.SourceId))
            {
                ControllerSessionsContainer.Remove(ApplicationModel.DataSource.SourceId);
                ApplicationModel.DataSource = null;
            }

            IControllerSession controllerSession = ControllerSessionsContainer.
                ControllerSessionCreator.CreateControllerSession(selectedDataSource, ApplicationModel);
            controllerSession.Logger = _logger;
            controllerSession.Logger.Log($"Connecting to {selectedDataSource.SourceName} ...");
            controllerSession.SenderHistoryService = SenderHistoryService;
            controllerSession.PredefinedPayloadsService = PredefinedPayloadsService;
            if (ControllerSessionsContainer.Add(selectedDataSource.SourceId, controllerSession))
            {
                ApplicationModel.TraceCapture.Init(selectedDataSource.SourceId);
                // Delay before first connect so the OS can release the port from a previous run.
                // When debugging, the previous session may have been stopped abruptly so wait longer.
                if (ApplicationModel.DataSource == null)
                {
                    int delayMs = Debugger.IsAttached ? 2500 : 500;
                    if (Debugger.IsAttached)
                    {
                        controllerSession.Logger.Log("Debugger attached; waiting for port ...");
                    }
                    Thread.Sleep(delayMs);
                }
                var connected = controllerSession.Connect(selectedDataSource) == CommunicationStatuses.Done;
                if (!connected)
                {
                    controllerSession.Logger.Log("First connect attempt failed; retrying in 2 s ...");
                    ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage("Reconnecting to " + selectedDataSource.SourceName + " ..."));
                    Thread.Sleep(2000);
                    connected = controllerSession.Connect(selectedDataSource) == CommunicationStatuses.Done;
                }
                if (!connected)
                {
                    controllerSession.Logger.Log("Second attempt failed; retrying in 4 s ...");
                    ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage("Reconnecting to " + selectedDataSource.SourceName + " ..."));
                    Thread.Sleep(4000);
                    connected = controllerSession.Connect(selectedDataSource) == CommunicationStatuses.Done;
                }
                if (!connected && Debugger.IsAttached)
                {
                    controllerSession.Logger.Log("Third attempt (debugging); retrying in 8 s ...");
                    ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage("Reconnecting to " + selectedDataSource.SourceName + " ..."));
                    Thread.Sleep(8000);
                    connected = controllerSession.Connect(selectedDataSource) == CommunicationStatuses.Done;
                }
                if (!connected && Debugger.IsAttached)
                {
                    controllerSession.Logger.Log("Fourth attempt (debugging); retrying in 12 s ...");
                    ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage("Reconnecting to " + selectedDataSource.SourceName + " ..."));
                    Thread.Sleep(12000);
                    connected = controllerSession.Connect(selectedDataSource) == CommunicationStatuses.Done;
                }
                if (connected)
                {
                    ApplicationModel.LastCommandExecutionResult = CommandExecutionResult.OK;
                    controllerSession.Logger.LogOk($"Connected to {selectedDataSource.SourceName}");
                    if (ApplicationModel.AppSettings != null)
                    {
                        if (ApplicationModel.AppSettings.SaveLastUsedDeviceSecondary)
                        {
                            ApplicationModel.AppSettings.LastUsedDeviceAlt = selectedDataSource.SourceName;
                        }
                        else
                        {
                            ApplicationModel.AppSettings.LastUsedDevice = selectedDataSource.SourceName;
                        }
                        ApplicationModel.AppSettings.SaveSettings();
                    }
                    ApplicationModel.InitControllerSessionCommands();
                    ApplicationModel.NotifyControllerChanged(NotifyProperty.ToggleSource, new { SourceId = selectedDataSource.SourceId, IsActive = true });
                    controllerSession.SenderHistoryService.Load();
                    controllerSession.PredefinedPayloadsService.Initialize();
                }
                else
                {
                    ControllerSessionsContainer.Remove(selectedDataSource.SourceId);
                    controllerSession.Logger.LogFail($"Connect to {selectedDataSource.SourceName} Failed");
                }
            }
        }
    }
}
