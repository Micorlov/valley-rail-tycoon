using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static void CheckTownTrams()
    {
        // Towns that grow into cities open a surface light-rail line through their streets, one a minute, each once.
        // Small towns open lines here too, so the check runs in a few game minutes instead of the hours cities take.
        var fast=new Balance();fast.city.basePoints=400;fast.city.tramLevel=CityLevel.SmallTown;var g=new GameSession(WorldState.New(fast),fast);
        var opened=new List<Notification>();
        for(int i=0;i<60000&&opened.Count<2;i++){g.Step();while(g.Cities.Notifications.Count>0){var n=g.Cities.Notifications.Dequeue();if(n.kind==Notification.TownTramOpened)opened.Add(n);}}
        Assert(opened.Count>=2,$"Grown cities open light-rail lines: {opened.Count} opened by tick {g.World.tick}");
        var lanes=RoadLanes.Build(g.World,g.Network);var cityIds=new HashSet<int>();
        foreach(var n in opened)
        {
            var city=g.World.cities.Find(c=>c.id==n.cityId);
            Assert(city!=null&&cityIds.Add(city.id),"Each town opens its line once");
            Assert(city.tram>0&&city.tram%1200==900&&city.level>=fast.city.tramLevel,$"{city.name} opened its line as a {city.level} at tick {city.tram}");
            Assert(n.value==TownTramLine.ColourIndex(g.World,city),"The notice carries the line's colour");
            var line=TownTramLine.Plan(g.World,city,lanes,g.Network);
            CheckTramLine(g,city,line,lanes);
            Console.WriteLine($"Town tram: {city.name} {TownTramLine.ColourName(n.value)} Line, {line.Count} cells, stops at {string.Join(" ",line.stops)}");
        }
        Assert(TownTramLine.ColourIndex(g.World,g.World.cities.Find(c=>c.id==opened[0].cityId))==0&&TownTramLine.ColourIndex(g.World,g.World.cities.Find(c=>c.id==opened[1].cityId))==1,"Lines are coloured in the order they opened");
        // The line brings passengers and growth; the town's saved totals stay consistent with its buildings.
        var tramCity=g.World.cities.Find(c=>c.id==opened[0].cityId);var producer=g.World.producers.Find(p=>p.id==tramCity.producerId);
        CitySimulation.Derive(tramCity,g.Balance,out _,out _,out _,out int withTram,out _);
        int rate=g.Cities.Rate(tramCity);long tick=tramCity.tram;tramCity.tram=0;
        CitySimulation.Derive(tramCity,g.Balance,out _,out _,out _,out int without,out _);int rateWithout=g.Cities.Rate(tramCity);tramCity.tram=tick;
        Assert(withTram==without+without*fast.city.tramPassengerPercent/100&&producer.production==withTram,$"The line adds {fast.city.tramPassengerPercent}% passengers: {without} → {withTram}");
        Assert(rate-rateWithout>=fast.city.tramPoints,$"The line adds growth points: {rateWithout} → {rate}");
        SaveService.Validate(g.World,g.Balance);
        // Saves keep the opening tick; a tick from the future or below zero is rejected.
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailTownTrams-"+Guid.NewGuid()),new Codec(),g.Balance);
        string json=saves.CaptureSnapshot(g.World);var restored=saves.RestoreSnapshot(json);
        Assert(restored.cities.Find(c=>c.id==tramCity.id).tram==tramCity.tram,"A town's line survives a save");
        foreach(long bad in new[]{-1L,g.World.tick+1})
        {
            var copy=saves.RestoreSnapshot(json);copy.cities.Find(c=>c.id==tramCity.id).tram=bad;
            bool rejected=false;try{SaveService.Validate(copy,g.Balance);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,$"A tram opening tick of {bad} is rejected");
        }
        var replay=new GameSession(saves.RestoreSnapshot(json),g.Balance);
        for(int i=0;i<3600;i++){g.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(g.World)==saves.CaptureSnapshot(replay.World),"Town trams replay identically");
        // A save from before town trams: its cities open their lines again within a few minutes, and the save stays valid.
        var old=saves.RestoreSnapshot(json);int due=0;
        foreach(var c in old.cities){if(c.tram!=0)due++;c.tram=0;var p=old.producers.Find(x=>x.id==c.producerId);CitySimulation.Recount(c,p,g.Balance);}
        var reopened=new GameSession(old,g.Balance);SaveService.Validate(reopened.World,reopened.Balance);
        for(int i=0;i<1200*(due+1);i++)reopened.Step();
        int again=0;foreach(var c in reopened.World.cities)if(c.tram!=0)again++;
        Assert(again>=due,$"An older save opens its lines again: {again} of {due}");
        SaveService.Validate(reopened.World,reopened.Balance);
        // Switched off, no town ever opens one.
        var off=new Balance();off.city.basePoints=400;off.city.tramLevel=CityLevel.SmallTown;off.city.tramsEnabled=false;var quiet=new GameSession(WorldState.New(off),off);
        for(int i=0;i<(int)g.World.tick&&i<60000;i++)quiet.Step();
        Assert(quiet.World.cities.TrueForAll(c=>c.tram==0),"Trams switched off: no line opens");
        Assert(quiet.World.cities.Exists(c=>c.level>=off.city.tramLevel),"…although the towns grew into cities");
        // A tiny town has no room for a line.
        var start=new GameSession(WorldState.New(new Balance()),new Balance());var startLanes=RoadLanes.Build(start.World,start.Network);
        Assert(start.World.cities.TrueForAll(c=>TownTramLine.Plan(start.World,c,startLanes,start.Network)==null),"A day-0 town is too small for a line");
    }
    /// <summary>The line is a simple drivable path on the town's own streets, with clear ends and stops a whole tram fits at.</summary>
    static void CheckTramLine(GameSession g,CityState city,TownTramLine line,RoadLanes lanes)
    {
        Assert(line!=null&&line.Count>=TownTramLine.MinCells,$"{city.name} has a line of at least {TownTramLine.MinCells} cells");
        var streets=new HashSet<int>();foreach(var r in city.roads)streets.Add(r.cell.Key);
        var seen=new HashSet<int>();
        for(int i=0;i<line.Count;i++)
        {
            var c=line.cells[i];
            Assert(streets.Contains(c.Key)&&seen.Add(c.Key),$"Line cell {c} is one of {city.name}'s streets, once");
            if(i>0)Assert(line.cells[i-1].Distance(c)==1&&(lanes.Exits(line.cells[i-1])&1<<Directions.Between(line.cells[i-1],c))!=0,$"The line drives on from {line.cells[i-1]} to {c}");
            Assert(line.Exit(i)!=line.Entry(i),"The line never turns back inside a cell");
        }
        foreach(bool end in new[]{false,true})
        {
            Cell At(int k)=>line.cells[end?line.Count-1-k:k];
            Assert(Directions.Between(At(0),At(1))==Directions.Between(At(1),At(2)),$"{city.name}'s line ends straight");
            for(int k=0;k<TownTramLine.EndCells;k++)Assert(g.Network.At(At(k))==null,"No track where a tram reverses");
        }
        Assert(line.stops.Count>=3&&line.stops[0]==TownTramLine.EndStop&&line.stops[line.stops.Count-1]==line.Count-TownTramLine.EndStop,$"{city.name}'s line has both ends and at least one stop between: {line.stops.Count}");
        for(int k=1;k<line.stops.Count;k++)Assert(line.stops[k]-line.stops[k-1]>=2.8f,$"Stops are spread out: {line.stops[k-1]} → {line.stops[k]}");
        Assert(line.StillValid(city,lanes,g.Network),"A fresh line is valid");
        var again=TownTramLine.Plan(g.World,city,lanes,g.Network);Assert(line.SameAs(again),"The same streets plan the same line");
        Assert(TownTramLine.Plan(g.World,city,lanes,g.Network,c=>line.cells.Contains(c))?.cells.TrueForAll(c=>!line.cells.Contains(c))??true,"Cells another network holds are avoided");
    }
}
