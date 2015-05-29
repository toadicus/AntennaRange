// AntennaRange
//
// PositionedObject.cs
//
// Copyright © 2015, toadicus
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

using System;
using System.Collections.Generic;

#pragma warning disable 1591

namespace AntennaRange
{
	public interface IPositionedObject
	{
		object HostObject { get; }
		Vector3d WorldPos { get; }
	}

	public interface IPositionedObject<T>
	{
		T HostObject { get; }
	}

	public abstract class PositionedObject<T> : IPositionedObject<T>, IPositionedObject
	{
		public abstract T HostObject { get; protected set; }

		object IPositionedObject.HostObject
		{
			get
			{
				return (object)this.HostObject;
			}
		}

		public abstract Vector3d WorldPos { get; }


		protected static Dictionary<T, PositionedObject<T>> bin = new Dictionary<T, PositionedObject<T>>(); 
	}

	public class VesselWrapper : PositionedObject<Vessel>
	{
		public override Vessel HostObject
		{
			get;
			protected set;
		}

		public override Vector3d WorldPos
		{
			get
			{
				if (this.HostObject != null)
				{
					return this.HostObject.GetWorldPos3D();
				}
				else
				{
					return Vector3d.zero;
				}
			}
		}

		public VesselWrapper(Vessel host)
		{
			this.HostObject = host;
		}

		public static explicit operator VesselWrapper(Vessel vessel)
		{
			PositionedObject<Vessel> wrapper;
			if (!bin.TryGetValue(vessel, out wrapper))
			{
				wrapper = new VesselWrapper(vessel);
				bin[vessel] = wrapper;
			}

			return (VesselWrapper)wrapper;
		}

		public static implicit operator Vessel(VesselWrapper wrapper)
		{
			return wrapper.HostObject;
		}
	}

	public class BodyWrapper : PositionedObject<CelestialBody>
	{
		public override CelestialBody HostObject
		{
			get;
			protected set;
		}

		public override Vector3d WorldPos
		{
			get
			{
				if (this.HostObject != null)
				{
					return this.HostObject.position;
				}
				else
				{
					return Vector3d.zero;
				}
			}
		}

		public BodyWrapper(CelestialBody host)
		{
			this.HostObject = host;
		}

		public static explicit operator BodyWrapper(CelestialBody body)
		{
			PositionedObject<CelestialBody> wrapper;
			if (!bin.TryGetValue(body, out wrapper))
			{
				wrapper = new BodyWrapper(body);
				bin[body] = wrapper;
			}

			return (BodyWrapper)wrapper;
		}

		public static implicit operator CelestialBody(BodyWrapper wrapper)
		{
			return wrapper.HostObject;
		}
	}
}

