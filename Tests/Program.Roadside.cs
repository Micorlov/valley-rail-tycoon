using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static void CheckRoadside()
    {
        // Grown towns link up by highway; each open highway gets a filling station, tyre shop and garage beside it.
        var fast=new Balance();fast.city.basePoints=400;var g=new GameSession(WorldState.New(fast),fast);
        bool opened=false;
        for(int i=0;i<40000;i++){g.Step();while(g.Cities.Notifications.Count>0)if(g.Cities.Notifications.Dequeue().kind==Notification.ServiceOpened)opened=true;}
        int planned=0,open=0;var highway=new HashSet<int>();
        foreach(var r in g.World.intercityRoads)foreach(var c in r.path)highway.Add(c.Key);
        foreach(var r in g.World.intercityRoads)
        {
            if(!Roadside.Planned(r))continue;
            planned++;if(Roadside.Open(r))open++;
            Assert(!r.ToBeach&&r.Complete&&r.path.Count>=Roadside.MinRoad&&Roadside.Shaped(r),"A service area stands beside a straight stretch of an open town highway");
            for(int i=0;i<Roadside.Cells;i++)
            {
                var c=Roadside.SiteCell(r,i);
                Assert(!highway.Contains(c.Key)&&g.Network.At(c)==null&&!MapDefinition.Water(c)&&!MapDefinition.Raised(c)&&g.Cities.CityAt(c)==null,"Service area on open ground at "+c);
                Assert(g.Cities.BlocksTrack(c)&&!g.Build.Placeable(c),"Track may not be laid through a service area at "+c);
                Assert(!g.Scenery.TreeAt(c),"No pine stands inside a service area at "+c);
            }
        }
        var kinds=new HashSet<int>();foreach(var r in g.World.intercityRoads)if(Roadside.Planned(r))kinds.Add(r.serviceKind);
        Assert(kinds.Count==Math.Min(planned,Roadside.Kinds),$"Station styles take turns: {kinds.Count} styles on {planned} service areas");
        Assert(planned>=3&&open>=3&&opened,$"Grown highways get open service areas: {planned} planned, {open} open on {g.World.intercityRoads.Count} roads, notice {opened}");
        Console.WriteLine($"Roadside: {planned} service areas ({open} open) on {g.World.intercityRoads.Count} roads after 40000 ticks");
        // Saves keep them; tampered sites are rejected.
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailRoadside-"+Guid.NewGuid()),new Codec(),g.Balance);
        string json=saves.CaptureSnapshot(g.World);var restored=saves.RestoreSnapshot(json);
        for(int i=0;i<g.World.intercityRoads.Count;i++){var a=g.World.intercityRoads[i];var b=restored.intercityRoads[i];Assert(a.service==b.service&&a.serviceAt==b.serviceAt&&a.serviceSide==b.serviceSide,"Service area survives a save");}
        var road=g.World.intercityRoads.Find(r=>Roadside.Open(r));
        foreach(var tamper in new Action<IntercityRoadState>[]{r=>r.serviceSide=Roadside.Along(r),r=>r.service=Roadside.Steps+1,r=>r.serviceAt=0,r=>r.serviceSide=7,r=>r.serviceKind=Roadside.Kinds})
        {
            var copy=saves.RestoreSnapshot(json);var target=copy.intercityRoads[g.World.intercityRoads.IndexOf(road)];tamper(target);
            bool rejected=false;try{SaveService.Validate(copy,g.Balance);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"A misplaced service area is rejected");
        }
        // Track laid beside a highway where a site would go keeps the service area off it; a replay plans the same sites.
        var replay=new GameSession(saves.RestoreSnapshot(json),g.Balance);
        for(int i=0;i<2400;i++){g.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(g.World)==saves.CaptureSnapshot(replay.World),"Service areas replay identically");
        // A save from before service areas: every road plans its site again, one a step, and the same ones.
        var old=saves.RestoreSnapshot(json);foreach(var r in old.intercityRoads){r.service=r.serviceAt=r.serviceSide=0;}
        var reopened=new GameSession(old,g.Balance);SaveService.Validate(reopened.World,reopened.Balance);
        for(int i=0;i<200*(planned+Roadside.Steps+1);i++)reopened.Step();
        int again=0;foreach(var r in reopened.World.intercityRoads)if(Roadside.Open(r))again++;
        Assert(again>=open,$"An older save builds its service areas: {again} open (was {open})");
        SaveService.Validate(reopened.World,reopened.Balance);
        // A beach road never gets one, and a short highway never does.
        foreach(var r in g.World.intercityRoads)Assert(!(r.ToBeach&&Roadside.Planned(r)),"No service area on a beach road");
    }
}
