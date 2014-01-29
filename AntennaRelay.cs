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

		protected System.Diagnostics.Stopwatch searchTimer;
		protected long millisecondsBetweenSearches;

		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		/// <value>The parent Vessel.</value>
		public Vessel vessel
		{
			get;
			protected set;
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

			// Set this relay as checked, so that we don't check it again.
			this.relayChecked = true;

			// Get a list of vessels within transmission range.
			List<Vessel> nearbyVessels = FlightGlobals.Vessels
				.Where(v => (v.GetWorldPos3D() - vessel.GetWorldPos3D()).magnitude < this.maxTransmitDistance)
					.ToList();

			nearbyVessels.RemoveAll(v => v.vesselType == VesselType.Debris);
			nearbyVessels.RemoveAll(v => v.vesselType == VesselType.Flag);

			Tools.PostDebugMessage(string.Format(
				"{0}: Non-debris, non-flag vessels in range: {1}",
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

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoDataTransmitter"/> class.
		/// </summary>
		/// <param name="ms"><see cref="ProtoPartModuleSnapshot"/></param>
		public AntennaRelay(Vessel v)
		{
			this.vessel = v;

			this.searchTimer = new System.Diagnostics.Stopwatch();
			this.millisecondsBetweenSearches = 5000;

			// HACK: This might not be safe in all circumstances, but since AntennaRelays are not built until Start,
			// we hope it is safe enough.
			this.Kerbin = FlightGlobals.Bodies.FirstOrDefault(b => b.name == "Kerbin");
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
	}
}

