using System;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    // Ski resorts at the snowy peaks: fixed sites and ids, a tourist station, roads from the towns, save checks.
    static void CheckSkiResorts()
    {
        var g=Open();
        var resorts=g.World.producers.FindAll(p=>p.kind==ProducerKind.SkiResort);
        Assert(resorts.Count==SkiResorts.Count,"Every snowy peak has a ski resort: "+resorts.Count);
        for(int i=0;i<SkiResorts.Count;i++)
        {
            var peak=SkiResorts.Peaks[i];var r=resorts.Find(p=>p.id==SkiResorts.FirstId+i);
            Assert(r!=null&&r.name==SkiResorts.Name(i)&&SkiResorts.Snowy(i),"Resort "+i+" has its fixed id and name");
            Assert(Math.Abs(MapDefinition.Height(peak.x,peak.z)-peak.height)<.01f,"The peak table matches the terrain: "+peak.name);
            Assert(SkiResorts.Up(r)==0&&r.cell.x==peak.x,"A new map puts the base straight below the south face: "+peak.name+" "+r.cell);
            Assert(MapDefinition.Raised(new Cell(r.cell.x,r.cell.z+SkiResorts.Half+1)),"The base stands right at the foot of the slope: "+peak.name);
            for(int k=0;k<SkiResorts.FootprintCells;k++)
            {
                var c=SkiResorts.FootprintCell(r.cell,k);
                Assert(!MapDefinition.Raised(c)&&MapDefinition.Blocked(c,g.World)&&!g.Build.Placeable(c)&&!g.Scenery.TreeAt(c),"The base is flat, clear of pines and keeps track off: "+c);
            }
            var e=SkiResorts.Entrance(r);
            Assert(!MapDefinition.Blocked(e,g.World)&&!g.Cities.Occupied(e)&&SkiResorts.Distance(r,e)==1,"The car park entrance is open ground just outside the base");
        }
        // Tourists go between towns and resorts both ways, never from one resort to another.
        Assert(MapDefinition.Produces(ProducerKind.SkiResort,Cargo.Passengers)&&MapDefinition.Accepts(ProducerKind.SkiResort,Cargo.Passengers),"A resort sends and takes tourists");
        Assert(CargoTransfer.Delivers(ProducerKind.Town,ProducerKind.SkiResort,Cargo.Passengers)&&CargoTransfer.Delivers(ProducerKind.SkiResort,ProducerKind.Town,Cargo.Passengers),"Town-resort passenger routes pay");
        Assert(!CargoTransfer.Delivers(ProducerKind.SkiResort,ProducerKind.SkiResort,Cargo.Passengers)&&!CargoTransfer.Delivers(ProducerKind.SkiResort,ProducerKind.Mine,Cargo.Coal),"No resort-to-resort or freight routes");
        Assert(StationCatalog.Town(ProducerKind.SkiResort)&&StationCatalog.Name(ProducerKind.SkiResort,3)=="Grand terminal","A resort station climbs the passenger ladder");
        // A station within three cells of the base serves the resort; four cells is too far.
        var granite=resorts.Find(p=>p.id==SkiResorts.FirstId);
        var near=new Cell(granite.cell.x+SkiResorts.Half+SkiResorts.Catchment,granite.cell.z);
        var plan=g.Stations.Plan(near,0,3,1,granite.id);
        Assert(plan.valid&&plan.nearby.Exists(p=>p.id==granite.id),"A station three cells from the base serves the resort: "+plan.reason);
        Assert(!g.Stations.Plan(new Cell(near.x+1,near.z),0,3,1,granite.id).valid,"Four cells from the base is out of reach");
        int resortStation=OK(g.Stations.Place(plan,granite.id));
        Assert(g.Trains.Station(resortStation).name=="Granite Ridge Ski Resort Station","The station takes the resort's name");
        // The AI links the resort to a town and the tourist train earns both ways.
        int train=OK(g.Trains.Buy(resortStation,2,Cargo.Passengers));
        var fix=g.Fixer.Plan(train);
        Assert(fix.valid,"AI rail fix links the resort to a town: "+fix.reason);
        OK(g.Fixer.Apply(train));
        var townStation=g.Trains.Station(g.Trains.Train(train).a==resortStation?g.Trains.Train(train).b:g.Trains.Train(train).a);
        var town=g.Cargo.Producer(townStation.producerId);
        Assert(town.kind==ProducerKind.Town,"The tourist line runs to a town: "+town.name);
        Drain(g);long income=g.World.totalIncome;int toResort=0,toTown=0;
        for(int i=0;i<24000;i++)
        {
            g.Step();
            foreach(var ev in Drain(g))
                if(ev.kind==GameEventKind.CargoDelivered){if(ev.cell.Equals(g.Trains.Station(resortStation).cell))toResort+=ev.aux;else if(ev.cell.Equals(townStation.cell))toTown+=ev.aux;}
        }
        Console.WriteLine($"Ski: {town.name} line carried {toResort} tourists up and {toTown} home, earning ${g.World.totalIncome-income:N0}");
        Assert(toResort>0&&toTown>0&&g.World.totalIncome>income,"Tourists ride to the resort and back home, and the line pays");
        // Save round trip keeps the resorts; a session on the loaded save adds none.
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailSki-"+Guid.NewGuid()),new Codec(),g.Balance);
        string json=saves.CaptureSnapshot(g.World);
        var again=new GameSession(saves.RestoreSnapshot(json),g.Balance);
        Assert(again.World.producers.FindAll(p=>p.kind==ProducerKind.SkiResort).Count==SkiResorts.Count&&again.World.producers.Count==g.World.producers.Count,"Resorts survive a save and are not added twice");
        // Tampered resorts are rejected.
        void Rejects(string what,Action<WorldState> tamper){var copy=saves.RestoreSnapshot(json);tamper(copy);bool rejected=false;try{SaveService.Validate(copy,g.Balance);}catch(InvalidDataException){rejected=true;}Assert(rejected,what+" is rejected");}
        Rejects("A resort moved onto the mountain",w=>w.producers.Find(p=>p.id==SkiResorts.FirstId).cell=new Cell(80,80));
        Rejects("A resort moved to another peak",w=>w.producers.Find(p=>p.id==SkiResorts.FirstId).cell=w.producers.Find(p=>p.id==SkiResorts.FirstId+1).cell);
        Rejects("A resort with an unknown id",w=>w.producers.Find(p=>p.id==SkiResorts.FirstId+3).id=SkiResorts.FirstId+SkiResorts.Count);
        Rejects("A resort on an industry",w=>w.producers.Find(p=>p.id==SkiResorts.FirstId).cell=new Cell(87,57));
        Rejects("Track on a resort base",w=>w.tracks.Add(new TrackPieceState{id=w.nextId++,mask=10,cell=new Cell(granite.cell.x,granite.cell.z-1)}));
        Rejects("A town kind with a resort id",w=>w.producers.Find(p=>p.id==SkiResorts.FirstId).kind=ProducerKind.Town);
        // An older map version keeps its producers: no resorts join a version-4 world.
        var old=New();old.World.mapVersion=4;old.World.producers.RemoveAll(p=>p.id>13);old.World.cities.RemoveAll(c=>c.producerId>13);
        Assert(SkiResorts.Establish(old.World,old.Cities,old.Network)==0,"Version-4 saves get no resorts");
        // Resorts whose ground is taken slide along the face: a town street on the south site moves Granite Ridge.
        var busy=New();var granite2=busy.World.producers.Find(p=>p.id==SkiResorts.FirstId);busy.World.producers.Remove(granite2);
        Track(busy,new Cell(granite2.cell.x-3,granite2.cell.z),new Cell(granite2.cell.x+3,granite2.cell.z));
        Assert(SkiResorts.Establish(busy.World,busy.Cities,busy.Network)==1,"A resort is re-established");
        var moved=busy.World.producers.Find(p=>p.id==SkiResorts.FirstId);
        Assert(!moved.cell.Equals(granite2.cell)&&SkiResorts.PeakOfSite(moved.cell)==0,"Track on the south site moves the base: "+moved.cell);
        for(int k=0;k<SkiResorts.FootprintCells;k++)Assert(busy.Network.At(SkiResorts.FootprintCell(moved.cell,k))==null,"The moved base stands clear of the track");
        SaveService.Validate(busy.World,busy.Balance);
        // Grown towns build a road to every resort once they are linked, and the roads end at the car park entrance.
        var fast=new Balance();fast.city.basePoints=400;fast.city.foundingEnabled=false;var grown=new GameSession(WorldState.New(fast),fast);
        int roadTicks=0;
        for(;roadTicks<400000&&grown.World.intercityRoads.FindAll(r=>r.ToSki&&r.Complete).Count<SkiResorts.Count;roadTicks++){grown.Step();grown.Cities.Notifications.Clear();}
        Console.WriteLine($"Ski: all {SkiResorts.Count} resort roads open after {roadTicks} ticks ({grown.World.intercityRoads.Count} roads)");
        foreach(var r in grown.World.producers.FindAll(p=>p.kind==ProducerKind.SkiResort))
        {
            var road=grown.Cities.SkiRoadOf(r.id);
            Assert(road!=null&&road.Complete&&road.path[road.path.Count-1].Equals(SkiResorts.Entrance(r)),r.name+" has a finished road to its entrance");
            Assert(grown.Cities.CityFor(road.a)!=null&&!Roadside.Planned(road),"A ski road leaves from a town and gets no service area");
        }
        var grownJson=saves.CaptureSnapshot(grown.World);
        Rejects2(saves,grownJson,g.Balance,"A ski road that stops short of the resort",w=>{var road=w.intercityRoads.Find(r=>r.ToSki);road.path.RemoveAt(road.path.Count-1);road.built=road.path.Count;});
    }
    static void Rejects2(SaveService saves,string json,Balance b,string what,Action<WorldState> tamper){var copy=saves.RestoreSnapshot(json);tamper(copy);bool rejected=false;try{SaveService.Validate(copy,b);}catch(InvalidDataException){rejected=true;}Assert(rejected,what+" is rejected");}
}
