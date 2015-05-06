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
		private Dictionary<Guid, bool> vesselFrameCache;
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
					lr.material = new Material(Shader.Find("Particles/Additive"));
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
			this.vesselLineRenderers = new Dictionary<Guid, LineRenderer>();
			this.vesselFrameCache = new Dictionary<Guid, bool>();
		}

		private void OnPreCull()
		{
			if (!HighLogic.LoadedSceneIsFlight || !MapView.MapIsEnabled)
			{
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

						log.Append("\tChecking connection status...\n");

						if (vessel.HasConnectedRelay())
						{
							log.AppendLine("\tHas a connection, checking for the best relay to use for the line.");

							IAntennaRelay vesselRelay = null;
							float bestScore = float.PositiveInfinity;
							float relayScore = float.NaN;

							foreach (IAntennaRelay relay in RelayDatabase.Instance[vessel].Values)
							{
								relayScore = (float)relay.transmitDistance / relay.maxTransmitDistance;

								if (relayScore < bestScore)
								{
									bestScore = relayScore;
									vesselRelay = relay as IAntennaRelay;
								}
							}

							if (vesselRelay != null)
							{
								log.AppendFormat("\t...picked relay {0} with a score of {1}", 
									vesselRelay, relayScore
								);

								this.SetRelayVertices(vesselRelay);
							}
						}
						else if (this.vesselLineRenderers.ContainsKey(vessel.id))
						{
							log.AppendLine("\tDisabling line because vessel has no connection.");
							this[vessel.id].enabled = false;
						}
					}
				}
			}
			finally
			{
				log.Print();
			}
		}

		private void OnDestroy()
		{
			this.vesselLineRenderers.Clear();
			this.vesselLineRenderers = null;
			print("ARMapRenderer: Destroyed.");
		}
		#endregion

		private void SetRelayVertices(IAntennaRelay relay)
		{
			do
			{
				if (this.vesselFrameCache.ContainsKey(relay.vessel.id))
				{
					break;
				}

				LineRenderer renderer = this[relay.vessel.id];

				if (relay.CanTransmit())
				{
					Vector3d start;
					Vector3d end;

					renderer.enabled = true;

					if (relay.transmitDistance < relay.nominalTransmitDistance)
					{
						renderer.SetColors(Color.green, Color.green);
					}
					else
					{
						renderer.SetColors(Color.yellow, Color.yellow);
					}

					start = ScaledSpace.LocalToScaledSpace(relay.vessel.GetWorldPos3D());

					if (relay.nearestRelay == null)
					{
						end = ScaledSpace.LocalToScaledSpace(AntennaRelay.Kerbin.position);
					}
					else
					{
						end = ScaledSpace.LocalToScaledSpace(relay.nearestRelay.vessel.GetWorldPos3D());
					}

					float lineWidth;

					if (MapView.Draw3DLines)
					{
						lineWidth = 0.004f * MapView.MapCamera.Distance;
					}
					else
					{
						lineWidth = 1f;

						start = MapView.MapCamera.camera.WorldToScreenPoint(start);
						end = MapView.MapCamera.camera.WorldToScreenPoint(end);

						float d = Screen.height / 2f + 0.01f;
						start.z = start.z >= 0f ? d : -d;
						end.z = end.z >= 0f ? d : -d;
					}

					renderer.SetWidth(lineWidth, lineWidth);

					renderer.SetPosition(0, start);
					renderer.SetPosition(1, end);

					this.vesselFrameCache[relay.vessel.id] = true;

					relay = relay.nearestRelay;
				}
			}
			while (relay != null);
		}
	}
}

