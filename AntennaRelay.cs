// AntennaRange
//
// AntennaRelay.cs
//
// Copyright © 2014-2015, toadicus
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice,
//    this list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation and/or other
//    materials provided with the distribution.
//
// 3. Neither the name of the copyright holder nor the names of its contributors may be used
//    to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using ToadicusTools;

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
		/// Gets the first <see cref="CelestialBody"/> found to be blocking line of sight.
		/// </summary>
		public virtual CelestialBody firstOccludingBody
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
			CelestialBody fob = null;

			if (
				this.transmitDistance > this.maxTransmitDistance ||
				(
					ARConfiguration.RequireLineOfSight &&
					this.nearestRelay == null &&
					!this.vessel.hasLineOfSightTo(this.Kerbin, out fob, ARConfiguration.RadiusRatio)
				)
			)
			{
				this.firstOccludingBody = fob;
				return false;
			}
			else
			{
				this.firstOccludingBody = null;
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

			this.firstOccludingBody = null;

			// Set this vessel as checked, so that we don't check it again.
			RelayDatabase.Instance.CheckedVesselsTable[vessel.id] = true;

			double nearestSqrDistance = double.PositiveInfinity;
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
				if (RelayDatabase.Instance.CheckedVesselsTable.ContainsKey(potentialVessel.id))
				{
					continue;
				}

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

				// Skip vessels to which we do not have line of sight.
				CelestialBody fob = null;

				if (ARConfiguration.RequireLineOfSight &&
					!this.vessel.hasLineOfSightTo(potentialVessel, out fob, ARConfiguration.RadiusRatio))
				{
					this.firstOccludingBody = fob;
					Tools.PostDebugMessage(
						this,
						"Vessel {0} discarded because we do not have line of sight.",
						potentialVessel.vesselName
					);
					continue;
				}

				this.firstOccludingBody = null;

				// Find the distance from here to the vessel...
				double potentialSqrDistance = (potentialVessel.GetWorldPos3D() - vessel.GetWorldPos3D()).sqrMagnitude;

				/*
				 * ...so that we can skip the vessel if it is further away than Kerbin, our transmit distance, or a
				 * vessel we've already checked.
				 * */
				if (
					potentialSqrDistance > Tools.Min(
						this.maxTransmitDistance * this.maxTransmitDistance,
						nearestSqrDistance,
						this.vessel.sqrDistanceTo(Kerbin)
					)
				)
				{
					Tools.PostDebugMessage(
						this,
						"Vessel {0} discarded because it is out of range, or farther than another relay.",
						potentialVessel.vesselName
					);
					continue;
				}

				nearestSqrDistance = potentialSqrDistance;

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

