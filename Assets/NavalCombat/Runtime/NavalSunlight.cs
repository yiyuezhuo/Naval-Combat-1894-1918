using NavalCombatCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    Material skyMaterial;
    static readonly int SunDirectionId=Shader.PropertyToID("_NavalSunDirection");
    static readonly int SunColorId=Shader.PropertyToID("_NavalSunColor");
    static readonly int AmbientId=Shader.PropertyToID("_NavalAmbient");
    static readonly int DaylightId=Shader.PropertyToID("_NavalDaylight");
    void OnEnable()=>RenderPipelineManager.beginCameraRendering+=ApplySunlight;
    void OnDisable()=>RenderPipelineManager.beginCameraRendering-=ApplySunlight;
    void SetupSky(Camera camera)
    {
        if(skyMaterial==null)skyMaterial=new Material(Resources.Load<Shader>("Naval/Sky")){name="Scenario sky"};
        camera.clearFlags=CameraClearFlags.Skybox;
        camera.gameObject.AddComponent<Skybox>().material=skyMaterial;
    }
    public SunState SunAtCamera(Camera camera)=>Session.GameState.scenarioState.GetSunPosition(Unproject(camera.transform.position));
    public static Vector3 SunDirection(SunState sun)
    {
        float azimuth=(float)sun.azimuthDeg*Mathf.Deg2Rad,altitude=(float)sun.altitudeDeg*Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(azimuth)*Mathf.Cos(altitude),Mathf.Sin(altitude),Mathf.Cos(azimuth)*Mathf.Cos(altitude));
    }
    void ApplySunlight(ScriptableRenderContext context,Camera camera)
    {
        if(Session?.GameState==null||skyMaterial==null||(camera!=ViewCamera&&camera!=previewCamera))return;
        var sun=SunAtCamera(camera);var direction=SunDirection(sun);float altitude=(float)sun.altitudeDeg;
        float day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,8,altitude));
        float direct=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.83f,5,altitude));
        var sunlight=Color.Lerp(new Color(1,.27f,.065f),new Color(1,.96f,.86f),Mathf.InverseLerp(0,25,altitude))*direct;
        var ambient=Color.Lerp(new Color(.006f,.009f,.018f),new Color(.24f,.29f,.36f),day);
        SetSun(skyMaterial,direction,sunlight,ambient,day);
        SetSun(shipMaterial,direction,sunlight,ambient,day);
        SetSun(seaMaterial,direction,sunlight,ambient,day);
        if(wakeMaterial!=null)SetSun(wakeMaterial,direction,sunlight,ambient,day);
        if(particleMaterial!=null)SetSun(particleMaterial,direction,sunlight,ambient,day);
    }
    static void SetSun(Material material,Vector3 direction,Color sun,Color ambient,float day)
    {
        material.SetVector(SunDirectionId,direction);material.SetColor(SunColorId,sun);
        material.SetColor(AmbientId,ambient);material.SetFloat(DaylightId,day);
    }
}
}
