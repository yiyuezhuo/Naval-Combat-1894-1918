using UnityEngine;
using UnityEngine.Audio;

namespace NavalCombat.Unity
{

[CreateAssetMenu(menuName="Naval Combat/Audio configuration")]
public sealed class NavalAudioConfig : ScriptableObject
{
    public AudioClip[] NearCannons=new AudioClip[4];
    public AudioClip[] FarCannons=new AudioClip[4];
    public AudioClip Torpedo;
    public AudioMixerGroup EffectsGroup;
    [Range(1,10)] public int MaximumVoices=10;
    [Min(.01f)] public float ScheduleLeadSeconds=.05f;
    [Min(0)] public float TorpedoCooldownSeconds=.6f;
    [Range(0,1)] public float OffscreenGain=.39810717f;
    [Range(.1f,1)] public float MaximumInputPeak=.85f;
}
}
