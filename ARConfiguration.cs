// AntennaRange © 2014 toadicus
//
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a
// copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/

using KSP;
using KSP.UI.Screens;
using System;
using ToadicusTools.Extensions;
using ToadicusTools.Text;
using ToadicusTools.GUIUtils;
using ToadicusTools.Wrappers;
using UnityEngine;

namespace AntennaRange
{
	/// <summary>
	/// A <see cref="UnityEngine.MonoBehaviour"/> responsible for managing configuration items for AntennaRange.
	/// </summary>
	[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
	public class ARConfiguration : MonoBehaviour
	{
		private const string WINDOW_POS_KEY = "configWindowPos";
		private const string REQUIRE_LOS_KEY = "requireLineOfSight";
		private const string GRACE_RATIO_KEY = "graceRatio";
		private const string REQUIRE_PROBE_CONNECTION_KEY = "requireConnectionForControl";
		private const string FIXED_POWER_KEY = "fixedPowerCost";
		private const string PRETTY_LINES_KEY = "drawPrettyLines";
		private const string UPDATE_DELAY_KEY = "updateDelay";
		private const string USE_ADDITIVE_KEY = "useAdditiveRanges";

		private const string TRACKING_STATION_RANGES_KEY = "TRACKING_STATION_RANGES";
		private const string RANGE_KEY = "range";

		private const string USE_TOOLBAR_KEY = "useToolbarIfAvailable";

		/// <summary>
		/// Indicates whether connections require line of sight.
		/// </summary>
		public static bool RequireLineOfSight
		{
			get;
			private set;
		}

		/// <summary>
		/// A "fudge factor" ratio that pretends planets and moons are slightly smaller than reality to make
		/// building communication constellations easier.
		/// </summary>
		public static double RadiusRatio
		{
			get;
			private set;
		}

		/// <summary>
		/// Indicates whether unmanned vessels require a connection for control.
		/// </summary>
		public static bool RequireConnectionForControl
		{
			get;
			private set;
		}

		/// <summary>
		/// If true, relays will fix their power cost when above nominal range, decreasing data rate instead.
		/// </summary>
		public static bool FixedPowerCost
		{
			get;
			private set;
		}

		/// <summary>
		/// Indicates whether this AntennaRange will draw pretty lines in map view.
		/// </summary>
		public static bool PrettyLines
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the update delay.
		/// </summary>
		public static long UpdateDelay
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a value indicating whether AntennaRange will use additive ranges.
		/// </summary>
		public static bool UseAdditiveRanges
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets Kerbin's relay range based on the current tracking station level.
		/// </summary>
		public static double KerbinRelayRange
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets Kerbin's nominal relay range based on the current tracking station level.
		/// </summary>
		public static double KerbinNominalRange
		{
			get
			{
				return KerbinRelayRange / 2.8284271247461901d;
			}
		}

		/// <summary>
		/// Gets a value indicating whether we should use Toolbar if available.
		/// </summary>
		/// <value><c>true</c> if we should use Toolbar if available; otherwise, <c>false</c>.</value>
		public static bool UseToolbarIfAvailable
		{
			get;
			private set;
		}

#pragma warning disable 1591

		private bool showConfigWindow;
		private Rect configWindowPos;

		private string updateDelayStr;
		private long updateDelay;

		private IButton toolbarButton;
		private ApplicationLauncherButton appLauncherButton;

		private double[] trackingStationRanges;

		private System.Version runningVersion;

		private bool runOnce;

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
			this.LogDebug("Waking up.");

			this.runningVersion = this.GetType().Assembly.GetName().Version;

			this.showConfigWindow = false;
			this.configWindowPos = new Rect(Screen.width / 4, Screen.height / 2, 180, 15);

			this.configWindowPos = this.LoadConfigValue(WINDOW_POS_KEY, this.configWindowPos);

			ARConfiguration.RequireLineOfSight = this.LoadConfigValue(REQUIRE_LOS_KEY, false);

			ARConfiguration.RadiusRatio = (1 - this.LoadConfigValue(GRACE_RATIO_KEY, .05d));
			ARConfiguration.RadiusRatio *= ARConfiguration.RadiusRatio;

			ARConfiguration.RequireConnectionForControl =
				this.LoadConfigValue(REQUIRE_PROBE_CONNECTION_KEY, false);

			ARConfiguration.FixedPowerCost = this.LoadConfigValue(FIXED_POWER_KEY, false);

			ARConfiguration.PrettyLines = this.LoadConfigValue(PRETTY_LINES_KEY, true);

			ARConfiguration.UpdateDelay = this.LoadConfigValue(UPDATE_DELAY_KEY, 16L);

			ARConfiguration.UseAdditiveRanges = this.LoadConfigValue(USE_ADDITIVE_KEY, true);

			ARConfiguration.PrettyLines = this.LoadConfigValue(PRETTY_LINES_KEY, true);

			ARConfiguration.UpdateDelay = this.LoadConfigValue(UPDATE_DELAY_KEY, 16L);
			this.updateDelayStr = ARConfiguration.UpdateDelay.ToString();

			ARConfiguration.UseToolbarIfAvailable = this.LoadConfigValue(USE_TOOLBAR_KEY, true);

			GameEvents.onGameSceneLoadRequested.Add(this.onSceneChangeRequested);
			GameEvents.OnKSCFacilityUpgraded.Add(this.onFacilityUpgraded);

			Debug.Log(string.Format("{0} v{1} - ARConfiguration loaded!", this.GetType().Name, this.runningVersion));

			ConfigNode[] tsRangeNodes = GameDatabase.Instance.GetConfigNodes(TRACKING_STATION_RANGES_KEY);

			if (tsRangeNodes.Length > 0)
			{
				string[] rangeValues = tsRangeNodes[0].GetValues(RANGE_KEY);

				this.trackingStationRanges = new double[rangeValues.Length];

				for (int idx = 0; idx < rangeValues.Length; idx++)
				{
					if (!double.TryParse(rangeValues[idx], out this.trackingStationRanges[idx]))
					{
						this.LogError("Could not parse value '{0}' to double; Tracking Station ranges may not work!");
						this.trackingStationRanges[idx] = 0d;
					}
				}

				this.Log("Loaded Tracking Station ranges from config: [{0}]", this.trackingStationRanges.SPrint());
			}
			else
			{
				this.trackingStationRanges = new double[]
				{
					51696576d,
					37152180000d,
					224770770000d
				};

				this.LogWarning("Failed to load Tracking Station ranges from config, using hard-coded values: [{0}]",
					this.trackingStationRanges.SPrint());
			}

			this.runOnce = true;

			this.LogDebug("Awake.");
		}

		public void Update()
		{
			if (
				this.runOnce &&
				(ScenarioUpgradeableFacilities.Instance != null || HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
			)
			{
				this.runOnce = false;

				this.SetKerbinRelayRange();
			}
		}

		public void OnGUI()
		{
			// Only runs once, if the Toolbar is available.
			if (ToolbarManager.ToolbarAvailable && ARConfiguration.UseToolbarIfAvailable)
			{
				if (this.toolbarButton == null)
				{
					this.LogDebug("Toolbar available; initializing toolbar button.");

					if (this.appLauncherButton != null)
					{
						ApplicationLauncher.Instance.RemoveModApplication(this.appLauncherButton);
						this.appLauncherButton = null;
					}

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
			}
			else if (this.appLauncherButton == null && ApplicationLauncher.Ready)
			{
				if (this.toolbarButton != null)
				{
					this.toolbarButton.Destroy();
					this.toolbarButton = null;
				}

				this.LogDebug("Toolbar available; initializing AppLauncher button.");

				this.appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					this.toggleConfigWindow,
					this.toggleConfigWindow,
					ApplicationLauncher.AppScenes.SPACECENTER,
					GameDatabase.Instance.GetTexture(
						"AntennaRange/Textures/appLauncherIcon",
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

				configPos = WindowTools.ClampRectToScreen(configPos, 20);

				if (configPos != this.configWindowPos)
				{
					this.configWindowPos = configPos;
					this.SaveConfigValue(WINDOW_POS_KEY, this.configWindowPos);
				}
			}
		}

		public void ConfigWindow(int _)
		{
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

			GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

			bool requireLineOfSight = Layout.Toggle(ARConfiguration.RequireLineOfSight, "Require Line of Sight");
			if (requireLineOfSight != ARConfiguration.RequireLineOfSight)
			{
				ARConfiguration.RequireLineOfSight = requireLineOfSight;
				this.SaveConfigValue(REQUIRE_LOS_KEY, requireLineOfSight);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

			bool requireConnectionForControl =
				Layout.Toggle(
					ARConfiguration.RequireConnectionForControl,
					"Require Connection for Probe Control"
				);
			if (requireConnectionForControl != ARConfiguration.RequireConnectionForControl)
			{
				ARConfiguration.RequireConnectionForControl = requireConnectionForControl;
				this.SaveConfigValue(REQUIRE_PROBE_CONNECTION_KEY, requireConnectionForControl);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			bool fixedPowerCost = Layout.Toggle(ARConfiguration.FixedPowerCost, "Use Fixed Power Cost");
			if (fixedPowerCost != ARConfiguration.FixedPowerCost)
			{
				ARConfiguration.FixedPowerCost = fixedPowerCost;
				this.SaveConfigValue(FIXED_POWER_KEY, fixedPowerCost);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			bool useAdditive = Layout.Toggle(ARConfiguration.UseAdditiveRanges, "Use Additive Ranges");
			if (useAdditive != ARConfiguration.UseAdditiveRanges)
			{
				ARConfiguration.UseAdditiveRanges = useAdditive;
				this.SaveConfigValue(USE_ADDITIVE_KEY, useAdditive);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			bool prettyLines = Layout.Toggle(ARConfiguration.PrettyLines, "Draw Pretty Lines");
			if (prettyLines != ARConfiguration.PrettyLines)
			{
				ARConfiguration.PrettyLines = prettyLines;
				this.SaveConfigValue(PRETTY_LINES_KEY, prettyLines);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			bool useToolbar = Layout.Toggle(ARConfiguration.UseToolbarIfAvailable, "Use Blizzy's Toolbar, if Available");
			if (useToolbar != ARConfiguration.UseToolbarIfAvailable)
			{
				ARConfiguration.UseToolbarIfAvailable = useToolbar;
				this.SaveConfigValue(USE_TOOLBAR_KEY, useToolbar);
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();

			GUILayout.Label("Update Delay", GUILayout.ExpandWidth(false));

			this.updateDelayStr = GUILayout.TextField(this.updateDelayStr, 4, GUILayout.Width(40f));

			GUILayout.Label("ms", GUILayout.ExpandWidth(false));

			GUILayout.EndHorizontal();

			if (this.updateDelayStr.Length > 1 && long.TryParse(this.updateDelayStr, out this.updateDelay))
			{
				ARConfiguration.UpdateDelay = Math.Min(Math.Max(this.updateDelay, 16), 2500);
				this.updateDelayStr = ARConfiguration.UpdateDelay.ToString();
			}

			if (requireLineOfSight)
			{
				GUILayout.BeginHorizontal();

				double graceRatio = 1d - Math.Sqrt(ARConfiguration.RadiusRatio);
				double newRatio;

				GUILayout.Label(string.Format("Line of Sight 'Fudge Factor': {0:P0}", graceRatio));

				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();

				newRatio = GUILayout.HorizontalSlider((float)graceRatio, 0f, 1f, GUILayout.ExpandWidth(true));
				newRatio = Math.Round(newRatio, 2);

				if (newRatio != graceRatio)
				{
					ARConfiguration.RadiusRatio = (1d - newRatio) * (1d - newRatio);
					this.SaveConfigValue(GRACE_RATIO_KEY, newRatio);
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.EndVertical();

			GUI.DragWindow();
		}

		public void OnDestroy()
		{
			GameEvents.onGameSceneLoadRequested.Remove(this.onSceneChangeRequested);
			GameEvents.OnKSCFacilityUpgraded.Remove(this.onFacilityUpgraded);

			if (this.toolbarButton != null)
			{
				this.toolbarButton.Destroy();
				this.toolbarButton = null;
			}

			if (this.appLauncherButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(this.appLauncherButton);
				this.appLauncherButton = null;
			}
		}

		private void onSceneChangeRequested(GameScenes scene)
		{
			if (scene != GameScenes.SPACECENTER)
			{
				print("ARConfiguration: Requesting Destruction.");
				MonoBehaviour.Destroy(this);
			}
		}

		private void onFacilityUpgraded(Upgradeables.UpgradeableFacility fac, int lvl)
		{
			if (fac.id == "SpaceCenter/TrackingStation")
			{
				this.Log("Caught onFacilityUpgraded for {0} at level {1}", fac.id, lvl);
				this.SetKerbinRelayRange();
			}
		}

		private void SetKerbinRelayRange()
		{
			int tsLevel;

			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				tsLevel = ScenarioUpgradeableFacilities.protoUpgradeables["SpaceCenter/TrackingStation"]
					.facilityRefs[0].FacilityLevel;
			}
			else
			{
				tsLevel = this.trackingStationRanges.Length - 1;
			}

			if (tsLevel < this.trackingStationRanges.Length && tsLevel >= 0)
			{
				KerbinRelayRange = this.trackingStationRanges[tsLevel];
				this.Log("Setting Kerbin's range to {0}", KerbinRelayRange);
			}
			else
			{
				this.LogError("Could not set Kerbin's range with invalid Tracking Station level {0}", tsLevel);
			}
		}

		private void toggleConfigWindow()
		{
			this.showConfigWindow = !this.showConfigWindow;
			this.updateDelayStr = ARConfiguration.UpdateDelay.ToString();
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
