/// SPDX-License-Identifier: BSD-3-Clause
/// SPDX-FileCopyrightText: Silicon Laboratories Inc. https://www.silabs.com
﻿using ZWaveController.Interfaces;
using ZWaveController.Interfaces.Services;

namespace ZWaveController.Commands
{
    public class SourcesCommandBase : CommandBase
    {
        public IApplicationModel ApplicationModel { get; private set; }
        public ISourcesInfoService SourcesInfoService { get; set; }

        public SourcesCommandBase(IApplicationModel applicationModel)
        {
            ApplicationModel = applicationModel;
            UseBackgroundThread = true;
        }

        protected override sealed void ExecuteAction(object param)
        {
            if (ShowBusyOverlay)
            {
                ApplicationModel.Invoke(() => ApplicationModel.SetBusy(true));
                ApplicationModel.Invoke(() => ApplicationModel.SetBusyMessage(BusyMessage));
            }
            ApplicationModel.ActiveCommand = this;
            try
            {
                ExecuteInner(param);
            }
            finally
            {
                ApplicationModel.ActiveCommand = null;
                if (ShowBusyOverlay)
                    ApplicationModel.Invoke(() => ApplicationModel.SetBusy(false));
            }
        }

        /// <summary>When true, a blocking overlay is shown during the command. Override to false to avoid blocking the UI.</summary>
        protected virtual bool ShowBusyOverlay => true;

        /// <summary>Message shown in the overlay when ShowBusyOverlay is true. Override to show a specific message.</summary>
        protected virtual string BusyMessage => "Waiting for Completed Action";

        protected virtual void ExecuteInner(object param)
        {
        }

        protected override bool CanExecuteAction(object param)
        {
            return !ApplicationModel.IsBusy;
        }
    }
}
