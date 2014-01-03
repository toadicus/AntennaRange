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

using System;
using System.Linq;

namespace AntennaRange
{
	/*
	 * Wrapper class for ProtoPartModuleSnapshot extending AntennaRelay and implementing IAntennaRelay.
	 * This is used for finding relays in unloaded Vessels.
	 * */
	public class ProtoAntennaRelay : AntennaRelay, IAntennaRelay
	{
		protected ProtoPartModuleSnapshot protoModule;
		protected Part partPrefab;

		/// <summary>
		/// The maximum distance at which this transmitter can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public override float maxTransmitDistance
		{
			get
			{
				return this.partPrefab.Modules.OfType<ModuleLimitedDataTransmitter>().First().maxTransmitDistance;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		public override bool relayChecked
		{
			get
			{
				bool result;
				Boolean.TryParse(this.protoModule.moduleValues.GetValue("relayChecked"), out result);
				return result;
			}
			protected set
			{
				if (this.protoModule.moduleValues.HasValue("relayChecked"))
				{
					this.protoModule.moduleValues.SetValue("relayChecked", value.ToString ());
				}
				else
				{
					this.protoModule.moduleValues.AddValue("relayChecked", value);
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoAntennaRelay"/> class.
		/// </summary>
		/// <param name="ms">The ProtoPartModuleSnapshot to wrap</param>
		/// <param name="vessel">The parent Vessel</param>
		public ProtoAntennaRelay(ProtoPartModuleSnapshot ppms, ProtoPartSnapshot pps, Vessel vessel) : base(vessel)
		{
			this.protoModule = ppms;
			this.partPrefab = PartLoader.getPartInfoByName(pps.partName).partPrefab;
		}
	}
}

