# AntennaRange
A KSP mod that enforces and encourages the use of the bigger antennas.

# For Part Developers
## The Fields
AntennaRange extends and augments the functionality of the stock ModuleDataTransmitter through the new ModuleLimitedDataTransmitter class. This class uses five additional configurable fields to define the part's behavior.

nominalRange is the range, in meters, at which the part should function identically to the stock module, i.e. without any modification to the power cost or packet size. This is used along with maxPowerFactor to calculate the maximum range of the part.
simpleRange is the same as nominalRange, but is used when the mod is in "simple" mode. In general it will probably need to be a much larger number than nominalRange.
maxPowerFactor effectively sets the maximum range of the antenna by essentially capping how much the power may be "turned up" to get better range. I originally used 8 for this number, because it felt right. I've since used 4 (for my DTS) and 16 (for my Comm. 88-88). You don't want this number to be too high, or small probes will go uncontrollable a lot when transmitting.
maxDataFactor defines the maximum "speed up" bonus that comes from using antennas at less their nominal range. I originally used 4 for this number for all parts; the DTS has a higher bonus now.

Note that all of the fields needed for Squad's ModuleDataTransmitter still need to be filled out. Depending on how you're defining your parts, they might need to go in your AntennaRange patch, or they might already be defined on the base part.

## The Mechanic
In general, the scaling functions assume the relation `D² α P/R,` where D is the total transmission distance, P is the transmission power, and R is the data rate.  Data rate increases as range decreases below nominalRange: `R α nominalRange² / D²`.  By default, power use increases as range increases above nominalRange: `P α D² / nominalRange²`.  Optionally, power use may remain fixed, and data rate instead decreases as range increases above nominalRange: `R α nominalRange² / D²`.

## Patch Conventions
To maximize cross-compatibility, please consider the following conventions for ModuleManager patches regarding AntennaRange:

When providing new definitions for your own parts, always specify a :FOR[YourModHere] pass name.
Whenever changing default AntennaRange definitions (e.g. if you were going to rebalance my antennas to suit your mod), please do so in the :AFTER[AntennaRange] pass.
I recommend providing all optional functionality (e.g. enabling RemoteTech vs. AntennaRange modules) in separate patches using :NEEDS[] blocks.

A sample AntennaRange configuration for an all-new mod part might look like this:
```
@PART[modPartName]:FOR[YourModName]:NEEDS[AntennaRange,!RemoteTech]
{
	MODULE
	{
		// ### Module Definition ###
		name = ModuleLimitedDataTransmitter
		
		// ### Squad Definitions ###
		// Delay between transmission packets, in seconds
		packetInterval = 0.10
		
		// Data capacity of nominal transmission packets, in MiT
		packetSize = 2
		
		// Resource cost of nominal transmission packets, in units
		packetResourceCost = 20.0
		
		// Resource name to be consumed by transmission
		requiredResource = ElectricCharge
		
		// Animation module index, 0-based, of the antenna extend/retract animation
		DeployFxModules = 0
		
		// ### AntennaRange Defintions ###
		// Range, in meters, at which the antenna behaves per the "nominal" characteristics above
		// Used with "additive" ranges.
		nominalRange = 10000000000
		
		// Range, in meters, at which the antenna behaves per the "nominal" characteristics above
		// Used with "simple" ranges.
		simpleRange = 56250000000
		
		// The maxmimum multiplier on packetResourceCost, essentially defining the maximum power output of the
		// transmitter.  Maximum range is defined as: maxTransmitDistance = nominalRange * sqrt(maxPowerFactor)
		maxPowerFactor = 16
		
		// The maximum multiplier on packetSize, essentially defining the maximum data throughput of the
		// transmitter.
		maxDataFactor = 2
	}
	
	// We add this ModuleScienceContainer so that when transmission fails the antennas can try to stash the data instead of dumping it to the void.
	MODULE
	{
		name = ModuleScienceContainer

		dataIsCollectable = true
		dataIsStorable = false

		storageRange = 2
	}
}
```

This example assumes that the base part definition does not include a ModuleDataTransmitter module, or any RT modules. If the base part definition includes a ModuleDataTransmitter module, a sample AntennaRange patch could look like this:
```
@PART[modPartName]:FOR[YourModName]:NEEDS[AntennaRange,!RemoteTech]
{
	@MODULE[ModuleDataTransmitter]
	{
		@name = ModuleLimitedDataTransmitter
		nominalRange = 10000000000
		simpleRange = 56250000000
		maxPowerFactor = 16
		maxDataFactor = 2
	}
	
	// We add this ModuleScienceContainer so that when transmission fails the antennas can try to stash the data instead of dumping it to the void.
	MODULE
	{
		name = ModuleScienceContainer

		dataIsCollectable = true
		dataIsStorable = false

		storageRange = 2
	}
}
```

IIRC, RemoteTech parts should not have ModuleDataTransmitter definitions. In that case, to facilitate RT, AR, and Stock compatibility, a suite of patches like this might be appropriate:

```
// If we don't have RemoteTech, add a stock ModuleDataTransmitter first.
@PART[modPartName]:NEEDS[!RemoteTech]:BEFORE[YourModName]
{
	MODULE
	{
		// ### Module Definition ###
		name = ModuleDataTransmitter
		
		// ### Squad Definitions ###
		// Delay between transmission packets, in seconds
		packetInterval = 0.10
		
		// Data capacity of nominal transmission packets, in MiT
		packetSize = 2
		
		// Resource cost of nominal transmission packets, in units
		packetResourceCost = 20.0
		
		// Resource name to be consumed by transmission
		requiredResource = ElectricCharge
		
		// Animation module index, 0-based, of the antenna extend/retract animation
		DeployFxModules = 0
	}
}

// If AntennaRange is installed, convert that to a ModuleLimitedDataTransmitter
@PART[modPartName]:NEEDS[AntennaRange,!RemoteTech]:FOR[YourModName]
{
	@MODULE[ModuleDataTransmitter]
	{
		// ### Module Redefinition ###
		@name = ModuleLimitedDataTransmitter
		
		// ### AntennaRange Defintions ###
		// Range, in meters, at which the antenna behaves per the "nominal" characteristics above
		// Used with "additive" ranges.
		nominalRange = 10000000000
		
		// Range, in meters, at which the antenna behaves per the "nominal" characteristics above
		// Used with "simple" ranges.
		simpleRange = 56250000000
		
		// The maxmimum multiplier on packetResourceCost, essentially defining the maximum power output of the
		// transmitter.  Maximum range is defined as: maxTransmitDistance = nominalRange * sqrt(maxPowerFactor)
		maxPowerFactor = 16
		
		// The maximum multiplier on packetSize, essentially defining the maximum data throughput of the
		// transmitter.
		maxDataFactor = 2
	}

	// We add this ModuleScienceContainer so that when transmission fails the antennas can try to stash the data instead of dumping it to the void.
	MODULE
	{
		name = ModuleScienceContainer

		dataIsCollectable = true
		dataIsStorable = false

		storageRange = 2
	}
}

// If RemoteTech is installed, do their module(s) instead
@PART[modPartName]:NEEDS[RemoteTech]:FOR[YourModName]
{
	// RemoteTech module(s) here
}
```

## Useful Formulas

### Per Antenna
`nominalRange` is a given, and is never calculated
`maxPowerFactor` is a given, and is never calculated
`maxTransmitDistance = nominalRange * sqrt(maxPowerFactor)`

### Per Link
A "link" is any connected pair of antennas.  
`NominalLinkDistance = sqrt(nominalRange1 * nominalRange2)`  
`MaxLinkDistance = sqrt(maxTransmitDistance1 * maxTransmitDistance2)`

Therefore, to find the `MaxLinkDistance` from two sets of `nominalRange` and `maxPowerFactor`:  
`MaxLinkDistance = sqrt(nominalRange1 * sqrt(maxPowerFactor1) * nominalRange2 * sqrt(maxPowerFactor2))`

To find a single antenna's `nominalRange` from a desired `maxTransmitDistance` given its `maxPowerFactor`:  
`nominalRange = maxTransmitDistance / sqrt(maxPowerFactor)`

To find a single antenna's desired maximum range given the desired maximum link distance and another set `maxTransmitDistance`:  
`maxTransmitDistance1 = MaxLinkDistance * MaxLinkDistance / maxTransmitDistance2`

Remember that `maxPowerFactor` may differ between antennas (and does, in my lastest configs: longAntenna is 8, mediumDish is 4, commDish is 16).

Currently Kerbin's `maxPowerFactor` is hard-coded as 8.

Feel free to use this spreadsheet for balancing antennas if it's useful to you: https://goo.gl/ChsbfL

## On Balance
In my configs I've balanced the three stock antennas to cover all of the stock solar system. Since you're introducing five more antennas and working with OPM, you will probably want to change the behavior of the stock parts and diversify the range to gradually cover the whole OPM system. Since you have some parts specifically designed for use in planetary subsystems, their balance when transmitting to other parts is probably more important than their balance when transmitting to Kerbin. For longer range parts designed to make the whole interplanetary leap, the inverse is probably true.

Feel free to ask questions! If anything's unclear or you just want to bounce balance ideas off of me, don't be shy. I'm always happy to help.
