using System;
using ValleyRail.Core;
partial class Program
{
    /// <summary>The industry window's facts and the tap pick on the tilted view (IndustryGuide).</summary>
    static void CheckIndustryGuide()
    {
        var g=New();var w=g.World;var mill=g.Cargo.Producer(13);var mine=g.Cargo.Producer(1);
        var takes=IndustryGuide.Takes(ProducerKind.SteelMill);
        Assert(takes.Count==1&&takes[0]==Cargo.IronOre&&IndustryGuide.Takes(ProducerKind.IronMine).Count==0,"A steel mill takes iron ore; an iron mine takes nothing");
        Assert(IndustryGuide.Takes(ProducerKind.Plant)[0]==Cargo.Coal&&IndustryGuide.Takes(ProducerKind.Factory)[0]==Cargo.Steel,"Power takes coal, a factory takes steel");
        Assert(IndustryGuide.NearestSource(w,mill,Cargo.IronOre).id==12,"Iron ore comes from Highland Iron Mine");
        Assert(IndustryGuide.NearestBuyer(w,mill,Cargo.Steel).kind==ProducerKind.Factory,"Steel goes to a factory");
        Assert(IndustryGuide.NearestBuyer(w,mine,Cargo.Coal).kind==ProducerKind.Plant&&IndustryGuide.NearestSource(w,mine,Cargo.Coal)==null,"Coal goes to power; nothing else mines coal");
        Assert(IndustryGuide.NearestBuyer(w,g.Cargo.Producer(9),Cargo.Goods).kind==ProducerKind.Town,"Sawmill goods go to a town");
        string how=IndustryGuide.HowItWorks(ProducerKind.SteelMill);
        Assert(how.Contains("iron ore")&&how.Contains("steel")&&how.Contains("nothing on its own"),"The mill explains 1:1 processing: "+how);
        Assert(IndustryGuide.HowItWorks(ProducerKind.IronMine).Contains("on its own")&&IndustryGuide.HowItWorks(ProducerKind.Plant).Contains("coal"),"Raw sites and power stations explain themselves");
        Assert(IndustryGuide.KindName(ProducerKind.SteelMill)=="Steel mill"&&IndustryGuide.KindName(ProducerKind.Plant)=="Power station","Kind names read naturally");
        // The default camera looks down (pitch 35.264°, yaw 45°), so the ray meets the ground ~1.4 cells back per unit of height.
        float d=(float)(1/Math.Sqrt(3));
        ProducerState Aim(float x,float y,float z)=>IndustryGuide.Hit(w,x-d*100,y+d*100,z-d*100,d,-d,d);
        Assert(Aim(53.5f,2f,55.5f)?.id==13,"A finger on the furnaces picks the mill though the ground behind it is 2 cells past the yard");
        Assert(Aim(13,0,15)==null,"The ground well behind a low coal mine is not the mine");
        Assert(Aim(10,.5f,12)?.id==1,"The coal mine's hall picks the mine");
        Assert(Aim(30,0,30)==null&&Aim(48,0,43)==null,"Open ground and towns are not industries");
        Assert(IndustryGuide.Hit(w,53,5,55,0,1,0)==null,"A ray pointing up hits nothing");
    }
}
