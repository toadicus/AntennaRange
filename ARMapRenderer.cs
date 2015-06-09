// AntennaRange
//
// ARMapRenderer.cs
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
	public class ARMapRenderer : MonoBehaviour
	{
		#if BENCH
		private static ulong updateCount = 0u;
		private static ulong updateTimer = 0u;
		private readonly static RollingAverage averager = new RollingAverage();
		private static long twiceAverageTime = long.MaxValue;
		#endif

		#region Fields
		private Dictionary<Guid, LineRenderer> vesselLineRenderers;

		// Debug Stuff
		#pragma warning disable 649
		private System.Diagnostics.Stopwatch timer;
		private Tools.DebugLogger log;
		private long relayStart;
		private long start;
		#pragma warning restore 649

		#pragma warning disable 414
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
			}

			#if DEBUG || BENCH
			this.timer = new System.Diagnostics.Stopwatch();
			#endif
			#if DEBUG
			this.log = Tools.DebugLogger.New(this);
			#endif
		}

		private void OnPreCull()
		{
			if (!HighLogic.LoadedSceneIsFlight || !MapView.MapIsEnabled || !ARConfiguration.PrettyLines)
			{
				this.Cleanup();

				return;
			}

			#if DEBUG || BENCH
			timer.Restart();
			#endif

			try
			{
				log.Clear();

				log.AppendFormat("OnPreCull.\n");

				log.AppendFormat("\tMapView: Draw3DLines: {0}\n" +
					"\tMapView.MapCamera.camera.fieldOfView: {1}\n" +
					"\tMapView.MapCamera.Distance: {2}\n",
					MapView.Draw3DLines,
					MapView.MapCamera.camera.fieldOfView,
					MapView.MapCamera.Distance
				);

				if (FlightGlobals.ready && FlightGlobals.Vessels != null)
				{
					log.AppendLine("FlightGlobals ready and Vessels list not null.");

					for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
					{
						Vessel vessel = FlightGlobals.Vessels[i];

						log.AppendFormat("\nStarting check for vessel {0} at {1}ms", vessel, timer.ElapsedMilliseconds);

						if (vessel == null)
						{
							log.AppendFormat("\n\tSkipping vessel {0} altogether because it is null.", vessel);
							continue;
						}

						switch (vessel.vesselType)
						{
							case VesselType.Debris:
							case VesselType.EVA:
							case VesselType.Unknown:
							case VesselType.SpaceObject:
								log.AppendFormat("\n\tDiscarded because vessel is of invalid type {0}",
									vessel.vesselType);
								continue;
						}

						log.AppendFormat("\n\tChecking vessel {0}.", vessel.vesselName);

						#if DEBUG
						start = timer.ElapsedMilliseconds;
						#endif

						IAntennaRelay vesselRelay = vessel.GetBestRelay();

						if (vesselRelay == null)
						{
							log.AppendFormat("\n\tGot null relay for vessel {0}", vessel.vesselName);
							continue;
						}

						log.AppendFormat("\n\tGot best relay {0} ({3}) for vessel {1} in {2} ms",
							vesselRelay, vessel, timer.ElapsedMilliseconds - start, vesselRelay.GetType().Name);

						if (vesselRelay != null)
						{
							#if DEBUG
							start = timer.ElapsedMilliseconds;
							#endif

							this.SetRelayVertices(vesselRelay);

							log.AppendFormat("\n\tSet relay vertices for {0} in {1}ms",
								vessel, timer.ElapsedMilliseconds - start);
						}
					}
				}
			}
			catch (Exception ex)
			{
				this.LogError("Caught {0}: {1}\n{2}\n", ex.GetType().Name, ex.ToString(), ex.StackTrace.ToString());
				this.Cleanup();
			}
			#if DEBUG
			finally
			{
				log.AppendFormat("\n\tOnPreCull finished in {0}ms\n", timer.ElapsedMilliseconds);

				log.Print();
			}
			#endif

			#if BENCH
			ARMapRenderer.updateCount++;
			ARMapRenderer.updateTimer += (ulong)this.timer.ElapsedTicks;

			if (ARMapRenderer.updateCount >= (ulong)(8d / Time.smoothDeltaTime))
			{
				ARMapRenderer.averager.AddItem((double)ARMapRenderer.updateTimer / (double)ARMapRenderer.updateCount);
				ARMapRenderer.updateTimer = 0u;
				ARMapRenderer.updateCount = 0u;
				ARMapRenderer.twiceAverageTime = (long)(ARMapRenderer.averager.Average * 2d);
			}

			if (this.timer.ElapsedTicks > ARMapRenderer.twiceAverageTime)
			{
				this.Log("PreCull took significant longer than usual ({0:S3}s vs {1:S3}s)",
					(double)this.timer.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency,
					ARMapRenderer.averager.Average / (double)System.Diagnostics.Stopwatch.Frequency
				);
			}
			#endif
		}

		private void OnDestroy()
		{
			this.Cleanup();

			this.Log("Destroyed");
		}
		#endregion

		#region Utility
		private void SetRelayVertices(IAntennaRelay relay)
		{
			log.AppendFormat("\n\t\tDrawing line for relay chain starting at {0}.", relay);

			if (relay.vessel == null)
			{
				log.Append("\n\t\tvessel is null, bailing out");
				return;
			}

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

			#if DEBUG
			relayStart = timer.ElapsedMilliseconds;
			#endif

			Vector3d nextPoint;

			renderer.enabled = true;

			if (!relay.CanTransmit())
			{
				thisColor = Color.red;
			}
			else
			{
				if (relay.LinkStatus == ConnectionStatus.Optimal)
				{
					thisColor = Color.green;
				}
				else
				{
					thisColor = Color.yellow;
				}
			}

			if (relay.KerbinDirect)
			{
				nextPoint = ScaledSpace.LocalToScaledSpace(AntennaRelay.Kerbin.position);
			}
			else
			{
				if (relay.targetRelay == null || relay.targetRelay.vessel == null)
				{
					this.LogError(
						"SetRelayVertices: relay {0} has null target relay or vessel when not KerbinDirect, bailing out!",
						relay
					);

					renderer.enabled = false;
					return;
				}

				nextPoint = ScaledSpace.LocalToScaledSpace(relay.targetRelay.vessel.GetWorldPos3D());
			}

			renderer.SetColors(thisColor, thisColor);

			if (!MapView.Draw3DLines)
			{
				nextPoint = MapView.MapCamera.camera.WorldToScreenPoint(nextPoint);
				nextPoint.z = nextPoint.z >= 0f ? d : -d;
			}

			idx++;

			renderer.SetVertexCount(idx + 1);
			renderer.SetPosition(idx, nextPoint);

			log.AppendFormat("\n\t\t\t...finished segment in {0} ms", timer.ElapsedMilliseconds - relayStart);
		}

		private void Cleanup()
		{
			if (this.vesselLineRenderers != null && this.vesselLineRenderers.Count > 0)
			{
				IEnumerator<LineRenderer> enumerator = this.vesselLineRenderers.Values.GetEnumerator();
				LineRenderer lineRenderer;

				while (enumerator.MoveNext())
				{
					lineRenderer = enumerator.Current;
					lineRenderer.enabled = false;
					GameObject.Destroy(lineRenderer.gameObject);
				}
				this.vesselLineRenderers.Clear();
			}
		}
		#endregion
	}
}
