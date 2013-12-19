using System;

namespace AntennaRange
{
	/*
	 * Wrapper class for ProtoPartModuleSnapshot extending AntennaRelay and implementing IAntennaRelay.
	 * This is used for finding relays in unloaded Vessels.
	 * */
	public class ProtoAntennaRelay : AntennaRelay, IAntennaRelay
	{
		protected ProtoPartModuleSnapshot snapshot;

		/// <summary>
		/// The maximum distance at which this transmitter can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public override float maxTransmitDistance
		{
			get
			{
				double result;
				Double.TryParse(snapshot.moduleValues.GetValue ("ARmaxTransmitDistance") ?? "0", out result);
				return (float)result;
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
				Boolean.TryParse(this.snapshot.moduleValues.GetValue("relayChecked"), out result);
				return result;
			}
			protected set
			{
				if (this.snapshot.moduleValues.HasValue("relayChecked"))
				{
					this.snapshot.moduleValues.SetValue("relayChecked", value.ToString ());
				}
				else
				{
					this.snapshot.moduleValues.AddValue("relayChecked", value);
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoAntennaRelay"/> class.
		/// </summary>
		/// <param name="ms">The ProtoPartModuleSnapshot to wrap</param>
		/// <param name="vessel">The parent Vessel</param>
		public ProtoAntennaRelay(ProtoPartModuleSnapshot ms, Vessel vessel) : base(vessel)
		{
			this.snapshot = ms;
		}
	}
}

