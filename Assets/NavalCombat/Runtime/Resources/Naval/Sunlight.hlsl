#ifndef NAVAL_SUNLIGHT_INCLUDED
#define NAVAL_SUNLIGHT_INCLUDED
float3 _NavalSunDirection;
float4 _NavalSunColor;
float4 _NavalAmbient;
float _NavalDaylight;
float3 NavalSky(float3 direction, bool disk)
{
    direction=normalize(direction);
    float height=saturate(direction.y);
    float3 zenith=lerp(float3(.0015,.003,.009),float3(.075,.24,.49),_NavalDaylight);
    float3 horizon=lerp(float3(.008,.012,.025),float3(.48,.62,.72),_NavalDaylight);
    float3 color=lerp(horizon,zenith,pow(height,.45));
    float alignment=dot(direction,_NavalSunDirection);
    float twilight=(1-smoothstep(.02,.3,abs(_NavalSunDirection.y)))*_NavalDaylight;
    color+=float3(.6,.16,.035)*twilight*pow(saturate(alignment),8)*pow(1-height,4);
    color+=_NavalSunColor.rgb*.12*pow(saturate(alignment),128);
    if(disk)
    {
        float edge=max(fwidth(alignment),.000002);
        float solarDisk=smoothstep(.9999892-edge,.9999892+edge,alignment);
        color+=_NavalSunColor.rgb*solarDisk*3;
    }
    return color;
}
float3 NavalSurfaceLight(float3 normal)
{
    return _NavalAmbient.rgb+_NavalSunColor.rgb*saturate(dot(normalize(normal),_NavalSunDirection))*.85;
}
#endif
