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
	/*
	 * A class of utility extensions for Vessels and Relays to help find a relay path back to Kerbin.
	 * */
	public static class Extensions
	{
		/// <summary>
		/// Returns the distance between this Vessel and another Vessel.
		/// </summary>
		/// <param name="vesselOne">This <see cref="Vessel"/><see ></param>
		/// <param name="vesselTwo">Another <see cref="Vessel"/></param>
		public static double DistanceTo(this Vessel vesselOne, Vessel vesselTwo)
		{
			return (vesselOne.GetWorldPos3D() - vesselTwo.GetWorldPos3D()).magnitude;
		}

		/// <summary>
		/// Returns the distance between this Vessel and a CelestialBody
		/// </summary>
		/// <param name="vessel">This Vessel</param>
		/// <param name="body">A <see cref="CelestialBody"/></param>
		public static double DistanceTo(this Vessel vessel, CelestialBody body)
		{
			return (vessel.GetWorldPos3D() - body.position).magnitude;
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
			return relay.vessel.DistanceTo(body);
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
		/// Returns all of the PartModules or ProtoPartModuleSnapshots implementing IAntennaRelay in this Vessel.
		/// </summary>
		/// <param name="vessel">This <see cref="Vessel"/></param>
		public static IEnumerable<IAntennaRelay> GetAntennaRelays (this Vessel vessel)
		{
			Tools.PostDebugMessage(string.Format(
				"{0}: Getting antenna relays from vessel {1}.",
				"IAntennaRelay",
				vessel.name
			));

			List<IAntennaRelay> Transmitters;

			// If the vessel is loaded, we can fetch modules implementing IAntennaRelay directly.
			if (vessel.loaded) {
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is loaded.",
					"IAntennaRelay",
					vessel.name
					));

				// Gets a list of PartModules implementing IAntennaRelay
				Transmitters = vessel.Parts
					.SelectMany (p => p.Modules.OfType<IAntennaRelay> ())
					.ToList();
			}
			// If the vessel is not loaded, we need to build ProtoAntennaRelays when we find relay ProtoPartSnapshots.
			else
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is not loaded.",
					"IAntennaRelay",
					vessel.name
					));

				Transmitters = new List<IAntennaRelay>();

				// Loop through the ProtoPartModuleSnapshots in this Vessel
				foreach (ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
				{
					IAntennaRelay relayModule;
					ProtoAntennaRelay protoRelay;
					int partHash;

					relayModule = null;
					protoRelay = null;
					partHash = pps.GetHashCode();

					if (prebuiltProtoRelays.ContainsKey(partHash))
					{
						protoRelay = prebuiltProtoRelays[partHash];
					}
					else
					{
						foreach (PartModule module in PartLoader.getPartInfoByName(pps.partName).partPrefab.Modules)
						{
							if (module is IAntennaRelay)
							{
								relayModule = module as IAntennaRelay;

								protoRelay = new ProtoAntennaRelay(relayModule, vessel);
								prebuiltProtoRelays[partHash] = protoRelay;
								break;
							}
						}
					}

					if (protoRelay != null)
					{
						Transmitters.Add(protoRelay);
					}
				}
			}

			Tools.PostDebugMessage(string.Format(
				"{0}: vessel {1} has {2} transmitters.",
				"IAntennaRelay",
				vessel.name,
				Transmitters.Count
				));

			// Return the list of IAntennaRelays
			return Transmitters;
		}

		private static Dictionary<int, ProtoAntennaRelay> prebuiltProtoRelays = new Dictionary<int, ProtoAntennaRelay>();
	}
}

