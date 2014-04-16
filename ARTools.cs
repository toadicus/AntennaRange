// AntennaRange
//
// ARTools.cs
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
//
// This software uses code from the MuMechLib library, © 2013 r4m0n, used under the GNU GPL version 3.

using System;

namespace AntennaRange
{
	public static class Tools
	{
		private static ScreenMessage debugmsg = new ScreenMessage("", 2f, ScreenMessageStyle.UPPER_RIGHT);
		// Function that posts messages to the screen and the log when DEBUG is defined.
		[System.Diagnostics.Conditional("DEBUG")]
		public static void PostDebugMessage(string Msg)
		{
			if (HighLogic.LoadedScene > GameScenes.SPACECENTER)
			{
				debugmsg.message = Msg;
				ScreenMessages.PostScreenMessage(debugmsg, true);
			}

			KSPLog.print(Msg);
		}

		/*
		 * MuMech_ToSI is a part of the MuMechLib library, © 2013 r4m0n, used under the GNU GPL version 3.
		 * */
		public static string MuMech_ToSI(double d, int digits = 3, int MinMagnitude = 0, int MaxMagnitude = int.MaxValue)
		{
			float exponent = (float)Math.Log10(Math.Abs(d));
			exponent = UnityEngine.Mathf.Clamp(exponent, (float)MinMagnitude, (float)MaxMagnitude);

			if (exponent >= 0)
			{
				switch ((int)Math.Floor(exponent))
				{
					case 0:
						case 1:
						case 2:
						return d.ToString("F" + digits);
						case 3:
						case 4:
						case 5:
						return (d / 1e3).ToString("F" + digits) + "k";
						case 6:
						case 7:
						case 8:
						return (d / 1e6).ToString("F" + digits) + "M";
						case 9:
						case 10:
						case 11:
						return (d / 1e9).ToString("F" + digits) + "G";
						case 12:
						case 13:
						case 14:
						return (d / 1e12).ToString("F" + digits) + "T";
						case 15:
						case 16:
						case 17:
						return (d / 1e15).ToString("F" + digits) + "P";
						case 18:
						case 19:
						case 20:
						return (d / 1e18).ToString("F" + digits) + "E";
						case 21:
						case 22:
						case 23:
						return (d / 1e21).ToString("F" + digits) + "Z";
						default:
						return (d / 1e24).ToString("F" + digits) + "Y";
				}
			}
			else if (exponent < 0)
			{
				switch ((int)Math.Floor(exponent))
				{
					case -1:
						case -2:
						case -3:
						return (d * 1e3).ToString("F" + digits) + "m";
						case -4:
						case -5:
						case -6:
						return (d * 1e6).ToString("F" + digits) + "μ";
						case -7:
						case -8:
						case -9:
						return (d * 1e9).ToString("F" + digits) + "n";
						case -10:
						case -11:
						case -12:
						return (d * 1e12).ToString("F" + digits) + "p";
						case -13:
						case -14:
						case -15:
						return (d * 1e15).ToString("F" + digits) + "f";
						case -16:
						case -17:
						case -18:
						return (d * 1e18).ToString("F" + digits) + "a";
						case -19:
						case -20:
						case -21:
						return (d * 1e21).ToString("F" + digits) + "z";
						default:
						return (d * 1e24).ToString("F" + digits) + "y";
				}
			}
			else
			{
				return "0";
			}
		}

		public static T Min<T>(params T[] values) where T : IComparable<T>
		{
			if (values.Length < 2)
			{
				throw new ArgumentException("Min must be called with at least two arguments.");
			}

			IComparable<T> minValue = values[0];

			for (long i = 1; i < values.LongLength; i++)
			{
				IComparable<T> value = values[i];

				if (value.CompareTo((T)minValue) < 0)
				{
					minValue = value;
				}
			}

			return (T)minValue;
		}

		public static void Restart(this System.Diagnostics.Stopwatch stopwatch)
		{
			stopwatch.Reset();
			stopwatch.Start();
		}
	}
}

