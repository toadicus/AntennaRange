using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaRange
{
	public class ProtoDataTransmitter : ILimitedScienceDataTransmitter
	{
		protected ProtoPartModuleSnapshot snapshot;
		protected Vessel vessel;

		// Returns the current distance to the center of Kerbin, which is totally where the Kerbals keep their radioes.
		protected double transmitDistance
		{
			get
			{
				Vector3d KerbinPos = FlightGlobals.Bodies.FirstOrDefault(b => b.name == "Kerbin").position;
				Vector3d ActivePos = this.vessel.GetWorldPos3D();

				return (ActivePos - KerbinPos).magnitude;
			}
		}

		/// <summary>
		/// The maximum distance at which this transmitter can operate.
		/// </summary>
		/// <value>The max transmit distance.</value>
		public float maxTransmitDistance
		{
			get
			{
				double result;
				Double.TryParse(snapshot.moduleValues.GetValue ("ARmaxTransmitDistance") ?? "0", out result);
				return (float)result;
			}
		}

		/// <summary>
		/// Gets the data rate in MiT per second.
		/// </summary>
		/// <value>The data rate.</value>
		public float DataRate
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets the data resource cost in units of ElectricCharge.
		/// </summary>
		/// <value>The data resource cost.</value>
		public double DataResourceCost
		{
			get;
			protected set;
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.ProtoDataTransmitter"/> has been checked during
		/// the current relay attempt.
		/// </summary>
		/// <value><c>true</c> if relay checked; otherwise, <c>false</c>.</value>
		public bool relayChecked
		{
			get;
			protected set;
		}

		/// <summary>
		/// Determines whether this module can transmit.
		/// </summary>
		/// <returns><c>true</c> if this module can transmit; otherwise, <c>false</c>.</returns>
		public bool CanTransmit()
		{
			Tools.PostDebugMessage (string.Format (
				"{0}: transmitDistance: {1}, maxDistance: {2}",
				this.GetType().Name,
				this.transmitDistance,
				this.maxTransmitDistance
			));

			if (this.transmitDistance < this.maxTransmitDistance)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Determines whether this module is busy.
		/// </summary>
		/// <returns><c>true</c> if this module is busy; otherwise, <c>false</c>.</returns>
		public bool IsBusy()
		{
			return false;
		}

		/// <summary>
		/// Transmits the data in a queue.
		/// </summary>
		/// <param name="dataQueue">Data queue to be transmitted.</param>
		public void TransmitData(List<ScienceData> dataQueue) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.ProtoDataTransmitter"/> class.
		/// </summary>
		/// <param name="ms"><see cref="ProtoPartModuleSnapshot"/></param>
		public ProtoDataTransmitter(ProtoPartModuleSnapshot ms, Vessel v)
		{
			this.snapshot = ms;
			this.vessel = v;

			this.relayChecked = false;
		}
	}
}

