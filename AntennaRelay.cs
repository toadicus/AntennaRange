// AntennaRange
//
// AntennaRelay.cs
//
// Copyright © 2014, toadicus
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

// @DONE TODO: Retool nearestRelay to always contain the nearest relay, even if out of range.
// @DONE TODO: Retool CanTransmit to not rely on nearestRelay == null.
// TODO: Track occluded vessels somehow.

namespace AntennaRange
{
	public class AntennaRelay
	{
		// We don't have a Bard, so we'll hide Kerbin here.
		private static CelestialBody _Kerbin;
		public static CelestialBody Kerbin
		{
			get
			{
				if (_Kerbin == null && FlightGlobals.ready)
				{
					_Kerbin = FlightGlobals.GetHomeBody();
				}

				return _Kerbin;
			}
		}

		protected bool canTransmit;

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
			get;
			protected set;
		}

		public IAntennaRelay bestOccludedRelay
		{
			get;
			protected set;
		}

		public IAntennaRelay targetRelay
		{
			get;
			protected set;
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
				this.FindNearestRelay();

				if (this.KerbinDirect || this.targetRelay == null)
				{
					return this.DistanceTo(Kerbin);
				}
				else
				{
					return this.DistanceTo(this.targetRelay);
				}
			}
		}

		public virtual double nominalTransmitDistance
		{
			get;
			set;
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

		public virtual bool KerbinDirect
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
			this.FindNearestRelay();
			return this.canTransmit;
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		/// <returns>The nearest relay or null, if no relays in range.</returns>
		private void FindNearestRelay()
		{
			if (!this.searchTimer.IsRunning || this.searchTimer.ElapsedMilliseconds > this.millisecondsBetweenSearches)
			{
				this.searchTimer.Reset();
			}
			else
			{
				return;
			}

			// Skip vessels that have already been checked for a nearest relay this pass.
			if (RelayDatabase.Instance.CheckedVesselsTable.ContainsKey(this.vessel.id))
			{
				return;
			}

			if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
			{
				Tools.PostLogMessage(string.Format(
					"{0}: finding nearest relay for {1}",
					this.GetType().Name,
					this.ToString()
				));
			}

			// Set this vessel as checked, so that we don't check it again.
			RelayDatabase.Instance.CheckedVesselsTable[vessel.id] = true;

			// Blank everything we're trying to find before the search.
			this.firstOccludingBody = null;
			this.bestOccludedRelay = null;
			this.targetRelay = null;
			this.nearestRelay = null;

			CelestialBody bodyOccludingBestOccludedRelay = null;

			double nearestRelaySqrDistance = double.PositiveInfinity;
			double bestOccludedSqrDistance = double.PositiveInfinity;
			double maxTransmitSqrDistance = this.maxTransmitDistance * this.maxTransmitDistance;

			/*
			 * Loop through all the vessels and exclude this vessel, vessels of the wrong type, and vessels that are too
			 * far away.  When we find a candidate, get through its antennae for relays which have not been checked yet
			 * and that can transmit.  Once we find a suitable candidate, assign it to nearestRelay for comparison
			 * against future finds.
			 * */
			foreach (Vessel potentialVessel in FlightGlobals.Vessels)
			{
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
				double potentialSqrDistance = this.sqrDistanceTo(potentialVessel);

				CelestialBody fob = null;

				// Skip vessels to which we do not have line of sight.
				if (
					ARConfiguration.RequireLineOfSight &&
					!this.vessel.hasLineOfSightTo(potentialVessel, out fob, ARConfiguration.RadiusRatio)
				)
				{
					this.firstOccludingBody = fob;

					if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
					{
						Tools.PostLogMessage("{6}: Vessel {0} discarded because we do not have line of sight." +
							"\npotentialSqrDistance: {1}, bestOccludedSqrDistance: {2}, maxTransmitSqrDistance: {3}" +
							"\npotentialSqrDistance < bestOccludedSqrDistance: {4}" +
							"\npotentialSqrDistance < (this.maxTransmitDistance * this.maxTransmitDistance): {5}",
							potentialVessel.vesselName,
							potentialSqrDistance, bestOccludedSqrDistance, this.maxTransmitDistance * this.maxTransmitDistance,
							potentialSqrDistance < bestOccludedSqrDistance,
							potentialSqrDistance < (this.maxTransmitDistance * this.maxTransmitDistance),
							this.ToString()
						);
					}

					if (
						(potentialSqrDistance < bestOccludedSqrDistance) &&
						(potentialSqrDistance < maxTransmitSqrDistance)
					)
					{
						if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
						{
							Tools.PostLogMessage("{0}: Checking {1} relays on {2}.",
								this.ToString(),
								potentialVessel.GetAntennaRelays().Count(),
								potentialVessel
							);
						}

						foreach (IAntennaRelay occludedRelay in potentialVessel.GetAntennaRelays())
						{
							if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
							{
								Tools.PostLogMessage(this.ToString() +  " Checking candidate for bestOccludedRelay: {0}" +
									"\n\tCanTransmit: {1}", occludedRelay, occludedRelay.CanTransmit());
							}

							if (occludedRelay.CanTransmit())
							{
								this.bestOccludedRelay = occludedRelay;
								bodyOccludingBestOccludedRelay = fob;
								bestOccludedSqrDistance = potentialSqrDistance;

								if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
								{
									Tools.PostLogMessage(this.ToString() + " Found new bestOccludedRelay: {0}" +
										"\nfirstOccludingBody: {1}" +
										"\nbestOccludedSqrDistance: {2}",
										occludedRelay,
										fob,
										potentialSqrDistance
									);
								}
								break;
							}
						}
					}

					continue;
				}

				/*
				 * ...so that we can skip the vessel if it is further away than a vessel we've already checked.
				 * */
				if (potentialSqrDistance > nearestRelaySqrDistance)
				{
					if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
					{
						Tools.PostLogMessage("{0}: Vessel {1} discarded because it is out of range, or farther than another relay.",
							this.ToString(),
							potentialVessel.vesselName
						);
					}
					continue;
				}

				nearestRelaySqrDistance = potentialSqrDistance;

				foreach (IAntennaRelay potentialRelay in potentialVessel.GetAntennaRelays())
				{
					if (potentialRelay.CanTransmit() && potentialRelay.targetRelay != this)
					{
						this.nearestRelay = potentialRelay;

						if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.id == this.vessel.id)
						{
							Tools.PostLogMessage(string.Format("{0}: found new best relay {1} ({2})",
								this.ToString(),
								this.nearestRelay.ToString(),
								this.nearestRelay.vessel.id
							));
						}
						break;
					}
				}
			}

			CelestialBody bodyOccludingKerbin = null;

			double kerbinSqrDistance = this.vessel.DistanceTo(Kerbin) - Kerbin.Radius;
			kerbinSqrDistance *= kerbinSqrDistance;

			System.Text.StringBuilder log = new System.Text.StringBuilder();

			log.AppendFormat("{0} ({1}): Search done, figuring status.", this.ToString(), this.GetType().Name);

			// If we don't have LOS to Kerbin, focus on relays
			if (!this.vessel.hasLineOfSightTo(Kerbin, out bodyOccludingKerbin, ARConfiguration.RadiusRatio))
			{
				log.AppendFormat("\n\tKerbin LOS is blocked by {0}.", bodyOccludingKerbin.bodyName);

				// nearestRelaySqrDistance will be infinity if all relays are occluded or none exist.
				// Therefore, this will only be true if a valid relay is in range.
				if (nearestRelaySqrDistance <= maxTransmitSqrDistance)
				{
					log.AppendFormat("\n\tCan transmit to nearby relay {0} ({1} <= {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.KerbinDirect = false;
					this.canTransmit = true;
					this.targetRelay = this.nearestRelay;
				}
				// If this isn't true, we can't transmit, but pick a second best of bestOccludedRelay and Kerbin anyway
				else
				{
					log.AppendFormat("\n\tCan't transmit to nearby relay {0} ({1} > {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.canTransmit = false;

					// If the best occluded relay is closer than Kerbin, target it.
					if (bestOccludedSqrDistance < kerbinSqrDistance)
					{
						log.AppendFormat("\n\t\tPicking occluded relay {0} as target ({1} < {2}).",
							this.bestOccludedRelay == null ? "null" : this.bestOccludedRelay.ToString(),
							bestOccludedSqrDistance, kerbinSqrDistance);

						this.KerbinDirect = false;
						this.targetRelay = this.bestOccludedRelay;
						this.firstOccludingBody = bodyOccludingBestOccludedRelay;
					}
					// Otherwise, target Kerbin and report the first body blocking it.
					else
					{
						log.AppendFormat("\n\t\tPicking Kerbin as target ({0} >= {1}).",
							bestOccludedSqrDistance, kerbinSqrDistance);

						this.KerbinDirect = true;
						this.targetRelay = null;
						this.firstOccludingBody = bodyOccludingKerbin;
					}
				}
			}
			// If we do have LOS to Kerbin, try to prefer the closest of nearestRelay and Kerbin
			else
			{
				log.AppendFormat("\n\tKerbin is in LOS.");

				// If the nearest relay is closer than Kerbin and in range, transmit to it.
				if (nearestRelaySqrDistance <= maxTransmitSqrDistance)
				{
					log.AppendFormat("\n\tCan transmit to nearby relay {0} ({1} <= {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.canTransmit = true;

					// If the nearestRelay is closer than Kerbin, use it.
					if (nearestRelaySqrDistance < kerbinSqrDistance)
					{
						log.AppendFormat("\n\tPicking relay {0} over Kerbin ({1} < {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, kerbinSqrDistance);

						this.KerbinDirect = false;
						this.targetRelay = this.nearestRelay;
					}
					// Otherwise, Kerbin is closer, so use it.
					else
					{
						log.AppendFormat("\n\tBut picking Kerbin over nearby relay {0} ({1} >= {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
								nearestRelaySqrDistance, kerbinSqrDistance);

						this.KerbinDirect = true;
						this.targetRelay = null;
					}
				}
				// If the nearest relay is out of range, we still need to check on Kerbin.
				else
				{
					log.AppendFormat("\n\tCan't transmit to nearby relay {0} ({1} > {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					// If Kerbin is in range, use it.
					if (kerbinSqrDistance <= maxTransmitSqrDistance)
					{
						log.AppendFormat("\n\tCan transmit to Kerbin ({0} <= {1}).",
							kerbinSqrDistance, maxTransmitSqrDistance);

						this.canTransmit = true;
						this.KerbinDirect = true;
						this.targetRelay = null;
					}
					// If Kerbin is out of range and the nearest relay is out of range, pick a second best between
					// Kerbin and bestOccludedRelay
					else
					{
						log.AppendFormat("\n\tCan't transmit to Kerbin ({0} > {1}).",
							kerbinSqrDistance, maxTransmitSqrDistance);

						this.canTransmit = false;

						// If the best occluded relay is closer than Kerbin, use it.
						// Since bestOccludedSqrDistance is infinity if there are no occluded relays,
						// this is safe
						if (bestOccludedSqrDistance < kerbinSqrDistance)
						{
							log.AppendFormat("\n\t\tPicking occluded relay {0} as target ({1} < {2}).",
								this.bestOccludedRelay == null ? "null" : this.bestOccludedRelay.ToString(),
									bestOccludedSqrDistance, kerbinSqrDistance);

							this.KerbinDirect = false;
							this.targetRelay = bestOccludedRelay;
							this.firstOccludingBody = bodyOccludingBestOccludedRelay;
						}
						// Otherwise, target Kerbin.  Since we have LOS, blank the first occluding body.
						else
						{
							log.AppendFormat("\n\t\tPicking Kerbin as target ({0} >= {1}).",
								bestOccludedSqrDistance, kerbinSqrDistance);

							this.KerbinDirect = true;
							this.targetRelay = null;
							this.firstOccludingBody = null;
						}
					}
				}
			}

			log.AppendFormat("\n{0}: Status determination complete.", this.ToString());

			Tools.PostLogMessage(log.ToString());

			// Now that we're done with our recursive CanTransmit checks, flag this relay as not checked so it can be
			// used next time.
			RelayDatabase.Instance.CheckedVesselsTable.Remove(vessel.id);
		}

		public override string ToString()
		{
			if (this is ProtoAntennaRelay)
			{
				return (this as ProtoAntennaRelay).ToString();
			}
			return this.moduleRef.ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoDataTransmitter"/> class.
		/// </summary>
		/// <param name="ms"><see cref="ProtoPartModuleSnapshot"/></param>
		public AntennaRelay(IAntennaRelay module)
		{
			this.moduleRef = module;

			this.searchTimer = new System.Diagnostics.Stopwatch();
			this.millisecondsBetweenSearches = 125;
		}
	}
}

