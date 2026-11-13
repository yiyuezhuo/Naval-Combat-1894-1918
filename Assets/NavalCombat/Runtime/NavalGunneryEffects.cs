using NavalCombat.Avalonia.Controls;
using NavalCombat.Avalonia.Services;
using NavalCombat.Application;
using NavalCombatCore;
using UnityEngine;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    sealed class ShotEffect
    {
        public Vector3 Start,End,Direction;
        public bool Impact;
    }
    readonly Dictionary<GunneryShellVisual,ShotEffect> shotEffects=new();
    readonly HashSet<GunneryShellVisual> retainedShots=new();
    readonly List<GunneryShellVisual> expiredShots=new();
    readonly System.Random effectRandom=new(7419);
    ParticleSystem muzzleFlash,muzzleSmoke,waterSpray,waterFoam;
    ParticleSystem[] gunneryParticles;
    Material particleMaterial;
    Transform gunneryEffectRoot;
    float RandomEffect(float min,float max)=>Mathf.Lerp(min,max,(float)effectRandom.NextDouble());
    ParticleSystem CreateGunneryParticles(string name,float gravity,float growth)
    {
        var ps=Child(name,gunneryEffectRoot).gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.maxParticles=8192;main.gravityModifier=gravity;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
        var color=ps.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.75f,.35f),new GradientAlphaKey(0,1)});color.color=gradient;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,growth));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.maxParticleSize=1;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }
    void EnsureGunneryEffects()
    {
        if(gunneryEffectRoot!=null)return;
        gunneryEffectRoot=Child("Gunnery particles (shared battlefield)",lineRoot.parent);
        particleMaterial=new Material(Resources.Load<Shader>("Naval/GunneryParticle"));
        muzzleFlash=CreateGunneryParticles("Muzzle flashes",0,.15f);
        var flashProperties=new MaterialPropertyBlock();flashProperties.SetFloat("_Emissive",1);
        muzzleFlash.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(flashProperties);
        muzzleSmoke=CreateGunneryParticles("Gun smoke",0,3);
        waterSpray=CreateGunneryParticles("Shell splash spray",1,.55f);
        waterFoam=CreateGunneryParticles("Shell splash mist",0,3);
        gunneryParticles=new[]{muzzleFlash,muzzleSmoke,waterSpray,waterFoam};
    }
    void Emit(ParticleSystem ps,Vector3 position,Vector3 velocity,float size,float life,Color color)
    {
        ps.Emit(new ParticleSystem.EmitParams{position=position,velocity=velocity,startSize=size,startLifetime=life,startColor=color,randomSeed=(uint)effectRandom.Next(1,int.MaxValue)},1);
        ps.Pause(false);
    }
    ShotEffect ResolveShotEffect(GunneryShellVisual shell)
    {
        var shot=shell.Shot;
        var source=Session.GameState.shipLogs.FirstOrDefault(s=>s.objectId==shot.ShooterId);
        var target=Session.GameState.shipLogs.FirstOrDefault(s=>s.objectId==shot.TargetId);
        var effect=new ShotEffect{Start=Project(shot.ShooterLatitudeDeg,shot.ShooterLongitudeDeg)+Vector3.up*10,
            End=Project(shot.TargetLatitudeDeg,shot.TargetLongitudeDeg)+Vector3.up*(shot.Hit?6:0)};
        if(source!=null)
        {
            // Resolve even if the chart has not needed to instantiate this vessel's model.
            if(!shell.Muzzle.HasValue && shot.WeaponKind==GunneryWeaponKind.Battery)
            {
                var pose=ShipLayoutMesh.UpdatePose(source);
                if(pose.Mounts.TryGetValue(shot.WeaponId,out var index)&&pose.Recoils[index].Length>0)
                    shell.Muzzle=pose.Model.Muzzle(index,shell.BarrelSequence%pose.Recoils[index].Length,pose.Angles[index],source.headingDeg);
            }
            if(shell.Muzzle is {} muzzle)
            {
                effect.Start=Project(source.position.LatDeg,source.position.LonDeg)+new Vector3(muzzle.Offset.X,muzzle.Offset.Z,muzzle.Offset.Y)*ShipScale(source);
                effect.Direction=new Vector3(muzzle.Direction.X,muzzle.Direction.Z,muzzle.Direction.Y);
            }
        }
        if(!shot.Hit)
        {
            // The rules report hit/miss, not a miss coordinate. Presentation-only scatter,
            // outside the target hull; used by BOTH shell endpoint and splash.
            float heading=target==null?0:(float)target.headingDeg;
            var dimensions=target==null?(Length:80f,Beam:12f):NavalShipMesh.Dimensions(target.shipClass);
            float side=RandomEffect(0,1)<.5f?-1:1;
            var scatter=new Vector3(side*RandomEffect(dimensions.Beam*.5f+12,dimensions.Beam*.5f+70),0,RandomEffect(-dimensions.Length*.65f,dimensions.Length*.65f));
            effect.End+=Quaternion.Euler(0,heading,0)*scatter;
        }
        if(effect.Direction.sqrMagnitude<.1f)effect.Direction=(effect.End-effect.Start).normalized;
        return effect;
    }
    void FireMuzzle(GunneryShellVisual shell,ShotEffect effect)
    {
        float caliber=Mathf.Clamp((float)shell.Shot.ShellDiameterInch,1,18);
        for(int i=0;i<7;i++)Emit(muzzleFlash,effect.Start+effect.Direction*i*caliber*.16f,effect.Direction*8,
            caliber*(.65f-i*.065f),RandomEffect(.07f,.16f),new Color(1,.75f,.23f,.95f));
        for(int i=0;i<10;i++)Emit(muzzleSmoke,effect.Start+effect.Direction*RandomEffect(0,caliber*.6f),
            effect.Direction*RandomEffect(2,8)+new Vector3(RandomEffect(-1,1),RandomEffect(1,3),RandomEffect(-1,1)),
            caliber*RandomEffect(.3f,.6f),RandomEffect(1.2f,2.4f),new Color(.55f,.54f,.5f,.45f));
    }
    void Splash(GunneryShellVisual shell,ShotEffect effect)
    {
        float caliber=Mathf.Clamp((float)shell.Shot.ShellDiameterInch,1,18);
        float height=5+caliber*2;
        for(int i=0;i<64;i++)
        {
            float angle=RandomEffect(0,Mathf.PI*2),spread=RandomEffect(.5f,3.5f);
            float up=Mathf.Sqrt(2*9.81f*height)*RandomEffect(.55f,1);
            Emit(waterSpray,effect.End+Vector3.up*.3f,new Vector3(Mathf.Cos(angle)*spread,up,Mathf.Sin(angle)*spread),
                RandomEffect(.5f,1.8f)*(1+caliber*.1f),up/9.81f*2,new Color(.85f,.93f,.96f,.8f));
        }
        for(int i=0;i<20;i++)
        {
            float angle=RandomEffect(0,Mathf.PI*2);
            Emit(waterFoam,effect.End+Vector3.up*.7f,new Vector3(Mathf.Cos(angle),.2f,Mathf.Sin(angle))*RandomEffect(1,3),
                RandomEffect(1,3)*(1+caliber*.15f),RandomEffect(2,4),new Color(.82f,.91f,.94f,.4f));
        }
    }
    void UpdateGunneryEffects()
    {
        if(Gunfire.BridgeShells.Count>0)EnsureGunneryEffects();
        if(gunneryEffectRoot==null)return;
        float dt=Session.Speed==SimulationSpeed.Paused?0:Time.unscaledDeltaTime;
        if(dt>0)foreach(var ps in gunneryParticles)ps.Simulate(dt,false,false,false);
        retainedShots.Clear();
        int flashBudget=96,splashBudget=96;
        foreach(var shell in Gunfire.BridgeShells)
        {
            retainedShots.Add(shell);
            if(!shotEffects.TryGetValue(shell,out var effect))
            {
                effect=ResolveShotEffect(shell);shotEffects.Add(shell,effect);
                if(shell.ElapsedWallSeconds<.25&&flashBudget-->0)FireMuzzle(shell,effect);
            }
            if(!effect.Impact&&shell.ElapsedWallSeconds>=shell.DurationWallSeconds)
            {
                effect.Impact=true;if(!shell.Shot.Hit&&splashBudget-->0)Splash(shell,effect);
            }
        }
        expiredShots.Clear();foreach(var pair in shotEffects)if(!retainedShots.Contains(pair.Key))expiredShots.Add(pair.Key);
        foreach(var shell in expiredShots)shotEffects.Remove(shell);
    }
    void ResetGunneryEffects()
    {
        shotEffects.Clear();retainedShots.Clear();expiredShots.Clear();
        if(gunneryEffectRoot!=null)foreach(var ps in gunneryParticles)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
}
