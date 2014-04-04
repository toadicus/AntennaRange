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

namespace AntennaRange
{
	public class AntennaRelay
	{
		// We don't have a Bard, so we'll hide Kerbin here.
		protected CelestialBody Kerbin;

		protected IAntennaRelay _nearestRelayCache;
		protected IAntennaRelay moduleRef;

		protected System.Diagnostics.Stopwatch searchTimer;
		protected long millisecondsBetweenSearches;

		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		/// <value>The parent Vessel.</value>
		public virtual Vessel vessel
		{
			get
			{
				return this.moduleRef.vessel;
			}
		}

		/// <summary>
		/// Gets or sets the nearest relay.
		/// </summary>
		/// <value>The nearest relay</value>
		public IAntennaRelay nearestRelay
		{
			get
			{
				if (this.searchTimer.IsRunning &&
					this.searchTimer.ElapsedMilliseconds > this.millisecondsBetweenSearches)
				{
					this._nearestRelayCache = this.FindNearestRelay();
					this.searchTimer.Restart();
				}

				return this._nearestRelayCache;
			}
			protected set
			{
				this._nearestRelayCache = value;
			}
		}

		/// <summary>
		/// Gets the transmit distance.
		/// </summary>
		/// <value>The transmit distance.</value>
		public double transmitDistance
		{
			get
			{
				this.nearestRelay = this.FindNearestRelay();

				// If there is no available relay nearby...
				if (this.nearestRelay == null)
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

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public virtual float maxTransmitDistance
		{
			get;
			set;
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		public virtual bool relayChecked
		{
			get;
			protected set;
		}

		/// <summary>
		/// Determines whether this instance can transmit.
		/// </summary>
		/// <returns><c>true</c> if this instance can transmit; otherwise, <c>false</c>.</returns>
		public virtual bool CanTransmit()
		{
			if (this.transmitDistance > this.maxTransmitDistance)
			{
				return false;
			}
			else
			{
				return true;
			}
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

			Tools.PostDebugMessage(string.Format(
				"{0}: finding nearest relay for {1} ({2})",
				this.GetType().Name,
				this,
				this.vessel.id
			));

			// Set this vessel as checked, so that we don't check it again.
			RelayDatabase.Instance.CheckedVesselsTable[vessel.id] = true;

			double nearestDistance = double.PositiveInfinity;
			IAntennaRelay _nearestRelay = null;

			/*
			 * Loop through all the vessels and exclude this vessel, vessels of the wrong type, and vessels that are too
			 * far away.  When we find a candidate, get through its antennae for relays which have not been checked yet
			 * and that can transmit.  Once we find a suitable candidate, assign it to _nearestRelay for comparison
			 * against future finds.
			 * */
			foreach (Vessel potentialVessel in FlightGlobals.Vessels)
			{
				// Skip vessels that have already been checked for a nearest relay this pass.
				try
				{
					if (RelayDatabase.Instance.CheckedVesselsTable[potentialVessel.id])
					{
						continue;
					}
				}
				catch (KeyNotFoundException) { /* If the key doesn't exist, don't skip it. */}

				// Skip vessels of the wrong type.
				switch (potentialVessel.vesselType)
				{
					case VesselType.Debris:
					case VesselType.Flag:
					case VesselType.EVA:
					case VesselType.SpaceObject:
					case VesselType.Unknown:
						continue;
					default:
						break;
				}

				// Skip vessels with the wrong ID
				if (potentialVessel.id == vessel.id)
				{
					continue;
				}

				// Find the distance from here to the vessel...
				double potentialDistance = (potentialVessel.GetWorldPos3D() - vessel.GetWorldPos3D()).magnitude;

				/*
				 * ...so that we can skip the vessel if it is further away than Kerbin, our transmit distance, or a
				 * vessel we've already checked.
				 * */
				if (potentialDistance > Tools.Min(this.maxTransmitDistance, nearestDistance, vessel.DistanceTo(Kerbin)))
				{
					continue;
				}

				nearestDistance = potentialDistance;

				foreach (IAntennaRelay potentialRelay in potentialVessel.GetAntennaRelays())
				{
					if (potentialRelay.CanTransmit())
					{
						_nearestRelay = potentialRelay;
						Tools.PostDebugMessage(string.Format("{0}: found new best relay {1} ({2})",
							this.GetType().Name,
							_nearestRelay.ToString(),
							_nearestRelay.vessel.id
						));
						break;
					}
				}
			}

			// Now that we're done with our recursive CanTransmit checks, flag this relay as not checked so it can be
			// used next time.
			RelayDatabase.Instance.CheckedVesselsTable.Remove(vessel.id);

			// Return the nearest available relay, or null if there are no available relays nearby.
			return _nearestRelay;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoDataTransmitter"/> class.
		/// </summary>
		/// <param name="ms"><see cref="ProtoPartModuleSnapshot"/></param>
		public AntennaRelay(IAntennaRelay module)
		{
			this.moduleRef = module;

			this.searchTimer = new System.Diagnostics.Stopwatch();
			this.millisecondsBetweenSearches = 5000;

			// HACK: This might not be safe in all circumstances, but since AntennaRelays are not built until Start,
			// we hope it is safe enough.
			this.Kerbin = FlightGlobals.Bodies.FirstOrDefault(b => b.name == "Kerbin");
		}
	}
}

