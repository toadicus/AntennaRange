// AntennaRange
//
// ModuleLimitedDataTransmitter.cs
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
using System.Collections.Generic;
using System.Text;
using ToadicusTools.DebugTools;
using ToadicusTools.Extensions;
using ToadicusTools.Text;
using UnityEngine;

namespace AntennaRange
{
	/// <summary>
	/// <para>ModuleLimitedDataTransmitter is designed as a drop-in replacement for ModuleDataTransmitter, and handles
	/// rangefinding, power scaling, and data scaling for antennas during science transmission.  Its functionality
	/// varies with three tunables: nominalRange, maxPowerFactor, and maxDataFactor, set in .cfg files.</para>
	/// 
	/// <para>In general, the scaling functions assume the following relation:</para>
	/// 
	///	<para>	D² α P/R,</para>
	/// 
	/// <para>where D is the total transmission distance, P is the transmission power, and R is the data rate.</para>
	/// </summary>
	public class ModuleLimitedDataTransmitter
		: ModuleDataTransmitter, IScienceDataTransmitter, IAntennaRelay, IModuleInfo
	{
		private const string tooltipSkinName = "PartTooltipSkin";

		// Every antenna is a relay.
		private AntennaRelay relay;

		// Sometimes we will need to communicate errors; this is how we do it.
		private ScreenMessage ErrorMsg;

		/// <summary>
		/// The base size of a transmission packet, in MiT.
		/// </summary>
		[KSPField(isPersistant = false)]
		public float basePacketSize;

		/// <summary>
		/// The base resource cost of a transmission packet, presumably in EC.
		/// </summary>
		[KSPField(isPersistant = false)]
		public float basePacketResourceCost;

		/// <summary>
		/// When additive ranges are enabled, the distance from Kerbin at which the antenna will perform exactly as
		/// prescribed by packetResourceCost and packetSize.
		/// </summary>
		[KSPField(isPersistant = false)]
		public double nominalRange;

		/// <summary>
		/// When additive ranges are disabled, the distance from Kerbin at which the antenna will perform exactly as
		/// prescribed by packetResourceCost and packetSize.
		/// </summary>
		[KSPField(isPersistant = false)]
		public double simpleRange;

		/// <summary>
		/// Relay status string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
		public string UIrelayStatus;

		/// <summary>
		/// Relay target string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Relay")]
		public string UIrelayTarget;

		/// <summary>
		/// Transmit distance string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Transmission Distance")]
		public string UItransmitDistance;

		/// <summary>
		/// The nominal range string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Nominal Range")]
		public string UInominalLinkDistance;

		/// <summary>
		/// Maximum distance string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Maximum Range")]
		public string UImaxTransmitDistance;

		/// <summary>
		/// Packet size string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Packet Size")]
		public string UIpacketSize;

		/// <summary>
		/// Packet cost string for use in action menus.
		/// </summary>
		[KSPField(isPersistant = false, guiActive = true, guiName = "Packet Cost")]
		public string UIpacketCost;

		/// <summary>
		/// The multiplier on packetResourceCost that defines the maximum power output of the antenna.  When the power
		/// cost exceeds packetResourceCost * maxPowerFactor, transmission will fail.
		/// </summary>
		[KSPField(isPersistant = false)]
		public float maxPowerFactor;

		/// <summary>
		/// The multipler on packetSize that defines the maximum data bandwidth of the antenna.
		/// </summary>
		[KSPField(isPersistant = false)]
		public float maxDataFactor;

		/// <summary>
		/// The packet throttle.
		/// </summary>
		[KSPField(
			isPersistant = true,
			guiName = "Packet Throttle",
			guiUnits = "%",
			guiActive = true,
			guiActiveEditor = false
		)]
		[UI_FloatRange(maxValue = 100f, minValue = 2.5f, stepIncrement = 2.5f)]
		public float packetThrottle;

		private bool actionUIUpdate;

		/*
		 * Properties
		 * */
		/// <summary>
		/// Gets the parent Vessel.
		/// </summary>
		public new Vessel vessel
		{
			get
			{
				if (base.vessel != null)
				{
					return base.vessel;
				}
				else if (this.part != null && this.part.vessel != null)
				{
					return this.part.vessel;
				}
				else if (
					this.part.protoPartSnapshot != null &&
					this.part.protoPartSnapshot.pVesselRef != null &&
					this.part.protoPartSnapshot.pVesselRef.vesselRef != null
				)
				{
					return this.part.protoPartSnapshot.pVesselRef.vesselRef;
				}
				else
				{
					this.LogError("Vessel and/or part reference are null, returning null vessel.");
					#if DEBUG && VERBOSE
					this.LogError(new System.Diagnostics.StackTrace().ToString());
					#endif
					return null;
				}
			}
		}

		private RelayDataCost _currentLinkCost = new RelayDataCost();
		/// <summary>
		/// Gets the current link resource rate in EC/MiT.
		/// </summary>
		/// <value>The current link resource rate in EC/MiT.</value>
		public RelayDataCost CurrentLinkCost
		{
			get
			{
				_currentLinkCost.PacketResourceCost = this.packetResourceCost;
				_currentLinkCost.PacketSize = this.packetSize;
				return _currentLinkCost;
			}
			set
			{
				this.packetResourceCost = value.PacketResourceCost;
				this.packetSize = value.PacketSize;
			}
		}

		/// <summary>
		/// Gets the base link resource rate in EC/MiT.
		/// </summary>
		/// <value>The base link resource rate in EC/MiT.</value>
		public RelayDataCost BaseLinkCost
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the packet throttle.
		/// </summary>
		/// <value>The packet throttle in range [0..100].</value>
		public float PacketThrottle
		{
			get
			{
				return this.packetThrottle;
			}
		}

		/// <summary>
		/// Gets the max data factor.
		/// </summary>
		/// <value>The max data factor.</value>
		public float MaxDataFactor
		{
			get
			{
				return this.maxDataFactor;
			}
		}

		/// <summary>
		/// Gets the target <see cref="AntennaRange.IAntennaRelay"/>relay.
		/// </summary>
		public IAntennaRelay targetRelay
		{
			get
			{
				if (this.relay == null)
				{
					return null;
				}

				return this.relay.targetRelay;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="AntennaRange.IAntennaRelay"/> Relay is communicating
		/// directly with Kerbin.
		/// </summary>
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

		/// <summary>
		/// Gets or sets the nominal link distance, in meters.
		/// </summary>
		public double NominalLinkSqrDistance
		{
			get
			{
				if (this.relay != null)
				{
					return this.relay.NominalLinkSqrDistance;
				}

				return 0d;
			}
		}

		/// <summary>
		/// Gets or sets the maximum link distance, in meters.
		/// </summary>
		public double MaximumLinkSqrDistance
		{
			get
			{
				if (this.relay != null)
				{
					return this.relay.MaximumLinkSqrDistance;
				}

				return 0d;
			}
		}

		/// <summary>
		/// Gets the distance to the nearest relay or Kerbin, whichever is closer.
		/// </summary>
		public double CurrentLinkSqrDistance
		{
			get
			{
				if (this.relay == null)
				{
					return double.PositiveInfinity;
				}

				return this.relay.CurrentLinkSqrDistance;
			}
		}

		/// <summary>
		/// Gets the link status.
		/// </summary>
		public ConnectionStatus LinkStatus
		{
			get
			{
				if (this.relay == null)
				{
					return ConnectionStatus.None;
				}

				return this.relay.LinkStatus;
			}
		}

		/// <summary>
		/// Gets the nominal transmit distance at which the Antenna behaves just as prescribed by Squad's config.
		/// </summary>
		public double nominalTransmitDistance
		{
			get
			{
				if (ARConfiguration.UseAdditiveRanges)
				{
					return this.nominalRange;
				}
				else
				{
					return this.simpleRange;
				}
			}
		}

		/// <summary>
		/// The maximum distance at which this relay can operate.
		/// </summary>
		public double maxTransmitDistance
		{
			get;
			protected set;
		}

		/// <summary>
		/// The first CelestialBody blocking line of sight to a 
		/// </summary>
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
		/// <summary>
		/// Override ModuleDataTransmitter.DataRate to just return packetSize, because we want antennas to be scored in
		/// terms of joules/byte
		/// </summary>
		public new float DataRate
		{
			get
			{
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

		/// <summary>
		/// Override ModuleDataTransmitter.DataResourceCost to just return packetResourceCost, because we want antennas
		/// to be scored in terms of joules/byte
		/// </summary>
		public new double DataResourceCost
		{
			get
			{
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

		/// <summary>
		/// Gets the current network resource rate in EC/MiT.
		/// </summary>
		/// <value>The current network resource rate in EC/MiT.</value>
		public RelayDataCost CurrentNetworkLinkCost
		{
			get
			{
				if (this.relay == null)
				{
					return RelayDataCost.Infinity;
				}

				return this.relay.CurrentNetworkLinkCost;
			}
		}

		/// <summary>
		/// Gets the Part title.
		/// </summary>
		public string Title
		{
			get
			{
				if (this.part != null && this.part.partInfo != null)
				{
					return this.part.partInfo.title;
				}

				return string.Empty;
			}
		}

		/*
		 * Methods
		 * */
		// Build ALL the objects.
		public ModuleLimitedDataTransmitter () : base()
		{
			this.ErrorMsg = new ScreenMessage("", 4f, ScreenMessageStyle.UPPER_LEFT);
			this.packetThrottle = 100f;
		}

		#if DEBUG
		/// <summary>
		/// PartModule OnAwake override; runs at Unity Awake.
		/// </summary>
		public override void OnAwake()
		{
			base.OnAwake();

			this.LogDebug("{0} loaded:\n" +
				"packetSize: {1}\n" +
				"packetResourceCost: {2}\n" +
				"nominalTransmitDistance: {3}\n" +
				"maxPowerFactor: {4}\n" +
				"maxDataFactor: {5}\n",
				this,
				base.packetSize,
				this.packetResourceCost,
				this.nominalTransmitDistance,
				this.maxPowerFactor,
				this.maxDataFactor
			);
		}
		#endif

		/// <summary>
		/// PartModule OnStart override; runs at Unity Start.
		/// </summary>
		/// <param name="state">State.</param>
		public override void OnStart (StartState state)
		{
			this.BaseLinkCost = new RelayDataCost(this.basePacketResourceCost, this.basePacketSize);
			this.RecalculateMaxRange();

			base.OnStart (state);

			if (state >= StartState.PreLaunch)
			{
				this.relay = new AntennaRelay(this);
				this.relay.nominalTransmitDistance = this.nominalTransmitDistance;
				this.relay.maxTransmitDistance = this.maxTransmitDistance;

				this.UImaxTransmitDistance = TextTools.Format("{0:S3}m", this.maxTransmitDistance);

				GameEvents.onPartActionUICreate.Add(this.onPartActionUICreate);
				GameEvents.onPartActionUIDismiss.Add(this.onPartActionUIDismiss);
			}
		}

		/// <summary>
		/// When the module loads, fetch the Squad KSPFields from the base.  This is necessary in part because
		/// overloading packetSize and packetResourceCostinto a property in ModuleLimitedDataTransmitter didn't
		/// work.
		/// </summary>
		/// <param name="node"><see cref="ConfigNode"/> with data for this module.</param>
		public override void OnLoad(ConfigNode node)
		{
			this.Fields.Load(node);
			base.Fields.Load(node);

			base.OnLoad (node);

			this.BaseLinkCost = new RelayDataCost(this.basePacketResourceCost, this.basePacketSize);
			this.RecalculateMaxRange();
		}

		/// <summary>
		/// Gets the human-friendly module title.
		/// </summary>
		public string GetModuleTitle()
		{
			return "Comms Transceiver";
		}

		/// <summary>
		/// Returns drawTooltipWidget as a callback for part tooltips.
		/// </summary>
		public Callback<Rect> GetDrawModulePanelCallback()
		{
			return this.drawTooltipWidget;
		}

		// Called by Squad's part tooltip system when drawing tooltips.
		// HACK: Currently hacks around Squad's extraneous layout box, see KSPModders issue #5118
		private void drawTooltipWidget(Rect rect)
		{
			/*
			 * Removed all this because Squad doesn't even call it anymore.
			 *
			this.moduleInfoContent.text = this.GetInfo();

			if (partTooltipSkin == null)
			{
				UnityEngine.Object[] skins = Resources.FindObjectsOfTypeAll(typeof(GUISkin));
				GUISkin skin;
				for (int sIdx = 0; sIdx < skins.Length; sIdx++)
				{
					skin = (GUISkin)skins[sIdx];

					if (skin.name == tooltipSkinName)
					{
						partTooltipSkin = skin;
						partTooltipBodyStyle = partTooltipSkin.customStyles[0];
						partTooltipHeaderStyle = partTooltipSkin.customStyles[1];
					}
				}

				if (partTooltipSkin == null)
				{
					this.LogError("Could not find GUISkin {0}?  Please report this!", tooltipSkinName);
					return;
				}
				else
				{
					this.Log("Loaded GUISkin {0}", tooltipSkinName);
				}
			}

			float width = rect.width;
			float orgHeight = rect.height;
			float height = partTooltipBodyStyle.CalcHeight(this.moduleInfoContent, width);

			rect.height = height;

			GUI.Box(rect, this.moduleInfoContent, partTooltipBodyStyle);
			GUI.Label(rect, this.GetModuleTitle(), partTooltipHeaderStyle);

			GUILayout.Space(height - orgHeight
				- partTooltipBodyStyle.padding.bottom - partTooltipBodyStyle.padding.top
				- 2f * (partTooltipBodyStyle.margin.bottom + partTooltipBodyStyle.margin.top)
			);*/
		}

		/// <summary>
		/// Returns an empty string, because we don't really have a "primary field" like some modules do.
		/// </summary>
		public string GetPrimaryField()
		{
			return string.Empty;
		}

		/// <summary>
		/// Override ModuleDataTransmitter.GetInfo to add nominal and maximum range to the VAB description.
		/// </summary>
		public override string GetInfo()
		{
			using (PooledStringBuilder sb = PooledStringBuilder.Get())
			{
				if (ARConfiguration.UseAdditiveRanges)
				{
					sb.AppendFormat("<b>Nominal Range to Kerbin: </b>{0:S3}m\n",
						Math.Sqrt(this.nominalTransmitDistance * ARConfiguration.KerbinNominalRange)
					);
					sb.AppendFormat("<b>Maximum Range to Kerbin: </b>{0:S3}m\n",
						Math.Sqrt(
							this.nominalTransmitDistance * Math.Sqrt(this.maxPowerFactor) *
							ARConfiguration.KerbinRelayRange
						)
					);
				}
				else
				{
					sb.AppendFormat("<b>Nominal Range: </b>{0:S3}m\n", this.nominalTransmitDistance);
					sb.AppendFormat("<b>Maximum Range: </b>{0:S3}m\n", this.maxTransmitDistance);
				}

				sb.AppendLine();

				sb.AppendFormat("<b>Nominal Packet Size: </b>{0:S2}iT\n", this.BaseLinkCost.PacketSize * 1000000f);
				sb.AppendFormat(
					"<b>Nominal Data Rate: </b>{0:S2}iT/sec\n",
					this.BaseLinkCost.PacketSize / this.packetInterval * 1000000f
				);

				sb.AppendLine();

				sb.AppendFormat("<b>Within Nominal Range...\n...Maximum Speedup:</b> {0:P0}\n", this.maxDataFactor);

				if (ARConfiguration.FixedPowerCost)
				{
					sb.AppendLine();

					sb.AppendFormat(
						"<b>Outside Nominal Range...\n...Maximum Slowdown:</b> {0:P1}\n",
						1f / this.maxPowerFactor
					);

					sb.AppendLine();

					sb.AppendFormat(
						"<b>Packet Cost:</b> {0:0.0#} {1}\n",
						this.BaseLinkCost.PacketResourceCost,
						this.requiredResource == "ElectricCharge" ? "EC" : this.requiredResource
					);
					sb.AppendFormat(
						"<b>Power Drain:</b> {0:0.0#} {1}/s\n",
						this.BaseLinkCost.PacketResourceCost / this.packetInterval,
						this.requiredResource == "ElectricCharge" ? "EC" : this.requiredResource
					);
				}
				else
				{
					sb.AppendLine();

					sb.AppendFormat(
						"<b>Nominal Packet Cost:</b> {0:0.0#} {1}\n",
						this.BaseLinkCost.PacketResourceCost,
						this.requiredResource == "ElectricCharge" ? "EC" : this.requiredResource
					);
					sb.AppendFormat(
						"<b>Nominal Power Drain:</b> {0:0.0#} {1}/s\n",
						this.BaseLinkCost.PacketResourceCost / this.packetInterval,
						this.requiredResource == "ElectricCharge" ? "EC" : this.requiredResource
					);

					sb.AppendLine();

					sb.AppendFormat(
						"<b>Outside Nominal Range...\n...Maximum Power Drain:</b> {0:0.0#} {1}/s\n",
						this.BaseLinkCost.PacketResourceCost / this.packetInterval * this.maxPowerFactor,
						this.requiredResource == "ElectricCharge" ? "EC" : this.requiredResource
					);
				}

				return sb.ToString();
			}
		}

		/// <summary>
		/// Determines whether this instance can transmit.
		/// <c>true</c> if this instance can transmit; otherwise, <c>false</c>.
		/// </summary>
		public new bool CanTransmit()
		{
			if (this.part == null || this.relay == null)
			{
				return false;
			}

			switch (this.part.State)
			{
				case PartStates.DEAD:
				case PartStates.DEACTIVATED:
					this.LogDebug(
						"{0}: {1} on {2} cannot transmit: {3}",
						this.GetType().Name,
						this.part.partInfo.title,
						this.vessel.vesselName,
						Enum.GetName(typeof(PartStates), this.part.State)
					);
					return false;
				default:
					break;
			}

			return this.relay.CanTransmit();
		}

		/// <summary>
		/// Recalculates the transmission rates.
		/// </summary>
		public void RecalculateTransmissionRates()
		{
			if (this.relay != null)
			{
				this.relay.RecalculateTransmissionRates();
				this.LogDebug("Recalculated transmission rates in MLDT, cost is {0}", this.CurrentLinkCost);
			}
			#if DEBUG
			else
			{
				this.LogDebug("Skipping recalculation; relay is null.");
			}
			#endif
		}

		/// <summary>
		/// Finds the nearest relay.
		/// </summary>
		public void FindNearestRelay()
		{
			if (this.relay != null)
			{
				this.relay.FindNearestRelay();
			}
		}

		/// <summary>
		/// Override ModuleDataTransmitter.TransmitData to check against CanTransmit and fail out when CanTransmit
		/// returns false.
		/// </summary>
		/// <param name="dataQueue">List of <see cref="ScienceData"/> to transmit.</param>
		public new void TransmitData(List<ScienceData> dataQueue)
		{
			this.LogDebug(
				"TransmitData(List<ScienceData> dataQueue, Callback callback) called.  dataQueue.Count={0}",
				dataQueue.Count
			);

			if (this.CanTransmit())
			{
				ScreenMessages.PostScreenMessage(this.buildTransmitMessage(), 4f, ScreenMessageStyle.UPPER_LEFT);

				this.LogDebug(
					"CanTransmit in TransmitData, calling base.TransmitData with dataQueue=[{0}] and callback={1}",
					dataQueue.SPrint()
				);

				base.TransmitData(dataQueue);
			}
			else
			{
				this.LogDebug("{0} unable to transmit during TransmitData.", this.part.partInfo.title);

				var logger = PooledDebugLogger.New(this);

				IList<ModuleScienceContainer> vesselContainers = this.vessel.getModulesOfType<ModuleScienceContainer>();
				ModuleScienceContainer scienceContainer;
				for (int cIdx = 0; cIdx < vesselContainers.Count; cIdx++)
				{
					scienceContainer = vesselContainers[cIdx];

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

					ScienceData data;
					for (int dIdx = 0; dIdx < dataQueue.Count; dIdx++)
					{
						data = dataQueue[dIdx];
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
					using (PooledStringBuilder sb = PooledStringBuilder.Get())
					{
						sb.Append('[');
						sb.Append(this.part.partInfo.title);
						sb.AppendFormat("]: {0} data items could not be saved: no space available in data containers.\n");
						sb.Append("Data to be discarded:\n");

						ScienceData data;
						for (int dIdx = 0; dIdx < dataQueue.Count; dIdx++)
						{
							data = dataQueue[dIdx];
							sb.AppendFormat("\t{0}\n", data.title);
						}

						ScreenMessages.PostScreenMessage(sb.ToString(), 4f, ScreenMessageStyle.UPPER_LEFT);

						this.LogDebug(sb.ToString());
					}
				}

				this.PostCannotTransmitError();
			}

			this.LogDebug(
				"distance: " + this.CurrentLinkSqrDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
			);
		}

		/// <summary>
		/// Override ModuleDataTransmitter.StartTransmission to check against CanTransmit and fail out when CanTransmit
		/// returns false.
		/// </summary>
		public new void StartTransmission()
		{
			this.LogDebug(
				"distance: " + this.CurrentLinkSqrDistance
				+ " packetSize: " + this.packetSize
				+ " packetResourceCost: " + this.packetResourceCost
				);

			if (this.CanTransmit())
			{
				ScreenMessages.PostScreenMessage(this.buildTransmitMessage(), 4f, ScreenMessageStyle.UPPER_LEFT);

				base.StartTransmission();
			}
			else
			{
				this.PostCannotTransmitError ();
			}
		}

		/// <summary>
		/// MonoBehaviour Update
		/// </summary>
		public void Update()
		{
			if (this.actionUIUpdate)
			{
				this.UImaxTransmitDistance = TextTools.Format("{0:S3}m",
					Math.Sqrt(this.MaximumLinkSqrDistance));
				this.UInominalLinkDistance = TextTools.Format("{0:S3}m",
					Math.Sqrt(this.NominalLinkSqrDistance));
				
				if (this.CanTransmit())
				{
					this.UIrelayStatus = this.LinkStatus.ToString();
					this.UItransmitDistance = TextTools.Format("{0:S3}m",
						Math.Sqrt(this.CurrentLinkSqrDistance));
					this.UIpacketSize = TextTools.Format("{0:S3}MiT", this.DataRate);
					this.UIpacketCost = TextTools.Format("{0:S3}EC", this.DataResourceCost);
				}
				else
				{
					if (this.relay.firstOccludingBody == null)
					{
						this.UItransmitDistance = TextTools.Format("{0:S3}m",
							Math.Sqrt(this.CurrentLinkSqrDistance));
						this.UIrelayStatus = "Out of range";
					}
					else
					{
						this.UItransmitDistance = "N/A";
						this.UIrelayStatus = TextTools.Format("Blocked by {0}", this.relay.firstOccludingBody.bodyName);
					}
					this.UIpacketSize = "N/A";
					this.UIpacketCost = "N/A";
				}

				if (this.KerbinDirect)
				{
					this.UIrelayTarget = AntennaRelay.Kerbin.bodyName;
				}
				else
				{
					if (this.targetRelay != null)
					{
						this.UIrelayTarget = this.targetRelay.ToString();
					}
					else
					{
						this.UIrelayTarget = "A mysterious null entity";
					}
				}
			}
		}

		/// <summary>
		/// Recalculates the max range; useful for making sure we're using additive ranges when enabled.
		/// </summary>
		public void RecalculateMaxRange()
		{
			this.maxTransmitDistance = Math.Sqrt(this.maxPowerFactor) * this.nominalTransmitDistance;

			#if DEBUG
			this.Log("Recalculated max range: sqrt({0}) * {1} = {2}",
				this.maxPowerFactor, this.nominalTransmitDistance, this.maxTransmitDistance);
			#endif
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.ModuleLimitedDataTransmitter"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="AntennaRange.ModuleLimitedDataTransmitter"/>.</returns>
		public override string ToString()
		{
			using (PooledStringBuilder sb = PooledStringBuilder.Get())
			{
				string msg;

				if (this.part != null && this.part.partInfo != null)
				{
					sb.Append(this.part.partInfo.title);
					#if DEBUG
					sb.Append('#');
					sb.Append(this.part.flightID);
					#endif
				}
				else
				{
					sb.Append(this.GetType().Name);
				}

				if (vessel != null)
				{
					sb.Append(" on ");
					sb.Append(vessel.vesselName);
				}
				else if (
					this.part != null &&
					this.part.protoPartSnapshot != null &&
					this.part.protoPartSnapshot != null &&
					this.part.protoPartSnapshot.pVesselRef != null)
				{
					sb.Append(" on ");
					sb.Append(this.part.protoPartSnapshot.pVesselRef.vesselName);
				}

				msg = sb.ToString();

				return msg;
			}
		}

		// When we catch an onPartActionUICreate event for our part, go ahead and update every frame to look pretty.
		private void onPartActionUICreate(Part eventPart)
		{
			if (eventPart == base.part)
			{
				this.actionUIUpdate = true;
			}
		}

		// When we catch an onPartActionUIDismiss event for our part, stop updating every frame to look pretty.
		private void onPartActionUIDismiss(Part eventPart)
		{
			if (eventPart == base.part)
			{
				this.actionUIUpdate = false;
			}
		}

		// Post an error in the communication messages describing the reason transmission has failed.  Currently there
		// is only one reason for this.
		private void PostCannotTransmitError()
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

			this.LogDebug(this.ErrorMsg.message);

			ScreenMessages.PostScreenMessage(this.ErrorMsg);
		}

		private string buildTransmitMessage()
		{
			using (PooledStringBuilder sb = PooledStringBuilder.Get())
			{
				string msg;

				sb.Append("[");
				sb.Append(base.part.partInfo.title);
				sb.Append("]: ");

				sb.Append("Beginning transmission ");

				if (this.KerbinDirect)
				{
					sb.Append("directly to Kerbin.");
				}
				else
				{
					sb.Append("via ");
					sb.Append(this.relay.targetRelay);
				}

				msg = sb.ToString();

				return msg;
			}
		}

		#if DEBUG
		// When debugging, it's nice to have a button that just tells you everything.
		[KSPEvent (guiName = "Show Debug Info", active = true, guiActive = true)]
		public void DebugInfo()
		{
			if (this.relay != null)
				this.relay.RecalculateTransmissionRates();

			DebugPartModule.DumpClassObject(this);
		}

		[KSPEvent (guiName = "Dump Vessels", active = true, guiActive = true)]
		public void PrintAllVessels()
		{
			using (PooledStringBuilder sb = PooledStringBuilder.Get())
			{
				sb.Append("Dumping FlightGlobals.Vessels:");

				Vessel vessel;
				for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
				{
					vessel = FlightGlobals.Vessels[i];
					sb.AppendFormat("\n'{0} ({1})'", vessel.vesselName, vessel.id);
				}
		    
				ToadicusTools.Logging.PostDebugMessage(sb.ToString());
			}
		}
		 
		/*[KSPEvent (guiName = "Dump RelayDB", active = true, guiActive = true)]
		public void DumpRelayDB()
		{
			RelayDatabase.Instance.Dump();
		}*/
		#endif
	}
}