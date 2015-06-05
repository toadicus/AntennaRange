// AntennaRange
//
// Extensions.cs
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
using ToadicusTools;

namespace AntennaRange
{
	/// <summary>
	/// A class of utility extensions for Vessels and Relays to help find a relay path back to Kerbin.
	/// </summary>
	public static class RelayExtensions
	{
		/// <summary>
		/// Returns the distance between two IAntennaRelays.
		/// </summary>
		/// <param name="relayOne">Relay one.</param>
		/// <param name="relayTwo">Relay two.</param>
		public static double DistanceTo(this IAntennaRelay relayOne, IAntennaRelay relayTwo)
		{
			return relayOne.vessel.DistanceTo(relayTwo.vessel);
		}

		/// <summary>
		/// Returns the distance from this IAntennaRelay to the given CelestialBody
		/// </summary>
		/// <param name="relay">Relay.</param>
		/// <param name="body">Body.</param>
		public static double SqrDistanceTo(this IAntennaRelay relay, CelestialBody body)
		{
			double range = relay.vessel.DistanceTo(body) - body.Radius;

			return range * range;
		}

		/// <summary>
		/// Returns the distance between two IAntennaRelays.
		/// </summary>
		/// <param name="relayOne">Relay one.</param>
		/// <param name="relayTwo">Relay two.</param>
		public static double SqrDistanceTo(this IAntennaRelay relayOne, IAntennaRelay relayTwo)
		{
			return relayOne.vessel.sqrDistanceTo(relayTwo.vessel);
		}

		/// <summary>
		/// Returns the distance from this IAntennaRelay to the given CelestialBody
		/// </summary>
		/// <param name="relay">Relay.</param>
		/// <param name="body">Body.</param>
		public static double DistanceTo(this IAntennaRelay relay, CelestialBody body)
		{
			double range = relay.vessel.DistanceTo(body) - body.Radius;

			return range;
		}

		/// <summary>
		/// Returns the distance between this IAntennaRelay and a Vessel
		/// </summary>
		/// <param name="relay">This <see cref="IAntennaRelay"/></param>
		/// <param name="Vessel">A <see cref="Vessel"/></param>
		public static double DistanceTo(this AntennaRelay relay, Vessel Vessel)
		{
			return relay.vessel.DistanceTo(Vessel);
		}

		/// <summary>
		/// Returns the distance between this IAntennaRelay and a CelestialBody
		/// </summary>
		/// <param name="relay">This <see cref="IAntennaRelay"/></param>
		/// <param name="body">A <see cref="CelestialBody"/></param>
		public static double DistanceTo(this AntennaRelay relay, CelestialBody body)
		{
			return relay.vessel.DistanceTo(body) - body.Radius;
		}

		/// <summary>
		/// Returns the distance between this IAntennaRelay and another IAntennaRelay
		/// </summary>
		/// <param name="relayOne">This <see cref="IAntennaRelay"/></param>
		/// <param name="relayTwo">Another <see cref="IAntennaRelay"/></param>
		public static double DistanceTo(this AntennaRelay relayOne, IAntennaRelay relayTwo)
		{
			return relayOne.DistanceTo(relayTwo.vessel);
		}

		/// <summary>
		/// Returns the square of the distance between this IAntennaRelay and a Vessel
		/// </summary>
		/// <param name="relay">This <see cref="IAntennaRelay"/></param>
		/// <param name="vessel">A <see cref="Vessel"/></param>
		public static double sqrDistanceTo(this AntennaRelay relay, Vessel vessel)
		{
			return relay.vessel.sqrDistanceTo(vessel);
		}

		/// <summary>
		/// Returns the square of the distance between this IAntennaRelay and a CelestialBody
		/// </summary>
		/// <param name="relay">This <see cref="IAntennaRelay"/></param>
		/// <param name="body">A <see cref="CelestialBody"/></param>
		public static double sqrDistanceTo(this AntennaRelay relay, CelestialBody body)
		{
			return relay.vessel.sqrDistanceTo(body);
		}

		/// <summary>
		/// Returns the square of the distance between this IAntennaRelay and another IAntennaRelay
		/// </summary>
		/// <param name="relayOne">This <see cref="IAntennaRelay"/></param>
		/// <param name="relayTwo">Another <see cref="IAntennaRelay"/></param>
		public static double sqrDistanceTo(this AntennaRelay relayOne, IAntennaRelay relayTwo)
		{
			return relayOne.vessel.sqrDistanceTo(relayTwo.vessel);
		}

		/// <summary>
		/// Returns all of the PartModules or ProtoPartModuleSnapshots implementing IAntennaRelay in this Vessel.
		/// </summary>
		/// <param name="vessel">This <see cref="Vessel"/></param>
		public static IList<IAntennaRelay> GetAntennaRelays (this Vessel vessel)
		{
			return RelayDatabase.Instance[vessel];
		}

		/// <summary>
		/// Determines if the specified vessel has a connected relay.
		/// </summary>
		/// <returns><c>true</c> if the specified vessel has a connected relay; otherwise, <c>false</c>.</returns>
		/// <param name="vessel"></param>
		public static bool HasConnectedRelay(this Vessel vessel)
		{
			IList<IAntennaRelay> vesselRelays = RelayDatabase.Instance[vessel];
			IAntennaRelay relay;
			for (int rIdx = 0; rIdx < vesselRelays.Count; rIdx++)
			{
				relay = vesselRelays[rIdx];
				if (relay.CanTransmit())
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets the <see cref="AntennaRange.ConnectionStatus"/> for this <see cref="Vessel"/>
		/// </summary>
		/// <param name="vessel">This <see cref="Vessel"/></param>
		public static ConnectionStatus GetConnectionStatus(this Vessel vessel)
		{
			bool canTransmit = false;

			IList<IAntennaRelay> vesselRelays = RelayDatabase.Instance[vessel];
			IAntennaRelay relay;
			for (int rIdx = 0; rIdx < vesselRelays.Count; rIdx++)
			{
				relay = vesselRelays[rIdx];
				if (relay.LinkStatus > ConnectionStatus.None)
				{
					canTransmit = true;

					if (relay.LinkStatus == ConnectionStatus.Optimal)
					{
						return ConnectionStatus.Optimal;
					}
				}
			}

			if (canTransmit)
			{
				return ConnectionStatus.Suboptimal;
			}
			else
			{
				return ConnectionStatus.None;
			}
		}

		/// <summary>
		/// Gets the best relay on this Vessel.  The best relay may not be able to transmit.
		/// </summary>
		/// <param name="vessel">This <see cref="Vessel"/></param>
		public static IAntennaRelay GetBestRelay(this Vessel vessel)
		{
			return RelayDatabase.Instance.GetBestVesselRelay(vessel);
		}
	}

	#pragma warning disable 1591
	/// <summary>
	/// An Enum describing the connection status of a vessel or relay.
	/// </summary>
	public enum ConnectionStatus
	{
		None,
		Suboptimal,
		Optimal
	}
}

