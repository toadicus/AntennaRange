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
			// If the vessel is not loaded, we need to find ProtoPartModuleSnapshots with a true IsAntenna field.
			else
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is not loaded.",
					"IAntennaRelay",
					vessel.name
					));

				Transmitters = new List<IAntennaRelay>();

				// Loop through the ProtoPartModuleSnapshots in this Vessel
				foreach (ProtoPartModuleSnapshot ms in vessel.protoVessel.protoPartSnapshots.SelectMany(ps => ps.modules))
				{
					// If they are antennas...
					if (ms.IsAntenna())
					{
						// ...add a new ProtoAntennaRelay wrapper to the list.
						Transmitters.Add(new ProtoAntennaRelay(ms, vessel));
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

		// Returns true if this PartModule contains a True IsAntenna field, false otherwise.
		public static bool IsAntenna (this PartModule module)
		{
			return module.Fields.GetValue<bool> ("IsAntenna");
		}

		// Returns true if this ProtoPartModuleSnapshot contains a persistent True IsAntenna field, false otherwise
		public static bool IsAntenna(this ProtoPartModuleSnapshot protomodule)
		{
			bool result;

			return Boolean.TryParse (protomodule.moduleValues.GetValue ("IsAntenna") ?? "False", out result)
				? result : false;
		}
	}
}

