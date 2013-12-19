using System;

namespace AntennaRange
{
	public class ProtoAntennaRelay : AntennaRelay
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
				this.snapshot.moduleValues.SetValue("relayChecked", value.ToString());
			}
		}

		public ProtoAntennaRelay(ProtoPartModuleSnapshot ms, Vessel vessel) : base(vessel)
		{
			this.snapshot = ms;
		}
	}
}

