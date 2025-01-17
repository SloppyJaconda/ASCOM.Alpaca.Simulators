﻿// tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM FilterWheel driver for FilterWheelSimulator
//
// Description:	A port of the VB6 ASCOM Filterwheel simulator to VB.Net.
// Converted and built in Visual Studio 2008.
// The port leaves some messy code - it could really do with
// a ground up re-write!
//
// Implements:	ASCOM FilterWheel interface version: 5.1.0
// Author:		Mark Crossley <mark@markcrossley.co.uk>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 06-Jun-2009	mpc	1.0.0	Initial edit, from FilterWheel template
// --------------------------------------------------------------------------------
//
// Your driver's ID is ASCOM.FilterWheelSim.FilterWheel  ???
//
// The Guid attribute sets the CLSID for ASCOM.FilterWheelSim.FilterWheel
// The ClassInterface/None addribute prevents an empty interface called
// _FilterWheel from being created and used as the [default] interface
//

using ASCOM.Common;
using ASCOM.Common.DeviceInterfaces;
using ASCOM.Common.Interfaces;
using System;
using System.Collections.Generic;

namespace ASCOM.Simulators
{
    public class FilterWheel : IFilterWheelV2, IAlpacaDevice, ISimulation // Early-bind interface implemented by this driver
    {
        // ==========

        private const string MSG_NOT_CONNECTED = "The filter wheel is not connected";

        private const string UNIQUE_ID_PROFILE_NAME = "UniqueID";

        //
        // Constructor - Must be public for COM registration!
        //
        public FilterWheel(int deviceNumber, ILogger logger, IProfile profile)
        {
            logger.LogInformation($"FilterWheel {deviceNumber} - Starting initialization");
            FilterWheelHardware.Logger = logger;
            DeviceNumber = deviceNumber;

            FilterWheelHardware.g_Profile = profile;

            FilterWheelHardware.Initialize();

            //This should be replaced by the next bit of code but is semi-unique as a default.
            UniqueID = Name + deviceNumber.ToString();
            //Create a Unique ID if it does not exist
            try
            {
                if (!profile.ContainsKey(UNIQUE_ID_PROFILE_NAME))
                {
                    var uniqueid = Guid.NewGuid().ToString();
                    profile.WriteValue(UNIQUE_ID_PROFILE_NAME, uniqueid);
                }
                UniqueID = profile.GetValue(UNIQUE_ID_PROFILE_NAME);
            }
            catch (Exception ex)
            {
                logger.LogError($"FilterWheel {deviceNumber} - {ex.Message}");
            }

            logger.LogInformation($"FilterWheel {deviceNumber} - UUID of {UniqueID}");
        }

        #region ISimulation

        public void ResetSettings()
        {
            FilterWheelHardware.g_Profile.Clear();
        }

        public string GetXMLProfile()
        {
            return FilterWheelHardware.g_Profile.GetProfile();
        }

        #endregion ISimulation

        public string DeviceName { get => Name; }
        public int DeviceNumber { get; private set; }
        public string UniqueID { get; private set; }

        public void Dispose()
        {
        }

        //
        // PUBLIC COM INTERFACE IFilterWheel IMPLEMENTATION
        //
        public bool Connected
        {
            get
            {
                return FilterWheelHardware.Connected;
            }
            set
            {
                FilterWheelHardware.Connected = value;
            }
        }

        public string Description
        {
            get
            {
                return "Simulator description";
            }
        }

        public string DriverInfo
        {
            get
            {
                return "ASCOM filter wheel driver simulator";
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}";
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return 2;
            }
        }

        public string Name
        {
            get
            {
                return "Alpaca Filter Wheel Sim";
            }
        }

        public string Action(string ActionName, string ActionParameters)
        {
            throw new ASCOM.MethodNotImplementedException("Action is not implemented in this driver");
        }

        public IList<string> SupportedActions
        {
            get
            {
                return new List<string>();
            }
        }

        public string CommandString(string Cmd, bool Raw = false)
        {
            if (Cmd == "CommandString")
                return "FWCommandString";
            else
                return "Bad command: " + Cmd;
        }

        public bool CommandBool(string Cmd, bool Raw = false)
        {
            if (Cmd == "CommandBool")
                return true;
            else
                return false;
        }

        public void CommandBlind(string Cmd, bool Raw = false)
        {
        }

        public short Position
        {
            get
            {
                return FilterWheelHardware.Position;
            }
            set
            {
                FilterWheelHardware.Position = value;
            }
        }

        public int[] FocusOffsets
        {
            get
            {
                return FilterWheelHardware.FocusOffsets;
            }
        }

        public string[] Names
        {
            get
            {
                return FilterWheelHardware.FilterNames;
            }
        }

        public void SetupDialog()
        {
        }
    }
}