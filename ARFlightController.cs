// AntennaRange
//
// ARFlightController.cs
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

#pragma warning disable 1591

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
		private Dictionary<ConnectionStatus, string> connectionTextures;
		private Dictionary<ConnectionStatus, Texture> appLauncherTextures;

		private ARMapRenderer mapRenderer;

		private IButton toolbarButton;

		private ApplicationLauncherButton appLauncherButton;
		private Tools.DebugLogger log;

		private System.Diagnostics.Stopwatch updateTimer;
		#endregion

		#region Properties
		public ConnectionStatus currentConnectionStatus
		{
			get;
			private set;
		}

		private string currentConnectionTexture
		{
			get
			{
				return this.connectionTextures[this.currentConnectionStatus];
			}
		}

		private Texture currentAppLauncherTexture
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
			private set;
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
		private void Awake()
		{
			this.lockID = "ARConnectionRequired";

			this.log = Tools.DebugLogger.New(this);

			this.updateTimer = new System.Diagnostics.Stopwatch();

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

		private void Start()
		{
			this.mapRenderer = MapView.MapCamera.gameObject.AddComponent<ARMapRenderer>();
		}

		private void FixedUpdate()
		{
			if (this.appLauncherButton == null && !ToolbarManager.ToolbarAvailable && ApplicationLauncher.Ready)
			{
				this.appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
					this.appLauncherTextures[ConnectionStatus.None]
				);
			}

			this.log.Clear();

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

			log.Print();
		}

		private void Update()
		{
			if (!this.updateTimer.IsRunning || this.updateTimer.ElapsedMilliseconds > 16L)
			{
				this.updateTimer.Restart();
			}
			else
			{
				return;
			}

			this.log.Clear();

			if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null)
			{
				Vessel vessel;
				IAntennaRelay relay;
				IList<IAntennaRelay> activeVesselRelays;

				for (int vIdx = 0; vIdx < FlightGlobals.Vessels.Count; vIdx++)
				{
					vessel = FlightGlobals.Vessels[vIdx];

					if (vessel == null || vessel == FlightGlobals.ActiveVessel)
					{
						continue;
					}

					log.AppendFormat("Fetching best relay for vessel {0}", vessel);

					relay = vessel.GetBestRelay();

					if (relay != null)
					{
						log.AppendFormat("Finding nearest relay for best relay {0}", relay);

						relay.FindNearestRelay();
					}
				}

				activeVesselRelays = RelayDatabase.Instance[FlightGlobals.ActiveVessel];
				for (int rIdx = 0; rIdx < activeVesselRelays.Count; rIdx++)
				{
					relay = activeVesselRelays[rIdx];

					relay.FindNearestRelay();
				}

				if (this.toolbarButton != null || this.appLauncherButton != null)
				{
					log.Append("Checking vessel relay status.\n");

					this.currentConnectionStatus = FlightGlobals.ActiveVessel.GetConnectionStatus();

					log.AppendFormat("currentConnectionStatus: {0}, setting texture to {1}",
						this.currentConnectionStatus, this.currentConnectionTexture);

					if (this.toolbarButton != null)
					{
						this.toolbarButton.TexturePath = this.currentConnectionTexture;

						if (this.currentConnectionStatus == ConnectionStatus.None)
						{
							if (!this.toolbarButton.Important) this.toolbarButton.Important = true;
						}
						else
						{
							if (this.toolbarButton.Important) this.toolbarButton.Important = false;
						}
					}
					if (this.appLauncherButton != null)
					{
						this.appLauncherButton.SetTexture(this.currentAppLauncherTexture);
					}
				}
			}

			log.Print();
		}

		private void OnDestroy()
		{
			InputLockManager.RemoveControlLock(this.lockID);

			if (this.mapRenderer != null)
			{
				GameObject.Destroy(this.mapRenderer);
			}

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
		private void onSceneChangeRequested(GameScenes scene)
		{
			print("ARFlightController: Requesting Destruction.");
			MonoBehaviour.Destroy(this);
		}

		private void onVesselChange(Vessel vessel)
		{
			InputLockManager.RemoveControlLock(this.lockID);
		}
		#endregion
	}
}
