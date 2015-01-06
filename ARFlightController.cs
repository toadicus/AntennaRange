// AntennaRange
//
// ARFlightController.cs
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
using ToadicusTools;
using UnityEngine;

namespace AntennaRange
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ARFlightController : MonoBehaviour
	{
		#region Fields
		protected Dictionary<ConnectionStatus, string> connectionTextures;
		protected Dictionary<ConnectionStatus, Texture> appLauncherTextures;

		protected IButton toolbarButton;

		protected ApplicationLauncherButton appLauncherButton;
		#endregion

		#region Properties
		public ConnectionStatus currentConnectionStatus
		{
			get;
			protected set;
		}

		protected string currentConnectionTexture
		{
			get
			{
				return this.connectionTextures[this.currentConnectionStatus];
			}
		}

		protected Texture currentAppLauncherTexture
		{
			get
			{
				return this.appLauncherTextures[this.currentConnectionStatus];
			}
		}

		public ControlTypes currentControlLock
		{
			get
			{
				if (this.lockID == string.Empty)
				{
					return ControlTypes.None;
				}

				return InputLockManager.GetControlLock(this.lockID);
			}
		}

		public string lockID
		{
			get;
			protected set;
		}

		public ControlTypes lockSet
		{
			get
			{
				return ControlTypes.ALL_SHIP_CONTROLS;
			}
		}

		public Vessel vessel
		{
			get
			{
				if (FlightGlobals.ready && FlightGlobals.ActiveVessel != null)
				{
					return FlightGlobals.ActiveVessel;
				}

				return null;
			}
		}
		#endregion

		#region MonoBehaviour LifeCycle
		protected void Awake()
		{
			this.lockID = "ARConnectionRequired";

			this.connectionTextures = new Dictionary<ConnectionStatus, string>();

			this.connectionTextures[ConnectionStatus.None] = "AntennaRange/Textures/toolbarIconNoConnection";
			this.connectionTextures[ConnectionStatus.Suboptimal] = "AntennaRange/Textures/toolbarIconSubOptimal";
			this.connectionTextures[ConnectionStatus.Optimal] = "AntennaRange/Textures/toolbarIcon";

			this.appLauncherTextures = new Dictionary<ConnectionStatus, Texture>();

			this.appLauncherTextures[ConnectionStatus.None] =
				GameDatabase.Instance.GetTexture("AntennaRange/Textures/appLauncherIconNoConnection", false);
			this.appLauncherTextures[ConnectionStatus.Suboptimal] =
				GameDatabase.Instance.GetTexture("AntennaRange/Textures/appLauncherIconSubOptimal", false);
			this.appLauncherTextures[ConnectionStatus.Optimal] =
				GameDatabase.Instance.GetTexture("AntennaRange/Textures/appLauncherIcon", false);

			if (ToolbarManager.ToolbarAvailable)
			{
				this.toolbarButton = ToolbarManager.Instance.add("AntennaRange", "ARConnectionStatus");

				this.toolbarButton.TexturePath = this.connectionTextures[ConnectionStatus.None];
				this.toolbarButton.Text = "AntennaRange";
				this.toolbarButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
				this.toolbarButton.Enabled = false;
			}

			GameEvents.onGameSceneLoadRequested.Add(this.onSceneChangeRequested);
			GameEvents.onVesselChange.Add(this.onVesselChange);
		}

		protected void FixedUpdate()
		{
			if (this.appLauncherButton == null && !ToolbarManager.ToolbarAvailable && ApplicationLauncher.Ready)
			{
				this.appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					ApplicationLauncher.AppScenes.FLIGHT,
					this.appLauncherTextures[ConnectionStatus.None]
				);
			}

			Tools.DebugLogger log = Tools.DebugLogger.New(this);

			VesselCommand availableCommand;

			if (ARConfiguration.RequireConnectionForControl)
			{
				availableCommand = this.vessel.CurrentCommand();
			}
			else
			{
				availableCommand = VesselCommand.Crew;
			}

			log.AppendFormat("availableCommand: {0}\n\t" +
				"(availableCommand & VesselCommand.Crew) == VesselCommand.Crew: {1}\n\t" +
				"(availableCommand & VesselCommand.Probe) == VesselCommand.Probe: {2}\n\t" +
				"vessel.HasConnectedRelay(): {3}",
				(int)availableCommand,
				(availableCommand & VesselCommand.Crew) == VesselCommand.Crew,
				(availableCommand & VesselCommand.Probe) == VesselCommand.Probe,
				vessel.HasConnectedRelay()
			);

			// If we are requiring a connection for control, the vessel does not have any adequately staffed pods,
			// and the vessel does not have any connected relays...
			if (
				HighLogic.LoadedSceneIsFlight &&
				ARConfiguration.RequireConnectionForControl &&
				this.vessel != null &&
				this.vessel.vesselType != VesselType.EVA &&
				!(
				    (availableCommand & VesselCommand.Crew) == VesselCommand.Crew ||
				    (availableCommand & VesselCommand.Probe) == VesselCommand.Probe && vessel.HasConnectedRelay()
				))
			{
				// ...and if the controls are not currently locked...
				if (currentControlLock == ControlTypes.None)
				{
					// ...lock the controls.
					InputLockManager.SetControlLock(this.lockSet, this.lockID);
				}
			}
			// ...otherwise, if the controls are locked...
			else if (currentControlLock != ControlTypes.None)
			{
				// ...unlock the controls.
				InputLockManager.RemoveControlLock(this.lockID);
			}

			if (
				(this.toolbarButton != null || this.appLauncherButton != null) &&
				HighLogic.LoadedSceneIsFlight &&
				FlightGlobals.ActiveVessel != null
			)
			{
				log.Append("Checking vessel relay status.\n");

				List<ModuleLimitedDataTransmitter> relays =
					FlightGlobals.ActiveVessel.getModulesOfType<ModuleLimitedDataTransmitter>();

				log.AppendFormat("\t...found {0} relays\n", relays.Count);

				bool vesselCanTransmit = false;
				bool vesselHasOptimalRelay = false;

				foreach (ModuleLimitedDataTransmitter relay in relays)
				{
					log.AppendFormat("\tvesselCanTransmit: {0}, vesselHasOptimalRelay: {1}\n",
						vesselCanTransmit, vesselHasOptimalRelay);

					log.AppendFormat("\tChecking relay {0}\n" +
						"\t\tCanTransmit: {1}, transmitDistance: {2}, nominalRange: {3}\n",
						relay,
						relay.CanTransmit(),
						relay.transmitDistance,
						relay.nominalRange
					);

					bool relayCanTransmit = relay.CanTransmit();

					if (!vesselCanTransmit && relayCanTransmit)
					{
						vesselCanTransmit = true;
					}

					if (!vesselHasOptimalRelay &&
						relayCanTransmit &&
						relay.transmitDistance <= (double)relay.nominalRange)
					{
						vesselHasOptimalRelay = true;
					}

					if (vesselCanTransmit && vesselHasOptimalRelay)
					{
						break;
					}
				}

				log.AppendFormat("Done checking.  vesselCanTransmit: {0}, vesselHasOptimalRelay: {1}\n",
					vesselCanTransmit, vesselHasOptimalRelay);

				if (vesselHasOptimalRelay)
				{
					this.currentConnectionStatus = ConnectionStatus.Optimal;
				}
				else if (vesselCanTransmit)
				{
					this.currentConnectionStatus = ConnectionStatus.Suboptimal;
				}
				else
				{
					this.currentConnectionStatus = ConnectionStatus.None;
				}

				log.AppendFormat("currentConnectionStatus: {0}, setting texture to {1}",
					this.currentConnectionStatus, this.currentConnectionTexture);

				if (this.toolbarButton != null)
				{
					this.toolbarButton.TexturePath = this.currentConnectionTexture;
				}
				if (this.appLauncherButton != null)
				{
					this.appLauncherButton.SetTexture(this.currentAppLauncherTexture);
				}
			}

			log.Print();
		}

		protected void OnDestroy()
		{
			InputLockManager.RemoveControlLock(this.lockID);

			if (this.toolbarButton != null)
			{
				this.toolbarButton.Destroy();
			}

			if (this.appLauncherButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(this.appLauncherButton);
				this.appLauncherButton = null;
			}

			GameEvents.onGameSceneLoadRequested.Remove(this.onSceneChangeRequested);
			GameEvents.onVesselChange.Remove(this.onVesselChange);

			print("ARFlightController: Destroyed.");
		}
		#endregion

		#region Event Handlers
		protected void onSceneChangeRequested(GameScenes scene)
		{
			print("ARFlightController: Requesting Destruction.");
			MonoBehaviour.Destroy(this);
		}

		protected void onVesselChange(Vessel vessel)
		{
			InputLockManager.RemoveControlLock(this.lockID);
		}
		#endregion

		public enum ConnectionStatus
		{
			None,
			Suboptimal,
			Optimal
		}
	}
}
