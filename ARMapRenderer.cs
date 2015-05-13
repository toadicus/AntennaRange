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

				if (!this.vesselLineRenderers.ContainsKey(idx))
				{
					GameObject obj = new GameObject();
					obj.layer = 31;

					LineRenderer lr = obj.AddComponent<LineRenderer>();

					lr.SetColors(Color.green, Color.green);
					lr.material = MapView.OrbitLinesMaterial;
					lr.SetVertexCount(2);

					this.vesselLineRenderers[idx] = lr;
				}

				return this.vesselLineRenderers[idx];
			}
		}
		#endregion

		#region MonoBehaviour Lifecycle
		private void Awake()
		{
			if (ARConfiguration.PrettyLines)
			{
				this.vesselLineRenderers = new Dictionary<Guid, LineRenderer>();
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

		private void SetRelayVertices(IAntennaRelay relay)
		{
			if (relay == null)
			{
				return;
			}

			LineRenderer renderer = this[relay.vessel.id];

			Vector3d start;
			Vector3d end;

			renderer.enabled = true;

			if (!relay.CanTransmit())
			{
				renderer.SetColors(Color.red, Color.red);
			}
			else
			{
				if (relay.transmitDistance < relay.nominalTransmitDistance)
				{
					renderer.SetColors(Color.green, Color.green);
				}
				else
				{
					renderer.SetColors(Color.yellow, Color.yellow);
				}
			}

			start = ScaledSpace.LocalToScaledSpace(relay.vessel.GetWorldPos3D());

			if (relay.KerbinDirect)
			{
				end = ScaledSpace.LocalToScaledSpace(AntennaRelay.Kerbin.position);
			}
			else
			{
				if (relay.targetRelay == null)
				{
					return;
				}
				end = ScaledSpace.LocalToScaledSpace(relay.targetRelay.vessel.GetWorldPos3D());
			}

			float lineWidth;

			if (MapView.Draw3DLines)
			{
				lineWidth = 0.005859375f * MapView.MapCamera.Distance;
			}
			else
			{
				lineWidth = 2f;

				start = MapView.MapCamera.camera.WorldToScreenPoint(start);
				end = MapView.MapCamera.camera.WorldToScreenPoint(end);

				float d = Screen.height / 2f + 0.01f;
				start.z = start.z >= 0f ? d : -d;
				end.z = end.z >= 0f ? d : -d;
			}

			renderer.SetWidth(lineWidth, lineWidth);

			renderer.SetPosition(0, start);
			renderer.SetPosition(1, end);
		}

		public void Cleanup()
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
		}
	}
}

