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
using ToadicusTools;

namespace AntennaRange
{
	/// <summary>
	/// Relay code at the heart of AntennaRange
	/// </summary>
	public class AntennaRelay
	{
		public static readonly System.Diagnostics.Stopwatch searchTimer = new System.Diagnostics.Stopwatch();
		public const long millisecondsBetweenSearches = 125L;

		// We don't have a Bard, so we'll hide Kerbin here.
		private static CelestialBody _Kerbin;

		/// <summary>
		/// Fetches, caches, and returns a <see cref="CelestialBody"/> reference to Kerbin
		/// </summary>
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

		private long lastSearch;

		private bool canTransmit;
		private bool isChecked;

		private IAntennaRelay nearestRelay;
		private IAntennaRelay bestOccludedRelay;

		/// <summary>
		/// The <see cref="AntennaRange.ModuleLimitedDataTransmitter"/> reference underlying this AntennaRelay, as an
		/// <see cref="AntennaRange.IAntennaRelay"/>
		/// </summary>
		protected IAntennaRelay moduleRef;

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
		/// Gets the target <see cref="AntennaRange.IAntennaRelay"/>relay.
		/// </summary>
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
				//this.FindNearestRelay();

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

		/// <summary>
		/// Gets the nominal transmit distance at which the Antenna behaves just as prescribed by Squad's config.
		/// </summary>
		public virtual double nominalTransmitDistance
		{
			get;
			set;
		}

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public virtual double maxTransmitDistance
		{
			get;
			set;
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.IAntennaRelay"/> Relay is communicating
		/// directly with Kerbin.
		/// </summary>
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
			//this.FindNearestRelay();
			return this.canTransmit;
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		/// <returns>The nearest relay or null, if no relays in range.</returns>
		private void FindNearestRelay()
		{
			if (!FlightGlobals.ready)
			{
				return;
			}

			if (!searchTimer.IsRunning)
			{
				searchTimer.Start();
			}

			Tools.DebugLogger log;
			#if DEBUG
			log = Tools.DebugLogger.New(this);
			#endif

			long searchTime = searchTimer.ElapsedMilliseconds;
			long timeSinceLast = searchTime - this.lastSearch;

			if (timeSinceLast < millisecondsBetweenSearches)
			{
				log.AppendFormat(
					"{0}: Target search skipped because it's not time to search again yet ({1} - {2}) < {3})",
					this, searchTime, this.lastSearch, millisecondsBetweenSearches
				);
				log.Print();
				return;
			}

			// Skip vessels that have already been checked for a nearest relay this pass.
			if (this.isChecked)
			{
				log.AppendFormat("{0}: Target search skipped because our vessel has been checked already this search.",
					this);
				log.Print();
				return;
			}

			log.AppendFormat("{0}: Target search started at {1} ms ({2} ms since last search).",
				this.ToString(), searchTime, timeSinceLast);

			#if DEBUG
			try {
			#endif
			// Set this vessel as checked, so that we don't check it again.
			this.isChecked = true;

			this.lastSearch = searchTime;

			// Blank everything we're trying to find before the search.
			this.firstOccludingBody = null;
			this.bestOccludedRelay = null;
			this.targetRelay = null;
			this.nearestRelay = null;

			// Default to KerbinDirect = true in case something in here doesn't work right.
			this.KerbinDirect = true;

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
			Vessel potentialVessel;
			IList<IAntennaRelay> vesselRelays;
			for (int vIdx = 0; vIdx < FlightGlobals.Vessels.Count; vIdx++)
			{
				log.AppendFormat("\nFetching vessel at index {0}", vIdx);
				potentialVessel = FlightGlobals.Vessels[vIdx];
				
				if (potentialVessel == null)
				{
					log.AppendFormat("\n\tSkipping vessel at index {0} because it is null.", vIdx);
					log.Print();
					return;
				}
				#if DEBUG
				else
				{
					log.AppendFormat("\n\tGot vessel {0}", potentialVessel);
				}
				#endif

				// Skip vessels of the wrong type.
				log.Append("\n\tchecking vessel type");
				switch (potentialVessel.vesselType)
				{
					case VesselType.Debris:
					case VesselType.Flag:
					case VesselType.EVA:
					case VesselType.SpaceObject:
					case VesselType.Unknown:
							log.Append("\n\tSkipping because vessel is the wrong type.");
						continue;
					default:
						break;
				}
				
				log.Append("\n\tchecking if vessel is this vessel");
				// Skip vessels with the wrong ID
				if (potentialVessel.id == vessel.id)
				{
					log.Append("\n\tSkipping because vessel is this vessel.");
					continue;
				}

				// Find the distance from here to the vessel...
				log.Append("\n\tgetting distance to potential vessel");
				double potentialSqrDistance = this.sqrDistanceTo(potentialVessel);
				log.Append("\n\tgetting vessel relays");
				vesselRelays = potentialVessel.GetAntennaRelays();
					log.AppendFormat("\n\t\tvesselRelays: {0}",
						vesselRelays == null ? "null" : vesselRelays.Count.ToString());

				CelestialBody fob = null;
				
				log.Append("\n\tdoing LOS check");
				// Skip vessels to which we do not have line of sight.
				if (
					ARConfiguration.RequireLineOfSight &&
					!this.vessel.hasLineOfSightTo(potentialVessel, out fob, ARConfiguration.RadiusRatio)
				)
				{
					log.Append("\n\tfailed LOS check");
					this.firstOccludingBody = fob;
					
					log.AppendFormat("\n\t{0}: Vessel {1} not in line of sight.",
						this.ToString(), potentialVessel.vesselName);
					
					log.AppendFormat("\n\t\tpotentialSqrDistance: {0}", potentialSqrDistance);
					log.AppendFormat("\n\t\tbestOccludedSqrDistance: {0}", bestOccludedSqrDistance);
					log.AppendFormat("\n\t\tmaxTransmitSqrDistance: {0}", maxTransmitSqrDistance);

					if (
						(potentialSqrDistance < bestOccludedSqrDistance) &&
						(potentialSqrDistance < maxTransmitSqrDistance)
					)
					{
						log.Append("\n\t\t...vessel is close enough to check for occluded relays");
						log.AppendFormat("\n\t\tthis: {0}", this);
						log.AppendFormat("\n\t\tpotentialVessel: {0}",
							potentialVessel == null ? "null" : potentialVessel.ToString());
						log.AppendFormat("\n\t\tvesselRelays: {0}",
							vesselRelays == null ? "null" : vesselRelays.ToString());

						log.AppendFormat("\n\t\t{0}: Checking {1} relays on occluded vessel {2}.",
							this.ToString(),
							vesselRelays.Count,
							potentialVessel
						);

						IAntennaRelay occludedRelay;
						for (int rIdx = 0; rIdx < vesselRelays.Count; rIdx++)
						{
							occludedRelay = vesselRelays[rIdx];

							log.AppendFormat(
								"\n\t\t{0}: Checking candidate for bestOccludedRelay: {1}" +
								"\n\t\tCanTransmit: {2}",
								this.ToString(), occludedRelay, occludedRelay.CanTransmit()
							);
							
							if (occludedRelay.CanTransmit())
							{
								this.bestOccludedRelay = occludedRelay;
								bodyOccludingBestOccludedRelay = fob;
								bestOccludedSqrDistance = potentialSqrDistance;

								log.AppendFormat("\n\t{0}: Found new bestOccludedRelay: {1}" +
									" (blocked by {2}; distance: {3} m)",
									this.ToString(),
									occludedRelay.ToString(),
									fob,
									potentialSqrDistance
								);
								break;
							}
						}
					}
					
					log.Append("\n\t\t...vessel is not close enough to check for occluded relays, carrying on");
					continue;
				}

				log.Append("\n\tpassed LOS check");

				/*
				 * ...so that we can skip the vessel if it is further away than a vessel we've already checked.
				 * */
				if (potentialSqrDistance > nearestRelaySqrDistance)
				{
					
					log.AppendFormat("\n\t{0}: Vessel {1} discarded because it is farther than another the nearest relay.",
						this.ToString(),
						potentialVessel.vesselName
					);
					continue;
				}

				log.Append("\n\tpassed distance check");

				IAntennaRelay potentialRelay;
				for (int rIdx = 0; rIdx < vesselRelays.Count; rIdx++)
				{
					log.AppendFormat("\n\t\tfetching vessel relay at index {0}", rIdx);
					potentialRelay = vesselRelays[rIdx];
					log.AppendFormat("\n\t\tgot relay {0}", potentialRelay == null ? "null" : potentialRelay.ToString());

					if (potentialRelay == null)
					{
						log.Append("\n\t\t...skipping null relay");
						continue;
					}

					if (
						potentialRelay.CanTransmit() &&
						(potentialRelay.targetRelay == null || potentialRelay.targetRelay.vessel != this.vessel))
					{
						// @TODO: Moved this here from outside the loop; why was it there?
						nearestRelaySqrDistance = potentialSqrDistance;
						this.nearestRelay = potentialRelay;

						log.AppendFormat("\n\t{0}: found new nearest relay {1} ({2}m)",
							this.ToString(),
							this.nearestRelay.ToString(),
							Math.Sqrt(nearestRelaySqrDistance)
						);
						
						break;
					}
				}
			}

			CelestialBody bodyOccludingKerbin = null;

			double kerbinSqrDistance = this.vessel.DistanceTo(Kerbin) - Kerbin.Radius;
			kerbinSqrDistance *= kerbinSqrDistance;

			log.AppendFormat("\n{0} ({1}): Search done, figuring status.", this.ToString(), this.GetType().Name);

			// If we don't have LOS to Kerbin, focus on relays
			if (!this.vessel.hasLineOfSightTo(Kerbin, out bodyOccludingKerbin, ARConfiguration.RadiusRatio))
			{
				log.AppendFormat("\n\tKerbin LOS is blocked by {0}.", bodyOccludingKerbin.bodyName);

				// nearestRelaySqrDistance will be infinity if all relays are occluded or none exist.
				// Therefore, this will only be true if a valid relay is in range.
				if (nearestRelaySqrDistance <= maxTransmitSqrDistance)
				{
					log.AppendFormat("\n\t\tCan transmit to nearby relay {0} ({1} <= {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.KerbinDirect = false;
					this.canTransmit = true;
					this.targetRelay = this.nearestRelay;
				}
				// If this isn't true, we can't transmit, but pick a second best of bestOccludedRelay and Kerbin anyway
				else
				{
					log.AppendFormat("\n\t\tCan't transmit to nearby relay {0} ({1} > {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.canTransmit = false;

					// If the best occluded relay is closer than Kerbin, check it against the nearest relay.
					// Since bestOccludedSqrDistance is infinity if there are no occluded relays, this is safe
					if (bestOccludedSqrDistance < kerbinSqrDistance)
					{
						log.AppendFormat("\n\t\t\tBest occluded relay is closer than Kerbin ({0} < {1})",
							bestOccludedRelay, kerbinSqrDistance);
						
						this.KerbinDirect = false;

						// If the nearest relay is closer than the best occluded relay, pick it.
						// Since nearestRelaySqrDistane is infinity if there are no nearby relays, this is safe.
						if (nearestRelaySqrDistance < bestOccludedSqrDistance)
						{
							log.AppendFormat("\n\t\t\t\t...but the nearest relay is closer ({0} < {1}), so picking it.",
								nearestRelaySqrDistance, bestOccludedSqrDistance);
							
							this.targetRelay = nearestRelay;
							this.firstOccludingBody = null;
						}
						// Otherwise, target the best occluded relay.
						else
						{
							log.AppendFormat("\n\t\t\t\t...and closer than the nearest relay ({0} >= {1}), so picking it.",
								nearestRelaySqrDistance, bestOccludedSqrDistance);
							
							this.targetRelay = bestOccludedRelay;
							this.firstOccludingBody = bodyOccludingBestOccludedRelay;
						}
					}
					// Otherwise, check Kerbin against the nearest relay.
					// Since we have LOS, blank the first occluding body.
					else
					{
						log.AppendFormat("\n\t\t\tKerbin is closer than the best occluded relay ({0} >= {1})",
							bestOccludedRelay, kerbinSqrDistance);
						
						this.firstOccludingBody = null;

						// If the nearest relay is closer than Kerbin, pick it.
						// Since nearestRelaySqrDistane is infinity if there are no nearby relays, this is safe.
						if (nearestRelaySqrDistance < kerbinSqrDistance)
						{
							log.AppendFormat("\n\t\t\t\t...but the nearest relay is closer ({0} < {1}), so picking it.",
								nearestRelaySqrDistance, kerbinSqrDistance);
							
							this.KerbinDirect = false;
							this.targetRelay = nearestRelay;
						}
						// Otherwise, pick Kerbin.
						else
						{
							log.AppendFormat("\n\t\t\t\t...and closer than the nearest relay ({0} >= {1}), so picking it.",
								nearestRelaySqrDistance, kerbinSqrDistance);
							
							this.KerbinDirect = true;
							this.targetRelay = null;
						}
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
					log.AppendFormat("\n\t\tCan transmit to nearby relay {0} ({1} <= {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					this.canTransmit = true;

					// If the nearestRelay is closer than Kerbin, use it.
					if (nearestRelaySqrDistance < kerbinSqrDistance)
					{
						log.AppendFormat("\n\t\t\tPicking relay {0} over Kerbin ({1} < {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, kerbinSqrDistance);

						this.KerbinDirect = false;
						this.targetRelay = this.nearestRelay;
					}
					// Otherwise, Kerbin is closer, so use it.
					else
					{
						log.AppendFormat("\n\t\t\tBut picking Kerbin over nearby relay {0} ({1} >= {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
								nearestRelaySqrDistance, kerbinSqrDistance);

						this.KerbinDirect = true;
						this.targetRelay = null;
					}
				}
				// If the nearest relay is out of range, we still need to check on Kerbin.
				else
				{
					log.AppendFormat("\n\t\tCan't transmit to nearby relay {0} ({1} > {2}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							nearestRelaySqrDistance, maxTransmitSqrDistance);

					// If Kerbin is in range, use it.
					if (kerbinSqrDistance <= maxTransmitSqrDistance)
					{
						log.AppendFormat("\n\t\t\tCan transmit to Kerbin ({0} <= {1}).",
							kerbinSqrDistance, maxTransmitSqrDistance);

						this.canTransmit = true;
						this.KerbinDirect = true;
						this.targetRelay = null;
					}
					// If Kerbin is out of range and the nearest relay is out of range, pick a second best between
					// Kerbin and bestOccludedRelay
					else
					{
						log.AppendFormat("\n\t\t\tCan't transmit to Kerbin ({0} > {1}).",
							kerbinSqrDistance, maxTransmitSqrDistance);

						this.canTransmit = false;

						// If the best occluded relay is closer than Kerbin, check it against the nearest relay.
						// Since bestOccludedSqrDistance is infinity if there are no occluded relays, this is safe
						if (bestOccludedSqrDistance < kerbinSqrDistance)
						{
							log.AppendFormat("\n\t\t\tBest occluded relay is closer than Kerbin ({0} < {1})",
								bestOccludedRelay, kerbinSqrDistance);
							
							this.KerbinDirect = false;

							// If the nearest relay is closer than the best occluded relay, pick it.
							// Since nearestRelaySqrDistane is infinity if there are no nearby relays, this is safe.
							if (nearestRelaySqrDistance < bestOccludedSqrDistance)
							{
								log.AppendFormat("\n\t\t\t\t...but the nearest relay is closer ({0} < {1}), so picking it.",
									nearestRelaySqrDistance, bestOccludedSqrDistance);
								
								this.targetRelay = nearestRelay;
								this.firstOccludingBody = null;
							}
							// Otherwise, target the best occluded relay.
							else
							{
								log.AppendFormat("\n\t\t\t\t...and closer than the nearest relay ({0} >= {1}), so picking it.",
									nearestRelaySqrDistance, bestOccludedSqrDistance);
								
								this.targetRelay = bestOccludedRelay;
								this.firstOccludingBody = bodyOccludingBestOccludedRelay;
							}
						}
						// Otherwise, check Kerbin against the nearest relay.
						// Since we have LOS, blank the first occluding body.
						else
						{
							log.AppendFormat("\n\t\t\tKerbin is closer than the best occluded relay ({0} >= {1})",
								bestOccludedRelay, kerbinSqrDistance);
							
							this.firstOccludingBody = null;

							// If the nearest relay is closer than Kerbin, pick it.
							// Since nearestRelaySqrDistane is infinity if there are no nearby relays, this is safe.
							if (nearestRelaySqrDistance < kerbinSqrDistance)
							{
								log.AppendFormat("\n\t\t\t\t...but the nearest relay is closer ({0} < {1}), so picking it.",
									nearestRelaySqrDistance, kerbinSqrDistance);
								
								this.KerbinDirect = false;
								this.targetRelay = nearestRelay;
							}
							// Otherwise, pick Kerbin.
							else
							{
								log.AppendFormat("\n\t\t\t\t...and closer than the nearest relay ({0} >= {1}), so picking it.",
									nearestRelaySqrDistance, kerbinSqrDistance);
								
								this.KerbinDirect = true;
								this.targetRelay = null;
							}
						}
					}
				}
			}

			log.AppendFormat("{0}: Target search completed at {1} ms ({2} ms elapsed).",
				this.ToString(), searchTimer.ElapsedMilliseconds, searchTimer.ElapsedMilliseconds - searchTime);;

			log.AppendFormat("\n{0}: Status determination complete.", this.ToString());
			
			#if DEBUG
			} catch (Exception ex) {
				log.AppendFormat("\nCaught {0}: {1}\n{2}", ex.GetType().FullName, ex.ToString(), ex.StackTrace);
				UnityEngine.Application.Quit();
			} finally {
			#endif
			log.Print(false);
			#if DEBUG
			}
			#endif
			// Now that we're done with our recursive CanTransmit checks, flag this relay as not checked so it can be
			// used next time.
			this.isChecked = false;
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.AntennaRelay"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="AntennaRange.AntennaRelay"/>.</returns>
		public override string ToString()
		{
			if (this is ProtoAntennaRelay)
			{
				return (this as ProtoAntennaRelay).ToString();
			}
			return this.moduleRef.ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.AntennaRelay"/> class.
		/// </summary>
		/// <param name="module">The module reference underlying this AntennaRelay,
		/// as an <see cref="AntennaRange.IAntennaRelay"/></param>
		public AntennaRelay(IAntennaRelay module)
		{
			this.moduleRef = module;
			this.isChecked = false;
		}
	}
}

