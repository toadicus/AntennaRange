// AntennaRange
//
// BodyRelay.cs
//
// Copyright © 2015, toadicus
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

#pragma warning disable 1591
#define DEBUG

using KSP;
using System;
using System.Collections.Generic;
using ToadicusTools;
using UnityEngine;

namespace AntennaRange
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class KerbinRelay : MonoBehaviour, IAntennaRelay
	{
		public static KerbinRelay Kerbin;
		public static int TrackingStationMaxLevel = 2;

		private List<double> stationLevelRanges;

		public IList<double> StationLevelRange
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		public BodyWrapper Host
		{
			get;
			private set;
		}
		IPositionedObject IAntennaRelay.Host
		{
			get
			{
				return (IPositionedObject)this.Host;
			}
		}

		/// <summary>
		/// Gets the target <see cref="AntennaRange.IAntennaRelay"/>relay.
		/// </summary>
		public IAntennaRelay targetRelay
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Gets the distance to the nearest relay or Kerbin, whichever is closer.
		/// </summary>
		public double transmitDistance
		{
			get
			{
				return double.PositiveInfinity;
			}
		}

		/// <summary>
		/// Gets the nominal transmit distance at which the Antenna behaves just as prescribed by Squad's config.
		/// </summary>
		public double nominalTransmitDistance
		{
			get;
			private set;
		}

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		public double maxTransmitDistance
		{
			get;
			private set;
		}

		/// <summary>
		/// The first CelestialBody blocking line of sight to a 
		/// </summary>
		public CelestialBody firstOccludingBody
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.IAntennaRelay"/> Relay is communicating
		/// directly with Kerbin.
		/// </summary>
		public bool KerbinDirect
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// Gets the Part title.
		/// </summary>
		public string Title
		{
			get
			{
				if (this.Host != null)
				{
					return string.Format("Tracking Station on {0}", this.Host.HostObject.bodyName);
				}
				else
				{
					return "Tracking Station on an unknown world";
				}
			}
		}

		/// <summary>
		/// Determines whether this instance can transmit.
		/// <c>true</c> if this instance can transmit; otherwise, <c>false</c>.
		/// </summary>
		public bool CanTransmit()
		{
			return true;
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		public void FindNearestRelay()
		{
			return;
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.IAntennaRelay"/>.
		/// </summary>
		public override string ToString()
		{
			return this.Title;
		}

		private void Awake()
		{
			this.stationLevelRanges = new List<double>();
			this.StationLevelRange = this.stationLevelRanges.AsReadOnly();

			this.stationLevelRanges.Add(51696576d);
			this.stationLevelRanges.Add(37152180000d);
			this.stationLevelRanges.Add(224770770000d);

			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				GameEvents.OnKSCFacilityUpgraded.Add(this.onFacilityUpgraded);
			}

		}

		private void Update()
		{
			if (FlightGlobals.ready && Kerbin == null)
			{
				this.Host = (BodyWrapper)FlightGlobals.GetHomeBody();

				if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
				{
					this.maxTransmitDistance = this.stationLevelRanges[this.TrackingStationLevel()];
				}
				else
				{
					this.maxTransmitDistance = this.stationLevelRanges[2];
				}

				this.nominalTransmitDistance = this.maxTransmitDistance;

				Kerbin = this;
			}
		}

		private void OnDestroy()
		{
			GameEvents.OnKSCFacilityUpgraded.Remove(this.onFacilityUpgraded);
		}

		public int TrackingStationLevel()
		{
			this.LogDebug("Tracking station level: {0} ({1} * {2})",(int)(
				ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) *
				(float)TrackingStationMaxLevel),
				ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation),
				TrackingStationMaxLevel
			);

			return (int)(
				ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) *
				(float)TrackingStationMaxLevel);
		}

		private void onFacilityUpgraded(Upgradeables.UpgradeableFacility fac, int level)
		{
			// fac.FacilityLevel
			this.maxTransmitDistance = this.stationLevelRanges[this.TrackingStationLevel()];
			this.nominalTransmitDistance = this.maxTransmitDistance;
		}
	}
}

