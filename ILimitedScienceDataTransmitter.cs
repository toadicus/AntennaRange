using System;

namespace AntennaRange
{
	public interface ILimitedScienceDataTransmitter : IScienceDataTransmitter
	{
		float maxTransmitDistance { get; }

		bool relayChecked { get; }
	}
}

