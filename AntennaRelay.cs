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
using ToadicusTools.DebugTools;
using ToadicusTools.Extensions;
using UnityEngine;

namespace AntennaRange
{
	/// <summary>
	/// Relay code at the heart of AntennaRange
	/// </summary>
	public class AntennaRelay
	{
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

		#if BENCH
		private static ushort relayCount = 0;
		private static ulong searchCount = 0u;
		private static ulong searchTimer = 0u;
		private readonly static RollingAverage averager = new RollingAverage(16);
		private static long doubleAverageTime = long.MaxValue;


		private System.Diagnostics.Stopwatch performanceTimer = new System.Diagnostics.Stopwatch();
		#endif

		private bool canTransmit;

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
		/// Gets a value indicating whether this <see cref="AntennaRange.IAntennaRelay"/> Relay is communicating
		/// directly with Kerbin.
		/// </summary>
		public virtual bool KerbinDirect
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets or sets the nominal link distance, in meters.
		/// </summary>
		public virtual double NominalLinkSqrDistance
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets or sets the maximum link distance, in meters.
		/// </summary>
		public virtual double MaximumLinkSqrDistance
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
		public double CurrentLinkSqrDistance
		{
			get
			{
				if (this.KerbinDirect || this.targetRelay == null)
				{
					return this.SqrDistanceTo(Kerbin);
				}
				else
				{
					return this.SqrDistanceTo(this.targetRelay);
				}
			}
		}

		/// <summary>
		/// Gets the current link resource rate in EC/MiT.
		/// </summary>
		/// <value>The current link resource rate in EC/MiT.</value>
		public virtual RelayDataCost CurrentLinkCost
		{
			get
			{
				return this.moduleRef?.CurrentLinkCost ?? RelayDataCost.Infinity;
			}
			set
			{
				throw new NotImplementedException(
					string.Format(
						"{0} must not assign CurrentLinkCost.  This is probably a bug.",
						this.GetType().FullName
					)
				);
			}
		}

		/// <summary>
		/// Gets the current network link cost back to Kerbin, in EC/MiT.
		/// </summary>
		/// <value>The current network link cost back to Kerbin, in EC/MiT.</value>
		public virtual RelayDataCost CurrentNetworkLinkCost
		{
			get
			{
				RelayDataCost cost = new RelayDataCost();

				IAntennaRelay relay = this.moduleRef;

				ushort iters = 0;
				while (relay != null)
				{
					cost += relay.CurrentLinkCost;

					if (relay.KerbinDirect)
					{
						break;
					}

					iters++;

					if (iters > 255)
					{
						this.LogError("Bailing out of AntennaRelay.CurrentNetworkLinkCost because it looks like " +
							"we're stuck in an infinite loop.  This is probably a bug.");
						
						break;
					}

					relay = relay.targetRelay;
				}

				return cost;
			}
		}

		/// <summary>
		/// Gets or sets the link status.
		/// </summary>
		public virtual ConnectionStatus LinkStatus
		{
			get;
			protected set;
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
		/// Determines whether this instance can transmit.
		/// </summary>
		/// <returns><c>true</c> if this instance can transmit; otherwise, <c>false</c>.</returns>
		public virtual bool CanTransmit()
		{
			return this.canTransmit;
		}

		/// <summary>
		/// Recalculates the transmission rates.
		/// </summary>
		public void RecalculateTransmissionRates()
		{
			if (!this.canTransmit) {
				this.moduleRef.CurrentLinkCost = RelayDataCost.Infinity;
				return;
			}

			RelayDataCost cost = this.GetPotentialLinkCost(this.CurrentLinkSqrDistance, this.NominalLinkSqrDistance);

			this.moduleRef.CurrentLinkCost = cost;
		}

		/// <summary>
		/// Gets the potential link cost, in EC/MiT.
		/// </summary>
		/// <returns>The potential link cost, in EC/MiT.</returns>
		/// <param name="currentSqrDistance">Square of the current distance to the target</param>
		/// <param name="nominalSqrDistance">Square of the nominal range to the target.</param>
		public RelayDataCost GetPotentialLinkCost(double currentSqrDistance, double nominalSqrDistance)
		{
			RelayDataCost linkCost = new RelayDataCost();

			float rangeFactor = (float)(nominalSqrDistance / currentSqrDistance);

			RelayDataCost baseCost = this.moduleRef?.BaseLinkCost ?? RelayDataCost.Infinity;

			if (ARConfiguration.FixedPowerCost)
			{
				linkCost.PacketResourceCost = baseCost.PacketResourceCost;

				linkCost.PacketSize = Mathf.Min(
					baseCost.PacketSize * rangeFactor,
					baseCost.PacketSize * this.moduleRef.MaxDataFactor
				);
			}
			else
			{
				if (currentSqrDistance > nominalSqrDistance)
				{
					linkCost.PacketSize = baseCost.PacketSize;
					linkCost.PacketResourceCost = baseCost.PacketResourceCost / rangeFactor;
				}
				else
				{
					linkCost.PacketSize = Mathf.Min(
						baseCost.PacketSize * rangeFactor,
						baseCost.PacketSize * this.moduleRef.MaxDataFactor
					);
					linkCost.PacketResourceCost = baseCost.PacketResourceCost;
				}
			}

			linkCost.PacketResourceCost *= this.moduleRef.PacketThrottle / 100f;
			linkCost.PacketSize *= this.moduleRef.PacketThrottle / 100f;

			return linkCost;
		}

		/// <summary>
		/// Gets the potential link cost, in EC/MiT.
		/// </summary>
		/// <returns>The potential link cost, in EC/MiT.</returns>
		/// <param name="potentialTarget">Potential target relay.</param>
		public RelayDataCost GetPotentialLinkCost(IAntennaRelay potentialTarget)
		{
			if (potentialTarget == null)
			{
				return RelayDataCost.Infinity;
			}

			double currentSqrDistance = this.SqrDistanceTo(potentialTarget);

			if (currentSqrDistance > this.MaxLinkSqrDistanceTo(potentialTarget))
			{
				return RelayDataCost.Infinity;
			}

			double nominalSqrDistance;
			if (ARConfiguration.UseAdditiveRanges)
			{
				nominalSqrDistance = this.nominalTransmitDistance * potentialTarget.nominalTransmitDistance;
			}
			else
			{
				nominalSqrDistance = this.nominalTransmitDistance * this.nominalTransmitDistance;
			}

			return GetPotentialLinkCost(currentSqrDistance, nominalSqrDistance);
		}

		/// <summary>
		/// Gets the potential link cost, in EC/MiT.
		/// </summary>
		/// <returns>The potential link cost, in EC/MiT.</returns>
		/// <param name="body">Potential target Body</param>
		public RelayDataCost GetPotentialLinkCost(CelestialBody body)
		{
			if (body == null || body != Kerbin)
			{
				return RelayDataCost.Infinity;
			}

			double currentSqrDistance = this.SqrDistanceTo(body);

			if (currentSqrDistance > this.MaxLinkSqrDistanceTo(body))
			{
				return RelayDataCost.Infinity;
			}

			double nominalSqrDistance;
			if (ARConfiguration.UseAdditiveRanges)
			{
				nominalSqrDistance = this.nominalTransmitDistance * ARConfiguration.KerbinNominalRange;
			}
			else
			{
				nominalSqrDistance = this.nominalTransmitDistance * this.nominalTransmitDistance;
			}

			return GetPotentialLinkCost(currentSqrDistance, nominalSqrDistance);
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		/// <returns>The nearest relay or null, if no relays in range.</returns>
		public void FindNearestRelay()
		{
			if (!FlightGlobals.ready)
			{
				return;
			}

			PooledDebugLogger log;
			#if DEBUG
			log = PooledDebugLogger.New(this);
			#endif

			#if BENCH
			this.performanceTimer.Restart();

			long startVesselLoopTicks;
			long totalVesselLoopTicks;

			string slowestLOSVesselName = string.Empty;
			long slowestLOSVesselTicks = long.MinValue;
			long startLOSVesselTicks;
			long totalLOSVesselTicks;

			string slowestCircularVesselName = string.Empty;
			long slowestCircularVesselTicks = long.MinValue;
			long startCircularVesselTicks;
			long totalCircularVesselTicks;

			long startKerbinLOSTicks;
			long totalKerbinLOSTicks;
			long statusResolutionTicks;

			ushort usefulVesselCount = 0;
			#endif

			log.AppendFormat("{0}: Target search started).", this.ToString());

			#if DEBUG
			try {
			#endif

			// Declare a bunch of variables we'll be using.
			CelestialBody bodyOccludingBestOccludedRelay = null;
			IAntennaRelay needle;

			RelayDataCost cheapestRelayRate = RelayDataCost.Infinity;
			RelayDataCost cheapestOccludedRelayRate = RelayDataCost.Infinity;

			RelayDataCost potentialRelayRate;

			RelayDataCost kerbinRelayRate = this.GetPotentialLinkCost(Kerbin);

			bool isCircular;
			int iterCount;

			// Blank everything we're trying to find before the search.
			this.firstOccludingBody = null;
			this.bestOccludedRelay = null;
			this.targetRelay = null;
			this.nearestRelay = null;

			// Default to KerbinDirect = true in case something in here doesn't work right.
			this.KerbinDirect = true;

			/*
			 * Loop through the useful relays as determined by ARFlightController and check each for line of sight and
			 * distance, searching for the relay with the best distance/maxRange ratio that is in sight, in range, and
			 * can transmit, also stashing the "best" relay outside of line of sight for failure report.
			 * */
			IAntennaRelay potentialBestRelay;
			CelestialBody fob;

			#if BENCH
			startVesselLoopTicks = performanceTimer.ElapsedTicks;
			#endif
			
			for (int rIdx = 0; rIdx < ARFlightController.UsefulRelays.Count; rIdx++)
			{
				potentialBestRelay = ARFlightController.UsefulRelays[rIdx];
				log.AppendFormat("\n\tgot useful relay {0}",
					potentialBestRelay == null ? "null" : potentialBestRelay.ToString());

				if (potentialBestRelay == null)
				{
					log.Append("\n\t...skipping null relay");
					continue;
				}

				if (potentialBestRelay == this || potentialBestRelay.vessel == this.vessel)
				{
					log.AppendFormat(
						"\n\t...skipping relay {0} because it or its vessel ({1}) is the same as ours" +
						"\n\t\t(our vessel is {2})",
						potentialBestRelay,
						potentialBestRelay.vessel == null ? "null" : potentialBestRelay.vessel.vesselName,
						this.vessel == null ? "null" : this.vessel.vesselName
					);
					continue;
				}

				#if BENCH
				usefulVesselCount++;
				#endif

				// Find the distance from here to the vessel...
				log.Append("\n\tgetting cost to potential vessel");
				potentialRelayRate = potentialBestRelay.CurrentNetworkLinkCost +
					this.GetPotentialLinkCost(potentialBestRelay);

				log.AppendFormat(
					"\n\tpotentialRelayRate = {0} ({1} + {2})",
					potentialRelayRate,
					potentialBestRelay.CurrentNetworkLinkCost,
					this.GetPotentialLinkCost(potentialBestRelay)
				);

				#if BENCH
				startLOSVesselTicks = performanceTimer.ElapsedTicks;
				#endif

				log.Append("\n\t\tdoing LOS check");
				// Skip vessels to which we do not have line of sight.
				if (
					ARConfiguration.RequireLineOfSight &&
					!this.vessel.hasLineOfSightTo(potentialBestRelay.vessel, out fob, ARConfiguration.RadiusRatio)
				)
				{
					#if BENCH
					totalLOSVesselTicks = performanceTimer.ElapsedTicks - startLOSVesselTicks;

					if (totalLOSVesselTicks > slowestLOSVesselTicks)
					{
						slowestLOSVesselTicks = totalLOSVesselTicks;
						slowestLOSVesselName = vessel.vesselName;
					}
					#endif

					log.Append("\n\t\t...failed LOS check");

					log.AppendFormat("\n\t\t\t{0}: Relay {1} not in line of sight.",
						this.ToString(), potentialBestRelay);
					
					log.AppendFormat("\n\t\t\tpotentialRelayRate: {0}", potentialRelayRate);
					log.AppendFormat("\n\t\t\tcheapestOccludedRelayRate: {0}", cheapestOccludedRelayRate);

					if (
						(potentialRelayRate < cheapestRelayRate) &&
						this.IsInRangeOf(potentialBestRelay) &&
						potentialBestRelay.CanTransmit()
					)
					{
						log.Append("\n\t\t...vessel is cheapest and in range and potentialBestRelay can transmit");
						log.AppendFormat("\n\t\t...{0} found new best occluded relay {1}", this, potentialBestRelay);

						this.bestOccludedRelay = potentialBestRelay;
						bodyOccludingBestOccludedRelay = fob;
						cheapestOccludedRelayRate = potentialRelayRate;
					}
					else
					{
						log.Append("\n\t\t...vessel is not close enough to check for occluded relays, carrying on");
					}
					
					continue;
				}
				#if BENCH
				else
				{
					totalLOSVesselTicks = performanceTimer.ElapsedTicks - startLOSVesselTicks;
				}

				if (totalLOSVesselTicks > slowestLOSVesselTicks)
				{
					slowestLOSVesselTicks = totalLOSVesselTicks;
					slowestLOSVesselName = vessel.vesselName;
				}
				#endif

				log.Append("\n\t\t...passed LOS check");

				/*
				 * ...so that we can skip the vessel if it is further away than a vessel we've already checked.
				 * */
				if (potentialRelayRate > cheapestRelayRate)
				{
					
					log.AppendFormat(
							"\n\t{0}: Relay {1} discarded because it is more expensive than the cheapest relay." +
							"\n\t\t({2}, {3} > {4})",
						this.ToString(),
						potentialBestRelay,
						this.nearestRelay == null ? "NULL" : this.nearestRelay.ToString(),
						potentialRelayRate, cheapestRelayRate
					);
					continue;
				}

				log.Append("\n\t\t...passed distance check");

				if (potentialBestRelay.CanTransmit())
				{
					#if BENCH
					startCircularVesselTicks = performanceTimer.ElapsedTicks;
					#endif

					needle = potentialBestRelay;
					isCircular = false;

					iterCount = 0;
					while (needle != null)
					{
						iterCount++;

						if (needle.KerbinDirect)
						{
							break;
						}

						if (needle.targetRelay == null)
						{
							break;
						}

						if (needle.targetRelay.vessel == this.vessel || needle == this.moduleRef)
						{
							isCircular = true;
							break;
						}

						// Avoid infinite loops when we're not catching things right.
						if (iterCount > FlightGlobals.Vessels.Count)
						{
							this.LogError(
								"iterCount exceeded while checking for circular network; assuming it is circular" +
								"\n\tneedle={0}" +
								"\n\tthis.moduleRef={1}",
								needle == null ? "null" : string.Format(
									"{0}, needle.KerbinDirect={1}, needle.targetRelay={2}",
									needle, needle.KerbinDirect, needle.targetRelay == null ? "null" : string.Format(
										"{0}\n\tneedle.targetRelay.vessel={1}",
										needle.targetRelay,
										needle.targetRelay.vessel == null ?
											"null" : needle.targetRelay.vessel.vesselName
									)
								),
								this.moduleRef == null ? "null" : this.moduleRef.ToString()
							);
							isCircular = true;
							break;
						}

						needle = needle.targetRelay;
					}

					if (!isCircular)
					{
						cheapestRelayRate = potentialRelayRate;
						this.nearestRelay = potentialBestRelay;

						log.AppendFormat("\n\t{0}: found new cheapest relay {1} ({2} EC/MiT)",
							this.ToString(),
							this.nearestRelay.ToString(),
							cheapestRelayRate
						);
					}
					else
					{
						log.AppendFormat("\n\t\t...connection to {0} would result in a circular network, skipping",
							potentialBestRelay
						);
					}

					#if BENCH
					totalCircularVesselTicks = performanceTimer.ElapsedTicks - startCircularVesselTicks;

					if (totalCircularVesselTicks > slowestCircularVesselTicks)
					{
						slowestCircularVesselName = vessel.vesselName;
						slowestCircularVesselTicks = totalCircularVesselTicks;
					}

					#endif
				}
			}

			#if BENCH
			totalVesselLoopTicks = performanceTimer.ElapsedTicks - startVesselLoopTicks;
			#endif

			CelestialBody bodyOccludingKerbin = null;

			log.AppendFormat("\n{0} ({1}): Search done, figuring status.", this.ToString(), this.GetType().Name);
			log.AppendFormat(
					"\n{0}: nearestRelay={1} ({2})), bestOccludedRelay={3} ({4}), kerbinRelayRate={5} EC/MiT)",
				this,
				this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
				cheapestRelayRate,
				this.bestOccludedRelay == null ? "null" : this.bestOccludedRelay.ToString(),
				cheapestOccludedRelayRate,
				kerbinRelayRate
			);
			
			#if BENCH
			startKerbinLOSTicks = this.performanceTimer.ElapsedTicks;
			#endif

			// If we don't have LOS to Kerbin, focus on relays
			if (
				ARConfiguration.RequireLineOfSight &&
				!this.vessel.hasLineOfSightTo(Kerbin, out bodyOccludingKerbin, ARConfiguration.RadiusRatio)
			)
			{
				#if BENCH
				totalKerbinLOSTicks = this.performanceTimer.ElapsedTicks - startKerbinLOSTicks;
				#endif
				log.AppendFormat("\n\tKerbin LOS is blocked by {0}.", bodyOccludingKerbin.bodyName);

				// If we're in range of the "nearest" (actually cheapest) relay, use it.
				if (this.IsInRangeOf(this.nearestRelay))
				{
					log.AppendFormat("\n\t\tCan transmit to nearby relay {0}).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString()
					);

					this.KerbinDirect = false;
					this.canTransmit = true;
					this.targetRelay = this.nearestRelay;
				}
				// If this isn't true, we can't transmit, but pick a second best of bestOccludedRelay and Kerbin anyway
				else
				{
					log.AppendFormat("\n\t\tCan't transmit to nearby relay {0}.",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString()
					);

					this.canTransmit = false;

					// If the best occluded relay is cheaper than Kerbin, check it against the nearest relay.
					// Since cheapestOccludedRelayRate is infinity if there are no occluded relays, this is safe
					if (cheapestOccludedRelayRate < kerbinRelayRate)
					{
						log.AppendFormat("\n\t\t\tBest occluded relay is cheaper than Kerbin ({0} < {1})",
							cheapestOccludedRelayRate, kerbinRelayRate);
						
						this.KerbinDirect = false;

						// If the nearest relay is cheaper than the best occluded relay, pick it.
						// Since cheapestRelayRate is infinity if there are no nearby relays, this is safe.
						if (cheapestRelayRate < cheapestOccludedRelayRate)
						{
							log.AppendFormat("\n\t\t\t\t...but the cheapest relay is cheaper ({0} < {1}), so picking it.",
								cheapestRelayRate, cheapestOccludedRelayRate);
							
							this.targetRelay = this.nearestRelay;
							this.firstOccludingBody = null;
						}
						// Otherwise, target the best occluded relay.
						else
						{
							log.AppendFormat("\n\t\t\t\t...and cheaper than the nearest relay ({0} >= {1}), so picking it.",
								cheapestRelayRate, cheapestOccludedRelayRate);
							
							this.targetRelay = bestOccludedRelay;
							this.firstOccludingBody = bodyOccludingBestOccludedRelay;
						}
					}
					// Otherwise, check Kerbin against the "nearest" (cheapest) relay.
					else
					{
						log.AppendFormat("\n\t\t\tKerbin is cheaper than the best occluded relay ({0} >= {1})",
							cheapestOccludedRelayRate, kerbinRelayRate);
						
						// If the "nearest" (cheapest) relay is cheaper than Kerbin, pick it.
						// Since cheapestRelayRate is infinity if there are no nearby relays, this is safe.
						if (cheapestRelayRate < kerbinRelayRate)
						{
							log.AppendFormat("\n\t\t\t\t...but the nearest relay is cheaper ({0} < {1}), so picking it.",
								cheapestRelayRate, kerbinRelayRate);

							// Since we have LOS, blank the first occluding body.
							this.firstOccludingBody = null;

							this.KerbinDirect = false;
							this.targetRelay = this.nearestRelay;
						}
						// Otherwise, pick Kerbin.
						else
						{
							log.AppendFormat("\n\t\t\t\t...and cheaper than the nearest relay ({0} >= {1}), so picking it.",
								cheapestRelayRate, kerbinRelayRate);
							
							this.KerbinDirect = true;
							this.firstOccludingBody = bodyOccludingKerbin;
							this.targetRelay = null;
						}
					}
				}
			}
			// If we do have LOS to Kerbin, try to prefer the closest of nearestRelay and Kerbin
			else
			{
				#if BENCH
				totalKerbinLOSTicks = this.performanceTimer.ElapsedTicks - startKerbinLOSTicks;
				#endif

				log.AppendFormat("\n\tKerbin is in LOS.");

				// If the nearest relay is in range, we can transmit.
				if (this.IsInRangeOf(this.nearestRelay))
				{
					log.AppendFormat("\n\t\tCan transmit to nearby relay {0} (in range).",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString()
					);

					this.canTransmit = true;

					// If the nearestRelay is closer than Kerbin, use it.
					if (cheapestRelayRate < kerbinRelayRate)
					{
						log.AppendFormat("\n\t\t\tPicking relay {0} over Kerbin ({1} < {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							cheapestRelayRate, kerbinRelayRate);

						this.KerbinDirect = false;
						this.targetRelay = this.nearestRelay;
					}
					// Otherwise, Kerbin is closer, so use it.
					else
					{
						log.AppendFormat("\n\t\t\tBut picking Kerbin over nearby relay {0} ({1} >= {2}).",
							this.nearestRelay == null ? "null" : this.nearestRelay.ToString(),
							cheapestRelayRate, kerbinRelayRate);

						this.KerbinDirect = true;
						this.targetRelay = null;
					}
				}
				// If the nearest relay is out of range, we still need to check on Kerbin.
				else
				{
					log.AppendFormat("\n\t\tCheapest relay {0} is out of range.",
						this.nearestRelay == null ? "null" : this.nearestRelay.ToString()
					);

					// If Kerbin is in range, use it.
					if (this.IsInRangeOf(Kerbin))
					{
						log.AppendFormat("\n\t\t\tCan transmit to Kerbin (in range).");

						this.canTransmit = true;
						this.KerbinDirect = true;
						this.targetRelay = null;
					}
					// If Kerbin is out of range and the nearest relay is out of range, pick a second best between
					// Kerbin and bestOccludedRelay
					else
					{
						log.AppendFormat("\n\t\t\tCan't transmit to Kerbin (out of range).");

						this.canTransmit = false;

						// If the best occluded relay is cheaper than Kerbin, check it against the nearest relay.
						// Since bestOccludedSqrDistance is infinity if there are no occluded relays, this is safe
						if (cheapestOccludedRelayRate < kerbinRelayRate)
						{
							log.AppendFormat("\n\t\t\tBest occluded relay is closer than Kerbin ({0} < {1})",
								cheapestOccludedRelayRate, kerbinRelayRate);
							
							this.KerbinDirect = false;

							// If the nearest relay is closer than the best occluded relay, pick it.
							// Since cheapestRelayRate is infinity if there are no nearby relays, this is safe.
							if (cheapestRelayRate < cheapestOccludedRelayRate)
							{
								log.AppendFormat("\n\t\t\t\t...but the cheapest relay is cheaper ({0} < {1}), so picking it.",
									cheapestRelayRate, cheapestOccludedRelayRate);
								
								this.targetRelay = this.nearestRelay;
								this.firstOccludingBody = null;
							}
							// Otherwise, target the best occluded relay.
							else
							{
								log.AppendFormat("\n\t\t\t\t...and cheaper than the cheapest relay ({0} >= {1}), so picking it.",
									cheapestRelayRate, cheapestOccludedRelayRate);
								
								this.targetRelay = bestOccludedRelay;
								this.firstOccludingBody = bodyOccludingBestOccludedRelay;
							}
						}
						// Otherwise, check Kerbin against the nearest relay.
						// Since we have LOS, blank the first occluding body.
						else
						{
							log.AppendFormat("\n\t\t\tKerbin is cheaper than the best occluded relay ({0} >= {1})",
								cheapestOccludedRelayRate, kerbinRelayRate);
							
							this.firstOccludingBody = null;

							// If the nearest relay is cheaper than Kerbin, pick it.
							// Since cheapestRelayRate is infinity if there are no nearby relays, this is safe.
							if (cheapestRelayRate < kerbinRelayRate)
							{
								log.AppendFormat("\n\t\t\t\t...but the nearest relay is cheaper ({0} < {1}), so picking it.",
									cheapestRelayRate, kerbinRelayRate);
								
								this.KerbinDirect = false;
								this.targetRelay = this.nearestRelay;
							}
							// Otherwise, pick Kerbin.
							else
							{
								log.AppendFormat("\n\t\t\t\t...and cheaper than the nearest relay ({0} >= {1}), so picking it.",
									cheapestRelayRate, kerbinRelayRate);
								
								this.KerbinDirect = true;
								this.targetRelay = null;
							}
						}
					}
				}
			}

			if (ARConfiguration.UseAdditiveRanges)
			{
				if (this.KerbinDirect)
				{
					this.NominalLinkSqrDistance = this.nominalTransmitDistance * ARConfiguration.KerbinNominalRange;
					this.MaximumLinkSqrDistance = this.maxTransmitDistance * ARConfiguration.KerbinRelayRange;
				}
				else
				{
					this.NominalLinkSqrDistance = this.nominalTransmitDistance * this.targetRelay.nominalTransmitDistance;
					this.MaximumLinkSqrDistance = this.maxTransmitDistance * this.targetRelay.maxTransmitDistance;
				}
			}
			else
			{
				this.NominalLinkSqrDistance = this.nominalTransmitDistance * this.nominalTransmitDistance;
				this.MaximumLinkSqrDistance = this.maxTransmitDistance * this.maxTransmitDistance;
			}

			if (this.canTransmit)
			{
				if (this.CurrentLinkSqrDistance < this.NominalLinkSqrDistance)
				{
					this.LinkStatus = ConnectionStatus.Optimal;
				}
				else
				{
					this.LinkStatus = ConnectionStatus.Suboptimal;
				}
			}
			else
			{
				this.LinkStatus = ConnectionStatus.None;
			}

			#if BENCH
			statusResolutionTicks = performanceTimer.ElapsedTicks - startKerbinLOSTicks - totalKerbinLOSTicks;
			#endif

			log.AppendFormat("\n{0}: Target search and status determination complete.", this.ToString());
			
			#if DEBUG
			} catch (Exception ex) {
				log.AppendFormat("\nCaught {0}: {1}\n{2}", ex.GetType().FullName, ex.ToString(), ex.StackTrace);
			#if QUIT_ON_EXCEPTION
				UnityEngine.Application.Quit();
			#endif
			} finally {
			#endif
			log.Print(false);
			#if DEBUG
			}
			#endif

			#if BENCH
			AntennaRelay.searchTimer += (ulong)this.performanceTimer.ElapsedTicks;
			AntennaRelay.searchCount++;
			this.performanceTimer.Stop();

			double averageSearchTime = (double)AntennaRelay.searchTimer / (double)AntennaRelay.searchCount;

			if (AntennaRelay.searchCount >= 8000u / (ulong)ARConfiguration.UpdateDelay)
			{
				AntennaRelay.searchCount = 0u;
				AntennaRelay.searchTimer = 0u;

				AntennaRelay.averager.AddItem(averageSearchTime);
				AntennaRelay.doubleAverageTime = (long)(AntennaRelay.averager.Average * 2d);
			}

			if (this.performanceTimer.ElapsedTicks > AntennaRelay.doubleAverageTime)
			{
				System.Text.StringBuilder sb = Tools.GetStringBuilder();

				sb.AppendFormat(Tools.SIFormatter, "[AntennaRelay] FindNearestRelay search for {0}" +
					" took significantly longer than average ({1:S3}s vs {2:S3}s)",
					this.ToString(),
					(double)this.performanceTimer.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency,
					(double)AntennaRelay.averager.Average / (double)System.Diagnostics.Stopwatch.Frequency
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\tVessel loop time: {0:S3}s",
					(double)totalVesselLoopTicks / (double)System.Diagnostics.Stopwatch.Frequency
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\t\tAverage vessel time for each of {1} vessels: {0:S3}s",
					(double)totalVesselLoopTicks / (double)System.Diagnostics.Stopwatch.Frequency /
					(double)usefulVesselCount,
					usefulVesselCount
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\t\tSlowest vessel LOS check: {0:S3}s to {1}",
					(double)slowestLOSVesselTicks / (double)System.Diagnostics.Stopwatch.Frequency,
					slowestLOSVesselName
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\t\tSlowest circular relay check: {0:S3}s for {1}",
					(double)slowestCircularVesselTicks / (double)System.Diagnostics.Stopwatch.Frequency,
					slowestCircularVesselName
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\tKerbin LOS check: {0:S3}s",
					(double)totalKerbinLOSTicks / (double)System.Diagnostics.Stopwatch.Frequency
				);

				sb.AppendFormat(Tools.SIFormatter, "\n\tStatus resolution check: {0:S3}s",
					(double)statusResolutionTicks / (double)System.Diagnostics.Stopwatch.Frequency
				);

				// sb.AppendFormat(Tools.SIFormatter, "", start)

				this.LogWarning(sb.ToString());

				Tools.PutStringBuilder(sb);
			}
			#endif
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
			this.KerbinDirect = true;
			this.moduleRef = module;

			#if BENCH
			AntennaRelay.relayCount++;
			#endif

			this.LogDebug("{0}: constructed {1}", this.GetType().Name, this.ToString());
		}
	}
}

