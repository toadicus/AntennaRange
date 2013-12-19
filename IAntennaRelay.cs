using KSP;
using System;

namespace AntennaRange
{
	/*
	 * Interface defining the basic functionality of AntennaRelay modules for AntennaRange.
	 * */
	public interface IAntennaRelay
	{
		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		/// <value>The parent Vessel.</value>
		Vessel vessel { get; }

		/// <summary>
		/// Gets the distance to the nearest relay or Kerbin, whichever is closer.
		/// </summary>
		/// <value>The distance to the nearest relay or Kerbin, whichever is closer.</value>
		double transmitDistance { get; }

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		float maxTransmitDistance { get; }

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		bool relayChecked { get; }

		/// <summary>
		/// Determines whether this instance can transmit.
		/// </summary>
		/// <returns><c>true</c> if this instance can transmit; otherwise, <c>false</c>.</returns>
		bool CanTransmit();
	}
}

