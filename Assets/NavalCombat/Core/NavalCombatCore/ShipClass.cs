using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using System.Collections;

using CoreUtils;
using System.Diagnostics;
using YYZ.Ballistic;

namespace NavalCombatCore
{

    public enum ShipType // SK5 type
    {
        NotSpecified,
        Battleship, // BB, ironclad, pre-dreadnought, dreadnought, post-dreadnought
        Battlecruiser, // BC, Battle Cruiser
        LightCruiser, // CL
        // Cruiser, // CR, CC
        ArmoredCruiser, // CA
        Destroyer, // DD
        PatrolGunboat, // PG
        TorpedoBoat, // TB
        ArmedMerchantCruiser, // AMC
        Transport, // TR, Transport or merchant ship
        Repair, // AR, Auxiliary Repair
        LandBattery // LB, Land Battery (CB, Coast Battery)
    }

    public enum ExtraShipType
    {
        NotSpecified,
        Ironclad,
        Predreatought,
        Dreadnought,
        UnprotectedCruiser, // Ram Cruiser
        ProtectedCruiser,
        ArmoredCruiser,
        TorpedoCruiser
    }

    public enum MountLocation // SEEKRIEG like 3x3 location
    {
        NotSpecified, // indicate binding error
        PortForward,
        Forward, // A, B
        StarboardForward,
        PortMidship,
        Midship,
        StarboardMidship,
        PortAfter,
        After, // X, Y
        StarboardAfter,
    }

    public enum GunSightType
    {
        Basic,
        Telescope
    }

    public enum FireControlInstrumentType
    {
        None,
        Basic,
        MechanicalComputer,
        AdvancedMechanicalComputer,
    }

    public enum RangeFinderType
    {
        None,
        Optical
    }

    public enum DirectorControlType
    {
        None,
        FollowThePointer, // FTP
        Director,
    }

    public enum StabilizationType
    {
        Manual,
        GyroAssisted,
        StableElement
    }

    public enum PowerRemoteControlType // RPC
    {
        None,
        Partial,
        Full
    }

    public enum FireControlSystemRole
    {
        Primary,
        Secondary
    }

    public enum FireControlSystemEra
    {
        Predreadnought,
        WorldWarI,
        WorldWarII
    }

    public enum FCSCode // SK5 Table A11 FIRE CONTROL SYSTEM DATA
    {
        Custom,
        Z, // Basic gun-sight, no fire control instruments, no director control, manual stabilization
        Y, // Basic gun-sight, basic fire control instruments, no optical range-finders, no director control, manual stabilization
        X, // Basic gun-sight, basic fire control instruments, optical range-finders, no director control, manual stabilization
        W, // Basic gun-sight, basic fire control instruments, no optical range-finders, early FTP control system, manual stabilization
        V, // Basic gun-sight, basic fire control instruments, optical range-finders, early FTP control system, manual stabilization
        U, // Telescopic gun-sights, no fire control instruments, no optical range-finders, no director control, manual stabilization,
        T, // Telescopic gun-sights, basic fire control instruments, no optical range-finders, no director control, manual stabilization
        S, // Telescopic gun-sights, basic fire control instruments, optical range-finders, no director control, manual stabilization
        R, // Telescopic gun-sights, basic fire control instruments, no optical range-finders, early FTP control system, manual stabilization
        Q, // Telescopic gun-sights, basic fire control instruments, optical range-finders, early FTP control system, manual stabilization
        P, // Telescopic gun-sights, mechanical computer, optical range-finders, no director control, manual stabilization
        N, // Telescopic gun-sights, mechanical computer, optical range-finders, early FTP control system, manual stabilization.
        M, // Telescopic gun-sights, basic fire control instruments, optical range-finders, early FTP control system, gyro-assisted stabilization.
        L, // Telescopic gun-sights, mechanical computer, optical range-finders, no director control, gyro-assisted stabilization.
        K, // Telescopic gun-sights, mechanical computer, optical range-finders, early FTP control system, gyro-assisted stabilization
        J, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, manual stabilization.
        H, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, partial RPC
        G, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, gyro-assisted stabilization.
        F, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization.
        E, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, partial RPC
        D, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, full RPC
        C, // Telescipic gun-sights, advanced mechanical computer, optical range-finders, director control system, stable element.
        B, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control ssytem, stable element, partial RPC.
        A, // Telescipic gun-sights, advanced mechanical computer, optical range-finders, director control system, stable element, full RPC.
    }

    public partial class FireControlSystem
    {
        public FireControlSystemRole role;
        public FireControlSystemEra era;
        public FCSCode code;
        public GunSightType gunSight; // Basic, Telescope
        public FireControlInstrumentType fireControlInstrument; // None, Basic, MechanicalComputer, AdvancedMechanicalComputer
        public RangeFinderType rangeFinder; // None, Optical
        public DirectorControlType directorControl; // None, FollowThePointer (FTP), Director
        public StabilizationType stabilization; // Manual, GyroAssisted, StableElement
        public PowerRemoteControlType powerRemoteControl; // None, Partial, Full

        // public FireControlSystem Clone()
        // {
        //     return new FireControlSystem()
        //     {
        //         gunSight = gunSight,
        //         fireControlInstrument = fireControlInstrument,
        //         rangeFinder = rangeFinder,
        //         directorControl = directorControl,
        //         stabilization = stabilization,
        //         powerRemoteControl = powerRemoteControl
        //     };
        // }

        public override string ToString()
        {
            return $"FireControlSystem({role} {code} {era}, {gunSight} {fireControlInstrument} {rangeFinder} {directorControl} {stabilization} {powerRemoteControl})";
        }

        static List<FireControlSystem> referenceFireControlSystems = new()
        {
            new(){code=FCSCode.Z, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.None,                       rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.Y, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.X, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.W, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.V, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.U, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.None,                       rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.T, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.S, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.R, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.Q, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.P, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.N, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.M, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.L, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.K, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.J, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.H, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.G, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.F, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.E, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.D, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Full    },
            new(){code=FCSCode.C, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.B, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.A, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.Full    },
        };

        (GunSightType, FireControlInstrumentType, RangeFinderType, DirectorControlType, StabilizationType, PowerRemoteControlType) GetStates() => (
            gunSight,
            fireControlInstrument,
            rangeFinder,
            directorControl,
            stabilization,
            powerRemoteControl
        );

        public void CopyStates(FireControlSystem x)
        {
            gunSight = x.gunSight;
            fireControlInstrument = x.fireControlInstrument;
            powerRemoteControl = x.powerRemoteControl;
            rangeFinder = x.rangeFinder;
            stabilization = x.stabilization;
            directorControl = x.directorControl;
        }

        static Dictionary<FCSCode, FireControlSystem> code2fcs = referenceFireControlSystems.ToDictionary(x => x.code, x => x);
        static Dictionary<(GunSightType, FireControlInstrumentType, RangeFinderType, DirectorControlType, StabilizationType, PowerRemoteControlType), FireControlSystem> states2fcs = referenceFireControlSystems.ToDictionary(x => x.GetStates(), x => x);

        public void SyncCodeByStates()
        {
            if(states2fcs.TryGetValue(GetStates(), out var refFcs))
            {
                // CopyKey(refFcs);
                code = refFcs.code;
            }
            else
            {
                code = FCSCode.Custom;
            }
        }

        public void SyncStatesByCode()
        {
            if(code != FCSCode.Custom && code2fcs.TryGetValue(code, out var refFcs))
            {
                CopyStates(refFcs);
            }
        }
    }

    public enum RangeBand
    {
        Short,
        Medium,
        Long,
        Extreme
    }

    public static class Sk5RangeBandRules
    {
        public static RangeBand FromAngleOfFallDeg(double angleOfFallDeg)
        {
            if (angleOfFallDeg < 7d)
                return RangeBand.Short;
            if (angleOfFallDeg <= 20d)
                return RangeBand.Medium;
            if (angleOfFallDeg <= 40d)
                return RangeBand.Long;
            return RangeBand.Extreme;
        }
    }

    public class FireControlTableRecord
    {
        public double speedThresholdKnot;
        public double shortBroad;
        public double shortNarrow;
        public double mediumBroad;
        public double mediumNarrow;
        public double longBroad;
        public double longNarrow;
        public double extremeBroad;
        public double extremeNarrow;

        public double GetValue(RangeBand rangeBand, TargetAspect targetAspect)
        {
            return (rangeBand, targetAspect) switch
            {
                (RangeBand.Short, TargetAspect.Broad) => shortBroad,
                (RangeBand.Short, TargetAspect.Narrow) => shortNarrow,
                (RangeBand.Medium, TargetAspect.Broad) => mediumBroad,
                (RangeBand.Medium, TargetAspect.Narrow) => mediumNarrow,
                (RangeBand.Long, TargetAspect.Broad) => longBroad,
                (RangeBand.Long, TargetAspect.Narrow) => longNarrow,
                (RangeBand.Extreme, TargetAspect.Broad) => extremeBroad,
                (RangeBand.Extreme, TargetAspect.Narrow) => extremeNarrow,
                _ => shortBroad
            };
        }
    }

    public enum AmmunitionType // SK5 Table A12
    {
        ArmorPiercing, // AP, APC (Armor-piercing Capped), APCBC
        // Physical Description:
        // Thick casing walls and a hardened nose. Nose of shell capped (in APC) with slightly softer metal in order to improve penetration. Relatively small bursting charge (c. 2% of projectile weight).
        // Base fuse. Sometimes provided with a ballistic cap (or windscreen) to improve aerodynamic capabilities by reducing air resistance as in the APCBC type.
        // Effect:
        // Intended for use against heavily armored targets and expected to successfully penetrate armor of up to it's caliber in thickness (i.e. a 12'' shell would be expected to penetrate to up 12'' of armor and remain intact for bursting).
        // Instantaneous detonation behind the armor, causing great fragmentation in all directions at very high velocities. All of the explosive is consumed and all parts of the projectile are broken into small pieces (500 to 1800 for an 8'' shell).
        SemiArmorPiercing, // SAP, SAPC, SAPBC
        // Physical Description:
        // Similar to AP but with a non-hardened cap and thinner walls. Bursting charge of c. 4%. SAP is sometimes used to refer to COM, HE or HC shell with base or delayed fuses. USN Special Common shells were a version of SAP.
        // Effect:
        // Intended for use against lightly armored targets. Better penetration than COM but less fragmentation and explosive effect.
        Common, // COM, CPC, CPCBC
        // Physical Description:
        // Thinner walls than AP shells and a higher bursting charge (c. 6-8% of projectile weight). The designation CPC and CPCBC indicate common shells with added caps to increase either penetration or ballistic performance.
        // Effect:
        // Indended for use against lightly armored targets, exposed personnel and earthworks. Expected to successfully penetrate armor of up to one-half it's caliber in thickness
        // (i.e. an 8'' shell would be expected to penetrate up to 4'' of armor and remain intact for bursting). Produced greater fragmentation than API and spread destruction over a larger area.
        HighExplosive, // HE, HC (High-Capacity)
        // Physical Description:
        // Thinner walls than AP and COM shells to accommodate a higher bursting charge (c. 10-12% or more of projectile weight).
        // Projectile is made only suffciently strong enough to withstand the shock of firing (allowing shells for lower MV guns to contain more explosive). Many different types of explosives and fragmentation material used in HC shells.
        // Effect:
        // Indended for use against unarmored targets, unprotected shore installations and aircraft. Expected to successfully penetrate armor of approximately one-tenth the caliber in thickness
        // (i.e. a 15'' shell would be expected to penetrate roughly 1.5'' of armor). Capable of producing devastating fragmentation effects over a large area and starting numerous fires.
    }

    public class PenetrationTableRecord
    {
        // Threshold distance; for the final row this may be the next SK5 threshold, while BatteryRecord.rangeYards is the actual max range.
        public double distanceYards;
        public double rateOfFire; // Rounds per 2 minutes (1 SK game turn = 2 min)
        public RangeBand rangeBand;
        public double horizontalPenetrationInchs;
        public double verticalPenetrationInchs;

        public double GetValue(ArmorLocationAngleType angleType)
        {
            return angleType switch
            {
                ArmorLocationAngleType.Horizontal => horizontalPenetrationInchs,
                ArmorLocationAngleType.Vertical => verticalPenetrationInchs,
                _ => verticalPenetrationInchs
            };
        }
    }

    public class MountArcRecord
    {
        [XmlAttribute]
        public double startDeg;

        [XmlAttribute]
        public double CoverageDeg;

        [XmlAttribute]
        public bool isCrossDeckFire;

        public MountArcRecord Clone() => new()
        {
            startDeg = startDeg,
            CoverageDeg = CoverageDeg,
            isCrossDeckFire = isCrossDeckFire
        };

        public string Summary()
        {
            var s = isCrossDeckFire ? "C" : "";
            return $"{startDeg}-{(startDeg + CoverageDeg) % 360}{s}";
        }

        public bool IsInArc(double bearingRelativeToBowDeg)
        {
            return MeasureUtils.IsAngleInArc(bearingRelativeToBowDeg, startDeg, CoverageDeg);
        }

        public bool IsInArc(double bearingRelativeToBowDeg, double relaxedAngle)
        {
            return MeasureUtils.IsAngleInArcRelaxed(bearingRelativeToBowDeg, startDeg, CoverageDeg, relaxedAngle);
        }

        public double AngleDifferenceFromArc(double bearingRelativeToBowDeg)
        {
            return MeasureUtils.AngleDifferenceFromArc(bearingRelativeToBowDeg, startDeg, CoverageDeg);
        }
    }

    public enum MountArcsPattern // G1 Table
    {
        Normal,
        Narrow, // mainly for torpedo 
        Casemate // mainly for battery
    }

    public abstract class MountRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountLocation mountLocation;
        public int barrels = 1; // Guns per gun mounting, tubes per torpedo launcher.

        // public List<MountArcRecord> mountArcs = new() { new() };
        public List<MountArcRecord> mountArcs = new();
        // public bool useRestAngle; // If rest angle is not overriden, it's derived from arc.
        // public double restAngleDeg; // Graphic purpose only
        // public bool defaultNarrow;
        public MountArcsPattern mountArcsPattern;
        
        public string SummaryArcs() => string.Join(",", mountArcs.Select(arc => arc.Summary()));

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public override string ToString()
        {
            return $"{GetType().Name}({mountLocation}, {barrels})";
        }

        public class DefaultMountArcsConfig
        {
            public List<MountArcRecord> normal = new();
            public List<MountArcRecord> narrow = new(); // or "fixed", reserved for torpedo
            public List<MountArcRecord> casemate = new();
        }

        static Dictionary<MountLocation, DefaultMountArcsConfig> mountLocation2defaultMountArcsConfig = new() // G1 Arc of Fire Examples
        {
            {MountLocation.NotSpecified, new()},
            {MountLocation.Forward, new(){
                normal=new(){new(){startDeg=240, CoverageDeg=240}},
                narrow=new(){new(){startDeg=345, CoverageDeg=30}}
            }},
            {MountLocation.StarboardForward, new(){
                normal=new(){new(){startDeg=0, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}},
                casemate=new(){new(){startDeg=10, CoverageDeg=100}}
            }},
            {MountLocation.StarboardMidship, new(){
                normal=new(){new(){startDeg=30, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}},
                casemate=new(){new(){startDeg=20, CoverageDeg=140}}
            }},
            {MountLocation.StarboardAfter, new(){
                normal=new(){new(){startDeg=60, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}},
                casemate=new(){new(){startDeg=70, CoverageDeg=100}}
            }},
            {MountLocation.After, new(){
                normal=new(){new(){startDeg=60, CoverageDeg=240}},
                narrow=new(){new(){startDeg=165, CoverageDeg=30}}
            }},
            {MountLocation.PortAfter, new(){
                normal=new(){new(){startDeg=180, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}},
                casemate=new(){new(){startDeg=190, CoverageDeg=100}}
            }},
            {MountLocation.PortMidship, new(){
                normal=new(){new(){startDeg=210, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}},
                casemate=new(){new(){startDeg=200, CoverageDeg=140}}
            }},
            {MountLocation.PortForward, new(){
                normal=new(){new(){startDeg=240, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}},
                casemate=new(){new(){startDeg=250, CoverageDeg=100}}
            }},
            {MountLocation.Midship, new(){
                normal=new(){
                    new(){startDeg=30, CoverageDeg=120},
                    new(){startDeg=210, CoverageDeg=120}
                },
                narrow=new(){
                    new(){startDeg=75, CoverageDeg=30},
                    new(){startDeg=255, CoverageDeg=30}
                }
            }},
        };

        public void SyncDefaultMountArcs()
        {
            if(mountLocation2defaultMountArcsConfig.TryGetValue(mountLocation, out var defaultMountArcsConfig))
            {
                mountArcs.Clear();
                // var defaultMountArcs = defaultNarrow ? defaultMountArcsConfig.narrow : defaultMountArcsConfig.normal;
                var defaultMountArcs = mountArcsPattern switch
                {
                    MountArcsPattern.Normal => defaultMountArcsConfig.normal,
                    MountArcsPattern.Narrow => defaultMountArcsConfig.narrow,
                    MountArcsPattern.Casemate => defaultMountArcsConfig.casemate,
                    _ => defaultMountArcsConfig.normal
                };
                mountArcs.AddRange(defaultMountArcs.Select(arc => arc.Clone()));
            }
        }

        public bool IsInArc(double bearingRelativeToBowDeg)
        {
            return mountArcs.Any(arc => arc.IsInArc(bearingRelativeToBowDeg));
        }

        public bool IsInArcRelaxed(double bearingRelativeToBowDeg, double relaxedAngle)
        {
            return mountArcs.Any(arc => arc.IsInArc(bearingRelativeToBowDeg, relaxedAngle));
        }

        public double AngleDifferenceFromArc(double bearingRelativeToBowDeg)
        {
            return mountArcs.Min(arc => arc.AngleDifferenceFromArc(bearingRelativeToBowDeg));
        }

        public static Dictionary<MountLocation, string> mountLocationAcronymMap = new()
        {
            // NotSpecified, // indicate binding error
            // PortForward,
            // Forward, // A, B
            // StarboardForward,
            // PortMidship,
            // Midship,
            // StarboardMidship,
            // PortAfter,
            // After, // X, Y
            // StarboardAfter,
            {MountLocation.NotSpecified, "NA"},
            {MountLocation.PortForward, "P/F"},
            {MountLocation.Forward, "F"},
            {MountLocation.StarboardForward, "S/F"},
            {MountLocation.PortMidship, "P/M"},
            {MountLocation.Midship, "M"},
            {MountLocation.StarboardMidship, "S/M"},
            {MountLocation.PortAfter, "P/A"},
            {MountLocation.After, "A"},
            {MountLocation.StarboardAfter, "S/A"},
        };

        public string mountLocationAcronym => mountLocationAcronymMap[mountLocation];
    }

    /// <summary>One physical gun mounting; barrels is its gun count.</summary>
    public sealed class GunMountRecord : MountRecord
    {
        [System.ComponentModel.DefaultValue(GunMountType.Unspecified)]
        public GunMountType mountType;
    }

    // The printed mounting type is independent of the gun's recorded arcs.
    public enum GunMountType { Unspecified, FixedMlrBlr }

    /// <summary>One physical torpedo launcher; barrels is its tube count.</summary>
    public sealed class TorpedoMountRecord : MountRecord
    {
        public TorpedoGraphicParameters torpedoGraphic = new();
        public bool trainable;
        public int reloadLimit;
    }

    public partial class BatteryRecord : IObjectIdLabeled
    {
        const double DamageRatingShellSizeCoef = 1.30d;
        const double DamageRatingShellWeightSqrtCoef = 0.82d;
        const double DamageRatingIntercept = 0.4d;

        public string objectId { get; set; }
        public GlobalString name = new();
        public double damageRating;
        public double maxRateOfFireShootPerMin; // shoot/min
        public int fireControlPositions;
        public FireControlSystem fireControlType = new();
        public double rangeYards;

        public bool hasFireControlRadar = false;
        public double fireControlRadarModifier;
        public GlobalString fireControlRadarName = new();

        public double barrelLengthCalibers; // Nominal barrel length / bore diameter; 0 = undefined.
        public double shellSizeInch;
        public double shellWeightPounds; // lb
        public int ammunitionCapacity;
        public BatteryRecordMetaInfo metaInfo = null;
        public BatteryRecordMetaInfoMcCoyOkun metaInfoMcCoyOkun = null;

        public List<FireControlTableRecord> fireControlTableRecords = new();
        public bool customFireControlTable = false;
        public AmmunitionType penetrationTableBaseType;
        public List<PenetrationTableRecord> penetrationTableRecords = new();
        public List<GunMountRecord> mountLocationRecords = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var m in mountLocationRecords)
            {
                yield return m;
            }
        }

        public override string ToString()
        {
            return $"BatteryRecord({name.GetMergedNamePure()})";
        }

        public double GetRoundsPerGun()
        {
            return (double)ammunitionCapacity / mountLocationRecords.Sum(mnt => mnt.barrels);
        }

        public void UpdateDamageRatingDefault()
        {
            var shellSize = Math.Max(0d, shellSizeInch);
            var shellWeight = Math.Max(0d, shellWeightPounds);
            damageRating = Math.Floor(DamageRatingIntercept
                + DamageRatingShellSizeCoef * shellSize
                + DamageRatingShellWeightSqrtCoef * Math.Sqrt(shellWeight)
                + 0.5d);
        }

        static XmlSerializer serializer = new XmlSerializer(typeof(BatteryRecord));

        public string ToXML()
        {
            return YYZ.XmlUtils.ToXML(this);
        }

        public static BatteryRecord FromXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return (BatteryRecord)serializer.Deserialize(reader);
            }
        }

        public double EvaluateFirepowerPerBarrel() // all-directional
        {
            var damageScrore = damageRating;
            var RateOfFireScore = penetrationTableRecords.FirstOrDefault()?.rateOfFire ?? 0;
            var fireControlScore = fireControlTableRecords.FirstOrDefault()?.shortBroad ?? 0;
            return damageScrore * RateOfFireScore * fireControlScore;
        }

        public double EvaluateFirepowerScore()
        {
            var barrels = mountLocationRecords.Sum(m => m.barrels);
            return barrels * EvaluateFirepowerPerBarrel();
        }

        // for a specified direction only
        public double EvaluateFirepowerPerBarrel(double distanceYards, TargetAspect targetAspect, double targetSpeedKnots)
        {
            var penetrationItem = penetrationTableRecords.FirstOrDefault(r => distanceYards <= r.distanceYards);
            if (penetrationItem == null)
                return 0;
            var rateOfFire = penetrationItem.rateOfFire;
            var rangeBand = penetrationItem.rangeBand;
            var fireControlRow = fireControlTableRecords.FirstOrDefault(r => targetSpeedKnots <= r.speedThresholdKnot);
            if (fireControlRow == null)
                return 0;
            var fireControlValue = fireControlRow.GetValue(rangeBand, targetAspect);
            if (distanceYards <= 4500 && rangeBand == RangeBand.Short)
            {
                var closeRangeFireControlValue = RuleChart.GetCloseRangeFireControlScore(distanceYards, targetSpeedKnots, targetAspect);
                fireControlValue = Math.Max(fireControlValue, closeRangeFireControlValue);
            }

            return damageRating * rateOfFire * fireControlValue;
        }

        public double EvaluateFirepowerScore(double distanceYards, TargetAspect targetAspect, double targetSpeedKnots, double bearingRelativeToBowDeg)
        {
            var firepowerPerBarrel = EvaluateFirepowerPerBarrel(distanceYards, targetAspect, targetSpeedKnots);
            var avaialbleBarrels = mountLocationRecords.Where(r => r.IsInArc(bearingRelativeToBowDeg)).Sum(r => r.barrels);
            return firepowerPerBarrel * avaialbleBarrels;
        }
    }

    public class TorpedoSetting
    {
        [XmlAttribute]
        public double rangeYards;

        [XmlAttribute]
        public double speedKnots;
    }

    public enum TorpedoDamageClass
    {
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I
    }

    public enum TorpedoPistolType
    {
        Contact,
        Magnetic
    }

    public class TorpedoSectorMetaInfo
    {
        public double warheadLb;
        public double diameterIn;
        public int year;
    }

    public partial class TorpedoSector
    {
        public GlobalString name = new();
        public List<TorpedoMountRecord> mountLocationRecords = new();
        public List<TorpedoSetting> torpedoSettings = new();
        // public List<TorpedoMountRecord> mountLocationRecords = new() { new() };
        // public List<TorpedoSetting> torpedoSettings = new() { new() };
        public int ammunitionCapacity;
        public TorpedoDamageClass damageClass;
        public TorpedoSectorMetaInfo metaInfo = null;
        public double dudProbability = 0.5d; // General value of 15% for 1880-1945 is too "optimistic" for 1894
        public TorpedoPistolType pistolType;

        public double EvaluateTorpedoThreatPerBarrel()
        {
            return 1; // TODO: Add handling for Damage CLass, Speed, and Damage.
        }

        public double EvaluateTorpedoThreatScore()
        {
            var barrels = mountLocationRecords.Sum(r => r.barrels);
            return barrels * EvaluateTorpedoThreatPerBarrel();
        }

        public double EvaluateTorpedoThreatScore(double distanceYards, double bearingRelativeToBowDeg)
        {
            var setting = torpedoSettings.FirstOrDefault(setting => setting.rangeYards * CoreParameter.Instance.automaticTorpedoFiringRangeRelaxedCoef >= distanceYards);
            if (setting == null)
                return 0;
            var barrels = mountLocationRecords.Where(m => m.IsInArc(bearingRelativeToBowDeg)).Sum(m => m.barrels);
            return barrels * EvaluateTorpedoThreatPerBarrel();
        }
    }

    public class BatteryRecordMetaInfo
    {
        public NaabLikeProjectile naabLikeProjectile = NaabLikeProjectile.CreateDefaultMetaProjectile();
        public double fallToNextFireSeconds = 15d;
    }

    public class BallisticSample
    {
        public string Id;
        public string Label;
        public McCoyPlusDragFunction DragFunction;
        public string ProjectilePresetId;
        public FacehardCapType CapType;
        public FacehardNoseSchema NoseSchema = FacehardNoseSchema.Standard;
        public double JapaneseCapHead = 2;
        public double ProjectileDiameter;
        public double ProjectileWeight;
        public double ProjectileBodyWeight;
        public double WindscreenWeight;
        public double WindscreenCapHeadWeight = 0;
        public double ProjectileLimitQuality = 1;
        public double ProjectileDamageQuality = 1;
        public double ShatterResistance = 0;
        public double BreakUnderNbl = 0;
        public double LightCase = 0;
        public double BallisticCoefficient;
        public double MuzzleVelocity;
        public double MaxRange;

        public BallisticSample Clone()
        {
            return (BallisticSample)MemberwiseClone();
        }
    }

    public static class BallisticSampleCatalog
    {
        static readonly List<BallisticSample> Samples = new()
        {
            new BallisticSample { Id = "britain-palliser-6", Label = "Palliser chilled cast iron shot and common shell / 6'' / Britain", DragFunction = McCoyPlusDragFunction.G1, ProjectilePresetId = "BPR1", CapType = FacehardCapType.None, ProjectileDiameter = 6, ProjectileWeight = 100, ProjectileBodyWeight = 100, WindscreenWeight = 0, BallisticCoefficient = 2.9727, MuzzleVelocity = 2230, MaxRange = 14600 },
            new BallisticSample { Id = "britain-palliser-10", Label = "Palliser chilled cast iron shot and common shell / 10'' / Britain", DragFunction = McCoyPlusDragFunction.G1, ProjectilePresetId = "BPR1", CapType = FacehardCapType.None, ProjectileDiameter = 10, ProjectileWeight = 500, ProjectileBodyWeight = 500, WindscreenWeight = 0, BallisticCoefficient = 5.1846, MuzzleVelocity = 2040, MaxRange = 11000 },
            new BallisticSample { Id = "britain-uncapped-75", Label = "Uncapped steel AP shot/shell 1890-1905 / 7.5'' / Britain", DragFunction = McCoyPlusDragFunction.G1, ProjectilePresetId = "BPR2", CapType = FacehardCapType.None, ProjectileDiameter = 7.5, ProjectileWeight = 200, ProjectileBodyWeight = 200, WindscreenWeight = 0, BallisticCoefficient = 2.489, MuzzleVelocity = 2827, MaxRange = 14328 },
            new BallisticSample { Id = "britain-uncapped-12", Label = "Uncapped steel AP shot/shell 1890-1905 / 12'' / Britain", DragFunction = McCoyPlusDragFunction.G1, ProjectilePresetId = "BPR2", CapType = FacehardCapType.None, ProjectileDiameter = 12, ProjectileWeight = 714, ProjectileBodyWeight = 715, WindscreenWeight = 0, BallisticCoefficient = 5.0293, MuzzleVelocity = 1914, MaxRange = 9450 },
            new BallisticSample { Id = "germany-38cm-psgr-bismarck", Label = "38cm Psgr.m.K. L/4.4 APC / Germany (Bismack)", DragFunction = McCoyPlusDragFunction.G7, ProjectilePresetId = "GPR12", CapType = FacehardCapType.Hard, ProjectileDiameter = 14.96, ProjectileWeight = 1763.7, ProjectileBodyWeight = 1552.05, WindscreenWeight = 52.91, BallisticCoefficient = 7.7734, MuzzleVelocity = 2690, MaxRange = 38870 },
        };

        public static List<BallisticSample> All()
        {
            return Samples.Select(sample => sample.Clone()).ToList();
        }

        public static BallisticSample SampleById(string id)
        {
            return Samples.FirstOrDefault(sample => sample.Id == id)?.Clone();
        }
    }

    public class BatteryRecordMetaInfoMcCoyOkun
    {
        public BallisticSample ballisticSample;
        public double fallToNextFireSeconds = 12d;
    }

    public class RapidFireBatteryFireControlLevelRecord
    {
        [XmlAttribute]
        public double fireControlMaxRange; // FC value for [eff, Max]

        [XmlAttribute]
        public double fireControlEffectiveRange; // FC value for [0, eff]
    }

    public class RapidFireBatteryRecordMetaInfo
    {
        public double shellSizeInch = RapidFireBatteryRecord.defaultShellSizeInch;
        public double shellWeightPounds = 0d;
        public int fireControlTier = 0;
    }

    public partial class RapidFireBatteryRecord
    {
        public GlobalString name = new();
        public double maxRangeYards;
        public double effectiveRangeYards;
        // public List<RapidFireBatteryFireControlLevelRecord> fireControlRecords = new() { new() };
        public List<RapidFireBatteryFireControlLevelRecord> fireControlRecords = new();
        public static double defaultShellSizeInch = 1.85d; // 47mm Hotchkiss default
        public RapidFireBatteryRecordMetaInfo metaInfo = null;
        public double damageFactor; // RF
        // public List<int> barrelsLevelPort = new() { 0 };
        // public List<int> barrelsLevelStarboard = new() { 0 };
        public List<int> barrelsLevelPort = new();
        public List<int> barrelsLevelStarboard = new();

        public double GetShellSizeInch()
        {
            return metaInfo?.shellSizeInch ?? defaultShellSizeInch;
        }

        public double EvaluateFirepowerPerBarrel()
        {
            var damageScore = damageFactor;
            var rateOfFireScore = 1;
            var fireControlScore = fireControlRecords.FirstOrDefault()?.fireControlEffectiveRange ?? 0;
            return damageScore * rateOfFireScore * fireControlScore;
        }

        public double EvaluateFirepowerScore()
        {
            var barrels = barrelsLevelPort.FirstOrDefault() + barrelsLevelStarboard.FirstOrDefault();
            return barrels * EvaluateFirepowerPerBarrel();
        }
    }

    public class SpeedIncreaseRecord
    {
        [XmlAttribute]
        public double thresholdSpeedKnots;

        [XmlAttribute]
        public double increaseSpeedKnots;
    }

    public class ArmorRatingReocrd
    {
        [XmlAttribute]
        public double effectInch;

        [XmlAttribute]
        public double actualInch;
    }

    public enum ArmorLocation
    {
        Deck,
        TurretHorizontal,
        SuperStructureHorizontal,
        ConningTower,
        MainBelt,
        BeltEnd,
        Barbette,
        TurretVertical,
        SuperStructureVertical,
        Ineffective,
    }

    public enum ArmorLocationAngleType
    {
        Horizontal,
        Vertical
    }

    public enum TargetAspect
    {
        Broad,
        Narrow
    }

    public enum ArmorType // SK5 Table A14 (ID is not encoded in the enum to simplify LocalizedEnum's binding)
    {
        NotSpecified, // Not Specified
        NoArmor, // No Armor
        WroughtIron, // 1, Wrought Iron, 1855-1890 (All) All naval armor, 0.6
        MildSteel, // 2, Mild Steel, 1876-1945 (All) Some armor. 0.75
        CompoundHardSteelFacedWroughtIron, // 3, Compound Hard Steel Faced Wriought Iron, 1878-1890 (All except France) Heavy vertical armor, 0.68
        NickelSteel, // 9, Nickel-Steel, 1890-1925 All armor, 0.9
        HarveyMildSteel, // 5, Harvey Mild Steel, 1891-1899 (All) Vertical armor 6'' and up, 0.74
        HarveyNickelSteel, // 21, Harvey Nickel-Steel, 1891-1899 (All) Vertical armor 6'' and up, 0.78
        KruppChromeNickelSteel, // 10, Krupp Chrome Nickel Steel (Krupp Soft), 1894-1918 (Germany) turret and CT roots and vertical light armor (to 3.2''), 0.95
        KruppCemented1894, // 6, Krupp Cemented (KCa), 1894-1918 (Germany) Vertical armor 3.2'' and up, 0.83
        HighTensileSteel, // 7, High Tensile Steel, 1895-1945 light armor and protective decks, 0.82
        // KruppCemented, // 22, Krupp Cemented (KCa), 1898-1918 (All), 0.83
        ClassAArmor1900, // 30, Class A Armor, 1900-1910 (USA) Vertical armor 4'' and up, 0.83
        KruppNickelSteel, // 4, Krupp Nickel Steel, 1900-1918 (Germany) protective decks, 0.83
        KruppNonCemented, // 12, Krupp Non-Cemented (KNC), 1900-1925 (Great Britain) turret and CT roof armor. Also Vertical armor (to 4''), 0.95
        KruppCementedWW1Era1905, // 25, Krupp Cemented (WW1 Era), 1905-1910 (Britain/Italy/Japan) Vertical armor over 4'', 0.83
        WitkowitzerKC, // 24, Witkowitzer KC, 1905-1918 (AustriaHungary) Vertical armor 3.2'' and up, 0.95
        ClassAArmorMidvaleNonCemented, // 32, Class A Armor Midvale Non-Cemented, 1907-1912 (USA) Vertical armor 4'' and up, 0.88
        ClassBArmor1910, // 16, Class B Armor, 1910-1932 (USA) Turret and CT root, gun mount, director and CT armor less than 4'', 0.95
        SpecialTreatmentSteel, // 15, Special Treatment Steel (STS), 1910-1960 (USA) vertical hull armor under 5''. amounred decks, lower belts 2'' to 12'', 1.0
        ClassAArmor1911, // 31, Class A Armor, 1911-1923 (USA) Vertical armor 4'' and up, 0.89
        KruppCementedWW1Era1911, // 26, Krupp Cemented WW1 Era, 1911-1936 (Britain/Italy/Japan) Vertical armor over 4'', 0.85
        KruppWotanHardNickelSteel, // 11, Krupp Wotan Hard Nickel Steel, 1925-1945 (Germany) horizontal and vertical armor (to 4.72''), 1.0
        DSiliconManganeseHTSteel, // 8, D Silicon-Manganese HT Steel, 1925-1945 light armor (to 2'') and bulkheads, 0.90
        NewVickersNonCemented, // 18, New Vickers Non-Cemented (NVNC), 1926-1945 (Japan), 0.95
        NonCementedArmor, // 13, Non Cemented Armor (NCA), 1926-1945 (Great Britain) turret and CT roofs, armored decks and vertical armor less than 4'', 1.0
        KruppCemented1928, // 23, Krupp Cemented (KCa), 1928-1945 (Germany) Vertical armor 3.94'' and up, 1.0
        POHomogenousPlate, // 14, PO Homogenous Plate, 1929-1943 (Italy) turret and CT roofs, armored decks and vertical armor less than 4'', 1.0
        ItalianWW2EraKruppCemented, // 28, Italian WW2 Era Krupp Cemented, 1929-1943 (Italy) Vertical armor over 4'', 1.0
        BritishCementedArmor, // 27, British Cemented Armor (CA), 1933-1946 (Britain) Vertical battleship armor over 4'', 1.0
        ClassBArmor1933, // 17, Class B Armor, 1933-1955 (USA) Turret roof, gun mount, director armor under 4''. Turret face 16'' and up. CT. 1.0
        ClassAArmor1933, // 33, Class A Armor, 1933-1955 (USA) Vertical armor 5'' and up to 16'', 1.0
        VickersNonCemented, // 29, Vickers Non-Cemented (VH), 1937-1945 (Japan) Vertical armor over 11'' and Yamato Class only. 0.84
        MolybdenumNonCemented // 19, Molybdenum Non-Cemented (MNC), 1941-1945 (Japan) Deck armor for Yamato Class only. 0.97
    }

    public partial class ArmorRating // Carrier is ignored at this point
    {
        public double armorTypeFactor;
        public ArmorRatingReocrd deck = new(); // 1H
        public ArmorRatingReocrd turretHorizontal = new(); // 2H
        public ArmorRatingReocrd superStructureHorizontal = new(); // 3H
        public ArmorRatingReocrd conningTower = new(); // 4V
        public ArmorRatingReocrd mainBelt = new(); // 5V
        public ArmorRatingReocrd beltEnd = new(); // 6V
        public ArmorRatingReocrd barbette = new(); // 7V
        public ArmorRatingReocrd turretVertical = new(); // 8V
        public ArmorRatingReocrd superStructureVertical = new(); // 9V

        public ArmorType armorType;

        public const string ArmorTypeReferenceUrl = "http://www.navweaps.com/index_nathan/metalprpsept2009.php";

        // This data comes from SK5 A14, which in turn is derived from Okun's work "Table of Metallurgical Properties of Naval Armor and Construction Materials" ( http://www.navweaps.com/index_nathan/metalprpsept2009.php )
        static Dictionary<ArmorType, double> armorTypeToFactor = new()
        {
            // { ArmorType.NotSpecified, 1 },
            { ArmorType.NoArmor, 0},
            { ArmorType.WroughtIron, 0.6d },
            { ArmorType.MildSteel, 0.75d },
            { ArmorType.CompoundHardSteelFacedWroughtIron, 0.68d },
            { ArmorType.NickelSteel, 0.9d },
            { ArmorType.HarveyMildSteel, 0.74d},
            { ArmorType.HarveyNickelSteel, 0.78d },
            { ArmorType.KruppChromeNickelSteel, 0.95d },
            { ArmorType.KruppCemented1894, 0.83d },
            { ArmorType.HighTensileSteel, 0.82d },
            { ArmorType.ClassAArmor1900, 0.83d },
            { ArmorType.KruppNickelSteel, 0.83d },
            { ArmorType.KruppNonCemented, 0.95d },
            { ArmorType.KruppCementedWW1Era1905, 0.83d },
            { ArmorType.WitkowitzerKC, 0.95d },
            { ArmorType.ClassAArmorMidvaleNonCemented, 0.88d },
            { ArmorType.ClassBArmor1910, 0.95d },
            { ArmorType.SpecialTreatmentSteel, 1d },
            { ArmorType.ClassAArmor1911, 0.89d },
            { ArmorType.KruppCementedWW1Era1911, 0.85d },
            { ArmorType.KruppWotanHardNickelSteel, 1d },
            { ArmorType.DSiliconManganeseHTSteel, 0.9d },
            { ArmorType.NewVickersNonCemented, 0.95d },
            { ArmorType.NonCementedArmor, 1d },
            { ArmorType.KruppCemented1928, 1d },
            { ArmorType.POHomogenousPlate, 1d },
            { ArmorType.ItalianWW2EraKruppCemented, 1d },
            { ArmorType.BritishCementedArmor, 1d },
            { ArmorType.ClassBArmor1933, 1d },
            { ArmorType.ClassAArmor1933, 1d },
            { ArmorType.VickersNonCemented, 0.84d },
            { ArmorType.MolybdenumNonCemented, 0.97d }
        };

        public void TryInferArmorType()
        {
            var pairs = armorTypeToFactor.Where(kv => kv.Value == armorTypeFactor).ToList();
            if(pairs.Count == 1)
            {
                armorType = armorType = pairs[0].Key;
                
                return;
            }
            armorType = ArmorType.NotSpecified;
        }

        IEnumerable<ArmorRatingReocrd> IterateArmorRatingRecords()
        {
            yield return deck;
            yield return turretHorizontal;
            yield return superStructureHorizontal;
            yield return conningTower;
            yield return mainBelt;
            yield return beltEnd;
            yield return barbette;
            yield return turretVertical;
            yield return superStructureVertical;
        }

        void SetEffectInchByArmorTypeFactor()
        {
            foreach(var armorRatingRecord in IterateArmorRatingRecords())
            {
                armorRatingRecord.effectInch = Math.Round(armorRatingRecord.actualInch * armorTypeFactor, 1);
            }
        }

        public void TrySetFactorAndEffectInch()
        {
            if(armorTypeToFactor.TryGetValue(armorType, out var factor))
            {
                armorTypeFactor = factor;
                SetEffectInchByArmorTypeFactor();
            }
        }

        public ArmorRatingReocrd GetRecord(ArmorLocation loc)
        {
            return loc switch
            {
                ArmorLocation.Deck => deck,
                ArmorLocation.TurretHorizontal => turretHorizontal,
                ArmorLocation.SuperStructureHorizontal => superStructureHorizontal,
                ArmorLocation.ConningTower => conningTower,
                ArmorLocation.MainBelt => mainBelt,
                ArmorLocation.BeltEnd => beltEnd,
                ArmorLocation.Barbette => barbette,
                ArmorLocation.TurretVertical => turretVertical,
                ArmorLocation.SuperStructureVertical => superStructureVertical,
                _ => mainBelt
            };
        }

        public double GetArmorEffectiveInch(ArmorLocation loc)
        {
            return GetRecord(loc).effectInch;
        }

        // public static double[,] broadAspectLocationWeightTable = new double[,]
        // {// Short   Medium  Long/Extreme
        //     {2,     12,     25}, // 1H DECK
        //     {1,     3,      6},  // 2H TURRET
        //     {2,     4,      8},  // 3H SUPERSTR
        //     {4,     3,      3},  // 4V CON
        //     {26,    18,     16}, // 5V MAIN BELT
        //     {9,     8,      7},  // 6V BELT ENDS
        //     {19,    17,     11}, // 7V BARBETTE
        //     {17,    16,     11}, // 8V TURRET
        //     {19,    17,     12}, // 9V SUPERSTR
        //     {1,     1,      1}   // INEFFECTIVE
        // };

        // public static double[,] narrowAspectLocationWeightTable = new double[,]
        // {// Short   Medium  Long/Extreme
        //     {4,     20,     34}, // 1H DECK
        //     {2,     3,      7},  // 2H TURRET
        //     {3,     9,      14}, // 3H SUPERSTR
        //     {6,     4,      3},  // 4V CON
        //     {7,     5,      5},  // 5V MAIN BELT
        //     {4,     3,      3},  // 6V BELT ENDS
        //     {28,    16,     6},  // 7V BARBETTE
        //     {23,    19,     13}, // 8V TURRET
        //     {22,    20,     14}, // 9V SUPERSTR
        //     {1,     1,      1}   // INEFFECTIVE
        // };

        // public static double[] GetLocationWeights(TargetAspect targetAspect, RangeBand rangeBand)
        // {
        //     var table = targetAspect switch
        //     {
        //         TargetAspect.Broad => broadAspectLocationWeightTable,
        //         TargetAspect.Narrow => narrowAspectLocationWeightTable,
        //         _ => broadAspectLocationWeightTable
        //     };
        //     var colIdx = Math.Min(table.GetLength(1), (int)rangeBand);
        //     var rows = table.GetLength(0);
        //     var weights = new double[rows];
        //     for (int rowIdx = 0; rowIdx < table.GetLength(0); rowIdx++)
        //         weights[rowIdx] = table[rowIdx, colIdx];
        //     return weights;
        // }

        public double GetWeightedArmor(TargetAspect targetAspect, RangeBand rangeBand)
        {
            var weights = RuleChart.GetLocationWeights(targetAspect, rangeBand);
            var sumWeights = 0.0;
            var sumArmor = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                var loc = (ArmorLocation)i;
                if (loc != ArmorLocation.Ineffective)
                {
                    sumWeights += weights[i];
                    sumArmor += weights[i] * GetArmorEffectiveInch(loc);
                }
            }
            return (double)(sumArmor / sumWeights);
        }

        // public static ArmorLocation RollArmorLocation(TargetAspect targetAspect, RangeBand rangeBand)
        // {
        //     var idx = RandomUtils.SampleIndex(GetLocationWeights(targetAspect, rangeBand));
        //     return (ArmorLocation)idx;
        // }
    }

    public enum LandBatteryType
    {
        Fixed,
        Temporary
    }

    public enum CamouflageType
    {
        None,
        Prepared,
    }

    public class LandBatteryRecord
    {
        public LandBatteryType type;
        public CamouflageType camouflage;
        public double commandPostArmorEffInch;
        public double munitionBunkerArmorEffInch;
        public double obsTowerEffInch;
        // TODO: Add obs tower's height
    }

    // A documented cruising range at a particular speed.
    public class CruisingRangeRecord
    {
        public double rangeNauticalMiles;
        public double speedKnots;
    }

    public partial class ShipClass : IObjectIdLabeled
    {
        [XmlIgnore]
        public int RuntimeLayoutRevision { get; internal set; }

        public string objectId { set; get; }
        public GlobalString name = new();
        public ShipType type;
        public ExtraShipType extraShipType;
        public Country country;
        public int referenceYear;
        // public int applicableYearBegin = 1900;
        // public int applicableYearEnd = 1900;
        public double displacementTons;
        public int complementMen;

        // Fuel capacities in the source ship log's tons (ts). Null means unknown.
        public double? coalTons;
        public double? oilTons;
        public bool ShouldSerializecoalTons() => coalTons.HasValue;
        public bool ShouldSerializeoilTons() => oilTons.HasValue;
        // Empty means no complete range/speed pair is known. Preserve source order.
        public List<CruisingRangeRecord> cruisingRanges = new();
        public bool ShouldSerializecruisingRanges() => cruisingRanges is { Count: > 0 };

        public double lengthFoot;
        public double beamFoot;
        public double draftFoot;
        // public GlobalString builderDesc = new();
        public GlobalString engineDesc = new();
        public GlobalString boilersDesc = new();
        // public List<BatteryRecord> batteryRecords = new() { new() };
        public List<BatteryRecord> batteryRecords = new();
        public TorpedoSector torpedoSector = new();
        // public List<RapidFireBatteryRecord> rapidFireBatteryRecords = new() { new() };
        public List<RapidFireBatteryRecord> rapidFireBatteryRecords = new();
        public int targetSizeModifier;
        public double damagePoint;
        public double speedKnots;
        public int damageControlRatingUnmodified;
        // public List<double> speedKnotsEngineRoomsLevels = new() { 0 };
        // public List<double> speedKnotsPropulsionShaftLevels = new() { 0 };
        // public List<double> speedKnotsBoilerRooms = new() { 0 };
        // public List<SpeedIncreaseRecord> speedIncreaseRecord = new() { new() };
        [XmlArrayItem("float", typeof(double))]
        public List<double> speedKnotsEngineRoomsLevels = new();
        [XmlArrayItem("float", typeof(double))]
        public List<double> speedKnotsPropulsionShaftLevels = new();
        [XmlArrayItem("float", typeof(double))]
        public List<double> speedKnotsBoilerRooms = new();
        public List<SpeedIncreaseRecord> speedIncreaseRecord = new();
        public double standardTurnDegPer2Min; // per 2 min
        public double emergencyTurnDegPer2Min; // per 2 min
        public ArmorRating armorRating = new();

        public string portraitUrl;
        public GlobalString remark = new();
        public bool isPoorlySupported; // So this data point should not used in the SK5 model fitting
        // public string portraitCode;
        // public string portraitTopCode;
        public PictureReference portraitReference = new();
        public PictureReference portraitTopReference = new();
        public PictureReference portraitIconReference = new();
        public bool isGraphicPlaceholder;
        public ShipGraphicParameters graphicParameters;

        public RamType ram;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var batteryRecord in batteryRecords)
            {
                yield return batteryRecord;
            }
            foreach (var mountLocationRecord in torpedoSector.mountLocationRecords)
            {
                yield return mountLocationRecord;
            }
        }

        public static Dictionary<ShipType, string> acronymForShipType = new()
        {
            {ShipType.Battleship, "BB"},
            {ShipType.Battlecruiser, "BC"},
            {ShipType.LightCruiser, "CL"},
            // {ShipType.Cruiser, "CC"},
            {ShipType.ArmoredCruiser, "CA"},
            {ShipType.Destroyer, "DD"},
            {ShipType.PatrolGunboat, "PG"},
            {ShipType.TorpedoBoat, "TB"},
            {ShipType.ArmedMerchantCruiser, "AMC"},
            {ShipType.Transport, "TR"},
            {ShipType.Repair, "AR"},
            {ShipType.LandBattery, "CB"} // Coast Battery
        };

        public static string GetAcronymFor(ShipType shipType)
        {
            if (acronymForShipType.TryGetValue(shipType, out string acronym))
            {
                return acronym;
            }
            return shipType.ToString();
        }

        public string GetAcronym()
        {
            return GetAcronymFor(type);
        }

        public static double torpedoWeightPounds = 845; // Whitehead Mk1 845 lbs
        public static double rapidFiringGunAmmoCapacityTacticalTurns = 15; // SK5 default rule: RF Gun can fire for 15 tactical turns (30 min)
        public static double rapidFiringGunAmmoWeightPoundsPerRound = 50; // 50 pounds, (47mm Hotchkiss)
        public static double rapidFiringGunAverageRoundPerMin = 10; // 10 round/min (47mm Hotchkiss)

        public static double CalculateDamagePointFromDisplacement(double displacementTons)
        {
            return Math.Round(100d * (double)Math.Sqrt(Math.Max(0d, displacementTons) * 0.033d));
        }

        public static int CalculateTargetSizeModifierFromWaterplaneArea(double waterplaneAreaFoot2)
        {
            waterplaneAreaFoot2 = Math.Max(0d, waterplaneAreaFoot2);
            // TODO: Submarine (SS) ship type is not implemented yet, so all ships use the non-SS branch.
            // SS rule when added: <= 5292 => -1, 5292-10830 => 0, > 10830 => +1.

            if (waterplaneAreaFoot2 <= 6228d)
            {
                return -1;
            }

            if (waterplaneAreaFoot2 <= 12949.5d)
            {
                return 0;
            }

            if (waterplaneAreaFoot2 <= 38935d)
            {
                return 1;
            }

            if (waterplaneAreaFoot2 <= 64458.5d)
            {
                return 2;
            }

            if (waterplaneAreaFoot2 <= 90500d)
            {
                return 3;
            }

            return 4;
        }

        public static int CalculateTargetSizeModifierFromDimensions(double lengthFoot, double beamFoot)
        {
            return CalculateTargetSizeModifierFromWaterplaneArea(lengthFoot * beamFoot);
        }

        public static int CalculateDamageControlRatingFromDisplacement(double displacementTons)
        {
            if (displacementTons <= 120d)
            {
                return 0;
            }

            if (displacementTons <= 1089d)
            {
                return 1;
            }

            if (displacementTons <= 3000d)
            {
                return 2;
            }

            if (displacementTons <= 5950d)
            {
                return 3;
            }

            if (displacementTons <= 9800d)
            {
                return 4;
            }

            if (displacementTons <= 14700d)
            {
                return 5;
            }

            if (displacementTons <= 20500d)
            {
                return 6;
            }

            if (displacementTons <= 27000d)
            {
                return 7;
            }

            if (displacementTons <= 35000d)
            {
                return 8;
            }

            if (displacementTons <= 44000d)
            {
                return 9;
            }

            if (displacementTons <= 50000d)
            {
                return 10;
            }

            return 11;
        }

        public void InferSpeedIncreaseRecord()
        {
            if (speedIncreaseRecord == null)
            {
                speedIncreaseRecord = new List<SpeedIncreaseRecord>();
            }

            speedIncreaseRecord.Clear();

            var speed = Math.Max(0d, speedKnots);
            if (speed <= 0d)
            {
                return;
            }

            var segmentCount = InferSpeedIncreaseSegmentCount(speed, displacementTons, type, extraShipType);
            var increases = InferSpeedIncreaseValues(segmentCount, speed, displacementTons, type);
            var thresholdRatios = GetSpeedIncreaseThresholdRatios(segmentCount);

            var previousThreshold = -1d;
            for (var i = 0; i < increases.Length; i++)
            {
                var threshold = i == 0 ? 0d : RoundSpeedIncreaseThreshold(speed * thresholdRatios[i]);
                if (i > 0)
                {
                    threshold = Math.Max(previousThreshold + 1d, threshold);
                    threshold = Math.Min(threshold, Math.Max(previousThreshold + 1d, Math.Floor(speed)));
                }

                speedIncreaseRecord.Add(new SpeedIncreaseRecord
                {
                    thresholdSpeedKnots = threshold,
                    increaseSpeedKnots = increases[i],
                });

                previousThreshold = threshold;
            }
        }

        public void InferTurnRate()
        {
            standardTurnDegPer2Min = InferStandardTurnDegPer2Min(speedKnots, displacementTons, type, extraShipType);
            emergencyTurnDegPer2Min = InferEmergencyTurnDegPer2Min(
                standardTurnDegPer2Min,
                speedKnots,
                displacementTons,
                type,
                extraShipType);
        }

        public void InferMachineryHitSpeedLimits()
        {
            var speed = Math.Max(0d, speedKnots);

            speedKnotsEngineRoomsLevels = BuildSpeedLimitLevels(speed, InferEngineRoomSpeedLimitDrops(speed, displacementTons, type, extraShipType));
            speedKnotsPropulsionShaftLevels = BuildSpeedLimitLevels(speed, InferPropulsionShaftSpeedLimitDrops(speed, displacementTons, type, extraShipType));
            speedKnotsBoilerRooms = BuildSpeedLimitLevels(speed, InferBoilerRoomSpeedLimitDrops(speed, displacementTons, type, extraShipType));
        }

        static int InferSpeedIncreaseSegmentCount(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            var isCapitalShip =
                shipType == ShipType.Battleship ||
                shipType == ShipType.Battlecruiser ||
                extraShipType == ExtraShipType.Ironclad ||
                extraShipType == ExtraShipType.Predreatought ||
                extraShipType == ExtraShipType.Dreadnought;
            var isCruiser =
                shipType == ShipType.LightCruiser ||
                shipType == ShipType.ArmoredCruiser ||
                shipType == ShipType.ArmedMerchantCruiser ||
                extraShipType == ExtraShipType.UnprotectedCruiser ||
                extraShipType == ExtraShipType.ProtectedCruiser ||
                extraShipType == ExtraShipType.ArmoredCruiser ||
                extraShipType == ExtraShipType.TorpedoCruiser;
            var isSmallFastShip =
                shipType == ShipType.Destroyer ||
                shipType == ShipType.TorpedoBoat ||
                shipType == ShipType.PatrolGunboat ||
                extraShipType == ExtraShipType.TorpedoCruiser ||
                displacementTons > 0d && displacementTons <= 3500d;

            if (speedKnots >= 27d)
            {
                return 5;
            }

            if (speedKnots >= 24d)
            {
                return isCapitalShip && displacementTons >= 7000d ? 4 : 5;
            }

            if (speedKnots >= 18d)
            {
                return 4;
            }

            if (speedKnots >= 16d)
            {
                return isCapitalShip || displacementTons >= 7000d ? 2 : 3;
            }

            if (speedKnots >= 12d)
            {
                return !isCapitalShip && isCruiser && displacementTons > 0d && displacementTons < 5000d ? 3 : 2;
            }

            return isSmallFastShip && !isCapitalShip ? 3 : 2;
        }

        static double[] InferSpeedIncreaseValues(int segmentCount, double speedKnots, double displacementTons, ShipType shipType)
        {
            switch (segmentCount)
            {
                case 2:
                    return new[] { 2d, 1d };
                case 3:
                    return new[] { 3d, 2d, 1d };
                case 4:
                    if (shipType == ShipType.TorpedoBoat && displacementTons > 0d && displacementTons <= 200d)
                    {
                        return new[] { 5d, 3d, 2d, 1d };
                    }

                    return speedKnots >= 18.5d && (displacementTons <= 0d || displacementTons < 7000d)
                        ? new[] { 5d, 4d, 2d, 1d }
                        : new[] { 4d, 3d, 2d, 1d };
                default:
                    if (speedKnots >= 30d)
                    {
                        return new[] { 8d, 6d, 4d, 2d, 1d };
                    }

                    if (speedKnots >= 27d)
                    {
                        return new[] { 7d, 5d, 3d, 2d, 1d };
                    }

                    if (speedKnots >= 25.5d && (shipType == ShipType.Destroyer || shipType == ShipType.TorpedoBoat))
                    {
                        return new[] { 6d, 5d, 2d, 2d, 1d };
                    }

                    return new[] { 6d, 5d, 3d, 2d, 1d };
            }
        }

        static double[] GetSpeedIncreaseThresholdRatios(int segmentCount)
        {
            return segmentCount switch
            {
                2 => new[] { 0d, 0.60d },
                3 => new[] { 0d, 0.405d, 0.60d },
                4 => new[] { 0d, 0.238d, 0.435d, 0.611d },
                _ => new[] { 0d, 0.258d, 0.429d, 0.548d, 0.645d },
            };
        }

        static double RoundSpeedIncreaseThreshold(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        static double InferStandardTurnDegPer2Min(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            var isCapitalShip =
                shipType == ShipType.Battleship ||
                shipType == ShipType.Battlecruiser ||
                extraShipType == ExtraShipType.Ironclad ||
                extraShipType == ExtraShipType.Predreatought ||
                extraShipType == ExtraShipType.Dreadnought;
            var isCruiser =
                shipType == ShipType.LightCruiser ||
                shipType == ShipType.ArmoredCruiser ||
                shipType == ShipType.ArmedMerchantCruiser ||
                extraShipType == ExtraShipType.UnprotectedCruiser ||
                extraShipType == ExtraShipType.ProtectedCruiser ||
                extraShipType == ExtraShipType.ArmoredCruiser ||
                extraShipType == ExtraShipType.TorpedoCruiser;

            if (shipType == ShipType.Destroyer)
            {
                return 120d;
            }

            if (shipType == ShipType.TorpedoBoat)
            {
                return displacementTons >= 90d && displacementTons <= 130d && speedKnots >= 23d
                    ? 180d
                    : 120d;
            }

            if (shipType == ShipType.PatrolGunboat)
            {
                return displacementTons > 0d && displacementTons <= 700d && speedKnots <= 13d ? 180d : 120d;
            }

            if (displacementTons > 0d && displacementTons <= 1500d)
            {
                return 120d;
            }

            if (shipType == ShipType.Transport || shipType == ShipType.Repair || shipType == ShipType.ArmedMerchantCruiser)
            {
                return 90d;
            }

            if (isCapitalShip)
            {
                if (speedKnots >= 20.5d)
                {
                    return 90d;
                }

                if (displacementTons >= 10000d && speedKnots < 18.5d)
                {
                    return 45d;
                }

                return displacementTons >= 7000d ? 60d : 90d;
            }

            if (shipType == ShipType.ArmoredCruiser || extraShipType == ExtraShipType.ArmoredCruiser)
            {
                if (displacementTons >= 12000d || speedKnots >= 21d && displacementTons >= 7000d)
                {
                    return 45d;
                }

                return displacementTons >= 3500d ? 60d : 90d;
            }

            if (isCruiser)
            {
                if (displacementTons >= 5800d && speedKnots >= 19d)
                {
                    return 45d;
                }

                if (displacementTons >= 3500d || speedKnots >= 22d)
                {
                    return 60d;
                }

                return 90d;
            }

            if (displacementTons > 0d && displacementTons <= 3500d)
            {
                return 90d;
            }

            return 60d;
        }

        static double InferEmergencyTurnDegPer2Min(
            double standardTurnDegPer2Min,
            double speedKnots,
            double displacementTons,
            ShipType shipType,
            ExtraShipType extraShipType)
        {
            return standardTurnDegPer2Min switch
            {
                45d => displacementTons >= 12000d || shipType == ShipType.Battleship || shipType == ShipType.Battlecruiser
                    ? 90d
                    : 60d,
                60d => 90d,
                90d => 120d,
                120d => 150d,
                180d => 225d,
                _ => Math.Round(standardTurnDegPer2Min * 4d / 3d),
            };
        }

        static List<double> BuildSpeedLimitLevels(double speedKnots, double[] drops)
        {
            return drops
                .Select(drop => Math.Max(0d, Math.Round(speedKnots - drop)))
                .ToList();
        }

        static double[] InferEngineRoomSpeedLimitDrops(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            var count = InferEngineRoomSpeedLimitCount(speedKnots, displacementTons, shipType, extraShipType);
            return count switch
            {
                1 => new[] { 0d },
                3 => new[] { 0d, 4d, speedKnots >= 22d ? 17d : 13d },
                _ => new[] { 0d, InferEngineRoomSecondDrop(speedKnots) },
            };
        }

        static double[] InferPropulsionShaftSpeedLimitDrops(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            var count = InferPropulsionShaftSpeedLimitCount(speedKnots, displacementTons, shipType, extraShipType);
            return count switch
            {
                1 => new[] { 0d },
                3 => new[] { 0d, speedKnots >= 22d ? 6d : 4d, speedKnots >= 22d ? 18d : 14d },
                4 => new[] { 0d, 3d, 12d, speedKnots >= 24d ? 23d : 18d },
                _ => new[] { 0d, InferPropulsionShaftSecondDrop(speedKnots) },
            };
        }

        static double[] InferBoilerRoomSpeedLimitDrops(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            var count = InferBoilerRoomSpeedLimitCount(speedKnots, displacementTons, shipType, extraShipType);
            return count switch
            {
                1 => new[] { 0d },
                3 => new[] { 0d, speedKnots >= 22d ? 4d : 3d, speedKnots >= 22d ? 16d : 13d },
                4 => new[] { 0d, speedKnots >= 27d ? 3d : 2d, speedKnots >= 27d ? 12d : 8d, speedKnots >= 27d ? 24d : 15d },
                5 => new[] { 0d, 1d, speedKnots >= 22d ? 6d : 5d, speedKnots >= 22d ? 13d : 10d, speedKnots >= 22d ? 20d : 16d },
                _ => new[] { 0d, InferBoilerRoomSecondDrop(speedKnots) },
            };
        }

        static int InferEngineRoomSpeedLimitCount(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            if (shipType == ShipType.TorpedoBoat)
            {
                return 1;
            }

            if (shipType == ShipType.Destroyer)
            {
                return speedKnots >= 26d && speedKnots < 29d ? 2 : 1;
            }

            if (displacementTons > 0d && displacementTons <= 1500d)
            {
                return 1;
            }

            if (displacementTons >= 12600d && displacementTons <= 14270d)
            {
                return 3;
            }

            if (shipType == ShipType.LightCruiser && displacementTons >= 3500d && displacementTons < 7000d && speedKnots >= 17d)
            {
                return 3;
            }

            return 2;
        }

        static int InferPropulsionShaftSpeedLimitCount(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            if (shipType == ShipType.TorpedoBoat && displacementTons <= 136d)
            {
                return 1;
            }

            if (shipType == ShipType.Battleship && displacementTons >= 18000d ||
                shipType == ShipType.Battlecruiser && displacementTons >= 17000d)
            {
                return 4;
            }

            if (displacementTons >= 12600d && displacementTons <= 14270d)
            {
                return 3;
            }

            if (shipType == ShipType.LightCruiser && displacementTons >= 3500d && displacementTons < 7000d && speedKnots >= 17d)
            {
                return 3;
            }

            if (displacementTons > 0d && displacementTons <= 136d)
            {
                return 1;
            }

            return 2;
        }

        static int InferBoilerRoomSpeedLimitCount(double speedKnots, double displacementTons, ShipType shipType, ExtraShipType extraShipType)
        {
            if (shipType == ShipType.TorpedoBoat && displacementTons <= 136d)
            {
                return 1;
            }

            if (displacementTons > 0d && displacementTons <= 1500d)
            {
                return speedKnots >= 20d ? 2 : 1;
            }

            if (shipType == ShipType.LightCruiser && displacementTons >= 5800d && displacementTons <= 8000d && speedKnots >= 19d)
            {
                return 5;
            }

            if (shipType == ShipType.Destroyer && speedKnots >= 30d)
            {
                return 4;
            }

            if (displacementTons >= 7000d && displacementTons <= 14270d)
            {
                return 4;
            }

            if (displacementTons >= 12000d || speedKnots >= 24d && displacementTons >= 3000d)
            {
                return 3;
            }

            return 2;
        }

        static double InferEngineRoomSecondDrop(double speedKnots)
        {
            if (speedKnots >= 23d)
            {
                return 10d;
            }

            if (speedKnots >= 18d)
            {
                return 8d;
            }

            if (speedKnots >= 15d)
            {
                return 7d;
            }

            return 5d;
        }

        static double InferPropulsionShaftSecondDrop(double speedKnots)
        {
            if (speedKnots >= 24d)
            {
                return 15d;
            }

            if (speedKnots >= 20d)
            {
                return 10d;
            }

            if (speedKnots >= 16d)
            {
                return 8d;
            }

            return 6d;
        }

        static double InferBoilerRoomSecondDrop(double speedKnots)
        {
            if (speedKnots >= 22d)
            {
                return 9d;
            }

            if (speedKnots >= 18d)
            {
                return 7d;
            }

            if (speedKnots >= 15d)
            {
                return 6d;
            }

            return 5d;
        }

        public static double CalculateLengthFootFromDisplacementAndType(double displacementTons, ShipType shipType)
        {
            return Math.Round(PredictDefaultFromDisplacementAndType(
                displacementTons,
                shipType,
                2.5669774d,
                0.3904741d,
                GetLengthTypeOffset(shipType)));
        }

        public static double CalculateBeamFootFromDisplacementAndType(double displacementTons, ShipType shipType)
        {
            return Math.Round(PredictDefaultFromDisplacementAndType(
                displacementTons,
                shipType,
                1.1975496d,
                0.3171914d,
                GetBeamTypeOffset(shipType)));
        }

        public static double CalculateDraftFootFromDisplacementAndType(double displacementTons, ShipType shipType)
        {
            return Math.Round(PredictDefaultFromDisplacementAndType(
                displacementTons,
                shipType,
                0.4707447d,
                0.2975393d,
                GetDraftTypeOffset(shipType)) * 10d) / 10d;
        }

        public static int CalculateComplementMenFromDisplacementAndType(double displacementTons, ShipType shipType)
        {
            return Math.Max(1, (int)Math.Round(PredictDefaultFromDisplacementAndType(
                displacementTons,
                shipType,
                0.1084306d,
                0.6979308d,
                GetComplementTypeOffset(shipType))));
        }

        static double PredictDefaultFromDisplacementAndType(
            double displacementTons,
            ShipType shipType,
            double intercept,
            double logDisplacementCoefficient,
            double typeOffset)
        {
            var logDisplacement = Math.Log(Math.Max(1d, displacementTons));
            return Math.Exp(intercept + logDisplacementCoefficient * logDisplacement + typeOffset);
        }

        static double GetLengthTypeOffset(ShipType shipType)
        {
            return shipType switch
            {
                ShipType.Battleship => -0.2935404d,
                ShipType.ArmoredCruiser => -0.1377085d,
                ShipType.TorpedoBoat => 0.5236686d,
                ShipType.Destroyer => 0.5377667d,
                ShipType.PatrolGunboat => 0.0132753d,
                ShipType.Transport => 0.0905690d,
                ShipType.ArmedMerchantCruiser => 0.1020602d,
                ShipType.Repair => 0.2181666d,
                ShipType.Battlecruiser => -0.0521659d,
                _ => 0d
            };
        }

        static double GetBeamTypeOffset(ShipType shipType)
        {
            return shipType switch
            {
                ShipType.Battleship => 0.0903524d,
                ShipType.ArmoredCruiser => 0.0465869d,
                ShipType.TorpedoBoat => 0.0391832d,
                ShipType.Destroyer => -0.0055650d,
                ShipType.PatrolGunboat => 0.0545587d,
                ShipType.Transport => 0.0005691d,
                ShipType.ArmedMerchantCruiser => 0.0099036d,
                ShipType.Repair => -0.1220814d,
                ShipType.Battlecruiser => 0.0744055d,
                _ => 0d
            };
        }

        static double GetDraftTypeOffset(ShipType shipType)
        {
            return shipType switch
            {
                ShipType.Battleship => -0.0231818d,
                ShipType.ArmoredCruiser => 0.0322265d,
                ShipType.TorpedoBoat => -0.4071087d,
                ShipType.Destroyer => -0.1104536d,
                ShipType.PatrolGunboat => -0.1003142d,
                ShipType.Transport => 0.5143422d,
                ShipType.ArmedMerchantCruiser => 0.5230984d,
                ShipType.Repair => -0.4029193d,
                ShipType.Battlecruiser => -0.1574514d,
                _ => 0d
            };
        }

        static double GetComplementTypeOffset(ShipType shipType)
        {
            return shipType switch
            {
                ShipType.Battleship => -0.2131911d,
                ShipType.ArmoredCruiser => 0.0607233d,
                ShipType.TorpedoBoat => -0.2046980d,
                ShipType.Destroyer => -0.0844927d,
                ShipType.PatrolGunboat => -0.1349758d,
                ShipType.Transport => -0.6856862d,
                ShipType.ArmedMerchantCruiser => -0.6651469d,
                ShipType.Repair => -0.4685847d,
                ShipType.Battlecruiser => -0.3309420d,
                _ => 0d
            };
        }

        // Move to ShipClass
        public double GetMaxAmmoWeightPounds()
        {
            var btyWeightPounds = batteryRecords.Sum(r => r.ammunitionCapacity * r.shellWeightPounds);
            var torpedoWeightPounds = torpedoSector.ammunitionCapacity * ShipClass.torpedoWeightPounds;
            // Rapid fire battery assume 30 min "rapid firing" capacity, 10 round/min ROF, 5 pounds/round (47mm Hotchkiss)
            var rapidFiringBarrels = rapidFireBatteryRecords.Sum(r => r.barrelsLevelPort.FirstOrDefault() + r.barrelsLevelStarboard.FirstOrDefault());
            var rapidFiringAmmo = rapidFiringBarrels * rapidFiringGunAmmoCapacityTacticalTurns;
            var rapidFiringEqRound = rapidFiringAmmo * 2 * rapidFiringGunAverageRoundPerMin;
            var rfBtyWeightPounds = rapidFiringEqRound * rapidFiringGunAmmoWeightPoundsPerRound;
            return btyWeightPounds + torpedoWeightPounds + rfBtyWeightPounds;
        }

        public double EvaluateArmorScore()
        {
            return EvaluateArmorScore(TargetAspect.Broad, RangeBand.Short);
        }

        public double EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand)
        {
            return armorRating.GetWeightedArmor(targetAspect, rangeBand);
        }

        public double EvaluateSurvivability()
        {
            // var armorScoreSmoothed = (double)(1 + Math.Sqrt(EvaluateArmorScore()));
            var armorScoreSmoothed = 1 + EvaluateArmorScore();
            return damagePoint * armorScoreSmoothed;
        }

        public double EvaluateBatteryFirepowerScore()
        {
            return batteryRecords.Sum(bs => bs.EvaluateFirepowerScore());
        }

        public double EvaluateBatteryFirepowerScore(double distanceYards, TargetAspect targetAspect, double targetSpeedKnots, double bearingRelativeToBowDeg)
        {
            return batteryRecords.Sum(bs => bs.EvaluateFirepowerScore(distanceYards, targetAspect, targetSpeedKnots, bearingRelativeToBowDeg));
        }

        public double EvaluateTorpedoThreatScore()
        {
            return torpedoSector.EvaluateTorpedoThreatScore();
        }

        public double EvaluateTorpedoThreatScore(double distanceYards, double bearingRelativeToBowDeg)
        {
            return torpedoSector.EvaluateTorpedoThreatScore(distanceYards, bearingRelativeToBowDeg);
        }

        public double EvaluateRapidFiringFirepowerScore()
        {
            return rapidFireBatteryRecords.Sum(rf => rf.EvaluateFirepowerScore());
        }

        public double EvaluateFirepowerScore()
        {
            var batteryFirepower = EvaluateBatteryFirepowerScore();
            // Torpedo is not handled here
            var torpedoThreat = EvaluateTorpedoThreatScore();
            var rapidFiringFirepower = EvaluateRapidFiringFirepowerScore();

            return 1d * batteryFirepower + 20d * torpedoThreat + 1d * rapidFiringFirepower;
        }

        public double EvaluateGeneralScore()
        {
            var survivability = EvaluateSurvivability();
            var firepowerScore = EvaluateFirepowerScore();
            // var armorScoreSmoothed = 1 + (double)Math.Sqrt(armorScore);
            return 1d * survivability + 1d * firepowerScore; // TODO: Consider DP?
        }

        public override string ToString()
        {
            return $"ShipClass({name.GetMergedName()})";
        }

        public double GetPoint() => EvaluateGeneralScore(); // TODO: Modify it with type?

        public static HashSet<ShipType> nonCombatShipTypes = new()
        {
            ShipType.Transport,
            ShipType.Repair
        };

        public bool IsCombatShip() => !nonCombatShipTypes.Contains(type);

        public DamageSchema GetDamageSchema() => type switch
        {
            ShipType.Transport => DamageSchema.MerchantVessal,
            ShipType.Repair => DamageSchema.MerchantVessal,
            ShipType.LandBattery => DamageSchema.LandBattery,
            _ => DamageSchema.Warship  
        };
    }
}
