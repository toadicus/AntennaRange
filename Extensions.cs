using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaRange
{
	public static class Extensions
	{
		public static IEnumerable<ILimitedScienceDataTransmitter> GetTransmitters (this Vessel vessel)
		{
			List<ILimitedScienceDataTransmitter> Transmitters;

			if (vessel.loaded) {
				Transmitters = vessel.Parts
					.SelectMany (p => p.Modules.OfType<ILimitedScienceDataTransmitter> ())
					.ToList();
			} else {
				Transmitters = new List<ILimitedScienceDataTransmitter>();

				foreach (ProtoPartModuleSnapshot ms in vessel.protoVessel.protoPartSnapshots.SelectMany(ps => ps.modules))
				{
					if (ms.IsAntenna())
					{
						Transmitters.Add(new ProtoDataTransmitter(ms, vessel));
					}
				}
			}

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

