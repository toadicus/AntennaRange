/*
 * AntennaRange © 2013 toadicus
 * 
 * AntennaRange provides incentive and requirements for the use of the various antenna parts.
 * Nominally, the breakdown is as follows:
 * 
 *     Communotron 16 - Suitable up to Kerbalsynchronous Orbit
 *     Comms DTS-M1 - Suitable throughout the Kerbin subsystem
 *     Communotron 88-88 - Suitable throughout the Kerbol system.
 * 
 * This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a
 * copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/
 * 
 * This software uses the ModuleManager library © 2013 ialdabaoth, used under a Creative Commons Attribution-ShareAlike
 * 3.0 Uported License.
 * 
 */

using System;
using System.Collections.Generic;
using KSP;

namespace AntennaRange
{
	/*
	 * ModuleLimitedDataTransmitter is designed as a drop-in replacement for ModuleDataTransmitter, and handles range-
	 * finding, power scaling, and data scaling for antennas during science transmission.  Its functionality varies with
	 * three tunables: nominalRange, maxPowerFactor, and maxDataFactor, set in .cfg files.
	 * 
	 * In general, the scaling functions assume the following relation:
	 * 
	 *     D² α P/R,
	 * 
	 * where D is the total transmission distance, P is the transmission power, and R is the data rate.
	 * 
	 * */
	public class ModuleLimitedDataTransmitter : PartModule, IScienceDataTransmitter
	{
		protected ModuleDataTransmitter dataTransmitter = null;

		// Stores the packetResourceCost as defined in the .cfg file.
		protected float _basepacketResourceCost;

		// Stores the packetSize as defined in the .cfg file.
		protected float _basepacketSize;

		// We don't have a Bard, so we're hiding Kerbin here.
		protected CelestialBody _Kerbin = null;

		// Returns the current distance to the center of Kerbin, which is totally where the Kerbals keep their radioes.
		protected double transmitDistance
		{
			get
			{
				Vector3d KerbinPos = this._Kerbin.position;
				Vector3d ActivePos = base.vessel.GetWorldPos3D();

				return (ActivePos - KerbinPos).magnitude;
			}
		}

		// Returns the maximum distance this module can transmit
		public double maxTransmitDistance
		{
			get
			{
				return Math.Sqrt (this.maxPowerFactor) * this.nominalRange;
			}
		}

		// The distance from Kerbin at which the antenna will perform exactly as prescribed by packetResourceCost
		// and packetSize.
		[KSPField(isPersistant = false)]
		public double nominalRange = 1500000d;

		// The multiplier on packetResourceCost that defines the maximum power output of the antenna.  When the power
		// cost exceeds packetResourceCost * maxPowerFactor, transmission will fail.
		[KSPField(isPersistant = false)]
		public float maxPowerFactor = 8f;

		// The multipler on packetSize that defines the maximum data bandwidth of the antenna.
		[KSPField(isPersistant = false)]
		public float maxDataFactor = 4f;

		public float DataRate
		{
			get
			{
				return dataTransmitter.DataRate;
			}
		}

		public double DataResourceCost
		{
			get
			{
				return dataTransmitter.DataRate;
			}
		}

		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base()
		{
			dataTransmitter = new ModuleDataTransmitter ();
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (state >= PartModule.StartState.PreLaunch && this._Kerbin == null)
			{
				// Go fetch Kerbin, because it is tricksy and hides from us.
				List<CelestialBody> bodies = FlightGlobals.Bodies;

				foreach (CelestialBody body in bodies)
				{
					if (body.name == "Kerbin")
					{
						this._Kerbin = body;
						break;
					}
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			dataTransmitter.OnLoad (node);
			this._basepacketSize = dataTransmitter.packetSize;
			this._basepacketResourceCost = dataTransmitter.packetResourceCost;
		}

		// Post an error in the communication messages describing the reason transmission has failed.  Currently there
		// is only one reason for this.
		protected void PostCannotTransmitError()
		{
			string ErrorText = String.Format(
				"Unable to transmit: out of range!  Maximum range = {0}; Current range = {1}.",
				this.maxTransmitDistance,
				this.transmitDistance
				);
			ScreenMessages.PostScreenMessage (new ScreenMessage (ErrorText, 4f, ScreenMessageStyle.UPPER_LEFT));
		}

		protected void PreTransmit_SetpacketSize()
		{
			if (this.transmitDistance >= this.nominalRange)
			{
				dataTransmitter.packetSize = this._basepacketSize;
			}
			else
			{
				// From above, data rate increases with the inverse square of the distance.
				dataTransmitter.packetSize = Math.Min(this._basepacketSize
				                           * (float)Math.Pow(this.nominalRange / this.transmitDistance, 2),
				                           this._basepacketSize * this.maxDataFactor);
			}
		}

		protected void PreTransmit_SetpacketResourceCost()
		{
			if (this.transmitDistance <= this.nominalRange)
			{
				dataTransmitter.packetResourceCost = this._basepacketResourceCost;
			}
			else
			{
				// From above, power increases with the square of the distance.
				dataTransmitter.packetResourceCost = this._basepacketResourceCost
					* (float)Math.Pow (this.transmitDistance / this.nominalRange, 2);
			}
		}

		// Override ModuleDataTransmitter.GetInfo to add nominal and maximum range to the VAB description.
		public override string GetInfo()
		{
			string text = dataTransmitter.GetInfo();
			text += "Nominal Range: " + this.nominalRange.ToString() + "\n";
			text += "Maximum Range: " + this.maxTransmitDistance.ToString() + "\n";
			return text;
		}

		// Override ModuleDataTransmitter.CanTransmit to return false when transmission is not possible.
		public bool CanTransmit()
		{
			if (this.transmitDistance > this.maxTransmitDistance)
			{
				return false;
			}
			return true;
		}

		public bool IsBusy()
		{
			return dataTransmitter.IsBusy ();
		}

		// Override ModuleDataTransmitter.TransmitData to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public void TransmitData(List<ScienceData> dataQueue)
		{
			dataTransmitter.TransmitData(dataQueue);
//			this.PreTransmit_SetpacketSize ();
//			this.PreTransmit_SetpacketResourceCost ();
//
//			Tools.PostDebugMessage(
//				"Attempting to TransmitData. Distance: " + this.transmitDistance.ToString()
//				+ " packetSize: " + this.packetSize.ToString()
//				+ " packetResourceCost: " + this.packetResourceCost.ToString()
//				);
//			if (this.CanTransmit())
//			{
//				base.TransmitData(dataQueue);
//			}
//			else
//			{
//				this.PostCannotTransmitError ();
//			}
		}

		// Override ModuleDataTransmitter.StartTransmission to check against CanTransmit and fail out when CanTransmit
		// returns false.
		[KSPEvent (guiName = "Transmit Data", active = true, guiActive = true)]
		public void StartTransmission()
		{
			dataTransmitter.StartTransmission ();
//			this.PreTransmit_SetpacketSize ();
//			this.PreTransmit_SetpacketResourceCost ();
//
//			Tools.PostDebugMessage(
//				"Attempting to TransmitData. Distance: " + this.transmitDistance.ToString()
//				+ " packetSize: " + this.packetSize.ToString()
//				+ " packetResourceCost: " + this.packetResourceCost.ToString()
//				);
//			if (this.CanTransmit())
//			{
//				base.StartTransmission();
//			}
//			else
//			{
//				this.PostCannotTransmitError ();
//			}
		}

		[KSPEvent (guiName = "Stop Transmitting", active = true, guiActive = true)]
		public void StopTransmission()
		{
			dataTransmitter.StopTransmission ();
		}
	}

	public static class Tools
	{
		[System.Diagnostics.Conditional("DEBUG")]
		public static void PostDebugMessage(string Str)
		{
			ScreenMessage Message = new ScreenMessage (Str, 4f, ScreenMessageStyle.UPPER_RIGHT);
			ScreenMessages.PostScreenMessage (Message);
		}
	}
}

