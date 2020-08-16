
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;   // Always needed
using RimWorld;      // RimWorld specific functions 
using Verse;         // RimWorld universal objects 
using Verse.Sound;

namespace Minerals
{
    /// <summary>
    /// Mineral class
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class DynamicMineral : StaticMineral
    {

        public new ThingDef_DynamicMineral attributes
        {
            get
            {
                return base.attributes as ThingDef_DynamicMineral;
            }
        }





        public virtual float GrowthRate
        {
            get
            {
                float output = 1f; // If there are no growth rate factors, grow at full speed

                // Get growth rate factors
                List<float> rateFactors = allGrowthRateFactors;
                List<float> positiveFactors = rateFactors.FindAll(fac => fac >= 0);
                List<float> negativeFactors = rateFactors.FindAll(fac => fac < 0);

                // if any factors are negative, add them together and ignore positive factors
                if (negativeFactors.Count > 0)
                {
                    output = negativeFactors.Sum();
                }
                else if (positiveFactors.Count > 0) // if all positive, multiply them
                {
                    output = positiveFactors.Aggregate(1f, (acc, val) => acc * val);
                }


                return output * MineralsMain.Settings.mineralGrowthSetting;
            }
        }



        public float GrowthPerTick
        {
            get
            {
                float growthPerTick = (1f / (GenDate.TicksPerDay * attributes.growDays));
                return growthPerTick * GrowthRate;
            }
        }


        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (DebugSettings.godMode)
            {
                stringBuilder.AppendLine("Size: " + size.ToStringPercent());
                stringBuilder.AppendLine("Growth rate: " + GrowthRate.ToStringPercent());
                float propSubmerged = 1 - submersibleFactor();
                if (propSubmerged > 0)
                {
                    stringBuilder.AppendLine("Submerged: " + propSubmerged.ToStringPercent());
                }
                foreach (growthRateModifier mod in attributes.allRateModifiers)
                {
                    stringBuilder.AppendLine(mod.GetType().Name + ": " + attributes.growthRateFactor(mod, getModValue(mod)));
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }


        public override void TickLong()
        {
            // Half the time, dont do anything
            if (Rand.Bool)
            {
                return;
            }

            // Try to grow
            float GrowthThisTick = GrowthPerTick;
            size += GrowthThisTick * 4000; // 1 long tick = 2000

            // Try to reproduce
            if (GrowthThisTick > 0 && size > attributes.minReproductionSize && Rand.Range(0f, 1f) < attributes.reproduceProp * GrowthRate * MineralsMain.Settings.mineralReproductionSetting)
            {
                attributes.TryReproduce(Map, Position);
            }

            // Refresh appearance if apparent size has changed
            float apparentSize = printSize();
            if (attributes.fastGraphicRefresh && Math.Abs(sizeWhenLastPrinted - apparentSize) > 0.1f)
            {
                sizeWhenLastPrinted = apparentSize;
                base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlag.Things);
            }

            // Try to die
            if (size <= 0 && Rand.Range(0f, 1f) < attributes.deathProb)
            {
                Destroy(DestroyMode.Vanish);
            }


        }
            

        public virtual float getModValue(growthRateModifier mod) 
        {
            // If growth rate factor is not needed, do not calculate
            if (mod == null)
            {
                return 0f;
            }
            else
            {
                return mod.valueAtPos(this);
            }
        }

        public List<float> allGrowthRateFactors 
        {
            get
            {
                return attributes.allRateModifiers.Select(mod => attributes.growthRateFactor(mod, getModValue(mod))).ToList();
            }
        }


    }       





    /// <summary>
    /// ThingDef_StaticMineral class.
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class ThingDef_DynamicMineral : ThingDef_StaticMineral
    {
        // The number of days it takes to grow at max growth speed
        public float growDays = 100f;


        public float minReproductionSize = 0.8f;
        public float reproduceProp = 0.001f;
        public float deathProb = 0.001f;
        public float spawnProb = 0.0001f; // chance of spawning de novo each tick
        public tempGrowthRateModifier tempGrowthRateModifer;  // Temperature effects on growth rate
        public rainGrowthRateModifier rainGrowthRateModifer;  // Rain effects on growth rate
        public lightGrowthRateModifier lightGrowthRateModifer; // Light effects on growth rate
        public fertGrowthRateModifier fertGrowthRateModifer;  // Fertility effects on growth rate
        public distGrowthRateModifier distGrowthRateModifer;  // Distance to needed terrain effects on growth rate
        public sizeGrowthRateModifier sizeGrowthRateModifer;  // Current size effects on growth rate
        public bool fastGraphicRefresh = false; // If true, the graphics are regenerated more often
        public int minSpawnClusterSize = 1; // The minimum number of crystals in clusters that are spawned during gameplay, not map creation
        public int maxSpawnClusterSize = 1; // The maximum number of crystals in clusters that are spawned during gameplay, not map creation


        public List<growthRateModifier> allRateModifiers 
        {
            get 
            {
                List<growthRateModifier> output = new List<growthRateModifier>{
                    tempGrowthRateModifer,
                    rainGrowthRateModifer,
                    lightGrowthRateModifer,
                    fertGrowthRateModifer,
                    distGrowthRateModifer,
                    sizeGrowthRateModifer
                };
                output.RemoveAll(item => item == null);
                return output;
            }
        }

        public List<growthRateModifier> mapRateModifiers
        {
            get
            {
                List<growthRateModifier> output = new List<growthRateModifier>{
                    tempGrowthRateModifer,
                    rainGrowthRateModifer,
                    lightGrowthRateModifer,
                    fertGrowthRateModifer,
                    distGrowthRateModifer,
                    sizeGrowthRateModifer
                };
                output.RemoveAll(item => item == null || (!item.wholeMapEffect));
                return output;
            }
        }

        public List<growthRateModifier> posRateModifiers
        {
            get
            {
                List<growthRateModifier> output = new List<growthRateModifier>{
                    tempGrowthRateModifer,
                    rainGrowthRateModifer,
                    lightGrowthRateModifer,
                    fertGrowthRateModifer,
                    distGrowthRateModifer,
                    sizeGrowthRateModifer
                };
                output.RemoveAll(item => item == null || item.wholeMapEffect);
                return output;
            }
        }

        // ======= Growth rate factors ======= //

        public virtual float growthRateFactor(growthRateModifier mod, float myValue)
        {
            // Growth rate factor not defined
            if (mod == null)
            {
                return 1f;
            }

            // decays if too high or low
            float stableRangeSize = mod.maxStable - mod.minStable;
            if (myValue > mod.maxStable) 
            {
                return - mod.aboveMaxDecayRate * (myValue - mod.maxStable) / stableRangeSize;
            } 
            if (myValue < mod.minStable) 
            {
                return - mod.belowMinDecayRate * (mod.minStable - myValue) / stableRangeSize;
            }

            // does not grow if too high or low
            if (myValue < mod.minGrow || myValue > mod.maxGrow) 
            {
                return 0f;
            } 

            // slowed growth if too high or low
            if (myValue < mod.idealGrow)
            {
                return 1f - ((mod.idealGrow - myValue) / (mod.idealGrow - mod.minGrow));
            }
            else 
            {
                return 1f - ((myValue - mod.idealGrow) / (mod.maxGrow - mod.idealGrow));
            }
        }

        public virtual List<float> allGrowthRateFactorsAtPos(IntVec3 aPosition, Map aMap, bool includePerMapEffects = true)
        {
            if (includePerMapEffects)
            {
                return allRateModifiers.Select(mod => growthRateFactor(mod, mod.valueAtPos(this, aPosition, aMap))).ToList();
            }
            else
            {
                return posRateModifiers.Select(mod => growthRateFactor(mod, mod.valueAtPos(this, aPosition, aMap))).ToList();
            }
        }

        public virtual List<float> allGrowthRateFactorsAtMap(Map aMap)
        {
            return mapRateModifiers.Select(mod => growthRateFactor(mod, mod.valueAtMap(aMap))).ToList();
        }

        public virtual float GrowthRateAtPos(Map aMap, IntVec3 aPosition, bool includePerMapEffects = true)
        {
            // Get growth rate factors
            List<float> rateFactors = allGrowthRateFactorsAtPos(aPosition, aMap, includePerMapEffects);
            List<float> positiveFactors = rateFactors.FindAll(fac => fac >= 0);
            List<float> negativeFactors = rateFactors.FindAll(fac => fac < 0);

            // if any factors are negative, add them together and ignore positive factors
            if (negativeFactors.Count > 0)
            {
                return negativeFactors.Sum();
            }

            // if all positive, multiply them
            if (positiveFactors.Count > 0)
            {
                return positiveFactors.Aggregate(1f, (acc, val) => acc * val);
            }

            // If there are no growth rate factors, grow at full speed
            return 1f;

        }

        public virtual float GrowthRateAtMap(Map aMap)
        {
            // Get growth rate factors
            List<float> rateFactors = allGrowthRateFactorsAtMap(aMap);
            List<float> positiveFactors = rateFactors.FindAll(fac => fac >= 0);
            List<float> negativeFactors = rateFactors.FindAll(fac => fac < 0);

            // if any factors are negative, add them together and ignore positive factors
            if (negativeFactors.Count > 0)
            {
                return negativeFactors.Sum();
            }

            // if all positive, multiply them
            if (positiveFactors.Count > 0)
            {
                return positiveFactors.Aggregate(1f, (acc, val) => acc * val);
            }

            // If there are no growth rate factors, grow at full speed
            return 1f;

        }

    }


    public abstract class growthRateModifier
    {
        public float aboveMaxDecayRate;  // How quickly it decays when above maxStableFert
        public float maxStable; // Will decay above this fertility level
        public float maxGrow; // Will not grow above this fertility level
        public float idealGrow; // Grows fastest at this fertility level
        public float minGrow; // Will not grow below this fertility level
        public float minStable; // Will decay below this fertility level
        public float belowMinDecayRate;  // How quickly it decays when below minStableFert
        public bool wholeMapEffect = false; // If a whole-map attribute can be used instead of a per-position attribute (faster)

        public abstract float valueAtPos(DynamicMineral aMineral);
        public abstract float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap);
        public abstract float valueAtMap(Map aMap);
    }

    public class tempGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Position.GetTemperature(aMineral.Map);
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aPosition.GetTemperature(aMap);
        }

        public override float valueAtMap(Map aMap)
        {
            return aMap.mapTemperature.OutdoorTemp;
        }

    }

    public class rainGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.weatherManager.curWeather.rainRate;
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.weatherManager.curWeather.rainRate;
        }
        public override float valueAtMap(Map aMap)
        {
            return aMap.weatherManager.curWeather.rainRate;
        }
    }

    public class lightGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.glowGrid.GameGlowAt(aMineral.Position);
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.glowGrid.GameGlowAt(aPosition);
        }
        public override float valueAtMap(Map aMap)
        {
            throw new InvalidOperationException("lightGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
    }


    public class fertGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.fertilityGrid.FertilityAt(aMineral.Position);
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.fertilityGrid.FertilityAt(aPosition);
        }
    
        public override float valueAtMap(Map aMap)
        {
            throw new InvalidOperationException("fertGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
    }

    public class distGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.distFromNeededTerrain;
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return myDef.posDistFromNeededTerrain(aMap, aPosition);
        }
        public override float valueAtMap(Map aMap)
        {
            throw new InvalidOperationException("distGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
    }


    public class sizeGrowthRateModifier : growthRateModifier
    {
        public override float valueAtPos(DynamicMineral aMineral)
        {
            return aMineral.size;
        }

        public override float valueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return 0.01f;
        }

        public override float valueAtMap(Map aMap)
        {
            throw new InvalidOperationException("sizeGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
    }



    public class DynamicMineralWatcher : MapComponent
    {

        public static int ticksPerLook = 1000; // 100 is about once a second on 1x speed
        public int tick_counter = 1;

        public DynamicMineralWatcher(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            // Run each class' watcher
            tick_counter += 1;
            if (tick_counter > ticksPerLook)
            {
                tick_counter = 1;
                Look();
            }
        }

        // The main function controlling what is done each time the map is looked at
        public void Look()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SpawnDynamicMinerals();
            watch.Stop();
            Log.Message("========== SpawnDynamicMinerals() took: " + watch.ElapsedMilliseconds);
        }


        public void SpawnDynamicMinerals() 
        {
            foreach (ThingDef_DynamicMineral mineralType in DefDatabase<ThingDef_DynamicMineral>.AllDefs)
            {
                //var watch = System.Diagnostics.Stopwatch.StartNew();
                Log.Message("Trying to spawn " + mineralType.defName);


                // Check that the map type is ok
                if (! mineralType.CanSpawnInBiome(map))
                {
                    continue;
                }
                //Log.Message("   Biome OK");

                // Get number of positions to check
                float perMapGrowthFactor = mineralType.GrowthRateAtMap(map);
                Log.Message("   perMapGrowthFactor: " + perMapGrowthFactor);
                Log.Message("   spawnProb: " + mineralType.spawnProb);
                float numToCheck = map.Area * mineralType.spawnProb * perMapGrowthFactor * MineralsMain.Settings.mineralSpawningSetting;
                if (numToCheck <= 0)
                {
                    continue;
                }

                // If less than one cell should be checked, randomly decide to check one or none
                if (numToCheck < 1)
                {
                    if (Rand.Range(0f, 1f) < numToCheck)
                    {
                        numToCheck = 1;
                    } else
                    {
                        continue;
                    }
                }

                // Never check more than 1/10 of the map (performance failsafe)
                if (numToCheck > map.Area / 10)
                {
                    numToCheck = map.Area / 10;
                }

                // Round to integer
                numToCheck = (float) Math.Round(numToCheck);

                Log.Message("   numToCheck: " + numToCheck);

                // Try to spawn in a subset of positions
                for (int i = 0; i < numToCheck; i++)
                {
                    // Pick a random location
                    IntVec3 aPos = CellIndicesUtility.IndexToCell(Rand.RangeInclusive(0, map.Area - 1), map.Size.x);

                    // Dont always spawn if growth rate is not good
                    if (Rand.Range(0f, 1f) > mineralType.GrowthRateAtPos(map, aPos, false))
                    {
                        continue;
                    }

//                    // If it is an associated ore, find a position nearby
//                    if (mineralType.PosIsAssociatedOre(map, aPos))
//                    {
//                        IntVec3 dest;
//                        if (mineralType.TryFindReproductionDestination(map, aPos, out dest))
//                        {
//                            aPos = dest;
//                        }
//                    }


                    // Try to spawn at that location
                    //Log.Message("Trying to spawn " + mineralType.defName);
                    //mineralType.TrySpawnAt(aPos, map, 0.01f);
                    mineralType.SpawnCluster(map, aPos, Rand.Range(0.01f, 0.05f), Rand.Range(mineralType.minSpawnClusterSize, mineralType.maxSpawnClusterSize));

                }

                //watch.Stop();
                //Log.Message("Spawning " + mineralType.defName + " took: " + watch.ElapsedMilliseconds);

            }
        }
    }
}



