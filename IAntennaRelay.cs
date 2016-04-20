// AntennaRange
//
// IAntennaRelay.cs
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

using KSP;
using System;

namespace AntennaRange
{
	/// <summary>
	/// Interface defining the basic functionality of AntennaRelay modules for AntennaRange.
	/// </summary>
	public interface IAntennaRelay
	{
		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		Vessel vessel { get; }

		/// <summary>
		/// Gets the target <see cref="AntennaRange.IAntennaRelay"/>relay.
		/// </summary>
		IAntennaRelay targetRelay { get; }

		float PacketSize { get; set; }

		float BasePacketSize { get; }

		float PacketResourceCost { get; set; }

		float BasePacketResourceCost { get; }

		float PacketThrottle { get; }

		float MaxDataFactor { get; }

		/// <summary>
		/// Gets the data resource cost in EC/MiT.
		/// </summary>
		/// <value>The data resource cost in EC/MiT.</value>
		double DataResourceCost { get; }

		/// <summary>
		/// Gets the current network resource rate in EC/MiT.
		/// </summary>
		/// <value>The current network resource rate in EC/MiT.</value>
		double CurrentNetworkResourceRate { get; }

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.IAntennaRelay"/> Relay is communicating
		/// directly with Kerbin.
		/// </summary>
		bool KerbinDirect { get; }

		/// <summary>
		/// The link distance, in meters, at which this relay behaves nominally.
		/// </summary>
		double NominalLinkSqrDistance { get; }

		/// <summary>
		/// The link distance, in meters, beyond which this relay cannot operate.
		/// </summary>
		double MaximumLinkSqrDistance { get; }

		/// <summary>
		/// Gets the distance to the nearest relay or Kerbin, whichever is closer.
		/// </summary>
		double CurrentLinkSqrDistance { get; }

		/// <summary>
		/// Gets the link status.
		/// </summary>
		ConnectionStatus LinkStatus { get; }

		/// <summary>
		/// Gets the nominal transmit distance at which the Antenna behaves just as prescribed by Squad's config.
		/// </summary>
		double nominalTransmitDistance { get; }

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		double maxTransmitDistance { get; }

		/// <summary>
		/// The first CelestialBody blocking line of sight to a 
		/// </summary>
		CelestialBody firstOccludingBody { get; }

		/// <summary>
		/// Gets the Part title.
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Determines whether this instance can transmit.
		/// <c>true</c> if this instance can transmit; otherwise, <c>false</c>.
		/// </summary>
		bool CanTransmit();

		/// <summary>
		/// Recalculates the transmission rates.
		/// </summary>
		void RecalculateTransmissionRates();

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		void FindNearestRelay();

		/// <summary>
		/// Recalculates the max range; useful for making sure we're using additive ranges when enabled.
		/// </summary>
		void RecalculateMaxRange();

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.IAntennaRelay"/>.
		/// </summary>
		string ToString();
	}
}

