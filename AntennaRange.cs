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
 * This software uses the ModuleManager library © 2013 ialdabaoth, used under a Creative Commons Attribution-ShareAlike 3.0 Uported License.
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
	public class ModuleLimitedDataTransmitter : ModuleDataTransmitter, IScienceDataTransmitter
	{
		// Stores the packetResourceCost as defined in the .cfg file.
		protected float _basepacketResourceCost;

		// Stores the packetSize as defined in the .cfg file.
		protected float _basepacketSize;

		// We don't have a Bard, so we're hiding Kerbin here.
		protected CelestialBody _Kerbin;

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

		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base() { }

		public override void OnStart (StartState state)
		{
			base.OnStart (state);

			if (state >= StartState.PreLaunch && this._Kerbin == null)
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
			base.OnLoad (node);
			this._basepacketSize = base.packetSize;
			this._basepacketResourceCost = base.packetResourceCost;
		}

		// Post an error in the communication messages describing the reason transmission has failed.  Currently there
		// is only one reason for this.
		protected void PostCannotTransmitError()
		{
			string ErrorText = string.Format (
				"Unable to transmit: out of range!  Maximum range = {0}; Current range = {1}.",
				this.maxTransmitDistance,
				this.transmitDistance);
			ScreenMessages.PostScreenMessage (new ScreenMessage (ErrorText, 4f, ScreenMessageStyle.UPPER_LEFT));
		}

		protected void PreTransmit_SetPacketResourceCost()
		{
			if (this.transmitDistance <= this.nominalRange)
			{
				base.packetResourceCost = this._basepacketResourceCost;
			}
			else
			{
				base.packetResourceCost = this._basepacketResourceCost
					* (float)Math.Pow (this.transmitDistance / this.nominalRange, 2);
			}
		}

		protected void PreTransmit_SetPacketSize()
		{
			if (this.transmitDistance >= this.nominalRange)
			{
				base.packetSize = this._basepacketSize;
			}
			else
			{
				base.packetSize = Math.Min (
					this._basepacketSize * (float)Math.Pow (this.nominalRange / this.transmitDistance, 2),
					this._basepacketSize * this.maxDataFactor);
			}
		}

		// Override ModuleDataTransmitter.GetInfo to add nominal and maximum range to the VAB description.
		public override string GetInfo()
		{
			string text = base.GetInfo();
			text += "Nominal Range: " + this.nominalRange.ToString() + "\n";
			text += "Maximum Range: " + this.maxTransmitDistance.ToString() + "\n";
			return text;
		}

		// Override ModuleDataTransmitter.CanTransmit to return false when transmission is not possible.
		public new bool CanTransmit()
		{
			if (this.transmitDistance > this.maxTransmitDistance)
			{
				return false;
			}
			return true;
		}

		// Override ModuleDataTransmitter.TransmitData to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public new void TransmitData(List<ScienceData> dataQueue)
		{
			PreTransmit_SetPacketSize ();
			PreTransmit_SetPacketResourceCost ();

			Tools.PostDebugMessage (
				"distance: " + this.transmitDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
			);
			if (this.CanTransmit())
			{
				base.TransmitData(dataQueue);
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}

		// Override ModuleDataTransmitter.StartTransmission to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public new void StartTransmission()
		{
			PreTransmit_SetPacketSize ();
			PreTransmit_SetPacketResourceCost ();

			Tools.PostDebugMessage (
				"distance: " + this.transmitDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
				);
			if (this.CanTransmit())
			{
				base.StartTransmission();
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}

		#if DEBUG
		[KSPEvent (guiName = "Show Debug Info", active = true, guiActive = true)]
		public void DebugInfo()
		{
			PreTransmit_SetPacketSize ();
			PreTransmit_SetPacketResourceCost ();

			string msg = string.Format(
				"'{0}'\n" + 
				"_basepacketSize: {1}\n" +
				"packetSize: {2}\n" +
				"_basepacketResourceCost: {3}\n" +
				"packetResourceCost: {4}\n" +
				"maxTransmitDistance: {5}\n" +
				"transmitDistance: {6}\n" +
				"nominalRange: {7}\n" +
				"CanTransmit: {8}",
				this.name,
				this._basepacketSize,
				base.packetSize,
				this._basepacketResourceCost,
				base.packetResourceCost,
				this.maxTransmitDistance,
				this.transmitDistance,
				this.nominalRange,
				this.CanTransmit()
				);
			ScreenMessages.PostScreenMessage (new ScreenMessage (msg, 30f, ScreenMessageStyle.UPPER_RIGHT));
		}
		#endif
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

