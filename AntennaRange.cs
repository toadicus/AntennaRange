using System;
using System.Linq;
using System.Collections.Generic;
using KSP;

namespace AntennaRange
{
	public class ModuleLimitedDataTransmitter : ModuleDataTransmitter, IScienceDataTransmitter
	{
		protected float _basepacketResourceCost;
		protected float _basepacketSize;
		protected CelestialBody _Kerbin;

		public float KSCantennaGain = 1f;

		protected double transmitDistance
		{
			get
			{
				Vector3d KerbinPos = this._Kerbin.position;

				Vector3d ActivePos = base.vessel.GetWorldPos3D();

				return (ActivePos - KerbinPos).magnitude;
			}
		}

		public double maxTransmitDistance
		{
			get
			{
				return Math.Sqrt (this.maxAntennaPower / this._basepacketResourceCost * this.antennaGain * this.KSCantennaGain) * this.optimalRange;
			}
		}

		[KSPField(isPersistant = false)]
		public double optimalRange;

		[KSPField(isPersistant = false)]
		public float maxAntennaPower;

		[KSPField(isPersistant = false)]
		public float antennaGain = 1f;

		[KSPField(isPersistant = false)]
		public new float packetSize
		{
			get
			{
				if (this.transmitDistance >= this.optimalRange)
				{
					return this._basepacketSize * this.antennaGain * this.KSCantennaGain;
				}
				else
				{
					return this._basepacketSize * (float)Math.Pow (this.optimalRange / this.transmitDistance, 2) * this.antennaGain * this.KSCantennaGain;
				}
			}
			set
			{
				this._basepacketSize = value / (this.antennaGain * this.KSCantennaGain);
			}
			
		}

		[KSPField(isPersistant = false)]
		public new float packetResourceCost
		{
			get
			{
				if (this.transmitDistance <= this.optimalRange)
				{
					return this._basepacketResourceCost / (this.antennaGain * this.KSCantennaGain);
				}
				else
				{
					return this._basepacketResourceCost * (float)Math.Pow (this.transmitDistance / this.optimalRange, 2) / (this.antennaGain * this.KSCantennaGain);
				}
			}
			set
			{
				this._basepacketResourceCost = value * (this.antennaGain * this.KSCantennaGain);
			}

		}

		public ModuleLimitedDataTransmitter () : base()
		{
			List<CelestialBody> bodies = FlightGlobals.Bodies;

			foreach (CelestialBody body in bodies)
			{
				if (body.name == "Kerbin")
				{
					this._Kerbin = body;
					break;
				}
			}
		}

		protected void PostCannotTransmitError()
		{
			string ErrorText = String.Format ("Unable to transmit: out of range!  Maximum range = {0}; Current range = {1}.", this.maxTransmitDistance, this.transmitDistance);
			ScreenMessages.PostScreenMessage (new ScreenMessage (ErrorText, 4f, ScreenMessageStyle.UPPER_LEFT));
		}

		public override string GetInfo()
		{
			string text = base.GetInfo();
			text += "Optimal Range: " + this.optimalRange.ToString() + "\n";
			text += "Maximum Range: " + this.maxTransmitDistance.ToString() + "\n";
			return text;
		}

		public new bool CanTransmit()
		{
			if (this.packetResourceCost > this.maxAntennaPower)
			{
				return false;
			}
			return true;
		}

		public new void TransmitData(List<ScienceData> dataQueue)
		{
			if (this.CanTransmit())
			{
				base.TransmitData(dataQueue);
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}

		public new void StartTransmission()
		{
			if (this.CanTransmit())
			{
				base.StartTransmission();
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}
	}
}

