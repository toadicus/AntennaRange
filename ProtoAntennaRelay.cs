// AntennaRange
//
// ProtoAntennaRelay.cs
//
// Copyright © 2014, toadicus
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
using System.Linq;
using ToadicusTools;

namespace AntennaRange
{
	/*
	 * Wrapper class for ProtoPartModuleSnapshot extending AntennaRelay and implementing IAntennaRelay.
	 * This is used for finding relays in unloaded Vessels.
	 * */
	public class ProtoAntennaRelay : AntennaRelay, IAntennaRelay
	{
		// Stores the prototype part so we can make sure we haven't exploded or so.
		protected ProtoPartSnapshot protoPart;

		public override Vessel vessel
		{
			get
			{
				return this.protoPart.pVesselRef.vesselRef;
			}
		}

		public override double nominalTransmitDistance
		{
			get
			{
				return this.moduleRef.nominalTransmitDistance;
			}
		}

		/// <summary>
		/// The maximum distance at which this transmitter can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public override float maxTransmitDistance
		{
			get
			{
				return moduleRef.maxTransmitDistance;
			}
		}


		public CelestialBody firstOccludingBody
		{
			get
			{
				return base.firstOccludingBody;
			}
		}


		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		public override bool relayChecked
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets the underlying part's title.
		/// </summary>
		/// <value>The title.</value>
		public string title
		{
			get
			{
				return this.protoPart.partInfo.title;
			}
		}

		public override bool CanTransmit()
		{
			PartStates partState = (PartStates)this.protoPart.state;
			if (partState == PartStates.DEAD || partState == PartStates.DEACTIVATED)
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: {1} on {2} cannot transmit: {3}",
					this.GetType().Name,
					this.title,
					this.vessel.vesselName,
					Enum.GetName(typeof(PartStates), partState)
				));
				return false;
			}
			return base.CanTransmit();
		}

		public override string ToString()
		{
			return string.Format(
				"{0} on {1} (proto)",
				this.title,
				this.protoPart.pVesselRef.vesselName
			);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoAntennaRelay"/> class.
		/// </summary>
		/// <param name="ms">The ProtoPartModuleSnapshot to wrap</param>
		/// <param name="vessel">The parent Vessel</param>
		public ProtoAntennaRelay(IAntennaRelay prefabRelay, ProtoPartSnapshot pps) : base(prefabRelay)
		{
			this.protoPart = pps;
		}

		~ProtoAntennaRelay()
		{
			Tools.PostDebugMessage(string.Format(
				"{0}: destroyed",
				this.ToString()
			));
		}
	}
}

