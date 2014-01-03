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

