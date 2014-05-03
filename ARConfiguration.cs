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

		public void Awake()
		{
			Tools.PostDebugMessage(this, "Waking up.");

			this.showConfigWindow = false;
			this.configWindowPos = new Rect(Screen.width / 4, Screen.height / 2, 180, 15);

			Tools.PostDebugMessage(this, "Awake.");
		}

		public void OnGUI()
		{
			if (this.toolbarButton == null && ToolbarManager.ToolbarAvailable)
			{
				Tools.PostDebugMessage(this, "Toolbar available; initializing button.");

				this.toolbarButton = ToolbarManager.Instance.add("AntennaRange", "ARConfiguration");
				this.toolbarButton.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER);
				this.toolbarButton.Text = "AR";
				this.toolbarButton.TexturePath = "AntennaRange/Textures/toolbarIcon";
				this.toolbarButton.TextColor = (Color)XKCDColors.Amethyst;
				this.toolbarButton.OnClick += delegate(ClickEvent e)
				{
					this.showConfigWindow = !this.showConfigWindow;
				};

				var config = KSP.IO.PluginConfiguration.CreateForType<AntennaRelay>();

				config.load();

				this.configWindowPos = config.GetValue<Rect>("configWindowPos", this.configWindowPos);
				AntennaRelay.requireLineOfSight = config.GetValue<bool>("requireLineOfSight", false);

				config.save();
			}

			if (this.showConfigWindow)
			{
				Rect configPos = GUILayout.Window(354163056,
					this.configWindowPos,
					this.ConfigWindow,
					"AntennaRange Configuration",
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

			GUILayout.EndVertical();

			GUI.DragWindow();
		}

		public void Destroy()
		{
			if (this.toolbarButton != null)
			{
				this.toolbarButton.Destroy();
			}
		}

		private T LoadConfigValue<T>(string key, T defaultValue)
		{
			var config = KSP.IO.PluginConfiguration.CreateForType<AntennaRelay>();

			config.load();

			return config.GetValue(key, defaultValue);
		}

		private void SaveConfigValue<T>(string key, T value)
		{
			var config = KSP.IO.PluginConfiguration.CreateForType<AntennaRelay>();

			config.load();

			config.SetValue(key, value);

			config.save();
		}
	}
}
