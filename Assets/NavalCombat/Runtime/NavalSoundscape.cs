namespace NavalCombat.Unity;

// Avalonia's activity-based naval ambience, with seconds on Unity's DSP clock.
// This RNG is presentation-only and never consumes the simulation's random stream.
public sealed class NavalSoundscape
{
    readonly Random random;
    readonly double[] next=new double[2];
    readonly int[] previous={-1,-1};
    double energy,lastReport,lastSound,lastTick;
    bool active,onScreen;
    public double Energy=>energy;
    public NavalSoundscape(int? seed=null)=>random=seed.HasValue?new Random(seed.Value):new Random();
    public void Report(int count,bool visible,double now)
    {
        if(count<=0)return;
        if(!active){active=true;lastTick=now;lastSound=double.NegativeInfinity;next[0]=now;next[1]=now+1d/3;previous[0]=previous[1]=-1;}
        lastReport=now;onScreen=visible;
        energy=Math.Min(1,energy+.16*Math.Sqrt(Math.Min(count,16)));
    }
    public void Tick(double now,float offscreenGain,Action<int,bool,float,float> emit)
    {
        if(!active)return;
        if(now-lastReport>=3 || now<lastTick){Reset();return;}
        energy*=Math.Exp(-(now-lastTick)/3);lastTick=now;
        double density=.26+1.29*energy;
        for(int side=0;side<2;side++)
        {
            if(now<next[side])continue;
            next[side]=now+(.5+random.NextDouble())/density;
            if(now-lastSound<.2)continue;
            lastSound=now;
            int clip=random.Next(3);
            if(previous[side]>=0 && clip>=previous[side])clip++;
            if(previous[side]<0)clip=random.Next(4);
            previous[side]=clip;
            bool far=random.NextDouble()<.45+.17*energy;
            float gain=(float)(far?.24+random.NextDouble()*.18:.58+random.NextDouble()*.27);
            gain*=.785f*(onScreen?1:offscreenGain);
            float pan=(float)((side==0?-1:1)*(.2+random.NextDouble()*.45));
            emit(clip,far,gain,pan);
        }
    }
    public void Reset(){active=false;energy=0;previous[0]=previous[1]=-1;}
}
