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
using ToadicusTools;

namespace AntennaRange
{
	/// <summary>
	/// Wrapper class for ProtoPartModuleSnapshot extending AntennaRelay and implementing IAntennaRelay.
	/// This is used for finding relays in unloaded Vessels.
	/// </summary>
	public class ProtoAntennaRelay : AntennaRelay, IAntennaRelay
	{
		// Stores the prototype part so we can make sure we haven't exploded or so.
		private ProtoPartSnapshot protoPart;

		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		public override Vessel vessel
		{
			get
			{
				if (
					this.protoPart != null &&
					this.protoPart.pVesselRef != null &&
					this.protoPart.pVesselRef.vesselRef != null
				)
				{
					return this.protoPart.pVesselRef.vesselRef;
				}
				else
				{
					Tools.PostErrorMessage("{0}: Could not fetch vessel!  {1}{2}{3}",
						this.ToString(),
						this.protoPart == null ? "\n\tprotoPart=null" : string.Empty,
						this.protoPart != null && this.protoPart.pVesselRef == null ?
							"\n\tthis.protoPart.pVesselRef=null" : string.Empty,
						this.protoPart != null && this.protoPart.pVesselRef != null &&
							this.protoPart.pVesselRef.vesselRef == null ?
							"\n\tthis.protoPart.pVesselRef.vesselRef=null" : string.Empty
					);
					return null;
				}
			}
		}

		/// <summary>
		/// Gets the nominal transmit distance at which the Antenna behaves just as prescribed by Squad's config.
		/// </summary>
		public override double nominalTransmitDistance
		{
			get
			{
				return this.moduleRef.nominalTransmitDistance;
			}
		}

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		public override double maxTransmitDistance
		{
			get
			{
				return moduleRef.maxTransmitDistance;
			}
		}

		/// <summary>
		/// Gets the underlying part's title.
		/// </summary>
		/// <value>The title.</value>
		public string Title
		{
			get
			{
				if (this.protoPart != null && this.protoPart.partInfo != null)
				{
					return this.protoPart.partInfo.title;
				}

				return string.Empty;
			}
		}

		/// <summary>
		/// Determines whether this instance can transmit.
		/// <c>true</c> if this instance can transmit; otherwise, <c>false</c>.
		/// </summary>
		public override bool CanTransmit()
		{
			PartStates partState = (PartStates)this.protoPart.state;
			if (partState == PartStates.DEAD || partState == PartStates.DEACTIVATED)
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: {1} on {2} cannot transmit: {3}",
					this.GetType().Name,
					this.Title,
					this.vessel.vesselName,
					Enum.GetName(typeof(PartStates), partState)
				));
				return false;
			}
			return base.CanTransmit();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.ProtoAntennaRelay"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="AntennaRange.ProtoAntennaRelay"/>.</returns>
		public override string ToString()
		{
			System.Text.StringBuilder sb = Tools.GetStringBuilder();

			sb.Append(this.Title);

			if (this.protoPart != null && this.protoPart.pVesselRef != null)
			{
				sb.AppendFormat(" on {0}", this.protoPart.pVesselRef.vesselName);
			}

			Tools.PutStringBuilder(sb);

			return sb.ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.AntennaRelay"/> class.
		/// </summary>
		/// <param name="prefabRelay">The module reference underlying this AntennaRelay,
		/// as an <see cref="AntennaRange.IAntennaRelay"/></param>
		/// <param name="pps">The prototype partreference on which the module resides.</param>
		public ProtoAntennaRelay(IAntennaRelay prefabRelay, ProtoPartSnapshot pps) : base(prefabRelay)
		{
			this.protoPart = pps;

			Tools.PostLogMessage("{0}: constructed {1}", this.GetType().Name, this.ToString());
		}
	}
}

