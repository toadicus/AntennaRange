using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaRange
{
	public static class Extensions
	{
		public static double DistanceTo(this Vessel vesselOne, Vessel vesselTwo)
		{
			return (vesselOne.GetWorldPos3D() - vesselTwo.GetWorldPos3D()).magnitude;
		}

		public static double DistanceTo(this Vessel vessel, CelestialBody body)
		{
			return (vessel.GetWorldPos3D() - body.position).magnitude;
		}

		public static double DistanceTo(this IAntennaRelay relay, Vessel Vessel)
		{
			return relay.vessel.DistanceTo(Vessel);
		}

		public static double DistanceTo(this IAntennaRelay relay, CelestialBody body)
		{
			return relay.vessel.DistanceTo(body);
		}

		public static double DistanceTo(this IAntennaRelay relayOne, IAntennaRelay relayTwo)
		{
			return relayOne.DistanceTo(relayTwo.vessel);
		}

		public static IEnumerable<IAntennaRelay> GetAntennaRelays (this Vessel vessel)
		{
			Tools.PostDebugMessage(string.Format(
				"{0}: Getting antenna relays from vessel {1}.",
				"IAntennaRelay",
				vessel.name
			));

			List<IAntennaRelay> Transmitters;

			if (vessel.loaded) {
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is loaded.",
					"IAntennaRelay",
					vessel.name
					));

				Transmitters = vessel.Parts
					.SelectMany (p => p.Modules.OfType<IAntennaRelay> ())
					.ToList();
			} else {
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is not loaded.",
					"IAntennaRelay",
					vessel.name
					));

				Transmitters = new List<IAntennaRelay>();

				foreach (ProtoPartModuleSnapshot ms in vessel.protoVessel.protoPartSnapshots.SelectMany(ps => ps.modules))
				{
					if (ms.IsAntenna())
					{
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

			return Transmitters;
		}

		public static bool IsAntenna (this PartModule module)
		{
			return module.Fields.GetValue<bool> ("IsAntenna");
		}

		public static bool IsAntenna(this ProtoPartModuleSnapshot protomodule)
		{
			bool result;

			return Boolean.TryParse (protomodule.moduleValues.GetValue ("IsAntenna") ?? "False", out result)
				? result : false;
		}
	}
}

