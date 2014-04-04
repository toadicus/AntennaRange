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
using System.Linq;

namespace AntennaRange
{
	/*
	 * Wrapper class for ProtoPartModuleSnapshot extending AntennaRelay and implementing IAntennaRelay.
	 * This is used for finding relays in unloaded Vessels.
	 * */
	public class ProtoAntennaRelay : AntennaRelay, IAntennaRelay
	{
		// Stores the relay prefab
		protected IAntennaRelay relayPrefab;

		// Stores the prototype part so we can make sure we haven't exploded or so.
		protected ProtoPartSnapshot protoPart;

		/// <summary>
		/// The maximum distance at which this transmitter can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public override float maxTransmitDistance
		{
			get
			{
				return relayPrefab.maxTransmitDistance;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		public override bool relayChecked
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets the underlying part's title.
		/// </summary>
		/// <value>The title.</value>
		public string title
		{
			get
			{
				return this.protoPart.partInfo.title;
			}
		}

		public override bool CanTransmit()
		{
			PartStates partState = (PartStates)this.protoPart.state;
			if (partState == PartStates.DEAD || partState == PartStates.DEACTIVATED)
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: {1} on {2} cannot transmit: {3}",
					this.GetType().Name,
					this.title,
					this.vessel.vesselName,
					Enum.GetName(typeof(PartStates), partState)
				));
				return false;
			}
			return base.CanTransmit();
		}

		public override string ToString()
		{
			return string.Format(
				"{0} on {1}.",
				this.title,
				this.protoPart.pVesselRef.vesselName
			);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoAntennaRelay"/> class.
		/// </summary>
		/// <param name="ms">The ProtoPartModuleSnapshot to wrap</param>
		/// <param name="vessel">The parent Vessel</param>
		public ProtoAntennaRelay(IAntennaRelay prefabRelay, ProtoPartSnapshot pps) : base(pps.pVesselRef.vesselRef)
		{
			this.relayPrefab = prefabRelay;
			this.protoPart = pps;
			this.vessel = pps.pVesselRef.vesselRef;
		}

		~ProtoAntennaRelay()
		{
			Tools.PostDebugMessage(string.Format(
				"{0}: destroyed",
				this.ToString()
			));
		}
	}
}

