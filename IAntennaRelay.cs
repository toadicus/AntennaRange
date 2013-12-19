using KSP;
using System;

namespace AntennaRange
{
	public interface IAntennaRelay
	{
		Vessel vessel { get; }

		float maxTransmitDistance { get; }

		bool relayChecked { get; }

		bool CanTransmit();
	}
}

