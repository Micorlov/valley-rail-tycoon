using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static int Chebyshev(Cell a,Cell b)=>Math.Max(Math.Abs(a.x-b.x),Math.Abs(a.z-b.z));
    static void CheckFounding()
    {
        // A valley below the founding population stays at its five towns.
        var quiet=New();for(int i=0;i<12000;i++)quiet.Step();
        Assert(quiet.World.producers.Count==22+SkiResorts.Count&&quiet.World.cities.Count==5,"No town is founded before the valley reaches the founding population");
        // The railway draws settlers: with a coal line through open country the best site lies by one of its stations.
        var open=New();Assert(open.Cities.FindTownSite(0,out var wild),"An empty valley has a town site");
        var rail=New();rail.World.money=100000;Coal(rail);Assert(rail.Cities.FindTownSite(0,out var railSite),"A railway valley has a town site");
        int nearStation=int.MaxValue;foreach(var s in rail.World.stations)nearStation=Math.Min(nearStation,Chebyshev(s.cell,railSite));
        Console.WriteLine($"Founding: open valley site {wild}, with a coal line {railSite} ({nearStation} cells from a station)");
        Assert(nearStation<=10&&!railSite.Equals(wild),"Settlers choose a site by the railway once one runs through open land");
        var watch=Stopwatch.StartNew();for(int i=0;i<10;i++)rail.Cities.FindTownSite(i,out _);watch.Stop();
        Console.WriteLine($"Founding: a site search takes {watch.Elapsed.TotalMilliseconds/10:F1} ms");
        // Grown towns found new villages one at a time, never too close to a town or an industry yard.
        var fast=new Balance();fast.city.basePoints=400;var g=new GameSession(WorldState.New(fast),fast);
        var forest=Scenery.Layout(g.World.producers);
        var announced=new List<Notification>();string midway=null;var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailFounding-"+Guid.NewGuid()),new Codec(),fast);
        for(int i=0;i<60000;i++)
        {
            g.Step();
            while(g.Cities.Notifications.Count>0){var n=g.Cities.Notifications.Dequeue();if(n.kind==Notification.CityFounded)announced.Add(n);}
            if(i==30000)midway=saves.CaptureSnapshot(g.World);
        }
        var founded=g.World.cities.FindAll(c=>c.founded>0);
        Assert(founded.Count>=3&&founded.Count<=fast.city.maxFounded&&announced.Count==founded.Count,$"Grown valley founds towns: {founded.Count} founded, {announced.Count} announced");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in g.World.producers)Assert(names.Add(p.name),"Every producer keeps a unique name: "+p.name);
        founded.Sort((a,b)=>a.founded.CompareTo(b.founded));
        for(int k=0;k<founded.Count;k++)
        {
            var town=founded[k];var p=g.Cargo.Producer(town.producerId);
            Console.WriteLine($"Founding: {town.name} at {town.center} on tick {town.founded}, now {CityBalance.LevelNames[(int)town.level]} with {town.population} people");
            Assert(p!=null&&p.kind==ProducerKind.Town&&CitySimulation.Founded(p)&&p.cell.Equals(town.center)&&p.name==town.name,"A founded town is an ordinary Town producer");
            Assert(k==0||town.founded-founded[k-1].founded>=fast.city.foundingInterval,"Towns are founded one interval apart");
            Assert(Connected(town)&&town.buildings.Exists(bs=>bs.def==BuildingCatalog.TownHall),town.name+" has a town hall on one street network");
            Assert(!MapDefinition.Water(town.center)&&!MapDefinition.Raised(town.center),town.name+" stands on dry flat land");
            foreach(var other in g.World.cities)
                if(other!=town)Assert(Chebyshev(other.center,town.center)>=fast.city.foundingSpacing,town.name+" keeps its distance from "+other.name);
            foreach(var q in g.World.producers)
                if(q.kind!=ProducerKind.Town)Assert(Chebyshev(q.cell,town.center)>=7,town.name+" keeps clear of "+q.name);
        }
        Assert(founded.Exists(t=>t.population>120),"Founded villages grow like any town");
        // Each village gets one feeder highway, to the nearest town listed before it, not a highway to every town.
        for(int j=0;j<g.World.cities.Count;j++)
        {
            var town=g.World.cities[j];if(town.founded<=0)continue;
            int earlier=0;foreach(var r in g.World.intercityRoads){if(r.ToBeach||r.ToSki)continue;int other=r.a==town.producerId?r.b:r.b==town.producerId?r.a:0;if(other!=0&&g.World.cities.FindIndex(c=>c.producerId==other)<j)earlier++;}
            Assert(earlier<=1,town.name+" has one feeder highway: "+earlier);
        }
        int highways=g.World.intercityRoads.FindAll(r=>!r.ToBeach&&!r.ToSki).Count;
        Assert(highways<=10+founded.Count,$"Founded towns add one highway each: {highways} highways for {founded.Count} villages");
        // The villages' highways come before the beach roads, which still open (coastal villages may take them).
        var coast=new GameSession(WorldState.New(fast),fast);int beachTicks=0;
        for(;beachTicks<400000&&coast.World.intercityRoads.FindAll(Coast.Open).Count<2;beachTicks++){coast.Step();coast.Cities.Notifications.Clear();}
        Console.WriteLine($"Founding: two beaches open after {beachTicks} ticks with {coast.World.cities.Count-5} founded towns and {coast.World.intercityRoads.Count} roads");
        Assert(coast.World.intercityRoads.FindAll(Coast.Open).Count==2,"Beach roads still open in a valley with founded towns");
        SaveService.Validate(coast.World,fast);
        Assert(announced.TrueForAll(n=>g.World.cities.Exists(c=>c.id==n.cityId)&&(n.value==0||g.World.cities.Exists(c=>c.id==n.value&&c.id!=n.cityId))),"The notice names the new town and its nearest neighbour");
        // The forest never reshuffles when a town is founded: the same pines stand in the same places.
        var after=Scenery.Layout(g.World.producers);Assert(after.Length==forest.Length,"Founding keeps the tree scatter");
        for(int i=0;i<forest.Length;i++)Assert(after[i].cell.Equals(forest[i].cell),"Founding keeps every pine in place");
        // Saves keep founded towns and replay the same foundings.
        SaveService.Validate(g.World,fast);
        var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
        Assert(restored.cities.FindAll(c=>c.founded>0).Count==founded.Count&&restored.producers.Count==g.World.producers.Count,"Founded towns survive a save");
        // The midway save was taken after step 30001 of 60000: replaying the rest founds the same towns on the same ticks.
        var replay=new GameSession(saves.RestoreSnapshot(midway),fast);
        for(int i=30001;i<60000;i++){replay.Step();replay.Cities.Notifications.Clear();}
        Assert(saves.CaptureSnapshot(replay.World)==saves.CaptureSnapshot(g.World),"Foundings replay identically from a save");
        // Tampered founded towns are rejected.
        string json=saves.CaptureSnapshot(g.World);int townId=founded[0].producerId;
        var tampers=new Action<WorldState>[]
        {
            w=>w.producers.Find(p=>p.id==townId).kind=ProducerKind.Mine,
            w=>w.producers.Find(p=>p.id==townId).cell=new Cell(31,40),
            w=>w.producers.Find(p=>p.id==townId).name="",
            w=>w.cities.Find(c=>c.producerId==townId).founded=0,
            w=>w.cities.Find(c=>c.producerId==4).founded=1,
            w=>w.producers.RemoveAll(p=>p.id==1),
        };
        foreach(var tamper in tampers)
        {
            var copy=saves.RestoreSnapshot(json);tamper(copy);
            bool rejected=false;try{SaveService.Validate(copy,fast);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"A tampered founded town is rejected");
        }
        // An older save keeps its smaller producer set for good: it never founds a town, so its saves stay valid.
        var eager=new Balance();eager.city.basePoints=400;eager.city.foundingPopulation=500;
        var legacy=new GameSession(WorldState.New(eager),eager);legacy.World.mapVersion=4;legacy.World.producers.RemoveAll(p=>p.id>13);legacy.World.cities.RemoveAll(c=>c.producerId>13);
        for(int i=0;i<40000;i++)legacy.Step();
        int legacyPopulation=0;foreach(var c in legacy.World.cities)legacyPopulation+=c.population;
        Assert(legacyPopulation>=eager.city.foundingPopulation&&legacy.World.cities.Count==2&&legacy.World.producers.Count==13,$"A version-4 save founds no town ({legacyPopulation} people)");
        SaveService.Validate(legacy.World,eager);new SaveService(Path.GetTempPath(),new Codec(),eager).CaptureSnapshot(legacy.World);
        // After the beaches open, and once every founded Village has a road (a pair with no route can let the beach roads start
        // first), each has exactly one feeder highway.
        bool Unlinked(){for(int j=0;j<coast.World.cities.Count;j++){var town=coast.World.cities[j];if(town.founded<=0||town.level<CityLevel.Village)continue;bool fed=coast.World.intercityRoads.Exists(r=>!r.ToBeach&&!r.ToSki&&(r.a==town.producerId||r.b==town.producerId));if(!fed)return true;}return false;}
        for(int i=0;i<20000&&Unlinked();i++){coast.Step();coast.Cities.Notifications.Clear();}
        for(int j=0;j<coast.World.cities.Count;j++)
        {
            var town=coast.World.cities[j];if(town.founded<=0||town.level<CityLevel.Village)continue;
            int feeders=0;foreach(var r in coast.World.intercityRoads){if(r.ToBeach||r.ToSki)continue;int other=r.a==town.producerId?r.b:r.b==town.producerId?r.a:0;if(other!=0&&coast.World.cities.FindIndex(c=>c.producerId==other)<j)feeders++;}
            Assert(feeders==1,town.name+" has exactly one feeder highway: "+feeders);
        }
        // Switched off, or at the cap, no town is founded.
        var off=new Balance();off.city.basePoints=400;off.city.foundingEnabled=false;var none=new GameSession(WorldState.New(off),off);
        for(int i=0;i<20000;i++)none.Step();
        Assert(none.World.cities.Count==5,"Founding can be switched off");
        var capped=new Balance();capped.city.basePoints=400;capped.city.maxFounded=1;var one=new GameSession(WorldState.New(capped),capped);
        for(int i=0;i<40000;i++)one.Step();
        Assert(one.World.cities.Count==6,"The cap limits founded towns");
    }
    static void CheckBigStationGrowth()
    {
        // The same served town with a halt and with a Grand terminal: the big station doubles its growth rate and lets it
        // put up two more buildings a minute, so it clearly outgrows the other.
        (GameSession g,CityState town) Served(int level)
        {
            var g=New();g.World.money=100000;Track(g,new Cell(14,46),new Cell(50,46));
            int a=OK(g.Stations.Place(new Cell(16,46),4)),z=OK(g.Stations.Place(new Cell(48,46),5));OK(g.Trains.AssignRoute(OK(g.Trains.Buy(a,2,Cargo.Passengers)),a,z));
            foreach(var s in g.World.stations)s.level=level;
            return(g,g.Cities.CityFor(4));
        }
        var halt=Served(0);var central=Served(2);var grand=Served(3);
        int haltRate=halt.g.Cities.Rate(halt.town),centralRate=central.g.Cities.Rate(central.town),grandRate=grand.g.Cities.Rate(grand.town);
        Assert(grand.g.Cities.ServedStationLevel(grand.town)==3&&StationCatalog.GrowthPercent(3)==100&&StationCatalog.ExtraBuildings(3)==2,"A Grand terminal is the top rung");
        Assert(grandRate==(haltRate+StationCatalog.GrowthBonus(3))*2&&centralRate==(haltRate+StationCatalog.GrowthBonus(2))*3/2,$"Big stations speed up the growth rate: halt {haltRate}, central {centralRate}, grand {grandRate} pts/min");
        for(int i=0;i<24000;i++){halt.g.Step();central.g.Step();grand.g.Step();}
        Console.WriteLine($"Big station: after 20 game min Willowbrook has {halt.town.population} people with a halt, {central.town.population} with a central station, {grand.town.population} with a Grand terminal");
        Assert(grand.town.population>halt.town.population*5/4&&central.town.population>halt.town.population,"A town with a big station outgrows one with a halt");
        // A big station no train uses adds nothing.
        var idle=New();idle.World.money=100000;Track(idle,new Cell(14,46),new Cell(50,46));OK(idle.Stations.Place(new Cell(16,46),4));
        foreach(var s in idle.World.stations)s.level=3;
        var plain=New();Assert(idle.Cities.ServedStationLevel(idle.Cities.CityFor(4))==0&&idle.Cities.Rate(idle.Cities.CityFor(4))==plain.Cities.Rate(plain.Cities.CityFor(4)),"An unserved big station does not speed a town up");
    }
}
