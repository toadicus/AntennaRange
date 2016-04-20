// AntennaRange
//
// RelayLinkCost.cs
//
// Copyright © 2016, toadicus
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
using UnityEngine;

namespace AntennaRange
{
	/// <summary>
	/// A struct representing the cost of sending data through a relay.
	/// </summary>
	public struct RelayDataCost : IComparable, IComparable<RelayDataCost>
	{
		/// <summary>
		/// A RelayDataCost object representing infinitely high cost.
		/// </summary>
		public static readonly RelayDataCost Infinity = new RelayDataCost(float.PositiveInfinity, 0f);

		/// <param name="one">Left</param>
		/// <param name="two">Right</param>
		public static RelayDataCost operator+ (RelayDataCost one, RelayDataCost two)
		{
			RelayDataCost gcd, lcd;

			if (one.PacketSize > two.PacketSize) {
				gcd = one;
				lcd = two;
			}
			else
			{
				gcd = two;
				lcd = one;
			}

			if (lcd.PacketSize != 0f)
			{
				float mul = gcd.PacketSize / lcd.PacketSize;

				lcd.PacketSize *= mul;
				lcd.PacketResourceCost *= mul;
			}

			return new RelayDataCost(gcd.PacketResourceCost + lcd.PacketResourceCost, gcd.PacketSize + lcd.PacketSize);
		}

		/// <param name="only">RelayDataCost to be negated</param>
		public static RelayDataCost operator- (RelayDataCost only)
		{
			return new RelayDataCost(-only.PacketResourceCost, only.PacketSize);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static RelayDataCost operator- (RelayDataCost left, RelayDataCost right)
		{
			return left + -right;
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator> (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) > 0);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator>= (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) >= 0);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator== (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) == 0);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator<= (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) <= 0);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator< (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) < 0);
		}

		/// <param name="left">Left.</param>
		/// <param name="right">Right.</param>
		public static bool operator!= (RelayDataCost left, RelayDataCost right)
		{
			return (left.CompareTo(right) != 0);
		}

		/// <summary>
		/// The resource cost of a packet, in EC/packet
		/// </summary>
		public float PacketResourceCost;

		/// <summary>
		/// The data capacity of a packet, MiT/packet
		/// </summary>
		public float PacketSize;

		/// <summary>
		/// Gets the resource cost per unit data, in EC/MiT
		/// </summary>
		/// <value>The resource cost per unit data, in EC/MiT</value>
		public double ResourceCostPerData
		{
			get
			{
				if (this.PacketSize == 0f || float.IsInfinity(this.PacketResourceCost))
				{
					return double.PositiveInfinity;
				}

				return (double)this.PacketResourceCost / (double)this.PacketSize;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AntennaRange.RelayDataCost"/> struct.
		/// </summary>
		/// <param name="cost">resource cost of a packet, in EC/packet</param>
		/// <param name="size">data capacity of a packet, MiT/packet</param>
		public RelayDataCost(float cost, float size)
		{
			this.PacketResourceCost = cost;
			this.PacketSize = size;
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="AntennaRange.RelayDataCost"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="AntennaRange.RelayDataCost"/>.</returns>
		public override string ToString()
		{
			return string.Format("{0} EC/MiT", this.ResourceCostPerData);
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="AntennaRange.RelayDataCost"/>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="AntennaRange.RelayDataCost"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
		/// <see cref="AntennaRange.RelayDataCost"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			if (obj is RelayDataCost)
			{
				return ((RelayDataCost)obj == this);
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="AntennaRange.RelayDataCost"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			int hash = 137;

			hash = (hash * 61) + this.PacketResourceCost.GetHashCode();
			hash = (hash * 61) + this.PacketSize.GetHashCode();

			return hash;
		}

		/// <summary>
		/// Compares this RelayDataCost to another object.  Throws NotImplementedException for objects
		/// that are not RelayDataCost objects
		/// </summary>
		/// <returns>-1 if this is less than o, 0 if this equals o, 1 if this is greater than o</returns>
		/// <param name="o">Another object</param>
		public int CompareTo(object o)
		{
			if (o is RelayDataCost)
			{
				return this.CompareTo((RelayDataCost)o);
			}

			throw new NotImplementedException(
				string.Format(
					"Cannot compare {0} to foreign type {1}",
					this.GetType().Name,
					o.GetType().Name
				)
			);
		}

		/// <summary>
		/// Compares this RelayDataCost to another object.  Throws NotImplementedException for objects
		/// that are not RelayDataCost objects
		/// </summary>
		/// <returns>-1 if this is less than o, 0 if this equals o, 1 if this is greater than o</returns>
		/// <param name="o">Another RelayDataCost</param>
		public int CompareTo(RelayDataCost o)
		{
			int val;

			if (this.ResourceCostPerData > o.ResourceCostPerData)
			{
				val = 1;
			}
			else if (this.ResourceCostPerData < o.ResourceCostPerData)
			{
				val = -1;
			}
			else
			{
				val = 0;
			}

			#if DEBUG
			Debug.LogErrorFormat("RelayLinkCost comparing {0} to {1}, returning {2}", this, o, val);
			#endif

			return val;
		}
	}
}

