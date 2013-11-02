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
		public float nominalRange;

		// The multiplier on packetResourceCost that defines the maximum power output of the antenna.  When the power
		// cost exceeds packetResourceCost * maxPowerFactor, transmission will fail.
		[KSPField(isPersistant = false)]
		public float maxPowerFactor;

		// The multipler on packetSize that defines the maximum data bandwidth of the antenna.
		[KSPField(isPersistant = false)]
		public float maxDataFactor;


		/*
		 * The next two functions overwrite the behavior of the stock functions and do not perform equivalently, except
		 * in that they both return floats.  Here's some quick justification:
		 * 
		 * The stock implementation of GetTransmitterScore (which I cannot override) is:
		 * 		Score = (1 + DataResourceCost) / DataRate
		 * 
		 * The stock DataRate and DataResourceCost are:
		 * 		DataRate = packetSize / packetInterval
		 * 		DataResourceCost = packetResourceCost / packetSize
		 * 
		 * So, the resulting score is essentially in terms of joules per byte per baud.  Rearranging that a bit, it
		 * could also look like joule-seconds per byte per byte, or newton-meter-seconds per byte per byte.  Either way,
		 * that metric is not a very reasonable one.
		 * 
		 * Two metrics that might make more sense are joules per byte or joules per byte per second.  The latter case
		 * would look like:
		 * 		DataRate = packetSize / packetInterval
		 * 		DataResourceCost = packetResourceCost
		 * 
		 * The former case, which I've chosen to implement below, is:
		 * 		DataRate = packetSize
		 * 		DataResourceCost = packetResourceCost
		 * 
		 * So... hopefully that doesn't screw with anything else.
		 * */
		// Override ModuleDataTransmitter.DataRate to just return packetSize, because we want antennas to be scored in
		// terms of joules/byte
		public new float DataRate
		{
			get
			{
				PreTransmit_SetPacketSize();
				return this.packetSize;
			}
		}

		// Override ModuleDataTransmitter.DataResourceCost to just return packetResourceCost, because we want antennas
		// to be scored in terms of joules/byte
		public new float DataResourceCost
		{
			get
			{
				PreTransmit_SetPacketResourceCost();
				return this.packetResourceCost;
			}
		}

		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base() { }

		// At least once, when the module starts with a state on the launch pad or later, go find Kerbin.
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

		// When the module loads, fetch the Squad KSPFields from the base.  This is necessary in part because
		// overloading packetSize and packetResourceCostinto a property in ModuleLimitedDataTransmitter didn't
		// work.
		public override void OnLoad(ConfigNode node)
		{
			this.Fields.Load(node);
			base.Fields.Load(node);

			base.OnLoad (node);

			this._basepacketSize = base.packetSize;
			this._basepacketResourceCost = base.packetResourceCost;

			Tools.PostDebugMessage(string.Format(
				"{0} loaded:\n" +
				"packetSize: {1}\n" +
				"packetResourceCost: {2}\n" +
				"nominalRange: {3}\n" +
				"maxPowerFactor: {4}\n" +
				"maxDataFactor: {5}\n",
				this.name,
				base.packetSize,
				this._basepacketResourceCost,
				this.nominalRange,
				this.maxPowerFactor,
				this.maxDataFactor
			));
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

		// Before transmission, set packetResourceCost.  Per above, packet size increases with the square of
		// distance.  packetResourceCost maxes out at _basepacketResourceCost * maxPowerFactor, at which point
		// transmission fails (see CanTransmit).
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

		// Before transmission, set packetSize.  Per above, packet size increases with the inverse square of
		// distance.  packetSize maxes out at _basepacketSize * maxDataFactor.
		protected void PreTransmit_SetPacketSize()
		{
			if (this.transmitDistance >= this.nominalRange)
			{
				base.packetSize = this._basepacketSize;
			}
			else
			{
				base.packetSize = Math.Min(
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

		// When debugging, it's nice to have a button that just tells you everything.
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
				"CanTransmit: {8}\n" +
				"DataRate: {9}\n" +
				"DataResourceCost: {10}\n" +
				"TransmitterScore: {11}",
				this.name,
				this._basepacketSize,
				base.packetSize,
				this._basepacketResourceCost,
				base.packetResourceCost,
				this.maxTransmitDistance,
				this.transmitDistance,
				this.nominalRange,
				this.CanTransmit(),
				this.DataRate,
				this.DataResourceCost,
				ScienceUtil.GetTransmitterScore(this)
				);
			ScreenMessages.PostScreenMessage (new ScreenMessage (msg, 4f, ScreenMessageStyle.UPPER_RIGHT));
		}
		#endif
	}

	public static class Tools
	{
		// When debugging, be verbose.  The Conditional attribute prevents this from firing when not DEBUGging.
		[System.Diagnostics.Conditional("DEBUG")]
		public static void PostDebugMessage(string Msg)
		{
			if (HighLogic.LoadedScene > GameScenes.SPACECENTER)
			{
				ScreenMessage Message = new ScreenMessage(Msg, 4f, ScreenMessageStyle.LOWER_CENTER);
				ScreenMessages.PostScreenMessage(Message);
			}
			else
			{
				KSPLog.print(Msg);
			}
		}
	}
}
