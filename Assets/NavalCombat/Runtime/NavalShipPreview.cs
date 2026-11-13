using NavalCombat.Avalonia.Controls;
using NavalCombatCore;
using UnityEngine;

namespace NavalCombat.Unity
{
public sealed partial class NavalWorld
{
    // Actual world models needed by the close camera but replaced by chart symbols
    // in the main camera. No duplicate vessels or preview environment exist.
    public const int PreviewLayer=30;
    const int ChartOverlayLayer=29;
    Camera previewCamera;
    RenderTexture previewTexture;
    ShipLog previewShip;
    public void HideShipPreview(){if(previewCamera!=null)previewCamera.enabled=false;}
    bool NearPreview(Vector3 position)=>previewCamera!=null&&previewCamera.enabled&&
        (position-previewCamera.transform.position).sqrMagnitude<5000f*5000f;
    public RenderTexture UpdateShipPreview(ShipLog ship)
    {
        if(previewCamera==null)
        {
            previewCamera=Child("Vessel close-up camera",EnvironmentRoot).gameObject.AddComponent<Camera>();
            SetupSky(previewCamera);
            previewCamera.cullingMask=~(1<<ChartOverlayLayer);ViewCamera.cullingMask&=~(1<<PreviewLayer);
            previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=5000;previewCamera.fieldOfView=35;
            previewTexture=new RenderTexture(512,512,24){name="Live battlefield close-up"};previewTexture.Create();previewCamera.targetTexture=previewTexture;
        }
        var position=Project(ship.position.LatDeg,ship.position.LonDeg);
        float scale=ShipScale(ship);
        float length=Mathf.Max(40,NavalShipMesh.Dimensions(ship.shipClass).Length*scale);
        var target=position+Vector3.up*6*scale;
        // World +Z is north: follow position, keeping the camera south of the ship
        // so changes in heading remain visible instead of rotating the camera too.
        previewCamera.transform.position=target+new Vector3(0,length*.72f,-length*1.65f);
        previewCamera.transform.LookAt(target);
        bool refresh=!previewCamera.enabled||!ReferenceEquals(previewShip,ship);
        previewShip=ship;previewCamera.enabled=true;
        // The HUD updates after the world. Prepare geometry immediately when opened
        // or when the selection jumps, avoiding a frame of symbols/missing models.
        if(refresh){UpdateShipViews();UpdateWakes();}
        return previewTexture;
    }
    void ResetShipPreview()=>HideShipPreview();
    void DisposeShipPreview(){if(previewTexture!=null){previewTexture.Release();Destroy(previewTexture);}}
}
}
