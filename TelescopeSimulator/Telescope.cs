﻿//tabs=4
// --------------------------------------------------------------------------------
// TODO fill in this information for your driver, then remove this line!
//
// ASCOM Telescope driver for Telescope
//
// Description:	ASCOM Driver for Simulated Telescope
//
// Implements:	ASCOM Telescope interface version: 2.0
// Author:		(rbt) Robert Turner <robert@robertturnerastro.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 07-JUL-2009	rbt	1.0.0	Initial edit, from ASCOM Telescope Driver template
// 29 Dec 2010  cdr         Extensive refactoring and bug fixes
// --------------------------------------------------------------------------------
//
using ASCOM.Common;
using ASCOM.Common.DeviceInterfaces;
using ASCOM.Common.Interfaces;
using ASCOM.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ASCOM.Simulators
{
    //
    // Your driver's ID is ASCOM.Telescope.Telescope
    //
    // The Guid attribute sets the CLSID for ASCOM.Telescope.Telescope
    // The ClassInterface/None attribute prevents an empty interface called
    // _Telescope from being created and used as the [default] interface
    //

    public class Telescope : ITelescopeV3, IDisposable, IAlpacaDevice, ISimulation
    {
        //
        // Driver private data (rate collections)
        //
        private AxisRates[] m_AxisRates;

        private TrackingRates m_TrackingRates;
        private TrackingRatesSimple m_TrackingRatesSimple;
        private long objectId;

        private const string SlewToHA = "SlewToHA"; private const string SlewToHAUpper = "SLEWTOHA";
        private const string AssemblyVersionNumber = "AssemblyVersionNumber"; private const string AssemblyVersionNumberUpper = "ASSEMBLYVERSIONNUMBER";
        private const string TimeUntilPointingStateCanChange = "TIMEUNTILPOINTINGSTATECANCHANGE";
        private const string AvailableTimeInThisPointingState = "AVAILABLETIMEINTHISPOINTINGSTATE";

        private const string UNIQUE_ID_PROFILE_NAME = "UniqueID";

        private ILogger Logger;

        //
        // Constructor - Must be public for COM registration!
        //
        public Telescope(int deviceNumber, ILogger logger, IProfile profile)
        {
            try
            {
                TelescopeHardware.TL = logger;
                Logger = logger;
                TelescopeHardware.s_Profile = profile;

                DeviceNumber = deviceNumber;

                TelescopeHardware.Init();
                m_AxisRates = new AxisRates[3];
                m_AxisRates[0] = new AxisRates(TelescopeAxis.Primary);
                m_AxisRates[1] = new AxisRates(TelescopeAxis.Secondary);
                m_AxisRates[2] = new AxisRates(TelescopeAxis.Tertiary);
                m_TrackingRates = new TrackingRates();
                m_TrackingRatesSimple = new TrackingRatesSimple();
                // get a unique instance id
                objectId = TelescopeHardware.GetId();

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
                    logger.LogError($"Telescope {deviceNumber} - {ex.Message}");
                }

                logger.LogInformation($"Telescope {deviceNumber} - UUID of {UniqueID}");

                TelescopeHardware.Start();

                TelescopeHardware.LogMessage("New", "Instance ID: " + objectId + ", new: " + "Driver ID: Alpaca Simulator");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        public string DeviceName { get => Name; }
        public int DeviceNumber { get; private set; }
        public string UniqueID { get; private set; }

        //
        // PUBLIC COM INTERFACE ITelescope IMPLEMENTATION
        //

        #region ITelescope Members

        public string Action(string ActionName, string ActionParameters)
        {
            //throw new MethodNotImplementedException("Action");
            string Response = "";
            if (ActionName == null)
                throw new InvalidValueException("no ActionName is provided");
            switch (ActionName.ToUpper(CultureInfo.InvariantCulture))
            {
                case AssemblyVersionNumberUpper:
                    Response = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    break;

                case SlewToHAUpper:
                    //Assume that we have just been supplied with an HA
                    //Let errors just go straight back to the caller
                    double HA = double.Parse(ActionParameters, CultureInfo.InvariantCulture);
                    double RA = this.SiderealTime - HA;
                    this.SlewToCoordinates(RA, 0.0);
                    Response = "Slew successful!";
                    break;

                case AvailableTimeInThisPointingState:
                    Response = TelescopeHardware.AvailableTimeInThisPointingState.ToString();
                    break;

                case TimeUntilPointingStateCanChange:
                    Response = TelescopeHardware.TimeUntilPointingStateCanChange.ToString();
                    break;

                default:
                    throw new ASCOM.InvalidOperationException("Command: '" + ActionName + "' is not recognised by the Scope Simulator .NET driver. " + AssemblyVersionNumberUpper + " " + SlewToHAUpper);
            }
            return Response;
        }

        /// <summary>
        /// Gets the supported actions.
        /// </summary>
        public IList<string> SupportedActions
        {
            // no supported actions, return empty array
            get
            {
                return new List<string>
                {
                    AssemblyVersionNumber, // Add a test action to return a value
                    SlewToHA, // Expects a numeric HA Parameter
                    "AvailableTimeInThisPointingState",
                    "TimeUntilPointingStateCanChange"
                };
            }
        }

        public void AbortSlew()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "AbortSlew: ");
            CheckParked("AbortSlew");
            TelescopeHardware.AbortSlew();

            SharedResources.TrafficEnd("(done)");
        }

        public AlignmentMode AlignmentMode
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "AlignmentMode: ");
                CheckCapability(TelescopeHardware.CanAlignmentMode, "AlignmentMode");
                SharedResources.TrafficEnd(TelescopeHardware.AlignmentMode.ToString());

                switch (TelescopeHardware.AlignmentMode)
                {
                    case AlignmentMode.AltAz:
                        return AlignmentMode.AltAz;

                    case AlignmentMode.GermanPolar:
                        return AlignmentMode.GermanPolar;

                    case AlignmentMode.Polar:
                        return AlignmentMode.Polar;

                    default:
                        return AlignmentMode.GermanPolar;
                }
            }
        }

        public double Altitude
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "Altitude: ");
                CheckCapability(TelescopeHardware.CanAltAz, "Altitude", false);
                if ((TelescopeHardware.AtPark || TelescopeHardware.SlewState == SlewType.SlewPark) && TelescopeHardware.NoCoordinatesAtPark)
                {
                    SharedResources.TrafficEnd("No coordinates at park!");
                    throw new PropertyNotImplementedException("Altitude", false);
                }
                SharedResources.TrafficEnd(Utilities.DegreesToDMS(TelescopeHardware.Altitude));
                return TelescopeHardware.Altitude;
            }
        }

        public double ApertureArea
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "ApertureArea: ");
                CheckCapability(TelescopeHardware.CanOptics, "ApertureArea", false);
                SharedResources.TrafficEnd(TelescopeHardware.ApertureArea.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.ApertureArea;
            }
        }

        public double ApertureDiameter
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "ApertureDiameter: ");
                CheckCapability(TelescopeHardware.CanOptics, "ApertureDiameter", false);
                SharedResources.TrafficEnd(TelescopeHardware.ApertureDiameter.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.ApertureDiameter;
            }
        }

        public bool AtHome
        {
            get
            {
                CheckVersionOne("AtHome");
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, "AtHome: " + TelescopeHardware.AtHome);
                return TelescopeHardware.AtHome;
            }
        }

        public bool AtPark
        {
            get
            {
                CheckVersionOne("AtPark");
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, "AtPark: " + TelescopeHardware.AtPark);
                return TelescopeHardware.AtPark;
            }
        }

        public IAxisRates AxisRates(TelescopeAxis Axis)
        {
            switch (Axis)
            {
                case TelescopeAxis.Primary:
                    //                    return m_AxisRates[0];
                    return new AxisRates(TelescopeAxis.Primary);

                case TelescopeAxis.Secondary:
                    //                    return m_AxisRates[1];
                    return new AxisRates(TelescopeAxis.Secondary);

                case TelescopeAxis.Tertiary:
                    //                    return m_AxisRates[2];
                    return new AxisRates(TelescopeAxis.Tertiary);

                default:
                    return null;
            }
        }

        public double Azimuth
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "Azimuth: ");

                CheckCapability(TelescopeHardware.CanAltAz, "Azimuth", false);

                if ((TelescopeHardware.AtPark || TelescopeHardware.SlewState == SlewType.SlewPark) && TelescopeHardware.NoCoordinatesAtPark)
                {
                    SharedResources.TrafficEnd("No coordinates at park!");
                    throw new PropertyNotImplementedException("Azimuth", false);
                }
                SharedResources.TrafficEnd(Utilities.DegreesToDMS(TelescopeHardware.Azimuth));
                return TelescopeHardware.Azimuth;
            }
        }

        public bool CanFindHome
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanFindHome: " + TelescopeHardware.CanFindHome);
                return TelescopeHardware.CanFindHome;
            }
        }

        public bool CanMoveAxis(TelescopeAxis Axis)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, string.Format(CultureInfo.CurrentCulture, "CanMoveAxis {0}: ", Axis.ToString()));
            CheckVersionOne("CanMoveAxis");
            SharedResources.TrafficEnd(TelescopeHardware.CanMoveAxis(Axis).ToString());

            return TelescopeHardware.CanMoveAxis(Axis);
        }

        public bool CanPark
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanPark: " + TelescopeHardware.CanPark);
                return TelescopeHardware.CanPark;
            }
        }

        public bool CanPulseGuide
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanPulseGuide: " + TelescopeHardware.CanPulseGuide);
                return TelescopeHardware.CanPulseGuide;
            }
        }

        public bool CanSetDeclinationRate
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSetDeclinationRate: ");
                CheckVersionOne("CanSetDeclinationRate");
                SharedResources.TrafficEnd(TelescopeHardware.CanSetDeclinationRate.ToString());
                return TelescopeHardware.CanSetDeclinationRate;
            }
        }

        public bool CanSetGuideRates
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSetGuideRates: " + TelescopeHardware.CanSetGuideRates);
                return TelescopeHardware.CanSetGuideRates;
            }
        }

        public bool CanSetPark
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSetPark: " + TelescopeHardware.CanSetPark);
                return TelescopeHardware.CanSetPark;
            }
        }

        public bool CanSetPierSide
        {
            get
            {
                if(AlignmentMode == AlignmentMode.GermanPolar) 
                { 
                    SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSetPointingState: ");
                    CheckVersionOne("CanSetPointingState");
                    SharedResources.TrafficEnd(TelescopeHardware.CanSetPointingState.ToString());
                    return TelescopeHardware.CanSetPointingState;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool CanSetRightAscensionRate
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSetRightAscensionRate: ");
                CheckVersionOne("CanSetRightAscensionRate");
                SharedResources.TrafficEnd(TelescopeHardware.CanSetRightAscensionRate.ToString());
                return TelescopeHardware.CanSetRightAscensionRate;
            }
        }

        public bool CanSetTracking
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSetTracking: " + TelescopeHardware.CanSetTracking);
                return TelescopeHardware.CanSetTracking;
            }
        }

        public bool CanSlew
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSlew: " + TelescopeHardware.CanSlew);
                return TelescopeHardware.CanSlew;
            }
        }

        public bool CanSlewAltAz
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSlewAltAz: ");
                CheckVersionOne("CanSlewAltAz");
                SharedResources.TrafficEnd(TelescopeHardware.CanSlewAltAz.ToString());
                return TelescopeHardware.CanSlewAltAz;
            }
        }

        public bool CanSlewAltAzAsync
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSlewAltAzAsync: ");
                CheckVersionOne("CanSlewAltAzAsync");
                SharedResources.TrafficEnd(TelescopeHardware.CanSlewAltAzAsync.ToString());
                return TelescopeHardware.CanSlewAltAzAsync;
            }
        }

        public bool CanSlewAsync
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSlewAsync: " + TelescopeHardware.CanSlewAsync);
                return TelescopeHardware.CanSlewAsync;
            }
        }

        public bool CanSync
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanSync: " + TelescopeHardware.CanSync);
                return TelescopeHardware.CanSync;
            }
        }

        public bool CanSyncAltAz
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "CanSyncAltAz: ");
                CheckVersionOne("CanSyncAltAz");
                SharedResources.TrafficEnd(TelescopeHardware.CanSyncAltAz.ToString());
                return TelescopeHardware.CanSyncAltAz;
            }
        }

        public bool CanUnpark
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Capabilities, "CanUnpark: " + TelescopeHardware.CanUnpark);
                return TelescopeHardware.CanUnpark;
            }
        }

        public void CommandBlind(string Command, bool Raw)
        {
            // TODO Replace this with your implementation
            throw new MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string Command, bool Raw)
        {
            // TODO Replace this with your implementation
            throw new MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string Command, bool Raw)
        {
            // TODO Replace this with your implementation
            throw new MethodNotImplementedException("CommandString");
        }

        public bool Connected
        {
            get
            {
                var connected = TelescopeHardware.Connected;
                SharedResources.TrafficLine(SharedResources.MessageType.Other, "Connected = " + connected.ToString());
                TelescopeHardware.TL.LogVerbose($"Connected Get {connected}");
                return connected;
            }
            set
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Other, "Set Connected to " + value.ToString());
                TelescopeHardware.LogMessage("Connected Set", value.ToString());
                TelescopeHardware.SetConnected(objectId, value);
            }
        }

        public double Declination
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "Declination: ");

                CheckCapability(TelescopeHardware.CanEquatorial, "Declination", false);

                if ((TelescopeHardware.AtPark || TelescopeHardware.SlewState == SlewType.SlewPark) && TelescopeHardware.NoCoordinatesAtPark)
                {
                    SharedResources.TrafficEnd("No coordinates at park!");
                    throw new PropertyNotImplementedException("Declination", false);
                }
                SharedResources.TrafficEnd(Utilities.DegreesToDMS(TelescopeHardware.Declination));
                return TelescopeHardware.Declination;
            }
        }

        public double DeclinationRate
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Gets, "DeclinationRate: " + TelescopeHardware.DeclinationRate);
                return TelescopeHardware.DeclinationRate;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "DeclinationRate:-> ");
                CheckCapability(TelescopeHardware.CanSetEquatorialRates, "DeclinationRate", true);
                TelescopeHardware.DeclinationRate = value;
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public string Description
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Gets, "Description: " + SharedResources.INSTRUMENT_DESCRIPTION);
                return SharedResources.INSTRUMENT_DESCRIPTION;
            }
        }

        public PointingState DestinationSideOfPier(double RightAscension, double Declination)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Other, "DestinationSideOfPier: ");
            CheckVersionOne("DestinationSideOfPier");
            SharedResources.TrafficStart(string.Format(CultureInfo.CurrentCulture, "Ra {0}, Dec {1} - ", RightAscension, Declination));
            CheckCapability(TelescopeHardware.CanDestinationSideofPier, "DestinationSideOfPier");

            PointingState ps = TelescopeHardware.SideOfPierRaDec(RightAscension, Declination);
            SharedResources.TrafficEnd(ps.ToString());
            return ps;
        }

        public bool DoesRefraction
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "DoesRefraction: ");
                CheckVersionOne("DoesRefraction");
                SharedResources.TrafficEnd(TelescopeHardware.Refraction.ToString());
                return TelescopeHardware.Refraction;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Capabilities, "DoesRefraction: ->");
                CheckVersionOne("DoesRefraction");
                SharedResources.TrafficEnd(value.ToString());
                TelescopeHardware.Refraction = value;
            }
        }

        public string DriverInfo
        {
            get
            {
                Assembly asm = Assembly.GetExecutingAssembly();

                string driverinfo = asm.FullName;

                SharedResources.TrafficLine(SharedResources.MessageType.Other, "DriverInfo: " + driverinfo);
                return driverinfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "DriverVersion: ");
                CheckVersionOne("DriverVersion");
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var driverversion = $"{version.Major}.{version.Minor}";
                SharedResources.TrafficEnd(driverversion);
                return driverversion;
            }
        }

        public EquatorialCoordinateType EquatorialSystem
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "EquatorialSystem: ");
                CheckVersionOne("EquatorialSystem");
                string output = "";
                EquatorialCoordinateType eq = EquatorialCoordinateType.Other;

                switch (TelescopeHardware.EquatorialSystem)
                {
                    case 0:
                        eq = EquatorialCoordinateType.Other;
                        output = "Other";
                        break;

                    case 1:
                        eq = EquatorialCoordinateType.Topocentric;
                        output = "Local";
                        break;

                    case 2:
                        eq = EquatorialCoordinateType.J2000;
                        output = "J2000";
                        break;

                    case 3:
                        eq = EquatorialCoordinateType.J2050;
                        output = "J2050";
                        break;

                    case 4:
                        eq = EquatorialCoordinateType.B1950;
                        output = "B1950";
                        break;
                }
                SharedResources.TrafficEnd(output);
                return eq;
            }
        }

        public void FindHome()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "FindHome: ");
            CheckCapability(TelescopeHardware.CanFindHome, "FindHome");

            CheckParked("FindHome");

            TelescopeHardware.FindHome();

            while (TelescopeHardware.SlewState == SlewType.SlewHome || TelescopeHardware.SlewState == SlewType.SlewSettle)
            {
                System.Threading.Thread.Sleep(1);
            }

            SharedResources.TrafficEnd(SharedResources.MessageType.Slew, "(done)");
        }

        public double FocalLength
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "FocalLength: ");
                CheckVersionOne("FocalLength");
                CheckCapability(TelescopeHardware.CanOptics, "FocalLength", false);
                SharedResources.TrafficEnd(TelescopeHardware.FocalLength.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.FocalLength;
            }
        }

        public double GuideRateDeclination
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "GuideRateDeclination: ");
                CheckVersionOne("GuideRateDeclination");
                SharedResources.TrafficEnd(TelescopeHardware.GuideRateDeclination.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.GuideRateDeclination;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "GuideRateDeclination->: ");
                CheckVersionOne("GuideRateDeclination");
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
                TelescopeHardware.GuideRateDeclination = value;
            }
        }

        public double GuideRateRightAscension
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "GuideRateRightAscension: ");
                CheckVersionOne("GuideRateRightAscension");
                SharedResources.TrafficEnd(TelescopeHardware.GuideRateRightAscension.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.GuideRateRightAscension;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "GuideRateRightAscension->: ");
                CheckVersionOne("GuideRateRightAscension");
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
                TelescopeHardware.GuideRateRightAscension = value;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                if (TelescopeHardware.VersionOneOnly)
                {
                    SharedResources.TrafficLine(SharedResources.MessageType.Other, "InterfaceVersion: 1");
                    return 1;
                }
                else
                {
                    SharedResources.TrafficLine(SharedResources.MessageType.Other, "InterfaceVersion: 3");
                    return 3;
                }
            }
        }

        public bool IsPulseGuiding
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Polls, "IsPulseGuiding: ");
                // TODO Is this correct, should it just return false?
                CheckCapability(TelescopeHardware.CanPulseGuide, "IsPulseGuiding", false);
                SharedResources.TrafficEnd(TelescopeHardware.IsPulseGuiding.ToString());

                return TelescopeHardware.IsPulseGuiding;
            }
        }

        public void MoveAxis(TelescopeAxis Axis, double Rate)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, string.Format(CultureInfo.CurrentCulture, "MoveAxis {0} {1}:  ", Axis.ToString(), Rate));
            CheckVersionOne("MoveAxis");
            CheckRate(Axis, Rate);

            if (!CanMoveAxis(Axis))
                throw new MethodNotImplementedException("CanMoveAxis " + Enum.GetName(typeof(TelescopeAxis), Axis));

            CheckParked("MoveAxis");

            switch (Axis)
            {
                case TelescopeAxis.Primary:
                    TelescopeHardware.rateMoveAxes.X = Rate;
                    break;

                case TelescopeAxis.Secondary:
                    TelescopeHardware.rateMoveAxes.Y = Rate;
                    break;

                case TelescopeAxis.Tertiary:
                    // not implemented
                    break;
            }

            SharedResources.TrafficEnd("(done)");
        }

        public string Name
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Other, "Description: " + SharedResources.INSTRUMENT_NAME);
                return SharedResources.INSTRUMENT_NAME;
            }
        }

        public void Park()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "Park: ");
            CheckCapability(TelescopeHardware.CanPark, "Park");

            if (TelescopeHardware.IsParked)
            {
                SharedResources.TrafficEnd("(Is Parked)");
                return;
            }

            TelescopeHardware.Park();

            SharedResources.TrafficEnd("(done)");
        }

        public void PulseGuide(GuideDirection Direction, int Duration)
        {
            if (TelescopeHardware.AtPark) throw new ParkedException();

            SharedResources.TrafficStart(SharedResources.MessageType.Slew, string.Format(CultureInfo.CurrentCulture, "Pulse Guide: {0}, {1}", Direction, Duration.ToString(CultureInfo.InvariantCulture)));

            CheckCapability(TelescopeHardware.CanPulseGuide, "PulseGuide");
            CheckRange(Duration, 0, 30000, "PulseGuide", "Duration");
            if (Duration == 0)
            {
                // stops the current guide command
                switch (Direction)
                {
                    case GuideDirection.North:
                    case GuideDirection.South:
                        TelescopeHardware.isPulseGuidingDec = false;
                        TelescopeHardware.guideDuration.Y = 0;
                        break;

                    case GuideDirection.East:
                    case GuideDirection.West:
                        TelescopeHardware.isPulseGuidingRa = false;
                        TelescopeHardware.guideDuration.X = 0;
                        break;
                }
            }
            else
            {
                //DateTime endTime = DateTime.Now + TimeSpan.FromMilliseconds(Duration);

                switch (Direction)
                {
                    case GuideDirection.North:
                        TelescopeHardware.guideRate.Y = Math.Abs(TelescopeHardware.guideRate.Y);
                        TelescopeHardware.isPulseGuidingDec = true;
                        TelescopeHardware.guideDuration.Y = Duration / 1000.0;
                        break;

                    case GuideDirection.South:
                        TelescopeHardware.guideRate.Y = -Math.Abs(TelescopeHardware.guideRate.Y);
                        //TelescopeHardware.pulseGuideDecEndTime = endTime;
                        TelescopeHardware.isPulseGuidingDec = true;
                        TelescopeHardware.guideDuration.Y = Duration / 1000.0;
                        break;

                    case GuideDirection.East:
                        TelescopeHardware.guideRate.X = -Math.Abs(TelescopeHardware.guideRate.X);
                        //TelescopeHardware.pulseGuideRaEndTime = endTime;
                        TelescopeHardware.isPulseGuidingRa = true;
                        TelescopeHardware.guideDuration.X = Duration / 1000.0;
                        break;

                    case GuideDirection.West:
                        TelescopeHardware.guideRate.X = Math.Abs(TelescopeHardware.guideRate.X);
                        //TelescopeHardware.pulseGuideRaEndTime = endTime;
                        TelescopeHardware.isPulseGuidingRa = true;
                        TelescopeHardware.guideDuration.X = Duration / 1000.0;
                        break;
                }
            }

            if (!TelescopeHardware.CanDualAxisPulseGuide) // Single axis synchronous pulse guide
            {
                System.Threading.Thread.Sleep(Duration); // Must be synchronous so wait out the pulseguide duration here
                TelescopeHardware.isPulseGuidingRa = false; // Make sure that IsPulseGuiding will return false
                TelescopeHardware.isPulseGuidingDec = false;
            }
            SharedResources.TrafficEnd(" (done) ");
        }

        public double RightAscension
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "Right Ascension: ");

                CheckCapability(TelescopeHardware.CanEquatorial, "RightAscension", false);

                if ((TelescopeHardware.AtPark || TelescopeHardware.SlewState == SlewType.SlewPark) && TelescopeHardware.NoCoordinatesAtPark)
                {
                    SharedResources.TrafficEnd("No coordinates at park!");
                    throw new PropertyNotImplementedException("RightAscension", false);
                }
                SharedResources.TrafficEnd(Utilities.HoursToHMS(TelescopeHardware.RightAscension));
                return TelescopeHardware.RightAscension;
            }
        }

        public double RightAscensionRate
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Gets, "RightAscensionRate: " + TelescopeHardware.RightAscensionRate);
                return TelescopeHardware.RightAscensionRate;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "RightAscensionRate:-> ");
                CheckCapability(TelescopeHardware.CanSetEquatorialRates, "RightAscensionRate", true);
                TelescopeHardware.RightAscensionRate = value;
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void SetPark()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Other, "Set Park: ");
            CheckCapability(TelescopeHardware.CanSetPark, "SetPark");

            TelescopeHardware.ParkAltitude = TelescopeHardware.Altitude;
            TelescopeHardware.ParkAzimuth = TelescopeHardware.Azimuth;

            SharedResources.TrafficEnd("(done)");
        }

        public PointingState SideOfPier
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, string.Format("SideOfPier: {0}", TelescopeHardware.SideOfPier));
                CheckCapability(TelescopeHardware.CanPointingState, "SideOfPier", false);
                return TelescopeHardware.SideOfPier;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SideOfPier: ");
                CheckCapability(TelescopeHardware.CanSetPointingState, "SideOfPier", true);

                if (value == TelescopeHardware.SideOfPier)
                {
                    SharedResources.TrafficEnd("(no change needed)");
                    return;
                }
                // TODO implement this correctly, it needs an overlap which can be reached on either side
                TelescopeHardware.SideOfPier = value;
                // slew to the same position, changing the side of pier appropriately if possible
                TelescopeHardware.StartSlewRaDec(TelescopeHardware.RightAscension, TelescopeHardware.Declination, true);
                SharedResources.TrafficEnd("(started)");
            }
        }

        public double SiderealTime
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Time, "Sidereal Time: ");
                CheckCapability(TelescopeHardware.CanSiderealTime, "SiderealTime", false);
                SharedResources.TrafficEnd(Utilities.HoursToHMS(TelescopeHardware.SiderealTime));
                return TelescopeHardware.SiderealTime;
            }
        }

        public double SiteElevation
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteElevation: ");

                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteElevation", false);
                SharedResources.TrafficEnd(TelescopeHardware.Elevation.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.Elevation;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteElevation: ->");
                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteElevation", true);
                CheckRange(value, -300, 10000, "SiteElevation");
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
                TelescopeHardware.Elevation = value;
            }
        }

        public double SiteLatitude
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteLatitude: ");

                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteLatitude", false);
                SharedResources.TrafficEnd(TelescopeHardware.Latitude.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.Latitude;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteLatitude: ->");
                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteLatitude", true);
                CheckRange(value, -90, 90, "SiteLatitude");
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
                TelescopeHardware.Latitude = value;
            }
        }

        public double SiteLongitude
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteLongitude: ");
                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteLongitude", false);
                SharedResources.TrafficEnd(TelescopeHardware.Longitude.ToString(CultureInfo.InvariantCulture));
                return TelescopeHardware.Longitude;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SiteLongitude: ->");
                CheckCapability(TelescopeHardware.CanLatLongElev, "SiteLongitude", true);
                CheckRange(value, -180, 180, "SiteLongitude");
                SharedResources.TrafficEnd(value.ToString(CultureInfo.InvariantCulture));
                TelescopeHardware.Longitude = value;
            }
        }

        public short SlewSettleTime
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Other, "SlewSettleTime: " + (TelescopeHardware.SlewSettleTime * 1000).ToString(CultureInfo.InvariantCulture));
                return (short)(TelescopeHardware.SlewSettleTime);
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "SlewSettleTime:-> ");
                CheckRange(value, 0, 100, "SlewSettleTime");
                SharedResources.TrafficEnd(value + " (done)");
                TelescopeHardware.SlewSettleTime = value;
            }
        }

        public void SlewToAltAz(double Azimuth, double Altitude)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToAltAz: ");
            CheckCapability(TelescopeHardware.CanSlewAltAz, "SlewToAltAz");
            CheckParked("SlewToAltAz");
            CheckTracking(false, "SlewToAltAz");
            CheckRange(Azimuth, 0, 360, "SlewToltAz", "azimuth");
            CheckRange(Altitude, -90, 90, "SlewToAltAz", "Altitude");

            SharedResources.TrafficStart(" Alt " + Utilities.DegreesToDMS(Altitude) + " Az " + Utilities.DegreesToDMS(Azimuth));

            TelescopeHardware.StartSlewAltAz(Altitude, Azimuth);

            while (TelescopeHardware.SlewState == SlewType.SlewAltAz || TelescopeHardware.SlewState == SlewType.SlewSettle)
            {
                System.Threading.Thread.Sleep(1);
            }

            SharedResources.TrafficEnd(" done");
        }

        public void SlewToAltAzAsync(double Azimuth, double Altitude)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToAltAzAsync: ");
            CheckCapability(TelescopeHardware.CanSlewAltAzAsync, "SlewToAltAzAsync");
            CheckParked("SlewToAltAz");
            CheckTracking(false, "SlewToAltAzAsync");
            CheckRange(Azimuth, 0, 360, "SlewToAltAzAsync", "Azimuth");
            CheckRange(Altitude, -90, 90, "SlewToAltAzAsync", "Altitude");

            SharedResources.TrafficStart(" Alt " + Utilities.DegreesToDMS(Altitude) + " Az " + Utilities.DegreesToDMS(Azimuth));

            TelescopeHardware.StartSlewAltAz(Altitude, Azimuth);
            SharedResources.TrafficEnd(" started");
        }

        public void SlewToCoordinates(double RightAscension, double Declination)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToCoordinates: ");
            CheckCapability(TelescopeHardware.CanSlew, "SlewToCoordinates");
            CheckRange(RightAscension, 0, 24, "SlewToCoordinates", "RightAscension");
            CheckRange(Declination, -90, 90, "SlewToCoordinates", "Declination");
            CheckParked("SlewToCoordinates");
            CheckTracking(true, "SlewToCoordinates");

            SharedResources.TrafficStart(" RA " + Utilities.HoursToHMS(RightAscension) + " DEC " + Utilities.DegreesToDMS(Declination));

            TelescopeHardware.TargetRightAscension = RightAscension; // Set the Target RA and Dec prior to the Slew attempt per the ASCOM Telescope specification
            TelescopeHardware.TargetDeclination = Declination;

            TelescopeHardware.StartSlewRaDec(RightAscension, Declination, true);

            while (TelescopeHardware.IsSlewing)
            {
                System.Threading.Thread.Sleep(1);
            }

            SharedResources.TrafficEnd("done");
        }

        public void SlewToCoordinatesAsync(double RightAscension, double Declination)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToCoordinatesAsync: ");
            CheckCapability(TelescopeHardware.CanSlewAsync, "SlewToCoordinatesAsync");
            CheckRange(RightAscension, 0, 24, "SlewToCoordinatesAsync", "RightAscension");
            CheckRange(Declination, -90, 90, "SlewToCoordinatesAsync", "Declination");
            CheckParked("SlewToCoordinatesAsync");
            CheckTracking(true, "SlewToCoordinatesAsync");

            TelescopeHardware.TargetRightAscension = RightAscension; // Set the Target RA and Dec prior to the Slew attempt per the ASCOM Telescope specification
            TelescopeHardware.TargetDeclination = Declination;

            SharedResources.TrafficStart(" RA " + Utilities.HoursToHMS(RightAscension) + " DEC " + Utilities.DegreesToDMS(Declination));

            TelescopeHardware.StartSlewRaDec(RightAscension, Declination, true);
            SharedResources.TrafficEnd("started");
        }

        public void SlewToTarget()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToTarget: ");
            CheckCapability(TelescopeHardware.CanSlew, "SlewToTarget");
            CheckRange(TelescopeHardware.TargetRightAscension, 0, 24, "SlewToTarget", "TargetRightAscension");
            CheckRange(TelescopeHardware.TargetDeclination, -90, 90, "SlewToTarget", "TargetDeclination");
            CheckParked("SlewToTarget");
            CheckTracking(true, "SlewToTarget");

            TelescopeHardware.StartSlewRaDec(TelescopeHardware.TargetRightAscension, TelescopeHardware.TargetDeclination, true);

            while (TelescopeHardware.SlewState == SlewType.SlewRaDec || TelescopeHardware.SlewState == SlewType.SlewSettle)
            {
                System.Threading.Thread.Sleep(1);
            }

            SharedResources.TrafficEnd("done");
        }

        public void SlewToTargetAsync()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SlewToTargetAsync: ");
            CheckCapability(TelescopeHardware.CanSlewAsync, "SlewToTargetAsync");
            CheckRange(TelescopeHardware.TargetRightAscension, 0, 24, "SlewToTargetAsync", "TargetRightAscension");
            CheckRange(TelescopeHardware.TargetDeclination, -90, 90, "SlewToTargetAsync", "TargetDeclination");
            CheckParked("SlewToTargetAsync");
            CheckTracking(true, "SlewToTargetAsync");
            TelescopeHardware.StartSlewRaDec(TelescopeHardware.TargetRightAscension, TelescopeHardware.TargetDeclination, true);
        }

        public bool Slewing
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, string.Format(CultureInfo.CurrentCulture, "Slewing: {0}", TelescopeHardware.SlewState != SlewType.SlewNone));
                return TelescopeHardware.IsSlewing;
            }
        }

        public void SyncToAltAz(double Azimuth, double Altitude)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SyncToAltAz: ");
            CheckCapability(TelescopeHardware.CanSyncAltAz, "SyncToAltAz");
            CheckRange(Azimuth, 0, 360, "SyncToAltAz", "Azimuth");
            CheckRange(Altitude, -90, 90, "SyncToAltAz", "Altitude");
            CheckParked("SyncToAltAz");
            CheckTracking(false, "SyncToAltAz");

            SharedResources.TrafficStart(" Alt " + Utilities.DegreesToDMS(Altitude) + " Az " + Utilities.DegreesToDMS(Azimuth));

            TelescopeHardware.ChangePark(false);

            TelescopeHardware.SyncToAltAzm(Azimuth, Altitude);

            SharedResources.TrafficEnd("done");
        }

        public void SyncToCoordinates(double RightAscension, double Declination)
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SyncToCoordinates: ");
            CheckCapability(TelescopeHardware.CanSync, "SyncToCoordinates");
            CheckRange(RightAscension, 0, 24, "SyncToCoordinates", "RightAscension");
            CheckRange(Declination, -90, 90, "SyncToCoordinates", "Declination");
            CheckParked("SyncToCoordinates");
            CheckTracking(true, "SyncToCoordinates");

            SharedResources.TrafficStart(string.Format(CultureInfo.CurrentCulture, " RA {0} DEC {1}", Utilities.HoursToHMS(RightAscension), Utilities.DegreesToDMS(Declination)));

            TelescopeHardware.TargetDeclination = Declination;
            TelescopeHardware.TargetRightAscension = RightAscension;

            TelescopeHardware.ChangePark(false);

            TelescopeHardware.SyncToTarget();

            SharedResources.TrafficEnd("done");
        }

        public void SyncToTarget()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "SyncToTarget: ");
            CheckCapability(TelescopeHardware.CanSync, "SyncToTarget");
            CheckRange(TelescopeHardware.TargetRightAscension, 0, 24, "SyncToTarget", "TargetRightAscension");
            CheckRange(TelescopeHardware.TargetDeclination, -90, 90, "SyncToTarget", "TargetDeclination");

            SharedResources.TrafficStart(" RA " + Utilities.HoursToHMS(TelescopeHardware.TargetRightAscension) + " DEC " + Utilities.DegreesToDMS(TelescopeHardware.TargetDeclination));

            CheckParked("SyncToTarget");
            CheckTracking(true, "SyncToTarget");

            TelescopeHardware.ChangePark(false);

            TelescopeHardware.SyncToTarget();

            SharedResources.TrafficEnd("done");
        }

        public double TargetDeclination
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "TargetDeclination: ");
                CheckCapability(TelescopeHardware.CanSlew, "TargetDeclination", false);
                CheckRange(TelescopeHardware.TargetDeclination, -90, 90, "TargetDeclination");
                SharedResources.TrafficEnd(Utilities.DegreesToDMS(TelescopeHardware.TargetDeclination));
                return TelescopeHardware.TargetDeclination;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "TargetDeclination:-> ");
                CheckCapability(TelescopeHardware.CanSlew, "TargetDeclination", true);
                CheckRange(value, -90, 90, "TargetDeclination");
                SharedResources.TrafficEnd(Utilities.DegreesToDMS(value));
                TelescopeHardware.TargetDeclination = value;
            }
        }

        public double TargetRightAscension
        {
            get
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "TargetRightAscension: ");
                CheckCapability(TelescopeHardware.CanSlew, "TargetRightAscension", false);
                CheckRange(TelescopeHardware.TargetRightAscension, 0, 24, "TargetRightAscension");
                SharedResources.TrafficEnd(Utilities.HoursToHMS(TelescopeHardware.TargetRightAscension));
                return TelescopeHardware.TargetRightAscension;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Gets, "TargetRightAscension:-> ");
                CheckCapability(TelescopeHardware.CanSlew, "TargetRightAscension", true);
                CheckRange(value, 0, 24, "TargetRightAscension");

                SharedResources.TrafficEnd(Utilities.HoursToHMS(value));
                TelescopeHardware.TargetRightAscension = value;
            }
        }

        public bool Tracking
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, "Tracking: " + TelescopeHardware.Tracking.ToString());
                return TelescopeHardware.Tracking;
            }
            set
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Polls, "Tracking:-> " + value.ToString());
                TelescopeHardware.Tracking = value;
            }
        }

        public DriveRate TrackingRate
        {
            get
            {
                DriveRate rate = TelescopeHardware.TrackingRate;
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "TrackingRate: ");
                CheckVersionOne("TrackingRate");
                SharedResources.TrafficEnd(rate.ToString());
                return rate;
            }
            set
            {
                SharedResources.TrafficStart(SharedResources.MessageType.Other, "TrackingRate: -> ");
                CheckVersionOne("TrackingRate");
                if ((value < DriveRate.Sidereal) || (value > DriveRate.King)) throw new InvalidValueException("TrackingRate", value.ToString(), "0 (DriveSidereal) to 3 (DriveKing)");
                TelescopeHardware.TrackingRate = value;
                SharedResources.TrafficEnd(value.ToString() + "(done)");
            }
        }

        public ITrackingRates TrackingRates
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Other, "TrackingRates: (done)");
                if (TelescopeHardware.CanTrackingRates)
                {
                    return m_TrackingRates;
                }
                else
                {
                    return m_TrackingRatesSimple;
                }
            }
        }

        public DateTime UTCDate
        {
            get
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Time, "UTCDate: " + DateTime.UtcNow.AddSeconds((double)TelescopeHardware.DateDelta).ToString());
                return DateTime.UtcNow.AddSeconds((double)TelescopeHardware.DateDelta);
            }
            set
            {
                SharedResources.TrafficLine(SharedResources.MessageType.Time, "UTCDate-> " + value.ToString());
                TelescopeHardware.DateDelta = (int)value.Subtract(DateTime.UtcNow).TotalSeconds;
            }
        }

        public void Unpark()
        {
            SharedResources.TrafficStart(SharedResources.MessageType.Slew, "Unpark: ");
            CheckCapability(TelescopeHardware.CanUnpark, "Unpark");

            TelescopeHardware.ChangePark(false);
            TelescopeHardware.Tracking = true;

            SharedResources.TrafficEnd("(done)");
        }

        #endregion ITelescope Members

        #region new pier side properties

        //public double AvailableTimeInThisPointingState
        //{
        //    get
        //    {
        //        if (AlignmentMode != AlignmentMode.GermanPolar)
        //        {
        //            return 86400;
        //        }
        //        return TelescopeHardware.AvailableTimeInThisPointingState;
        //    }
        //}

        //public double TimeUntilPointingStateCanChange
        //{
        //    get
        //    {
        //        if (AlignmentMode != AlignmentMode.GermanPolar)
        //        {
        //            return 0;
        //        }
        //        return TelescopeHardware.TimeUntilPointingStateCanChange;
        //    }
        //}

        #endregion new pier side properties

        #region private methods

        private void CheckRate(TelescopeAxis axis, double rate)
        {
            IAxisRates rates = AxisRates(axis);
            string ratesStr = string.Empty;
            foreach (Rate item in rates)
            {
                if (Math.Abs(rate) >= item.Minimum && Math.Abs(rate) <= item.Maximum)
                {
                    return;
                }
                ratesStr = string.Format("{0}, {1} to {2}", ratesStr, item.Minimum, item.Maximum);
            }
            throw new InvalidValueException("MoveAxis", rate.ToString(CultureInfo.InvariantCulture), ratesStr);
        }

        private static void CheckRange(double value, double min, double max, string propertyOrMethod, string valueName)
        {
            if (double.IsNaN(value))
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0}:{1} value has not been set", propertyOrMethod, valueName));
                throw new ValueNotSetException(propertyOrMethod + ":" + valueName);
            }
            if (value < min || value > max)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0}:{4} {1} out of range {2} to {3}", propertyOrMethod, value, min, max, valueName));
                throw new InvalidValueException(propertyOrMethod, value.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, "{0}, {1} to {2}", valueName, min, max));
            }
        }

        private static void CheckRange(double value, double min, double max, string propertyOrMethod)
        {
            if (double.IsNaN(value))
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0} value has not been set", propertyOrMethod));
                throw new ValueNotSetException(propertyOrMethod);
            }
            if (value < min || value > max)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0} {1} out of range {2} to {3}", propertyOrMethod, value, min, max));
                throw new InvalidValueException(propertyOrMethod, value.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, "{0} to {1}", min, max));
            }
        }

        private static void CheckVersionOne(string property)
        {
            if (TelescopeHardware.VersionOneOnly)
            {
                SharedResources.TrafficEnd(property + " is not implemented in version 1");
                throw new System.NotImplementedException(property);
            }
        }

        private static void CheckCapability(bool capability, string method)
        {
            if (!capability)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0} not implemented in {1}", capability, method));
                throw new MethodNotImplementedException(method);
            }
        }

        private static void CheckCapability(bool capability, string property, bool setNotGet)
        {
            if (!capability)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{2} {0} not implemented in {1}", capability, property, setNotGet ? "set" : "get"));
                throw new PropertyNotImplementedException(property, setNotGet);
            }
        }

        private static void CheckParked(string property)
        {
            if (TelescopeHardware.AtPark)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0} not possible when parked", property));
                throw new ParkedException(property);
            }
        }

        /// <summary>
        /// Checks the slew type and tracking state and raises an exception if they don't match.
        /// </summary>
        /// <param name="raDecSlew">if set to <c>true</c> this is a Ra Dec slew if  <c>false</c> an Alt Az slew.</param>
        /// <param name="method">The method name.</param>
        private static void CheckTracking(bool raDecSlew, string method)
        {
            if (raDecSlew != TelescopeHardware.Tracking)
            {
                SharedResources.TrafficEnd(string.Format(CultureInfo.CurrentCulture, "{0} not possible when tracking is {1}", method, TelescopeHardware.Tracking));
                throw new ASCOM.InvalidOperationException(string.Format("{0} is not allowed when tracking is {1}", method, TelescopeHardware.Tracking));
            }
        }

        #endregion private methods

        #region IDisposable Members

        public void Dispose()
        {
            Connected = false;
            m_AxisRates[0].Dispose();
            m_AxisRates[1].Dispose();
            m_AxisRates[2].Dispose();
            m_AxisRates = null;
            m_TrackingRates.Dispose();
            m_TrackingRates = null;
            m_TrackingRatesSimple.Dispose();
            m_TrackingRatesSimple = null;
        }

        #endregion IDisposable Members

        #region Simulation Members

        public void ResetSettings()
        {
            TelescopeHardware.ClearProfile();
        }

        public string GetXMLProfile()
        {
            return TelescopeHardware.s_Profile.GetProfile();
        }

        #endregion Simulation Members
    }

    //
    // The Rate class implements IRate, and is used to hold values
    // for AxisRates. You do not need to change this class.
    //
    // The Guid attribute sets the CLSID for ASCOM.Telescope.Rate
    // The ClassInterface/None attribute prevents an empty interface called
    // _Rate from being created and used as the [default] interface
    //
    [Guid("d0acdb0f-9c7e-4c53-abb7-576e9f2b8225")]
    [ClassInterface(ClassInterfaceType.None), ComVisible(true)]
    public class Rate : IRate, IDisposable
    {
        private double m_dMaximum = 0;
        private double m_dMinimum = 0;

        //
        // Default constructor - Internal prevents public creation
        // of instances. These are values for AxisRates.
        //
        internal Rate(double Minimum, double Maximum)
        {
            m_dMaximum = Maximum;
            m_dMinimum = Minimum;
        }

        #region IRate Members

        public IEnumerator GetEnumerator()
        {
            return null;
        }

        public double Maximum
        {
            get { return m_dMaximum; }
            set { m_dMaximum = value; }
        }

        public double Minimum
        {
            get { return m_dMinimum; }
            set { m_dMinimum = value; }
        }

        #endregion IRate Members

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            // nothing to do?
        }

        #endregion IDisposable Members
    }

    //
    // AxisRates is a strongly-typed collection that must be enumerable by
    // both COM and .NET. The IAxisRates and IEnumerable interfaces provide
    // this polymorphism.
    //
    // The Guid attribute sets the CLSID for ASCOM.Telescope.AxisRates
    // The ClassInterface/None attribute prevents an empty interface called
    // _AxisRates from being created and used as the [default] interface
    //
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix"), Guid("af5510b9-3108-4237-83da-ae70524aab7d"), ClassInterface(ClassInterfaceType.None), ComVisible(true)]
    public class AxisRates : IAxisRates, IEnumerable, IEnumerator, IDisposable
    {
        private TelescopeAxis m_axis;
        private Rate[] m_Rates;
        private int pos;

        //
        // Constructor - Internal prevents public creation
        // of instances. Returned by Telescope.AxisRates.
        //
        internal AxisRates(TelescopeAxis Axis)
        {
            m_axis = Axis;
            //
            // This collection must hold zero or more Rate objects describing the
            // rates of motion ranges for the Telescope.MoveAxis() method
            // that are supported by your driver. It is OK to leave this
            // array empty, indicating that MoveAxis() is not supported.
            //
            // Note that we are constructing a rate array for the axis passed
            // to the constructor. Thus we switch() below, and each case should
            // initialize the array for the rate for the selected axis.
            //
            double maxRate = TelescopeHardware.MaximumSlewRate;
            switch (m_axis)
            {
                case TelescopeAxis.Primary:
                    // TODO Initialize this array with any Primary axis rates that your driver may provide
                    // Example: m_Rates = new Rate[] { new Rate(10.5, 30.2), new Rate(54.0, 43.6) }
                    m_Rates = new Rate[] { new Rate(0.0, maxRate / 3), new Rate(maxRate / 2, maxRate) };
                    break;

                case TelescopeAxis.Secondary:
                    // TODO Initialize this array with any Secondary axis rates that your driver may provide
                    m_Rates = new Rate[] { new Rate(0.0, maxRate / 3), new Rate(maxRate / 2, maxRate) };
                    break;

                case TelescopeAxis.Tertiary:
                    // TODO Initialize this array with any Tertiary axis rates that your driver may provide
                    m_Rates = new Rate[] { new Rate(0.0, maxRate / 3), new Rate(maxRate / 2, maxRate) };
                    break;
            }
            pos = -1;
        }

        #region IAxisRates Members

        public int Count
        {
            get { return m_Rates.Length; }
        }

        public IEnumerator GetEnumerator()
        {
            pos = -1; //Reset pointer as this is assumed by .NET enumeration
            return this as IEnumerator;
        }

        public IRate this[int index]
        {
            get
            {
                if (index < 1 || index > this.Count)
                    throw new InvalidValueException("AxisRates.index", index.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, "1 to {0}", this.Count));
                return (IRate)m_Rates[index - 1]; 	// 1-based
            }
        }

        #endregion IAxisRates Members

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                m_Rates = null;
            }
        }

        #endregion IDisposable Members

        #region IEnumerator implementation

        public bool MoveNext()
        {
            if (++pos >= m_Rates.Length) return false;
            return true;
        }

        public void Reset()
        {
            pos = -1;
        }

        public object Current
        {
            get
            {
                if (pos < 0 || pos >= m_Rates.Length) throw new System.InvalidOperationException();
                return m_Rates[pos];
            }
        }

        #endregion IEnumerator implementation
    }

    //
    // TrackingRates is a strongly-typed collection that must be enumerable by
    // both COM and .NET. The ITrackingRates and IEnumerable interfaces provide
    // this polymorphism.
    //
    // The Guid attribute sets the CLSID for ASCOM.Telescope.TrackingRates
    // The ClassInterface/None attribute prevents an empty interface called
    // _TrackingRates from being created and used as the [default] interface
    //
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix"), Guid("4bf5c72a-8491-49af-8668-626eac765e91")]
    [ClassInterface(ClassInterfaceType.None)]
    public class TrackingRates : ITrackingRates, IEnumerable, IEnumerator, IDisposable
    {
        private DriveRate[] m_TrackingRates;
        private static int _pos = -1;

        //
        // Default constructor - Internal prevents public creation
        // of instances. Returned by Telescope.AxisRates.
        //
        internal TrackingRates()
        {
            //
            // This array must hold ONE or more DriveRate values, indicating
            // the tracking rates supported by your telescope. The one value
            // (tracking rate) that MUST be supported is DriveSidereal!
            //
            m_TrackingRates = new DriveRate[] { DriveRate.Sidereal, DriveRate.King, DriveRate.Lunar, DriveRate.Solar };
        }

        #region ITrackingRates Members

        public int Count
        {
            get { return m_TrackingRates.Length; }
        }

        public IEnumerator GetEnumerator()
        {
            _pos = -1; //Reset pointer as this is assumed by .NET enumeration
            return this as IEnumerator;
        }

        public DriveRate this[int index]
        {
            get
            {
                if (index < 1 || index > this.Count)
                    throw new InvalidValueException("TrackingRates.this", index.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, "1 to {0}", this.Count));
                return m_TrackingRates[index - 1];
            }	// 1-based
        }

        #endregion ITrackingRates Members

        #region IEnumerator implementation

        public bool MoveNext()
        {
            if (++_pos >= m_TrackingRates.Length) return false;
            return true;
        }

        public void Reset()
        {
            _pos = -1;
        }

        public object Current
        {
            get
            {
                if (_pos < 0 || _pos >= m_TrackingRates.Length) throw new System.InvalidOperationException();
                return m_TrackingRates[_pos];
            }
        }

        #endregion IEnumerator implementation

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                /* Following code commented out in Platform 6.4 because m_TrackingRates is a global variable for the whole driver and there could be more than one
                 * instance of the TrackingRates class (created by the calling application). One instance should not invalidate the variable that could be in use
                 * by other instances of which this one is unaware.

                m_TrackingRates = null;

                */
            }
        }

        #endregion IDisposable Members
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix"), Guid("46753368-42d1-424a-85fa-26eee8f4c178")]
    [ClassInterface(ClassInterfaceType.None)]
    public class TrackingRatesSimple : ITrackingRates, IEnumerable, IEnumerator, IDisposable
    {
        private DriveRate[] m_TrackingRates;
        private static int _pos = -1;

        //
        // Default constructor - Internal prevents public creation
        // of instances. Returned by Telescope.AxisRates.
        //
        internal TrackingRatesSimple()
        {
            //
            // This array must hold ONE or more DriveRate values, indicating
            // the tracking rates supported by your telescope. The one value
            // (tracking rate) that MUST be supported is DriveSidereal!
            //
            m_TrackingRates = new DriveRate[] { DriveRate.Sidereal };
        }

        #region ITrackingRates Members

        public int Count
        {
            get { return m_TrackingRates.Length; }
        }

        public IEnumerator GetEnumerator()
        {
            _pos = -1; //Reset pointer as this is assumed by .NET enumeration
            return this as IEnumerator;
        }

        public DriveRate this[int index]
        {
            get
            {
                if (index <= 1 || index > this.Count)
                    throw new InvalidValueException("TrackingRatesSimple.this", index.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, "1 to {0}", this.Count));
                return m_TrackingRates[index - 1];
            }	// 1-based
        }

        #endregion ITrackingRates Members

        #region IEnumerator implementation

        public bool MoveNext()
        {
            if (++_pos >= m_TrackingRates.Length) return false;
            return true;
        }

        public void Reset()
        {
            _pos = -1;
        }

        public object Current
        {
            get
            {
                if (_pos < 0 || _pos >= m_TrackingRates.Length) throw new System.InvalidOperationException();
                return m_TrackingRates[_pos];
            }
        }

        #endregion IEnumerator implementation

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                /* Following code commented out in Platform 6.4 because m_TrackingRates is a global variable for the whole driver and there could be more than one
                 * instance of the TrackingRatesSimple class (created by the calling application). One instance should not invalidate the variable that could be in use
                 * by other instances of which this one is unaware.

                if (m_TrackingRates != null)
                {
                    m_TrackingRates = null;
                }
                */
            }
        }

        #endregion IDisposable Members
    }
}