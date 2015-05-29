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
		/// Returns the world distance between two <see cref="AntennaRange.IPositionedObject"/> objects.
		/// </summary>
		public static double DistanceTo(this IPositionedObject object1, IPositionedObject object2)
		{
			double dist = (object1.WorldPos - object2.WorldPos).magnitude;

			if (object1 is BodyWrapper)
			{
				dist -= ((BodyWrapper)object1).HostObject.Radius;
			}

			if (object2 is BodyWrapper)
			{
				dist -= ((BodyWrapper)object2).HostObject.Radius;
			}

			return dist;
		}

		/// <summary>
		/// Returns the world distance between an <see cref="AntennaRange.IAntennaRelay"/> and
		/// an <see cref="AntennaRange.IPositionedObject"/>.
		/// </summary>
		public static double DistanceTo(this IAntennaRelay relay, IPositionedObject obj)
		{
			return relay.Host.DistanceTo(obj);
		}

		/// <summary>
		/// Returns the world distance between two <see cref="AntennaRange.IAntennaRelay"/> objects.
		/// </summary>
		public static double DistanceTo(this IAntennaRelay relay1, IAntennaRelay relay2)
		{
			return relay1.Host.DistanceTo(relay2.Host);
		}

		/// <summary>
		/// Returns the world distance between an <see cref="AntennaRange.IAntennaRelay"/> and a <see cref="Vessel"/>.
		/// </summary>
		public static double DistanceTo(this IAntennaRelay relay, Vessel vessel)
		{
			return relay.Host.DistanceTo((VesselWrapper)vessel);
		}

		/// <summary>
		/// Returns the square of the world distance between two <see cref="AntennaRange.IPositionedObject"/> objects.
		/// </summary>
		public static double sqrDistanceTo(this IPositionedObject object1, IPositionedObject object2)
		{
			if (object1 is BodyWrapper || object2 is BodyWrapper)
			{
				double dist = object1.DistanceTo(object2);
				dist *= dist;
				return dist;
			}
			else
			{
				return (object1.WorldPos - object2.WorldPos).sqrMagnitude;
			}
		}

		/// <summary>
		/// Returns the square of the world distance between an <see cref="AntennaRange.IAntennaRelay"/>
		/// and a <see cref="Vessel"/>.
		/// </summary>
		public static double sqrDistanceTo(this IAntennaRelay relay, Vessel vessel)
		{
			return relay.Host.sqrDistanceTo((VesselWrapper)vessel);
		}

		/// <summary>
		/// Returns the square of the world distance between an <see cref="AntennaRange.IAntennaRelay"/>
		/// and a <see cref="CelestialBody"/>.
		/// </summary>
		public static double sqrDistanceTo(this IAntennaRelay relay, CelestialBody body)
		{
			return relay.Host.sqrDistanceTo((BodyWrapper)body);
		}

		/// <summary>
		/// Returns <c>true</c> if the origin <see cref="AntennaRange.IPositionedObject"/> has line of sight to the
		/// target <see cref="AntennaRange.IPositionedObject"/>, <c>false</c> otherwise.  If not, firstOccludingBody
		/// outputs the first <see cref="CelestialBody"/> blocking line of sight.
		/// </summary>
		public static bool hasLineOfSightTo(
			this IPositionedObject origin,
			IPositionedObject target,
			out CelestialBody firstOccludingBody,
			double sqrRatio = 1d
		)
		{
			CelestialBody[] excludedBodies;

			if (target is BodyWrapper)
			{
				excludedBodies = new CelestialBody[] { (target as BodyWrapper).HostObject };
			}
			else
			{
				excludedBodies = null;
			}

			return VectorTools.IsLineOfSightBetween(
				origin.WorldPos,
				target.WorldPos,
				out firstOccludingBody,
				excludedBodies,
				sqrRatio
			);
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
				if (relay.CanTransmit())
				{
					canTransmit = true;
					if (relay.transmitDistance <= relay.nominalTransmitDistance)
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

