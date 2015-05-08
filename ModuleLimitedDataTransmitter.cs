// AntennaRange
//
// ModuleLimitedDataTransmitter.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToadicusTools;
using UnityEngine;

namespace AntennaRange
{
	/*
	 * ModuleLimitedDataTransmitter is designed as a drop-in replacement for ModuleDataTransmitter, and handles range-
	 * finding, power scaling, and data scaling for antennas during science transmission.  Its functionality varies with
	 * three tunables: nominalRange, maxPowerFactor, and maxDataFactor, set in .cfg files.
	 * 
	 * In general, the scaling functions assume the following relation:
	 * 
	 *     D² α P/R,
	 * 
	 * where D is the total transmission distance, P is the transmission power, and R is the data rate.
	 * 
	 * */

	/*
	 * Fields
	 * */
	public class ModuleLimitedDataTransmitter : ModuleDataTransmitter, IScienceDataTransmitter, IAntennaRelay
	{
		// Stores the packetResourceCost as defined in the .cfg file.
		protected float _basepacketResourceCost;

		// Stores the packetSize as defined in the .cfg file.
		protected float _basepacketSize;

		// Every antenna is a relay.
		protected AntennaRelay relay;

		// Keep track of vessels with transmitters for relay purposes.
		protected List<Vessel> _relayVessels;

		// Sometimes we will need to communicate errors; this is how we do it.
		protected ScreenMessage ErrorMsg;

		// The distance from Kerbin at which the antenna will perform exactly as prescribed by packetResourceCost
		// and packetSize.
		[KSPField(isPersistant = false)]
		public float nominalRange;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
		public string UIrelayStatus;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Relay")]
		public string UIrelayTarget;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Transmission Distance")]
		public string UItransmitDistance;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Maximum Distance")]
		public string UImaxTransmitDistance;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Packet Size")]
		public string UIpacketSize;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Packet Cost")]
		public string UIpacketCost;

		// The multiplier on packetResourceCost that defines the maximum power output of the antenna.  When the power
		// cost exceeds packetResourceCost * maxPowerFactor, transmission will fail.
		[KSPField(isPersistant = false)]
		public float maxPowerFactor;

		// The multipler on packetSize that defines the maximum data bandwidth of the antenna.
		[KSPField(isPersistant = false)]
		public float maxDataFactor;

		[KSPField(
			isPersistant = true,
			guiName = "Packet Throttle",
			guiUnits = "%",
			guiActive = true,
			guiActiveEditor = false
		)]
		[UI_FloatRange(maxValue = 100f, minValue = 2.5f, stepIncrement = 2.5f)]
		public float packetThrottle;

		protected bool actionUIUpdate;

		/*
		 * Properties
		 * */
		// Returns the parent vessel housing this antenna.
		public new Vessel vessel
		{
			get
			{
				return base.vessel;
			}
		}

		public IAntennaRelay nearestRelay
		{
			get
			{
				if (this.relay == null)
				{
					return null;
				}

				return this.relay.nearestRelay;
			}
		}

		public IAntennaRelay bestOccludedRelay
		{
			get
			{
				if (this.relay == null)
				{
					return null;
				}

				return this.relay.bestOccludedRelay;
			}
		}

		// Returns the distance to the nearest relay or Kerbin, whichever is closer.
		public double transmitDistance
		{
			get
			{
				if (this.relay == null)
				{
					return double.PositiveInfinity;
				}

				return this.relay.transmitDistance;
			}
		}

		public double nominalTransmitDistance
		{
			get
			{
				return this.nominalRange;
			}
		}

		// Returns the maximum distance this module can transmit
		public float maxTransmitDistance
		{
			get;
			private set;
		}

		public CelestialBody firstOccludingBody
		{
			get
			{
				return this.relay.firstOccludingBody;
			}
		}

		/*
		 * The next two functions overwrite the behavior of the stock functions and do not perform equivalently, except
		 * in that they both return floats.  Here's some quick justification:
		 * 
		 * The stock implementation of GetTransmitterScore (which I cannot override) is:
		 * 		Score = (1 + DataResourceCost) / DataRate
		 * 
		 * The stock DataRate and DataResourceCost are:
		 * 		DataRate = packetSize / packetInterval
		 * 		DataResourceCost = packetResourceCost / packetSize
		 * 
		 * So, the resulting score is essentially in terms of joules per byte per baud.  Rearranging that a bit, it
		 * could also look like joule-seconds per byte per byte, or newton-meter-seconds per byte per byte.  Either way,
		 * that metric is not a very reasonable one.
		 * 
		 * Two metrics that might make more sense are joules per byte or joules per byte per second.  The latter case
		 * would look like:
		 * 		DataRate = packetSize / packetInterval
		 * 		DataResourceCost = packetResourceCost
		 * 
		 * The former case, which I've chosen to implement below, is:
		 * 		DataRate = packetSize
		 * 		DataResourceCost = packetResourceCost
		 * 
		 * So... hopefully that doesn't screw with anything else.
		 * */
		// Override ModuleDataTransmitter.DataRate to just return packetSize, because we want antennas to be scored in
		// terms of joules/byte
		public new float DataRate
		{
			get
			{
				this.PreTransmit_SetPacketSize();

				if (this.CanTransmit())
				{
					return this.packetSize;
				}
				else
				{
					return float.Epsilon;
				}
			}
		}

		// Override ModuleDataTransmitter.DataResourceCost to just return packetResourceCost, because we want antennas
		// to be scored in terms of joules/byte
		public new float DataResourceCost
		{
			get
			{
				this.PreTransmit_SetPacketResourceCost();

				if (this.CanTransmit())
				{
					return this.packetResourceCost;
				}
				else
				{
					return float.PositiveInfinity;
				}
			}
		}

		// Reports whether this antenna has been checked as a viable relay already in the current FindNearestRelay.
		public bool relayChecked
		{
			get
			{
				if (this.relay != null)
				{
					return this.relay.relayChecked;
				}

				// If our relay is null, always return null so we're never checked.
				return true;
			}
		}

		public bool KerbinDirect
		{
			get
			{
				if (this.relay != null)
				{
					return this.relay.KerbinDirect;
				}

				return false;
			}
		}

		/*
		 * Methods
		 * */
		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base()
		{
			this.ErrorMsg = new ScreenMessage("", 4f, false, ScreenMessageStyle.UPPER_LEFT);
			this.packetThrottle = 100f;
		}

		public override void OnAwake()
		{
			base.OnAwake();

			this._basepacketSize = base.packetSize;
			this._basepacketResourceCost = base.packetResourceCost;
			this.maxTransmitDistance = Mathf.Sqrt(this.maxPowerFactor) * this.nominalRange;

			Tools.PostDebugMessage(string.Format(
				"{0} loaded:\n" +
				"packetSize: {1}\n" +
				"packetResourceCost: {2}\n" +
				"nominalRange: {3}\n" +
				"maxPowerFactor: {4}\n" +
				"maxDataFactor: {5}\n",
				this.name,
				base.packetSize,
				this._basepacketResourceCost,
				this.nominalRange,
				this.maxPowerFactor,
				this.maxDataFactor
			));
		}

		// At least once, when the module starts with a state on the launch pad or later, go find Kerbin.
		public override void OnStart (StartState state)
		{
			base.OnStart (state);

			if (state >= StartState.PreLaunch)
			{
				this.relay = new AntennaRelay(this);
				this.relay.maxTransmitDistance = this.maxTransmitDistance;
				this.relay.nominalTransmitDistance = this.nominalRange;

				this.UImaxTransmitDistance = Tools.MuMech_ToSI(this.maxTransmitDistance) + "m";

				GameEvents.onPartActionUICreate.Add(this.onPartActionUICreate);
				GameEvents.onPartActionUIDismiss.Add(this.onPartActionUIDismiss);
			}
		}

		// When the module loads, fetch the Squad KSPFields from the base.  This is necessary in part because
		// overloading packetSize and packetResourceCostinto a property in ModuleLimitedDataTransmitter didn't
		// work.
		public override void OnLoad(ConfigNode node)
		{
			this.Fields.Load(node);
			base.Fields.Load(node);

			base.OnLoad (node);
		}

		// Post an error in the communication messages describing the reason transmission has failed.  Currently there
		// is only one reason for this.
		protected void PostCannotTransmitError()
		{
			string ErrorText = string.Intern("Unable to transmit: no visible receivers in range!");

			this.ErrorMsg.message = string.Format(
				"<color='#{0}{1}{2}{3}'><b>{4}</b></color>",
				((int)(XKCDColors.OrangeRed.r * 255f)).ToString("x2"),
				((int)(XKCDColors.OrangeRed.g * 255f)).ToString("x2"),
				((int)(XKCDColors.OrangeRed.b * 255f)).ToString("x2"),
				((int)(XKCDColors.OrangeRed.a * 255f)).ToString("x2"),
				ErrorText
			);

			Tools.PostDebugMessage(this.GetType().Name + ": " + this.ErrorMsg.message);

			ScreenMessages.PostScreenMessage(this.ErrorMsg, false);
		}

		// Before transmission, set packetResourceCost.  Per above, packet cost increases with the square of
		// distance.  packetResourceCost maxes out at _basepacketResourceCost * maxPowerFactor, at which point
		// transmission fails (see CanTransmit).
		protected void PreTransmit_SetPacketResourceCost()
		{
			if (ARConfiguration.FixedPowerCost || this.transmitDistance <= this.nominalRange)
			{
				base.packetResourceCost = this._basepacketResourceCost;
			}
			else
			{
				double rangeFactor = (this.transmitDistance / this.nominalRange);
				rangeFactor *= rangeFactor;

				base.packetResourceCost = this._basepacketResourceCost
					* (float)rangeFactor;

				Tools.PostDebugMessage(
					this,
					"Pretransmit: packet cost set to {0} before throttle (rangeFactor = {1}).",
					base.packetResourceCost,
					rangeFactor);
			}

			base.packetResourceCost *= this.packetThrottle / 100f;
		}

		// Before transmission, set packetSize.  Per above, packet size increases with the inverse square of
		// distance.  packetSize maxes out at _basepacketSize * maxDataFactor.
		protected void PreTransmit_SetPacketSize()
		{
			if (!ARConfiguration.FixedPowerCost && this.transmitDistance >= this.nominalRange)
			{
				base.packetSize = this._basepacketSize;
			}
			else
			{
				double rangeFactor = (this.nominalRange / this.transmitDistance);
				rangeFactor *= rangeFactor;

				base.packetSize = Math.Min(
					this._basepacketSize * (float)rangeFactor,
					this._basepacketSize * this.maxDataFactor);

				Tools.PostDebugMessage(
					this,
					"Pretransmit: packet size set to {0} before throttle (rangeFactor = {1}).",
					base.packetSize,
					rangeFactor);
			}

			base.packetSize *= this.packetThrottle / 100f;
		}

		// Override ModuleDataTransmitter.GetInfo to add nominal and maximum range to the VAB description.
		public override string GetInfo()
		{
			string text = base.GetInfo();
			text += "Nominal Range: " + Tools.MuMech_ToSI((double)this.nominalRange, 2) + "m\n";
			text += "Maximum Range: " + Tools.MuMech_ToSI((double)this.maxTransmitDistance, 2) + "m\n";
			return text;
		}

		// Override ModuleDataTransmitter.CanTransmit to return false when transmission is not possible.
		public new bool CanTransmit()
		{
			if (this.part == null || this.relay == null)
			{
				return false;
			}

			PartStates partState = this.part.State;
			if (partState == PartStates.DEAD || partState == PartStates.DEACTIVATED)
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: {1} on {2} cannot transmit: {3}",
					this.GetType().Name,
					this.part.partInfo.title,
					this.vessel.vesselName,
					Enum.GetName(typeof(PartStates), partState)
				));
				return false;
			}
			return this.relay.CanTransmit();
		}

		// Override ModuleDataTransmitter.TransmitData to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public new void TransmitData(List<ScienceData> dataQueue)
		{
			this.PreTransmit_SetPacketSize();
			this.PreTransmit_SetPacketResourceCost();

			if (this.CanTransmit())
			{
				StringBuilder message = new StringBuilder();

				message.Append("[");
				message.Append(base.part.partInfo.title);
				message.Append("]: ");

				message.Append("Beginning transmission ");

				// @DONE TODO: Fix this to fall back to Kerbin if nearestRelay cannot be contacted.
				// @DONE TODO: Remove nearestRelay == null
				if (this.KerbinDirect)
				{
					message.Append("directly to Kerbin.");
				}
				else
				{
					message.Append("via ");
					message.Append(this.relay.nearestRelay);
				}

				ScreenMessages.PostScreenMessage(message.ToString(), 4f, ScreenMessageStyle.UPPER_LEFT);

				base.TransmitData(dataQueue);
			}
			else
			{
				Tools.PostDebugMessage(this, "{0} unable to transmit during TransmitData.", this.part.partInfo.title);

				var logger = Tools.DebugLogger.New(this);

				foreach (ModuleScienceContainer	scienceContainer in this.vessel.getModulesOfType<ModuleScienceContainer>())
				{
					logger.AppendFormat("Checking ModuleScienceContainer in {0}\n",
						scienceContainer.part.partInfo.title);

					if (
						scienceContainer.capacity != 0 &&
						scienceContainer.GetScienceCount() >= scienceContainer.capacity
					)
					{
						logger.Append("\tInsufficient capacity, skipping.\n");
						continue;
					}

					List<ScienceData> dataStored = new List<ScienceData>();

					foreach (ScienceData data in dataQueue)
					{
						if (!scienceContainer.allowRepeatedSubjects && scienceContainer.HasData(data))
						{
							logger.Append("\tAlready contains subject and repeated subjects not allowed, skipping.\n");
							continue;
						}

						logger.AppendFormat("\tAcceptable, adding data on subject {0}... ", data.subjectID);
						if (scienceContainer.AddData(data))
						{
							logger.Append("done, removing from queue.\n");

							dataStored.Add(data);
						}
						#if DEBUG
						else
						{
							logger.Append("failed.\n");
						}
						#endif
					}

					dataQueue.RemoveAll(i => dataStored.Contains(i));

					logger.AppendFormat("\t{0} data left in queue.", dataQueue.Count);
				}

				logger.Print();

				if (dataQueue.Count > 0)
				{
					StringBuilder msg = new StringBuilder();

					msg.Append('[');
					msg.Append(this.part.partInfo.title);
					msg.AppendFormat("]: {0} data items could not be saved: no space available in data containers.\n");
					msg.Append("Data to be discarded:\n");

					foreach (ScienceData data in dataQueue)
					{
						msg.AppendFormat("\n{0}\n", data.title);
					}

					ScreenMessages.PostScreenMessage(msg.ToString(), 4f, ScreenMessageStyle.UPPER_LEFT);

					Tools.PostDebugMessage(msg.ToString());
				}

				this.PostCannotTransmitError ();
			}

			Tools.PostDebugMessage (
				"distance: " + this.transmitDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
			);
		}

		// Override ModuleDataTransmitter.StartTransmission to check against CanTransmit and fail out when CanTransmit
		// returns false.
		public new void StartTransmission()
		{
			PreTransmit_SetPacketSize ();
			PreTransmit_SetPacketResourceCost ();

			Tools.PostDebugMessage (
				"distance: " + this.transmitDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
				);

			if (this.CanTransmit())
			{
				StringBuilder message = new StringBuilder();

				message.Append("[");
				message.Append(base.part.partInfo.title);
				message.Append("]: ");

				message.Append("Beginning transmission ");

				// @DONE TODO: Fix this to fall back to Kerbin if nearestRelay cannot be contacted.
				// @DONE TODO: Remove nearestRelay == null
				if (this.KerbinDirect)
				{
					message.Append("directly to Kerbin.");
				}
				else
				{
					message.Append("via ");
					message.Append(this.relay.nearestRelay);
				}

				ScreenMessages.PostScreenMessage(message.ToString(), 4f, ScreenMessageStyle.UPPER_LEFT);

				base.StartTransmission();
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}

		public void Update()
		{
			if (this.actionUIUpdate)
			{
				if (this.CanTransmit())
				{
					this.UIrelayStatus = "Connected";
					this.UItransmitDistance = Tools.MuMech_ToSI(this.transmitDistance) + "m";
					this.UIpacketSize = Tools.MuMech_ToSI(this.DataRate) + "MiT";
					this.UIpacketCost = Tools.MuMech_ToSI(this.DataResourceCost) + "E";
				}
				else
				{
					if (this.relay.firstOccludingBody == null)
					{
						this.UIrelayStatus = "Out of range";
					}
					else
					{
						this.UIrelayStatus = string.Format("Blocked by {0}", this.relay.firstOccludingBody.bodyName);
					}
					this.UImaxTransmitDistance = "N/A";
					this.UIpacketSize = "N/A";
					this.UIpacketCost = "N/A";
				}

				if (this.KerbinDirect)
				{
					if (this.relay.bestOccludedRelay != null)
					{
						this.UIrelayTarget = this.relay.bestOccludedRelay.ToString();
					}
					else
					{
						this.UIrelayTarget = "Kerbin";
					}
				}
				else
				{
					this.UIrelayTarget = this.relay.nearestRelay.ToString();
				}
			}
		}

		public void onPartActionUICreate(Part eventPart)
		{
			if (eventPart == base.part)
			{
				this.actionUIUpdate = true;
			}
		}

		public void onPartActionUIDismiss(Part eventPart)
		{
			if (eventPart == base.part)
			{
				this.actionUIUpdate = false;
			}
		}

		public override string ToString()
		{
			StringBuilder msg = new StringBuilder();

			msg.Append(this.part.partInfo.title);

			if (vessel != null)
			{
				msg.Append(" on ");
				msg.Append(vessel.vesselName);
			}

			return msg.ToString();
		}

		// When debugging, it's nice to have a button that just tells you everything.

		[KSPEvent (guiName = "Show Debug Info", active = true, guiActive = true)]
		public void DebugInfo()
		{
			PreTransmit_SetPacketSize ();
			PreTransmit_SetPacketResourceCost ();

			string msg = string.Format(
				"'{0}'\n" + 
				"_basepacketSize: {1}\n" +
				"packetSize: {2}\n" +
				"_basepacketResourceCost: {3}\n" +
				"packetResourceCost: {4}\n" +
				"maxTransmitDistance: {5}\n" +
				"transmitDistance: {6}\n" +
				"nominalRange: {7}\n" +
				"CanTransmit: {8}\n" +
				"DataRate: {9}\n" +
				"DataResourceCost: {10}\n" +
				"TransmitterScore: {11}\n" +
				"NearestRelay: {12}\n" +
				"BestOccludedRelay: {13}\n" +
				"KerbinDirect: {14}\n" +
				"Vessel ID: {15}",
				this.name,
				this._basepacketSize,
				base.packetSize,
				this._basepacketResourceCost,
				base.packetResourceCost,
				this.maxTransmitDistance,
				this.transmitDistance,
				this.nominalRange,
				this.CanTransmit(),
				this.DataRate,
				this.DataResourceCost,
				ScienceUtil.GetTransmitterScore(this),
				this.relay.nearestRelay == null ? "null" : this.relay.nearestRelay.ToString(),
				this.relay.bestOccludedRelay == null ? "null" : this.relay.bestOccludedRelay.ToString(),
				this.KerbinDirect,
				this.vessel.id
				);

			Tools.PostLogMessage(msg);
		}

		[KSPEvent (guiName = "Dump Vessels", active = true, guiActive = true)]
		public void PrintAllVessels()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("Dumping FlightGlobals.Vessels:");

			foreach (Vessel vessel in FlightGlobals.Vessels)
			{
				sb.AppendFormat("\n'{0} ({1})'", vessel.vesselName, vessel.id);
			}

			Tools.PostDebugMessage(sb.ToString());
		}

		/*[KSPEvent (guiName = "Dump RelayDB", active = true, guiActive = true)]
		public void DumpRelayDB()
		{
			RelayDatabase.Instance.Dump();
		}*/
	}
}