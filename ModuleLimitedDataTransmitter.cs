// AntennaRange © 2014 toadicus
//
// AntennaRange provides incentive and requirements for the use of the various antenna parts.
// Nominally, the breakdown is as follows:
//
//     Communotron 16 - Suitable up to Kerbalsynchronous Orbit
//     Comms DTS-M1 - Suitable throughout the Kerbin subsystem
//     Communotron 88-88 - Suitable throughout the Kerbol system.
//
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a
// copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/
//
// This software uses the ModuleManager library © 2013 ialdabaoth, used under a Creative Commons Attribution-ShareAlike
// 3.0 Uported License.
//
// This software uses code from the MuMechLib library, © 2013 r4m0n, used under the GNU GPL version 3.

using System;
using System.Collections.Generic;
using System.Linq;
using KSP;
using UnityEngine;

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

	/*
	 * Fields
	 * */
	public class ModuleLimitedDataTransmitter : ModuleDataTransmitter, IScienceDataTransmitter, IAntennaRelay
	{
		protected System.Diagnostics.Stopwatch searchTimer;
		protected long millisecondsBetweenSearches;

		// Stores the packetResourceCost as defined in the .cfg file.
		protected float _basepacketResourceCost;

		// Stores the packetSize as defined in the .cfg file.
		protected float _basepacketSize;

		// Keep track of vessels with transmitters for relay purposes.
		protected List<Vessel> _relayVessels;

		// Sometimes we will need to communicate errors; this is how we do it.
		protected ScreenMessage ErrorMsg;

		// Let's make the error text pretty!
		protected UnityEngine.GUIStyle ErrorStyle;

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

		protected CelestialBody _Kerbin;

		/*
		 * Properties
		 * */
		// We don't have a Bard, so we'll hide Kerbin here.
		protected CelestialBody Kerbin
		{
			get
			{
				if (this._Kerbin == null)
				{
					foreach (CelestialBody cb in FlightGlobals.Bodies)
					{
						if (cb.name == "Kerbin")
						{
							this._Kerbin = cb;
							break;
						}
					}
				}
				return this._Kerbin;
			}
		}

		/// <summary>
		/// Gets or sets the nearest relay.
		/// </summary>
		/// <value>The nearest relay</value>
		public IAntennaRelay nearestRelay
		{
			get;
			protected set;
		}


		// Returns the distance to the nearest relay or Kerbin, whichever is closer.
		public double transmitDistance
		{
			get
			{
				this.nearestRelay = this.FindNearestRelay();

				// If there is no available relay nearby...
				if (nearestRelay == null)
				{
					// .. return the distance to Kerbin
					return this.DistanceTo(this.Kerbin);
				}
				else
				{
					/// ...otherwise, return the distance to the nearest available relay.
					return this.DistanceTo(nearestRelay);
				}
			}
		}

		// Returns the maximum distance this module can transmit
		public float maxTransmitDistance
		{
			get
			{
				return Mathf.Sqrt (this.maxPowerFactor) * this.nominalRange;
			}
		}

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
				this.PreTransmit_SetPacketSize();

				if (this.CanTransmit())
				{
					return this.packetSize;
				}
				else
				{
					return float.Epsilon;
				}
			}
		}

		// Override ModuleDataTransmitter.DataResourceCost to just return packetResourceCost, because we want antennas
		// to be scored in terms of joules/byte
		public new float DataResourceCost
		{
			get
			{
				this.PreTransmit_SetPacketResourceCost();

				if (this.CanTransmit())
				{
					return this.packetResourceCost;
				}
				else
				{
					return float.PositiveInfinity;
				}
			}
		}

		// Reports whether this antenna has been checked as a viable relay already in the current FindNearestRelay.
		public bool relayChecked
		{
			get;
			protected set;
		}

		/*
		 * Methods
		 * */
		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base()
		{
			// Make the error posting prettier.
			this.ErrorStyle = new UnityEngine.GUIStyle();
			this.ErrorStyle.normal.textColor = (UnityEngine.Color)XKCDColors.OrangeRed;
			this.ErrorStyle.active.textColor = (UnityEngine.Color)XKCDColors.OrangeRed;
			this.ErrorStyle.hover.textColor = (UnityEngine.Color)XKCDColors.OrangeRed;
			this.ErrorStyle.fontStyle = UnityEngine.FontStyle.Normal;
			this.ErrorStyle.padding.top = 32;

			this.ErrorMsg = new ScreenMessage("", 4f, false, ScreenMessageStyle.UPPER_LEFT, this.ErrorStyle);
		}

		// At least once, when the module starts with a state on the launch pad or later, go find Kerbin.
		public override void OnStart (StartState state)
		{
			base.OnStart (state);
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

			this.searchTimer = new System.Diagnostics.Stopwatch();
			this.millisecondsBetweenSearches = 5000;

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
				"Unable to transmit: out of range!  Maximum range = {0}m; Current range = {1}m.",
				Tools.MuMech_ToSI((double)this.maxTransmitDistance, 2),
				Tools.MuMech_ToSI((double)this.transmitDistance, 2)
				);

			this.ErrorMsg.message = ErrorText;

			ScreenMessages.PostScreenMessage(this.ErrorMsg, true);
		}

		// Before transmission, set packetResourceCost.  Per above, packet cost increases with the square of
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
			text += "Nominal Range: " + Tools.MuMech_ToSI((double)this.nominalRange, 2) + "m\n";
			text += "Maximum Range: " + Tools.MuMech_ToSI((double)this.maxTransmitDistance, 2) + "m\n";
			return text;
		}

		// Override ModuleDataTransmitter.CanTransmit to return false when transmission is not possible.
		public new bool CanTransmit()
		{
			Tools.PostDebugMessage(string.Format(
				"{0}: Checking if {1} on {2} can transmit.",
				this.GetType().Name,
				base.part.name,
				this.vessel
			));

			if (this.transmitDistance > this.maxTransmitDistance)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		// Override ModuleDataTransmitter.TransmitData to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public new void TransmitData(List<ScienceData> dataQueue)
		{
			if (this.CanTransmit())
			{
				base.TransmitData(dataQueue);
			}
			else
			{
				this.PostCannotTransmitError ();
			}

			Tools.PostDebugMessage (
				"distance: " + this.transmitDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
			);
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		/// <returns>The nearest relay or null, if no relays in range.</returns>
		public IAntennaRelay FindNearestRelay()
		{
			if (this.searchTimer.IsRunning && this.searchTimer.ElapsedMilliseconds < this.millisecondsBetweenSearches)
			{
				return this.nearestRelay;
			}

			if (this.searchTimer.IsRunning)
			{
				this.searchTimer.Stop();
				this.searchTimer.Reset();
			}

			this.searchTimer.Start();

			// Set this relay as checked, so that we don't check it again.
			this.relayChecked = true;

			// Get a list of vessels within transmission range.
			List<Vessel> nearbyVessels = FlightGlobals.Vessels
				.Where(v => (v.GetWorldPos3D() - vessel.GetWorldPos3D()).magnitude < this.maxTransmitDistance)
				.ToList();

			nearbyVessels.RemoveAll(v => v.vesselType == VesselType.Debris);

			Tools.PostDebugMessage(string.Format(
				"{0}: Non-debris vessels in range: {1}",
				this.GetType().Name,
				nearbyVessels.Count
			));

			// Remove this vessel.
			nearbyVessels.RemoveAll(v => v.id == vessel.id);

			Tools.PostDebugMessage(string.Format(
				"{0}: Vessels in range excluding self: {1}",
				this.GetType().Name,
				nearbyVessels.Count
			));

			// Get a flattened list of all IAntennaRelay modules and protomodules in transmission range.
			List<IAntennaRelay> nearbyRelays = nearbyVessels.SelectMany(v => v.GetAntennaRelays()).ToList();

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays.",
				this.GetType().Name,
				nearbyRelays.Count
			));

			// Remove all relays already checked this time.
			nearbyRelays.RemoveAll(r => r.relayChecked);

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays not already checked.",
				this.GetType().Name,
				nearbyRelays.Count
			));

			// Remove all relays that cannot transmit.
			// This call to r.CanTransmit() starts a depth-first recursive search for relays with a path back to Kerbin.
			nearbyRelays.RemoveAll(r => !r.CanTransmit());

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays not already checked that can transmit.",
				this.GetType().Name,
				nearbyRelays.Count
			));

			// Sort the available relays by distance.
			nearbyRelays.Sort(new RelayComparer(this.vessel));

			// Get the nearest available relay, or null if there are no available relays nearby.
			IAntennaRelay _nearestRelay = nearbyRelays.FirstOrDefault();

			// If we have a nearby relay...
			if (_nearestRelay != null)
			{
				// ...but that relay is farther than Kerbin...
				if (this.DistanceTo(_nearestRelay) > this.DistanceTo(Kerbin))
				{
					// ...just use Kerbin.
					_nearestRelay = null;
				}
			}

			// Now that we're done with our recursive CanTransmit checks, flag this relay as not checked so it can be
			// used next time.
			this.relayChecked = false;

			// Return the nearest available relay, or null if there are no available relays nearby.
			return _nearestRelay;
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
				string message;

				message = "Beginning transmission ";

				if (this.nearestRelay == null)
				{
					message += "directly to Kerbin.";
				}
				else
				{
					message += "via relay " + this.nearestRelay;
				}

				ScreenMessages.PostScreenMessage(message, 4f, ScreenMessageStyle.UPPER_LEFT);

				base.StartTransmission();
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}


		/*		
		 * Class implementing IComparer<IAntennaRelay> for use in sorting relays by distance.
		 * */
		internal class RelayComparer : IComparer<IAntennaRelay>
		{
			/// <summary>
			/// The reference Vessel (usually the active vessel).
			/// </summary>
			protected Vessel referenceVessel;

			// We don't want no stinking public parameterless constructors.
			private RelayComparer() {}

			/// <summary>
			/// Initializes a new instance of the <see cref="AntennaRange.AntennaRelay+RelayComparer"/> class for use
			/// in sorting relays by distance.
			/// </summary>
			/// <param name="reference">The reference Vessel</param>
			public RelayComparer(Vessel reference)
			{
				this.referenceVessel = reference;
			}

			/// <summary>
			/// Compare the <see cref="IAntennaRelay"/>s "one" and "two".
			/// </summary>
			/// <param name="one">The first IAntennaRelay in the comparison</param>
			/// <param name="two">The second IAntennaRelay in the comparison</param>
			public int Compare(IAntennaRelay one, IAntennaRelay two)
			{
				double distanceOne;
				double distanceTwo;

				distanceOne = one.vessel.DistanceTo(referenceVessel);
				distanceTwo = two.vessel.DistanceTo(referenceVessel);

				return distanceOne.CompareTo(distanceTwo);
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
				"TransmitterScore: {11}\n" +
				"NearestRelay: {12}",
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
				ScienceUtil.GetTransmitterScore(this),
				this.FindNearestRelay()
				);
			ScreenMessages.PostScreenMessage (new ScreenMessage (msg, 4f, ScreenMessageStyle.UPPER_RIGHT));
		}
		#endif
	}
}