// AntennaRange © 2014 toadicus
//
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a
// copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/

using KSP;
using System;
using ToadicusTools;
using UnityEngine;

namespace AntennaRange
{
	[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
	public class ARConfiguration : MonoBehaviour
	{
		private bool showConfigWindow;
		private Rect configWindowPos;

		private IButton toolbarButton;
		private ApplicationLauncherButton appLauncherButton;

		private System.Version runningVersion;

		private KSP.IO.PluginConfiguration _config;
		private KSP.IO.PluginConfiguration config
		{
			get
			{
				if (this._config == null)
				{
					this._config = KSP.IO.PluginConfiguration.CreateForType<AntennaRelay>();
				}

				return this._config;
			}
		}

		public void Awake()
		{
			Tools.PostDebugMessage(this, "Waking up.");

			this.runningVersion = this.GetType().Assembly.GetName().Version;

			this.showConfigWindow = false;
			this.configWindowPos = new Rect(Screen.width / 4, Screen.height / 2, 180, 15);


			this.configWindowPos = this.LoadConfigValue("configWindowPos", this.configWindowPos);

			AntennaRelay.requireLineOfSight = this.LoadConfigValue("requireLineOfSight", false);

			AntennaRelay.radiusRatio = (1 - this.LoadConfigValue("graceRatio", .05d));
			AntennaRelay.radiusRatio *= AntennaRelay.radiusRatio;

			ARFlightController.requireConnectionForControl =
				this.LoadConfigValue("requireConnectionForControl", false);

			ModuleLimitedDataTransmitter.fixedPowerCost = this.LoadConfigValue("fixedPowerCost", false);

			GameEvents.onGameSceneLoadRequested.Add(this.onSceneChangeRequested);

			Debug.Log(string.Format("{0} v{1} - ARConfiguration loaded!", this.GetType().Name, this.runningVersion));

			Tools.PostDebugMessage(this, "Awake.");
		}

		public void OnGUI()
		{
			// Only runs once, if the Toolbar is available.
			if (this.toolbarButton == null && ToolbarManager.ToolbarAvailable)
			{
				Tools.PostDebugMessage(this, "Toolbar available; initializing toolbar button.");

				this.toolbarButton = ToolbarManager.Instance.add("AntennaRange", "ARConfiguration");
				this.toolbarButton.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER);
				this.toolbarButton.Text = "AR";
				this.toolbarButton.TexturePath = "AntennaRange/Textures/toolbarIcon";
				this.toolbarButton.TextColor = (Color)XKCDColors.Amethyst;
				this.toolbarButton.OnClick += delegate(ClickEvent e)
				{
					this.toggleConfigWindow();
				};
			}
			else if (this.appLauncherButton == null && ApplicationLauncher.Ready)
			{
				Tools.PostDebugMessage(this, "Toolbar available; initializing AppLauncher button.");

				this.appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					this.toggleConfigWindow,
					this.toggleConfigWindow,
					ApplicationLauncher.AppScenes.SPACECENTER,
					GameDatabase.Instance.GetTexture(
						"AntennaRange/Textures/toolbarIcon",
						false
					)
				);
			}

			if (this.showConfigWindow)
			{
				Rect configPos = GUILayout.Window(354163056,
					this.configWindowPos,
					this.ConfigWindow,
					string.Format("AntennaRange {0}.{1}", this.runningVersion.Major, this.runningVersion.Minor),
					GUILayout.ExpandHeight(true),
					GUILayout.ExpandWidth(true)
				);

				configPos = Tools.ClampRectToScreen(configPos, 20);

				if (configPos != this.configWindowPos)
				{
					this.configWindowPos = configPos;
					this.SaveConfigValue("configWindowPos", this.configWindowPos);
				}
			}
		}

		public void ConfigWindow(int _)
		{
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

			GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

			bool requireLineOfSight = GUILayout.Toggle(AntennaRelay.requireLineOfSight, "Require Line of Sight");
			if (requireLineOfSight != AntennaRelay.requireLineOfSight)
			{
				AntennaRelay.requireLineOfSight = requireLineOfSight;
				this.SaveConfigValue("requireLineOfSight", requireLineOfSight);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

			bool requireConnectionForControl =
				GUILayout.Toggle(
					ARFlightController.requireConnectionForControl,
					"Require Connection for Probe Control"
				);
			if (requireConnectionForControl != ARFlightController.requireConnectionForControl)
			{
				ARFlightController.requireConnectionForControl = requireConnectionForControl;
				this.SaveConfigValue("requireConnectionForControl", requireConnectionForControl);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			bool fixedPowerCost = GUILayout.Toggle(ModuleLimitedDataTransmitter.fixedPowerCost, "Use Fixed Power Cost");
			if (fixedPowerCost != ModuleLimitedDataTransmitter.fixedPowerCost)
			{
				ModuleLimitedDataTransmitter.fixedPowerCost = fixedPowerCost;
				this.SaveConfigValue("fixedPowerCost", fixedPowerCost);
			}

			GUILayout.EndHorizontal();

			if (requireLineOfSight)
			{
				GUILayout.BeginHorizontal();

				double graceRatio = 1d - Math.Sqrt(AntennaRelay.radiusRatio);
				double newRatio;

				GUILayout.Label(string.Format("Line of Sight 'Fudge Factor': {0:P0}", graceRatio));

				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();

				newRatio = GUILayout.HorizontalSlider((float)graceRatio, 0f, 1f, GUILayout.ExpandWidth(true));
				newRatio = Math.Round(newRatio, 2);

				if (newRatio != graceRatio)
				{
					AntennaRelay.radiusRatio = (1d - newRatio) * (1d - newRatio);
					this.SaveConfigValue("graceRatio", newRatio);
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();

			GUI.DragWindow();
		}

		public void OnDestroy()
		{
			GameEvents.onGameSceneLoadRequested.Remove(this.onSceneChangeRequested);

			if (this.toolbarButton != null)
			{
				this.toolbarButton.Destroy();
			}

			if (this.appLauncherButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(this.appLauncherButton);
			}
		}

		protected void onSceneChangeRequested(GameScenes scene)
		{
			if (scene != GameScenes.SPACECENTER)
			{
				print("ARConfiguration: Requesting Destruction.");
				MonoBehaviour.Destroy(this);
			}
		}

		private void toggleConfigWindow()
		{
			this.showConfigWindow = !this.showConfigWindow;
		}

		private T LoadConfigValue<T>(string key, T defaultValue)
		{
			this.config.load();

			return config.GetValue(key, defaultValue);
		}

		private void SaveConfigValue<T>(string key, T value)
		{
			this.config.load();

			this.config.SetValue(key, value);

			this.config.save();
		}
	}
}
