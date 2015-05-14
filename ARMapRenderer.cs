// AntennaRange
//
// ARMapRenderer.cs
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

#pragma warning disable 1591

using KSP;
using System;
using System.Collections.Generic;
using ToadicusTools;
using UnityEngine;

namespace AntennaRange
{
	public class ARMapRenderer : MonoBehaviour
	{
		#region Fields
		private Dictionary<Guid, LineRenderer> vesselLineRenderers;
		private Dictionary<Guid, bool> vesselFrameCache;

		#pragma warning disable 414
		private bool dumpBool;
		private Color lastColor;
		private Color thisColor;
		#pragma warning restore 414
		#endregion

		#region Properties
		public LineRenderer this[Guid idx]
		{
			get
			{
				if (this.vesselLineRenderers == null)
				{
					this.vesselLineRenderers = new Dictionary<Guid, LineRenderer>();
				}

				LineRenderer lr;

				if (this.vesselLineRenderers.TryGetValue(idx, out lr))
				{
					return lr;
				}
				else
				{
					GameObject obj = new GameObject();
					obj.layer = 31;

					lr = obj.AddComponent<LineRenderer>();

					// lr.SetColors(Color.green, Color.green);
					lr.material = MapView.OrbitLinesMaterial;
					// lr.SetVertexCount(2);

					this.vesselLineRenderers[idx] = lr;

					return lr;
				}
			}
		}
		#endregion

		#region MonoBehaviour Lifecycle
		private void Awake()
		{
			if (ARConfiguration.PrettyLines)
			{
				this.vesselLineRenderers = new Dictionary<Guid, LineRenderer>();
				this.vesselFrameCache = new Dictionary<Guid, bool>();
			}
		}

		private void OnPreCull()
		{
			if (!HighLogic.LoadedSceneIsFlight || !MapView.MapIsEnabled || !ARConfiguration.PrettyLines)
			{
				this.Cleanup();

				return;
			}

			Tools.DebugLogger log = Tools.DebugLogger.New(this);

			try
			{
				log.AppendFormat("OnPreCull.\n");

				log.AppendFormat("\tMapView: Draw3DLines: {0}\n" +
					"\tMapView.MapCamera.camera.fieldOfView: {1}\n" +
					"\tMapView.MapCamera.Distance: {2}\n",
					MapView.Draw3DLines,
					MapView.MapCamera.camera.fieldOfView,
					MapView.MapCamera.Distance
				);

				this.vesselFrameCache.Clear();

				log.AppendLine("vesselFrameCache cleared.");

				if (FlightGlobals.ready && FlightGlobals.Vessels != null)
				{
					log.AppendLine("FlightGlobals ready and Vessels list not null.");

					foreach (Vessel vessel in FlightGlobals.Vessels)
					{
						if (vessel == null)
						{
							log.AppendFormat("Skipping vessel {0} altogether because it is null.\n");
							continue;
						}

						if (this.vesselFrameCache.TryGetValue(vessel.id, out dumpBool))
						{
							log.AppendFormat("Skipping vessel {0} because it's already been processed this frame.");
							continue;
						}

						log.AppendFormat("Checking vessel {0}.\n", vessel.vesselName);

						switch (vessel.vesselType)
						{
							case VesselType.Debris:
							case VesselType.EVA:
							case VesselType.Unknown:
							case VesselType.SpaceObject:
								log.AppendFormat("\tDiscarded because vessel is of invalid type {0}\n",
									vessel.vesselType);
								continue;
						}

						IAntennaRelay vesselRelay = vessel.GetBestRelay();

						if (vesselRelay != null)
						{
							this.SetRelayVertices(vesselRelay);
						}
					}
				}
			}
			catch (Exception)
			{
				this.Cleanup();
			}
			#if DEBUG
			finally
			{
				log.Print();
			}
			#endif
		}

		private void OnDestroy()
		{
			this.Cleanup();

			print("ARMapRenderer: Destroyed.");
		}
		#endregion

		#region Utility
		private void SetRelayVertices(IAntennaRelay relay)
		{
			lastColor = default(Color);

			LineRenderer renderer = this[relay.vessel.id];
			Vector3d start = ScaledSpace.LocalToScaledSpace(relay.vessel.GetWorldPos3D());

			float lineWidth;
			float d = Screen.height / 2f + 0.01f;

			if (MapView.Draw3DLines)
			{
				lineWidth = 0.005859375f * MapView.MapCamera.Distance;
			}
			else
			{
				lineWidth = 2f;

				start = MapView.MapCamera.camera.WorldToScreenPoint(start);

				start.z = start.z >= 0f ? d : -d;
			}

			renderer.SetWidth(lineWidth, lineWidth);

			renderer.SetPosition(0, start);

			int idx = 0;

			while (relay != null)
			{
				Vector3d nextPoint;

				renderer.enabled = true;

				if (!relay.CanTransmit())
				{
					thisColor = Color.red;
				}
				else
				{
					if (relay.transmitDistance < relay.nominalTransmitDistance)
					{
						thisColor = Color.green;
					}
					else
					{
						thisColor = Color.yellow;
					}
				}

				if (lastColor != default(Color) && thisColor != lastColor)
				{
					break;
				}

				lastColor = thisColor;
				renderer.SetColors(thisColor, thisColor);

				this.vesselFrameCache[relay.vessel.id] = true;

				if (relay.KerbinDirect)
				{
					nextPoint = ScaledSpace.LocalToScaledSpace(AntennaRelay.Kerbin.position);
					relay = null;
				}
				else
				{
					if (relay.targetRelay == null)
					{
						return;
					}

					nextPoint = ScaledSpace.LocalToScaledSpace(relay.targetRelay.vessel.GetWorldPos3D());
					relay = relay.targetRelay;
				}

				if (!MapView.Draw3DLines)
				{
					nextPoint = MapView.MapCamera.camera.WorldToScreenPoint(nextPoint);
					nextPoint.z = nextPoint.z >= 0f ? d : -d;
				}

				renderer.SetPosition(++idx, nextPoint);
			}
		}

		private void Cleanup()
		{
			if (this.vesselLineRenderers != null && this.vesselLineRenderers.Count > 0)
			{
				foreach (LineRenderer lineRenderer in this.vesselLineRenderers.Values)
				{
					lineRenderer.enabled = false;
					GameObject.Destroy(lineRenderer.gameObject);
				}
				this.vesselLineRenderers.Clear();
			}

			if (this.vesselFrameCache != null && this.vesselFrameCache.Count > 0)
			{
				this.vesselFrameCache.Clear();
			}
		}
		#endregion
	}
}
