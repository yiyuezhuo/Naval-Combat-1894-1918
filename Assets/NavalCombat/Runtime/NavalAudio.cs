using NavalCombat.Application;
using NavalCombatCore;
using UnityEngine;

namespace NavalCombat.Unity
{

public sealed class NavalAudio : MonoBehaviour
{
    sealed class Voice
    {
        public AudioSource Source;
        public double Start,End;
        public float Gain;
        public bool Active,Envelope;
    }

    NavalAudioConfig config;
    readonly List<Voice> voices=new();
    readonly Dictionary<string,double> torpedoLast=new();
    readonly Dictionary<AudioClip,float> clipPeaks=new();
    readonly NavalSoundscape soundscape=new();
    readonly System.Random random=new();
    BattleSession session;
    Camera view;
    NavalWorld world;
    bool viewActive,modal,muted;
    float effectsVolume=.3f;

    public bool MusicEnabled {get;set;}
    public bool EffectsEnabled
    {
        get=>!muted;
        set {muted=!value;if(muted)StopEffects();}
    }
    public float MusicVolume {get;set;} = .14f;
    public float EffectsVolume
    {
        get=>effectsVolume;
        set {effectsVolume=Mathf.Clamp01(value);if(effectsVolume<=0)StopEffects();else UpdateVoiceVolumes(AudioSettings.dspTime);}
    }
    public int ActiveVoiceCount=>voices.Count(v=>v.Active&&v.End>AudioSettings.dspTime);
    bool CanPlay=>isActiveAndEnabled && EffectsEnabled && effectsVolume>0 && viewActive && !modal
        && session?.Phase==BattlePhase.Running && session.Speed!=SimulationSpeed.Paused && config!=null;

    void Awake()
    {

        config=Resources.Load<NavalAudioConfig>("Naval/Audio/CombatAudio");
        if(config==null){Debug.LogError("Missing Naval/Audio/CombatAudio configuration.");return;}
        foreach(var clip in config.NearCannons.Concat(config.FarCannons).Append(config.Torpedo).Where(c=>c!=null).Distinct())
        {
            var samples=new float[clip.samples*clip.channels];float peak=1;
            if(clip.GetData(samples,0)){peak=0;foreach(float sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));}
            clipPeaks[clip]=peak;
        }
        var pool=new GameObject("Combat audio voices");pool.transform.SetParent(transform,false);
        for(int i=0;i<Mathf.Clamp(config.MaximumVoices,1,10);i++)
        {
            var go=new GameObject($"Voice {i+1:00}");go.transform.SetParent(pool.transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
            source.loop=false;source.pitch=1;source.outputAudioMixerGroup=config.EffectsGroup;
            voices.Add(new Voice{Source=source});
        }
    }
    public void SetPresentationState(bool active,bool hasModal,NavalWorld navalWorld)
    {
        viewActive=active;modal=hasModal;world=navalWorld;view=world?.ViewCamera;
        if(!CanPlay)StopEffects();
    }
    public void Attach(BattleSession battle)
    {
        Detach();StopEffects();session=battle;
        session.GunneryShotsResolved+=Shots;session.TorpedoesLaunched+=Torpedoes;session.Changed+=StateChanged;
    }
    void Detach()
    {
        if(session==null)return;
        session.GunneryShotsResolved-=Shots;session.TorpedoesLaunched-=Torpedoes;session.Changed-=StateChanged;
        session=null;
    }
    void StateChanged(){if(!CanPlay)StopEffects();}
    bool OnScreen(double latitude,double longitude)
    {
        if(view==null||world==null)return false;
        var p=view.WorldToViewportPoint(world.Project(latitude,longitude));
        return p.z>view.nearClipPlane&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
    }
    void Shots(IReadOnlyList<GunneryShotResolvedEvent> shots,SimulationSpeed speed)
    {
        if(!CanPlay||shots.Count==0)return;
        soundscape.Report(shots.Count,shots.Any(s=>OnScreen(s.ShooterLatitudeDeg,s.ShooterLongitudeDeg)),AudioSettings.dspTime);
    }
    void Torpedoes(IReadOnlyList<TorpedoLaunchedEvent> shots,SimulationSpeed speed)
    {
        if(!CanPlay)return;
        double now=AudioSettings.dspTime;
        foreach(var group in shots.GroupBy(s=>s.ShooterId))
        {
            if(torpedoLast.TryGetValue(group.Key,out var last)&&now-last<config.TorpedoCooldownSeconds)continue;
            torpedoLast[group.Key]=now;
            var shot=group.First();
            float gain=(OnScreen(shot.LatitudeDeg,shot.LongitudeDeg)?1:config.OffscreenGain)*(float)(.85+random.NextDouble()*.15);
            Schedule(config.Torpedo,gain,0,true,now);
        }
        foreach(var key in torpedoLast.Where(p=>now-p.Value>2).Select(p=>p.Key).ToArray())torpedoLast.Remove(key);
    }
    void Schedule(AudioClip clip,float gain,float pan,bool envelope,double now)
    {
        if(clip==null||clip.loadState!=AudioDataLoadState.Loaded)return;
        var voice=voices.FirstOrDefault(v=>!v.Active||v.End<=now);
        if(voice==null)return;
        voice.Source.Stop();voice.Source.clip=clip;voice.Source.panStereo=pan;voice.Source.pitch=1;
        voice.Start=now+Math.Max(.01,config.ScheduleLeadSeconds);voice.End=voice.Start+clip.length;
        voice.Gain=gain;voice.Envelope=envelope;voice.Active=true;
        UpdateVoiceVolumes(now);
        voice.Source.PlayScheduled(voice.Start);
    }
    void EmitCannon(int index,bool far,float gain,float pan)
    {
        var clips=far?config.FarCannons:config.NearCannons;
        if(index<clips.Length)Schedule(clips[index],gain,pan,false,AudioSettings.dspTime);
    }
    void UpdateVoiceVolumes(double now)
    {
        float sum=0;
        foreach(var voice in voices)
            if(voice.Active&&now<voice.End)
                sum+=clipPeaks.GetValueOrDefault(voice.Source.clip,1)*voice.Gain*effectsVolume*Mathf.Sqrt(1+Mathf.Abs(voice.Source.panStereo));
        // Leave headroom before the mixer's compressor attack can respond. All voices
        // share this gain, preserving their relative near/far and screen/offscreen mix.
        float headroom=config==null?1:Mathf.Min(1,config.MaximumInputPeak/Mathf.Max(.0001f,sum));
        foreach(var voice in voices)
        {
            if(!voice.Active)continue;
            if(now>=voice.End){voice.Source.Stop();voice.Active=false;continue;}
            float envelope=voice.Envelope?Mathf.Clamp01((float)((now-voice.Start)/.005))*Mathf.Clamp01((float)((voice.End-now)/.05)):1;
            voice.Source.volume=voice.Gain*effectsVolume*envelope*headroom;
        }
    }
    public void StopEffects()
    {
        soundscape.Reset();torpedoLast.Clear();
        foreach(var voice in voices){voice.Source.Stop();voice.Source.clip=null;voice.Active=false;}
    }
    void Update()
    {
        if(CanPlay){soundscape.Tick(AudioSettings.dspTime,config.OffscreenGain,EmitCannon);UpdateVoiceVolumes(AudioSettings.dspTime);}
        else if(voices.Any(v=>v.Active))StopEffects();
    }
    void OnEnable()=>AudioSettings.OnAudioConfigurationChanged+=AudioConfigurationChanged;
    void AudioConfigurationChanged(bool deviceChanged)=>StopEffects();
    void OnDisable(){AudioSettings.OnAudioConfigurationChanged-=AudioConfigurationChanged;StopEffects();}
    void OnDestroy(){Detach();StopEffects();}
}
}
