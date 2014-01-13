// AntennaRange © 2014 toadicus
//
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a
// copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/

using KSP;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AntennaRange
{
	public class RelayDatabase
	{
		/*
		 * Static members
		 * */
		protected static RelayDatabase _instance;

		public static RelayDatabase Instance
		{
			get
			{	
				if (_instance == null)
				{
					_instance = new RelayDatabase();
				}

				return _instance;
			}
		}

		/*
		 * Instance members
		 * */

		/*
		 * Fields
		 * */
		protected Dictionary<Guid, Dictionary<int, IAntennaRelay>> relayDatabase;

		protected Dictionary<Guid, int> vesselPartCountDB;

		/*
		 * Properties
		 * */
		public Dictionary<int, IAntennaRelay> this [Vessel vessel]
		{
			get
			{
				if (!this.ContainsKey(vessel.id))
				{
					this.AddVessel(vessel);
				}
				if (this.vesselPartCountDB[vessel.id] != vessel.Parts.Count)
				{
					this.UpdateVessel(vessel);
				}

				return relayDatabase[vessel.id];
			}
		}

		/* 
		 * Methods
		 * */
		public bool AddVessel(Vessel vessel)
		{
			if (relayDatabase.ContainsKey(vessel.id))
			{
				Debug.LogWarning(string.Format(
					"{0}: Cannot add vessel '{1}' (id: {2}): Already in database.",
					this.GetType().Name,
					vessel.name,
					vessel.id
				));

				return false;
			}
			else
			{
				this.UpdateVessel(vessel);

				return true;
			}
		}

		public void UpdateVessel(Vessel vessel)
		{
			if (!relayDatabase.ContainsKey(vessel.id))
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: Update called vessel '{1}' (id: {2}) not in database: vessel will be added.",
					this.GetType().Name,
					vessel.name,
					vessel.id
				));
			}
			else
			{
				// Remove stuff?
			}

			this.relayDatabase[vessel.id] = this.getVesselRelays(vessel);
			this.vesselPartCountDB[vessel.id] = vessel.Parts.Count;
		}

		public bool ContainsKey(Guid key)
		{
			return (this.relayDatabase.ContainsKey(key) & this.vesselPartCountDB.ContainsKey(key));
		}

		public bool ContainsKey(Vessel vessel)
		{
			return this.ContainsKey(vessel.id);
		}

		public void onVesselWasModified(Vessel vessel)
		{
			if (this.ContainsKey(vessel))
			{
				if (this.vesselPartCountDB[vessel.id] != vessel.Parts.Count)
				{
					Tools.PostDebugMessage(string.Format(
						"{0}: vessel '{1}' (id: {2}) was modified.",
						this.GetType().Name,
						vessel.name,
						vessel.id
					));

					this.vesselPartCountDB[vessel.id] = -1;
				}
			}
		}

		protected Dictionary<int, IAntennaRelay> getVesselRelays(Vessel vessel)
		{
			Dictionary<int, IAntennaRelay> relays;

			if (this.ContainsKey(vessel))
			{
				relays = this.relayDatabase[vessel.id];
				relays.Clear();
			}
			else
			{
				relays = new Dictionary<int, IAntennaRelay>();
			}

			Tools.PostDebugMessage(string.Format(
				"{0}: Getting antenna relays from vessel {1}.",
				"IAntennaRelay",
				vessel.name
			));

			// If the vessel is loaded, we can fetch modules implementing IAntennaRelay directly.
			if (vessel.loaded) {
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is loaded, searching for modules in loaded parts.",
					"IAntennaRelay",
					vessel.name
				));

				// Gets a list of PartModules implementing IAntennaRelay
				foreach (Part part in vessel.Parts)
				{
					foreach (PartModule module in part.Modules)
					{
						if (module is IAntennaRelay)
						{
							relays.Add(part.GetHashCode(), module as IAntennaRelay);
							break;
						}
					}
				}
			}
			// If the vessel is not loaded, we need to build ProtoAntennaRelays when we find relay ProtoPartSnapshots.
			else
			{
				Tools.PostDebugMessage(string.Format(
					"{0}: vessel {1} is not loaded, searching for modules in prototype parts.",
					"IAntennaRelay",
					vessel.name
				));

				// Loop through the ProtoPartModuleSnapshots in this Vessel
				foreach (ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
				{
					ProtoAntennaRelay protoRelay;
					Part partPrefab = PartLoader.getPartInfoByName(pps.partName).partPrefab;

					foreach (PartModule module in partPrefab.Modules)
					{
						if (module is IAntennaRelay)
						{
							protoRelay =
								new ProtoAntennaRelay(module as IAntennaRelay, pps);
							relays.Add(pps.GetHashCode(), protoRelay);
							break;
						}
					}
				}
			}

			Tools.PostDebugMessage(string.Format(
				"{0}: vessel '{1}' has {2} transmitters.",
				"IAntennaRelay",
				vessel.name,
				relays.Count
			));

			// Return the list of IAntennaRelays
			return relays;
		}

		protected RelayDatabase()
		{
			relayDatabase =	new Dictionary<Guid, Dictionary<int, IAntennaRelay>>();
			vesselPartCountDB = new Dictionary<Guid, int>();

			GameEvents.onVesselWasModified.Add(this.onVesselWasModified);
			GameEvents.onVesselChange.Add(this.onVesselWasModified);
		}

		~RelayDatabase()
		{
			GameEvents.onVesselWasModified.Remove(this.onVesselWasModified);
			GameEvents.onVesselChange.Remove(this.onVesselWasModified);

			Tools.PostDebugMessage(this.GetType().Name + " destroyed.");
		}
	}
}

