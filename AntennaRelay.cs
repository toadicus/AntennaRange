using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaRange
{
	public class AntennaRelay : IAntennaRelay
	{
		protected CelestialBody Kerbin;

		public Vessel vessel
		{
			get;
			protected set;
		}

		// Returns the current distance to the center of Kerbin, which is totally where the Kerbals keep their radioes.
		public double transmitDistance
		{
			get
			{
				IAntennaRelay nearestRelay = this.FindNearestRelay();

				if (nearestRelay == null)
				{
					return this.DistanceTo(this.Kerbin);
				}
				else
				{
					return this.DistanceTo(nearestRelay);
				}
			}
		}

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

		public bool CanTransmit()
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
		/// <returns>The nearest relay.</returns>
		public IAntennaRelay FindNearestRelay()
		{
			this.relayChecked = true;

			List<Vessel> nearbyVessels = FlightGlobals.Vessels
				.Where(v => (v.GetWorldPos3D() - vessel.GetWorldPos3D()).magnitude < this.maxTransmitDistance)
					.ToList();

			Tools.PostDebugMessage(string.Format(
				"{0}: Vessels in range: {1}",
				this.GetType().Name,
				nearbyVessels.Count
				));

			nearbyVessels.RemoveAll(v => v.id == vessel.id);

			Tools.PostDebugMessage(string.Format(
				"{0}: Vessels in range excluding self: {1}",
				this.GetType().Name,
				nearbyVessels.Count
				));

			List<IAntennaRelay> nearbyRelays = nearbyVessels.SelectMany(v => v.GetAntennaRelays()).ToList();

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays.",
				this.GetType().Name,
				nearbyRelays.Count
				));

			nearbyRelays.RemoveAll(r => r.relayChecked);

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays not already checked.",
				this.GetType().Name,
				nearbyRelays.Count
				));

			nearbyRelays.RemoveAll(r => !r.CanTransmit());

			Tools.PostDebugMessage(string.Format(
				"{0}: Found {1} nearby relays not already checked that can transmit.",
				this.GetType().Name,
				nearbyRelays.Count
				));

			nearbyRelays.Sort(new RelayComparer(this.vessel));

			IAntennaRelay nearestRelay = nearbyRelays.FirstOrDefault();

			this.relayChecked = false;

			return nearestRelay;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoDataTransmitter"/> class.
		/// </summary>
		/// <param name="ms"><see cref="ProtoPartModuleSnapshot"/></param>
		public AntennaRelay(Vessel v)
		{
			this.vessel = v;

			this.Kerbin = FlightGlobals.Bodies.FirstOrDefault(b => b.name == "Kerbin");
		}

		internal class RelayComparer : IComparer<IAntennaRelay>
		{
			protected Vessel referenceVessel;

			private RelayComparer() {}

			public RelayComparer(Vessel reference)
			{
				this.referenceVessel = reference;
			}

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

