using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ValleyRail.Core;
class Codec:ISnapshotCodec
{static JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true};public string Encode<T>(T v)=>JsonSerializer.Serialize(v,options);public T Decode<T>(string s)=>JsonSerializer.Deserialize<T>(s,options);}
partial class Program
{
    static int checks;
    static void Assert(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static int OK(Result r){Assert(r.ok,r.message);return r.id;}
    static GameSession New()=>new GameSession(WorldState.New(new Balance()),new Balance());
    static void Track(GameSession g,Cell a,Cell b){var p=g.Build.Preview(a,b);Assert(p.valid,p.reason);OK(g.Build.CommitBuild(p));}
    static (int a,int b,int train) Coal(GameSession g)
    {Track(g,new Cell(8,15),new Cell(50,15));int a=OK(g.Stations.Place(new Cell(10,15),1)),b=OK(g.Stations.Place(new Cell(48,15),2));int t=OK(g.Trains.Buy(a,0,Cargo.Coal));OK(g.Trains.AssignRoute(t,a,b));return(a,b,t);}
    static void Main()
    {
        try{Run();Console.WriteLine($"PASS: {checks} assertions");}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
    }
    static void CheckTrainCatalog()
    {
        for(int model=0;model<TrainCatalog.Count;model++)
        foreach(Cargo cargo in new[]{Cargo.Coal,Cargo.Goods,Cargo.Passengers})
        {
            var game=New(); game.World.money=200000;
            int a,b;
            if(cargo==Cargo.Passengers)
            {
                Track(game,new Cell(14,46),new Cell(50,46));
                a=OK(game.Stations.Place(new Cell(16,46),4)); b=OK(game.Stations.Place(new Cell(48,46),5));
            }
            else if(cargo==Cargo.Goods)
            {
                var cells=new List<Cell>();for(int z=31;z<=40;z++)cells.Add(new Cell(12,z));for(int x=13;x<=18;x++)cells.Add(new Cell(x,40));
                OK(game.Build.CommitBuild(game.Build.ValidateBuild(cells)));
                a=OK(game.Stations.Place(new Cell(12,32),3)); b=OK(game.Stations.Place(new Cell(16,40),4));
            }
            else
            {
                Track(game,new Cell(8,15),new Cell(50,15));
                a=OK(game.Stations.Place(new Cell(10,15),1)); b=OK(game.Stations.Place(new Cell(48,15),2));
            }
            int before=game.World.money;
            var purchase=game.Trains.Buy(a,model,cargo);
            bool expected=(model==2||model==4||model==5)==(cargo==Cargo.Passengers);
            Assert(purchase.ok==expected,"Cargo compatibility: "+model+" / "+cargo);
            if(!expected){Assert(game.World.money==before,"Rejected purchase does not charge");continue;}
            Assert(game.World.money==before-game.Balance.trainPrice[model],"Model purchase price");
            var train=game.Trains.Train(purchase.id);
            game.Cargo.Producer(game.Trains.Station(a).producerId).inventory=200;
            OK(game.Trains.AutoDestination(train.id));
            Assert(train.a==a&&train.b==b,"Auto destination chooses compatible stop for every model and cargo");
            Assert(train.units==game.Balance.capacity[model],"Model loads its full capacity");
            var saves=new SaveService(Path.GetTempPath(),new Codec(),game.Balance);
            var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(game.World));
            Assert(restored.trains[0].model==model&&restored.trains[0].units==train.units,"Model save round trip");
            long expensesBefore=game.World.totalExpenses;
            for(int i=0;i<1200;i++)game.Step();
            Assert(game.World.delivered>=game.Balance.capacity[model],"Model completes delivery");
            Assert(game.World.totalExpenses-expensesBefore==game.Balance.runningCost[model],"Model running cost");
            SaveService.Validate(game.World,game.Balance);
            before=game.World.money;OK(game.Trains.Sell(train.id));
            Assert(game.World.money==before+game.Balance.trainPrice[model]/2,"Model sale refund");
            Assert(!game.Trains.Buy(a,-1,cargo).ok&&!game.Trains.Buy(a,TrainCatalog.Count,cargo).ok&&!game.Trains.Buy(a,model,(Cargo)99).ok,"Invalid model and cargo rejected");
        }
    }
    static void CheckAutoDestination()
    {
        var g=New();g.World.money=100000;
        Track(g,new Cell(8,15),new Cell(50,15));
        int a=OK(g.Stations.Place(new Cell(10,15),1));
        int trainId=OK(g.Trains.Buy(a,0,Cargo.Coal));
        var train=g.Trains.Train(trainId);
        string before=new Codec().Encode(g.World);
        Assert(!g.Trains.AutoDestination(trainId).ok,"Auto rejects missing destination");
        Assert(new Codec().Encode(g.World)==before,"Failed auto selection has no side effects");
        Assert(!g.Trains.AutoDestination(-1).ok,"Auto rejects missing train");
        Track(g,new Cell(8,9),new Cell(14,9));OK(g.Stations.Place(new Cell(10,9),1));
        Track(g,new Cell(45,9),new Cell(51,9));OK(g.Stations.Place(new Cell(48,9),2));
        Assert(!g.Trains.AutoDestination(trainId).ok,"Auto skips same producer and disconnected compatible stations");
        int destination=OK(g.Stations.Place(new Cell(48,15),2));
        // A second plant on this railway gives auto selection two valid destinations.
        int plant=g.World.nextId++;
        g.World.producers.Add(new ProducerState{id=plant,kind=ProducerKind.Plant,cell=new Cell(40,12),name="Nearby Plant"});
        int nearer=OK(g.Stations.Place(new Cell(40,15),plant));
        OK(g.Trains.AutoDestination(trainId));
        Assert(train.destination==nearer&&train.destination!=destination,"Auto selects shortest rail distance rather than first station");
        before=new Codec().Encode(g.World);
        Assert(!g.Trains.AutoDestination(trainId).ok,"Auto refuses route change during loading");
        Assert(new Codec().Encode(g.World)==before,"Rejected route change preserves cargo and route");
        train.state=ServiceState.Parked;
        Assert(train.units>0&&!g.Trains.AutoDestination(trainId).ok,"Auto refuses to discard undelivered cargo");
        OK(g.Trains.Sell(trainId));
        trainId=OK(g.Trains.Buy(destination,0,Cargo.Coal));
        OK(g.Trains.AutoDestination(trainId));
        Assert(g.Trains.Train(trainId).destination==a,"Auto can start at receiving station and find producer");
    }
    static void CheckExpandedMap()
    {
        var keys=new HashSet<int>();
        for(int z=0;z<MapDefinition.Size;z++)for(int x=0;x<MapDefinition.Size;x++)
        {
            var c=new Cell(x,z);
            Assert(keys.Add(c.Key)&&Cell.FromKey(c.Key).Equals(c),"Expanded cell keys are unique and reversible");
        }
        Assert(keys.Count==16384,"Map has four times the original tile area");
        Assert(MapDefinition.InBounds(new Cell(127,127))&&!MapDefinition.InBounds(new Cell(128,0)),"New map edges");
        var g=New();g.World.money=300000;
        Track(g,new Cell(60,20),new Cell(120,20));
        Assert(g.Network.At(new Cell(90,20))!=null&&g.Network.At(new Cell(26,21))==null,"Tracks beyond x=63 do not alias old cells");
        foreach(int row in new[]{78,110})
        {
            Track(g,new Cell(28,row),new Cell(34,row));
            Assert(g.Network.At(new Cell(31,row)).bridge==row,"Northern river crossing is buildable");
        }
        Assert(!g.Build.Preview(new Cell(80,82),new Cell(65,82)).valid,"Raised mountain blocks construction");
        Assert(!g.Build.Preview(new Cell(54,76),new Cell(64,76)).valid,"Lake blocks construction");
        var detour=g.Build.Preview(new Cell(64,82),new Cell(96,82));
        Assert(detour.valid,"Railway can route around mountain foothills");
        OK(g.Build.CommitBuild(detour));
        foreach(var t in g.World.tracks)Assert(!MapDefinition.Raised(t.cell),"No track sinks into raised terrain");
        foreach(var p in g.World.producers.FindAll(p=>p.id>=14&&p.kind!=ProducerKind.SkiResort))
        {
            var test=New();var c=new Cell(p.cell.x,p.cell.z+3);
            Track(test,c.Move(3),c.Move(1));OK(test.Stations.Place(c,p.id));
        }
        var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
        var loaded=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
        Assert(loaded.cities.Count==5&&loaded.producers.Count==22+SkiResorts.Count&&loaded.tracks.Count==g.World.tracks.Count,"Expanded map and railway survive save/load");
        var old=New();old.World.mapVersion=4;old.World.producers.RemoveAll(p=>p.id>13);old.World.cities.RemoveAll(c=>c.producerId>13);
        Coal(old);
        var legacySave=new SaveService(Path.GetTempPath(),new Codec(),old.Balance);
        var restored=new GameSession(legacySave.RestoreSnapshot(legacySave.CaptureSnapshot(old.World)),old.Balance);
        for(int i=0;i<1600;i++)restored.Step();
        Assert(restored.World.producers.Count==13&&restored.World.delivered>0,"Original industry-map save retains layout and working railway");
        Assert(MapDefinition.Surface(new Cell(100,30))==2&&MapDefinition.Surface(new Cell(15,80))==1&&MapDefinition.Surface(new Cell(56,112))==5,"Desert, woodland and snowy peaks are distinct");
    }
    static void CheckIndustries()
    {
        foreach(var pair in new[]{(8,9,Cargo.Wood),(10,11,Cargo.Oil),(12,13,Cargo.IronOre)})
        {
            var g=New();var source=g.Cargo.Producer(pair.Item1);var target=g.Cargo.Producer(pair.Item2);
            for(int i=0;i<1200;i++)g.Cargo.Step();
            Assert(source.inventory==72,"Raw materials replenish");
            Assert(target.inventory==0,"Processing requires input deliveries");
            // Every new industry can be served by a real station on the map.
            foreach(var p in new[]{source,target})
            {
                var c=new Cell(p.cell.x,p.cell.z+3);
                Track(g,c.Move(3),c.Move(1));
                OK(g.Stations.Place(c,p.id));
            }
            var a=g.World.stations[0];var b=g.World.stations[1];
            var t=g.Trains.Train(OK(g.Trains.Buy(a.id,0,pair.Item3)));
            Assert(!TrainCatalog.SupportsCargo(2,pair.Item3),"Passenger trains reject bulk cargo");
            g.Cargo.Service(t,a,b);Assert(t.units==30,"New cargo loads at its source");
            int money=g.World.money;g.Cargo.Service(t,b,a);
            Assert(t.units==0&&target.inventory==30&&g.World.money>money,"Delivered input earns revenue and creates output");
            g.Cargo.Service(t,b,a);Assert(target.inventory==30,"Delivery cannot be counted twice");
            var output=IndustryCatalog.Output(target.kind).Value;
            var receiver=g.Cargo.Producer(output==Cargo.Steel?3:4);
            var onward=new TrainState{model=0,cargo=output};
            var destination=new StationState{producerId=receiver.id};
            g.Cargo.Service(onward,b,destination);Assert(onward.units==30&&target.inventory==0,"Processed output can be collected");
            long deliveries=g.World.delivered;g.Cargo.Service(onward,destination,b);
            Assert(onward.units==0&&g.World.delivered==deliveries+30,"Output reaches factory or town");
            g.Cargo.Receive(target,pair.Item3,1000);Assert(target.inventory==g.Balance.storage,"Processor storage is capped");
            g.Cargo.Receive(target,Cargo.Passengers,10);Assert(target.inventory==g.Balance.storage,"Wrong input is rejected");
            var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
            var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
            Assert(restored.producers.Find(p=>p.id==target.id).inventory==200,"Processed inventory survives save/load");
        }
        var rail=New();Track(rail,new Cell(7,56),new Cell(22,56));
        int start=OK(rail.Stations.Place(new Cell(8,56),8)),end=OK(rail.Stations.Place(new Cell(21,56),9));
        int train=OK(rail.Trains.Buy(start,0,Cargo.Wood));OK(rail.Trains.AutoDestination(train));
        for(int i=0;i<1400;i++)rail.Step();
        Assert(rail.World.delivered>=30&&rail.Cargo.Producer(9).inventory>=30,"Timber railway runs and processes real deliveries");
        var old=New();old.World.mapVersion=3;old.World.producers.RemoveAll(p=>p.id>7);old.World.cities.RemoveAll(c=>c.producerId>7);
        var service=new SaveService(Path.GetTempPath(),new Codec(),old.Balance);
        Assert(service.RestoreSnapshot(service.CaptureSnapshot(old.World)).producers.Count==7,"Version 3 retains original industry layout");
        Assert(!MapDefinition.Accepts(ProducerKind.Town,Cargo.Oil),"Towns reject raw materials");
    }
    static void CheckPowerStations()
    {
        foreach(int plantId in new[]{6,7})
        {
            var g=New(); var plant=g.Cargo.Producer(plantId);
            Assert(g.World.producers.FindAll(p=>p.kind==ProducerKind.Plant).Count==3,"Three power stations on new maps");
            Assert(MapDefinition.Blocked(plant.cell,g.World),"Power station footprint blocks track");
            int z=plant.cell.z-3;
            Track(g,new Cell(8,15),new Cell(plant.cell.x,15));
            Track(g,new Cell(plant.cell.x,15),new Cell(plant.cell.x,z+1));
            int a=OK(g.Stations.Place(new Cell(10,15),1));
            int b=OK(g.Stations.Place(new Cell(plant.cell.x,z),plantId));
            int t=OK(g.Trains.Buy(a,0,Cargo.Coal)); OK(g.Trains.AssignRoute(t,a,b));
            for(int i=0;i<1800;i++)g.Step();
            Assert(g.World.delivered>=30,"New power station receives coal");
            var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
            var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
            Assert(restored.mapVersion==5&&restored.producers.Count==22+SkiResorts.Count,"Expanded map round trips through save");
        }
        var old=New();old.World.mapVersion=2;old.World.producers.RemoveAll(p=>p.id>5);old.World.cities.RemoveAll(c=>c.producerId>5);
        Track(old,new Cell(18,23),new Cell(22,23));
        var oldSaves=new SaveService(Path.GetTempPath(),new Codec(),old.Balance);
        var loaded=oldSaves.RestoreSnapshot(oldSaves.CaptureSnapshot(old.World));
        Assert(loaded.mapVersion==2&&loaded.producers.Count==5&&loaded.tracks.Count==5,"Legacy railway at new plant site survives loading");
    }
    static void CheckCrossingSignals()
    {
        var g=New();
        Track(g,new Cell(8,15),new Cell(50,15));
        int before=g.World.money;
        Track(g,new Cell(25,13),new Cell(25,17));
        var crossing=g.Network.At(new Cell(25,15));
        Assert(crossing.mask==15&&before-g.World.money==700,"Crossing builds with signal-inclusive price");
        Assert(Directions.Allows(15,0,2)&&Directions.Allows(15,1,3)&&Directions.Allows(15,0,1)&&Directions.Allows(15,3,0)&&!Directions.Allows(15,2,2),"Crossings run straight across and turn onto the other line");
        var turn=g.Pathfinder.FindPath(new Cell(24,15),new Cell(25,17));
        Assert(turn!=null&&turn.Exists(step=>step.trackId==crossing.id&&step.entry==3&&step.exit==0),"Pathfinder turns at a crossing");
        int a=OK(g.Stations.Place(new Cell(10,15),1)),b=OK(g.Stations.Place(new Cell(48,15),2));
        int id=OK(g.Trains.Buy(a,0,Cargo.Coal));OK(g.Trains.AssignRoute(id,a,b));
        var train=g.Trains.Train(id);int index=train.path.FindIndex(step=>step.trackId==crossing.id);
        train.state=ServiceState.Travelling;train.step=index-1;train.distance=train.path[train.step].length;g.World.tick=0;
        Assert(CrossingSignals.IsGreen(g.World,crossing.id,3)&&!CrossingSignals.IsGreen(g.World,crossing.id,0),"Lights turn green for the approaching train");
        // Tick 0 is the idle timer's north-south phase. The player's lone train still stopped here on every trip.
        g.Step();Assert(train.step==index,"A lone train runs straight through the crossing without waiting");
        var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
        var replay=new GameSession(saves.RestoreSnapshot(saves.CaptureSnapshot(g.World)),g.Balance);
        for(int i=0;i<130;i++){g.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(g.World)==saves.CaptureSnapshot(replay.World),"Crossing run is deterministic after save/load");
        train.step=index;train.distance=100;g.World.tick=0;
        Assert(CrossingSignals.IsGreen(g.World,crossing.id,3)&&!CrossingSignals.IsGreen(g.World,crossing.id,0),"Occupied crossing holds green and stops conflicting axis");
        train.step=index+1;train.distance=100;
        Assert(CrossingSignals.IsGreen(g.World,crossing.id,3),"Signals protect trailing wagons");
        Assert(CrossingSignals.MayEnter(g.World,crossing.id,0,train.id),"A train's own trailing wagons never hold it at red");
        // A second train whose wagons cover the crossing north-south holds the east-west train until it clears.
        var other=new TrainState{id=g.World.nextId++,state=ServiceState.Parked,path=new List<RailStep>{new RailStep{trackId=crossing.id,entry=0,exit=2,length=1000}},distance=100};
        Assert(!CrossingSignals.MayEnter(g.World,crossing.id,0,other.id)&&CrossingSignals.MayEnter(g.World,crossing.id,3,other.id),"Occupied crossing still holds other trains on the conflicting axis");
        g.World.trains.Add(other);
        train.state=ServiceState.Travelling;train.step=index-1;train.distance=train.path[train.step].length;
        g.Step();Assert(train.step==index-1&&!CrossingSignals.IsGreen(g.World,crossing.id,3),"Train waits while another train covers the crossing");
        g.World.trains.Remove(other);
        g.Step();Assert(train.step==index,"Train enters once the crossing clears");
        for(int i=0;i<1500;i++)g.Step();
        Assert(g.World.delivered>=30,"Coal service delivers through signalled crossing");
        Assert(!g.Build.Bulldoze(crossing.cell).ok,"Active crossing remains route-protected");
    }
    // In a grid of crossings a train turns onto a side line around a crossing another train holds, instead of waiting at red.
    static void CheckCrossingTurns()
    {
        var g=New();
        Track(g,new Cell(8,15),new Cell(50,15));
        for(int x=25;x<=27;x++)Track(g,new Cell(x,14),new Cell(x,16));
        Track(g,new Cell(25,16),new Cell(27,16));
        TrackPieceState At(int x,int z)=>g.Network.At(new Cell(x,z));
        var held=At(26,15);
        Assert(held.mask==15&&At(25,15).mask==15&&At(27,15).mask==15&&At(26,16).mask==15&&At(25,16).mask==7&&At(27,16).mask==13,"Cross lines and a siding lay a grid of crossings");
        int a=OK(g.Stations.Place(new Cell(10,15),1)),b=OK(g.Stations.Place(new Cell(48,15),2));
        int id=OK(g.Trains.Buy(a,0,Cargo.Coal));OK(g.Trains.AssignRoute(id,a,b));
        var train=g.Trains.Train(id);
        Assert(train.path.Exists(s=>s.trackId==held.id&&s.entry==3&&s.exit==1),"The shortest route runs straight along the main line");
        // Another train's wagons stand across the middle crossing north-south.
        var other=new TrainState{id=g.World.nextId++,state=ServiceState.Parked,path=new List<RailStep>{new RailStep{trackId=held.id,entry=0,exit=2,length=1000}},distance=100};
        g.World.trains.Add(other);
        int next=train.path.FindIndex(s=>s.trackId==At(23,15).id);
        train.state=ServiceState.Travelling;train.step=next-1;train.distance=train.path[train.step].length;
        g.Step();
        Assert(train.step==next&&!train.path.Exists(s=>s.trackId==held.id)&&train.path.Exists(s=>s.trackId==At(26,16).id),"A train turns aside onto the siding around a held crossing");
        Assert(train.path[train.path.Count-1].trackId==train.returnPath[0].trackId,"The detour keeps the platform the way back starts from");
        g.World.trains.Remove(other);
        var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
        Assert(saves.RestoreSnapshot(saves.CaptureSnapshot(g.World)).trains.Find(t=>t.id==id).path.Count==train.path.Count,"The detour is a valid route after save and load");
        g.World.trains.Add(other);
        int bend=At(27,15).id;
        for(int i=0;i<2000&&train.path[train.step].trackId!=bend;i++)g.Step();
        var back=train.path[train.step];
        Assert(back.trackId==bend&&back.entry==0&&back.exit==1,"It turns back onto the main line past the held crossing without waiting");
        // A turn needs the whole crossing: it holds even a straight run along the line it came in on.
        var turner=new TrainState{id=g.World.nextId++,state=ServiceState.Parked,path=new List<RailStep>{new RailStep{trackId=held.id,entry=3,exit=0,length=785}},distance=100};
        g.World.trains.Remove(other);g.World.trains.Add(turner);
        Assert(!CrossingSignals.MayEnter(g.World,held.id,3,train.id)&&!CrossingSignals.MayEnter(g.World,held.id,1,train.id)&&!CrossingSignals.MayEnter(g.World,held.id,0,2,train.id)&&!CrossingSignals.MayEnter(g.World,held.id,2,1,train.id),"A turning train holds every other move through the crossing");
        Assert(CrossingSignals.IsGreen(g.World,held.id,3)&&!CrossingSignals.IsGreen(g.World,held.id,1)&&!CrossingSignals.IsGreen(g.World,held.id,0)&&!CrossingSignals.IsGreen(g.World,held.id,2),"Only the turning train's own signal shows green");
        g.World.trains.Remove(turner);
        // A train loading at a platform still has its wagons on the way it came in, here across the crossing behind it.
        var loader=new TrainState{id=g.World.nextId++,state=ServiceState.Loading,
            path=new List<RailStep>{new RailStep{trackId=At(26,14).id,entry=2,exit=0,length=500,fromCenter=true}},
            returnPath=new List<RailStep>{new RailStep{trackId=held.id,entry=0,exit=2,length=1000},new RailStep{trackId=At(26,14).id,entry=0,exit=2,length=500,toCenter=true}}};
        g.World.trains.Add(loader);
        Assert(!CrossingSignals.MayEnter(g.World,held.id,3,train.id)&&CrossingSignals.Covered(g.World,g.Network,train.id).Contains(held.id),"Wagons of a train loading beyond a crossing still hold it");
        g.World.trains.Remove(loader);
        for(int i=0;i<1500;i++)g.Step();
        Assert(g.World.delivered>=30,"Coal service keeps delivering after the detour");
    }
    static List<GameEvent> Drain(GameSession g){var list=new List<GameEvent>();while(g.Events.TryDequeue(out var e))list.Add(e);return list;}
    static void CheckGameEvents()
    {
        var g=New();
        Assert(!g.Stations.Place(new Cell(10,15),1).ok&&g.Events.Count==0,"Rejected station pushes nothing");
        Track(g,new Cell(8,15),new Cell(50,15));
        var built=Drain(g);Assert(built.Count==1&&built[0].kind==GameEventKind.TrackBuilt&&built[0].value>0,"Track build pushes one event with its cost");
        Assert(built[0].cell.Equals(new Cell(50,15))&&built[0].aux==new Cell(8,15).Key,"Track build event marks both ends of the line");
        Track(g,new Cell(8,15),new Cell(50,15));Assert(g.Events.Count==0,"Re-laying existing track is not an event");
        int a=OK(g.Stations.Place(new Cell(10,15),1)),b=OK(g.Stations.Place(new Cell(48,15),2));
        var stations=Drain(g);Assert(stations.Count==2&&stations[0].kind==GameEventKind.StationBuilt&&stations[0].id==a&&stations[0].cell.Equals(new Cell(10,15))&&stations[1].id==b,"Station events carry id and cell");
        Assert(!g.Trains.Buy(999,0,Cargo.Coal).ok&&g.Events.Count==0,"Rejected purchase pushes nothing");
        int t=OK(g.Trains.Buy(a,0,Cargo.Coal));
        var bought=Drain(g);Assert(bought.Count==1&&bought[0].kind==GameEventKind.TrainPurchased&&bought[0].id==t&&bought[0].cell.Equals(new Cell(10,15)),"Purchase event at the station");
        OK(g.Trains.AssignRoute(t,a,b));
        var route=Drain(g);Assert(route.Count==1&&route[0].kind==GameEventKind.RouteCreated&&route[0].id==t,"Assigning a route loads cargo without a delivery event");
        var seen=new List<GameEvent>();long income=g.World.totalIncome;
        for(int i=0;i<2400&&!seen.Exists(e=>e.kind==GameEventKind.CargoDelivered);i++){g.Step();seen.AddRange(Drain(g));}
        int departed=seen.FindIndex(e=>e.kind==GameEventKind.TrainDeparted),arrived=seen.FindIndex(e=>e.kind==GameEventKind.TrainArrived),delivered=seen.FindIndex(e=>e.kind==GameEventKind.CargoDelivered);
        Assert(departed>=0&&departed<arrived&&arrived<delivered,"Depart, arrive, then deliver");
        Assert(seen[departed].cell.Equals(new Cell(10,15))&&seen[arrived].cell.Equals(new Cell(48,15))&&seen[delivered].cell.Equals(new Cell(48,15)),"Departure and arrival events name their stations");
        Assert(seen[delivered].id==t&&seen[delivered].aux==30&&seen[delivered].value==g.World.totalIncome-income,"Delivery event reports units and the revenue credited");
        // A return-to-station stop unloads through the same helper and reports it too.
        var r=New();var rr=Coal(r);Drain(r);OK(r.Trains.ReturnToStation(rr.train));income=r.World.totalIncome;var back=new List<GameEvent>();
        for(int i=0;i<2400&&r.Trains.Train(rr.train).state!=ServiceState.Parked;i++){r.Step();back.AddRange(Drain(r));}
        var unload=back.FindAll(e=>e.kind==GameEventKind.CargoDelivered);
        Assert(unload.Count==1&&unload[0].value>0&&unload[0].value==r.World.totalIncome-income,"Return-to-station delivery reported once");
        var s=New();Track(s,new Cell(8,15),new Cell(20,15));int st=OK(s.Stations.Place(new Cell(10,15),1));Drain(s);
        OK(s.Build.Bulldoze(new Cell(10,15)));var gone=Drain(s);Assert(gone.Count==1&&gone[0].kind==GameEventKind.Bulldozed&&gone[0].aux==1&&gone[0].id==st,"Station removal event");
        OK(s.Build.Bulldoze(new Cell(15,15)));gone=Drain(s);Assert(gone.Count==1&&gone[0].aux==0&&gone[0].cell.Equals(new Cell(15,15)),"Track removal event");
        var q=new GameEvents();for(int i=1;i<=100;i++)q.Push(GameEventKind.TrainArrived,i,default);
        Assert(q.Count==GameEvents.Capacity&&q.TryDequeue(out var oldest)&&oldest.id==37,"A full queue keeps the newest events");
        q.Clear();Assert(q.Count==0&&!q.TryDequeue(out _),"Cleared queue is empty");
    }
    static bool Connected(CityState city)
    {
        var streets=new HashSet<int>();foreach(var r in city.roads)streets.Add(r.cell.Key);
        var reached=new HashSet<int>{city.center.Key};var queue=new Queue<Cell>();queue.Enqueue(city.center);
        while(queue.Count>0){var c=queue.Dequeue();for(int d=0;d<4;d++){var n=c.Move(d);if(streets.Contains(n.Key)&&reached.Add(n.Key))queue.Enqueue(n);}}
        return reached.Count==streets.Count;
    }
    static bool FacesStreet(CityState city,BuildingState bs)
    {
        for(int i=0;i<CityLayout.FootprintCells(bs);i++)for(int d=0;d<4;d++){var n=CityLayout.FootprintCell(bs,i).Move(d);if(city.roads.Exists(r=>r.cell.Equals(n)))return true;}
        return false;
    }
    static void CheckCityLayout()
    {
        // The catalog offers at least ten homes and ten municipal buildings; the original eight keep their saved indices.
        int homes=0,civics=0;var keys=new HashSet<string>();
        for(int i=0;i<BuildingCatalog.Defaults.Length;i++){var d=BuildingCatalog.Get(i);Assert(keys.Add(d.key)&&!string.IsNullOrEmpty(d.name),"Building keys are unique and named");if(d.category==BuildingCategory.Residential)homes++;if(d.category==BuildingCategory.Civic)civics++;}
        var landmarkSizes=new HashSet<int>();foreach(int def in BuildingCatalog.CivicOrder)if(BuildingCatalog.Size(def)>1)landmarkSizes.Add(BuildingCatalog.Size(def));
        Assert(homes>=10&&BuildingCatalog.CivicOrder.Length>=10&&landmarkSizes.Count>=4&&landmarkSizes.Contains(8)&&BuildingCatalog.MaxSize==8,$"Catalog offers {homes} homes, {BuildingCatalog.CivicOrder.Length} municipal buildings and landmarks in {landmarkSizes.Count} sizes up to 8×8");
        string[] original={"house","large-house","apartments","large-apartments","shop","store","office","large-commercial"};
        for(int i=0;i<original.Length;i++)Assert(BuildingCatalog.Get(i).key==original[i]&&BuildingCatalog.Index(i<4?BuildingCategory.Residential:BuildingCategory.Commercial,i%4+1)==i,"Saved building indices never move");
        for(int level=1;level<=4;level++)Assert(BuildingCatalog.Variants(BuildingCategory.Residential,level).Length>=(level<4?3:2),"Every home level has variants");
        // Day 0: the plaza on a main street, the town hall and five different homes.
        var day0=New();var town=day0.Cities.CityFor(4);var styles=new HashSet<BuildingStyle>();
        foreach(var bs in town.buildings)if(BuildingCatalog.Get(bs.def).category==BuildingCategory.Residential)styles.Add(BuildingCatalog.Get(bs.def).style);
        Assert(town.buildings.Count==6&&town.roads.Count==3&&styles.Count==5&&CitySimulation.CivicCount(town)==1&&Connected(town),"Day-0 town: main street, town hall and five kinds of home");
        // Served growth keeps one connected street network per town, every building faces a street, services unlock once each.
        var grow=New();grow.World.money=100000;Track(grow,new Cell(14,46),new Cell(50,46));int ga=OK(grow.Stations.Place(new Cell(16,46),4)),gz=OK(grow.Stations.Place(new Cell(48,46),5));OK(grow.Trains.AssignRoute(OK(grow.Trains.Buy(ga,2,Cargo.Passengers)),ga,gz));
        for(int i=0;i<60000;i++)grow.Step();
        SaveService.Validate(grow.World,grow.Balance);
        foreach(var city in grow.World.cities)
        {
            Assert(Connected(city),city.name+" streets form one network around the plaza");
            foreach(var bs in city.buildings)Assert(FacesStreet(city,bs),city.name+" building at "+bs.cell+" faces a street");
            var civicKeys=new HashSet<string>();foreach(var bs in city.buildings)if(BuildingCatalog.IsCivic(bs.def))Assert(civicKeys.Add(BuildingCatalog.Get(bs.def).key),"Each municipal building appears once per town");
        }
        var willow=grow.Cities.CityFor(4);int mask=CitySimulation.CivicMask(willow);
        Assert(willow.level>=CityLevel.Village&&(mask&0b111)==0b111,"A Village gains a chapel and a school beside its town hall");
        Assert(grow.Cities.Rate(willow)>=grow.Balance.city.basePoints+3*CitySimulation.CivicCount(willow),"Municipal buildings add growth points");
        Assert(grow.World.intercityRoads.Count>0,"Grown towns open highways");
        // Towns that already have highways keep growing without allocating, apart from the path of any new highway.
        int highwaysBefore=grow.World.intercityRoads.Count;long grownBytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<6000;i++)grow.Step();
        grownBytes=GC.GetAllocatedBytesForCurrentThread()-grownBytes;long newPaths=0;
        for(int i=highwaysBefore;i<grow.World.intercityRoads.Count;i++)newPaths+=16L*grow.World.intercityRoads[i].path.Count+256;
        Assert(grownBytes<1024+newPaths,"Growing towns with highways do not allocate per tick: "+grownBytes);
        foreach(var road in grow.World.intercityRoads)
        {
            if(road.ToBeach||road.ToSki)continue; // a beach road ends at its car park by the sea (see Coast), a ski road at its resort
            var a=grow.Cities.CityFor(road.a);var b=grow.Cities.CityFor(road.b);
            Assert(a.roads.Exists(r=>r.cell.Equals(road.path[0]))&&b.roads.Exists(r=>r.cell.Equals(road.path[road.path.Count-1])),"Highways start and end on town streets");
        }
        // A straight railway across the main-street line becomes a level crossing and the street carries on beyond it.
        var fast=new Balance();fast.city.basePoints=400;var cross=new GameSession(WorldState.New(fast),fast);Track(cross,new Cell(13,38),new Cell(13,48));
        for(int i=0;i<40000;i++)cross.Step();
        var crossing=cross.Cities.CityFor(4);
        Assert(crossing.roads.Exists(r=>r.cell.Equals(new Cell(13,43)))&&cross.Network.At(new Cell(13,43))!=null&&crossing.roads.Exists(r=>r.cell.Equals(new Cell(12,43)))&&Connected(crossing),"Streets cross the railway at right angles");
        SaveService.Validate(cross.World,cross.Balance);
        // Saves from before the street grid: the enclosed ring and a loose street fragment are rebuilt once, connected.
        var codec=new Codec();var old=New();var oldTown=old.Cities.CityFor(4);var c0=oldTown.center;
        oldTown.buildings.Clear();oldTown.roads.Clear();oldTown.roads.Add(new RoadState{cell=c0});
        int[] ringX={0,1,0,-1,1,1,-1,-1},ringZ={1,0,-1,0,1,-1,-1,1};
        for(int i=0;i<8;i++)oldTown.buildings.Add(new BuildingState{cell=new Cell(c0.x+ringX[i],c0.z+ringZ[i]),def=i==1||i==3?1:0});
        for(int i=-2;i<=1;i++)oldTown.buildings.Add(new BuildingState{cell=new Cell(c0.x+i,c0.z-3),def=0});
        oldTown.roads.Add(new RoadState{cell=new Cell(c0.x-2,c0.z-2)});oldTown.roads.Add(new RoadState{cell=new Cell(c0.x-1,c0.z-2)});
        CitySimulation.Recount(oldTown,old.Cargo.Producer(4),old.Balance);old.World.cityLayout=0;
        Assert(oldTown.population==580&&oldTown.level==CityLevel.Village&&!Connected(oldTown),"Fixture reproduces the old disconnected layout");
        var oldSaves=new SaveService(Path.GetTempPath(),codec,old.Balance);string oldJson=oldSaves.CaptureSnapshot(old.World);
        var rebuilt=new GameSession(oldSaves.RestoreSnapshot(oldJson),old.Balance);var again=new GameSession(oldSaves.RestoreSnapshot(oldJson),old.Balance);
        var fresh=rebuilt.Cities.CityFor(4);
        Assert(rebuilt.World.cityLayout==CityLayout.Current&&Connected(fresh)&&fresh.population>=580&&fresh.level==CityLevel.Village,$"Old towns are rebuilt connected at their population ({fresh.population})");
        foreach(var bs in fresh.buildings)Assert(FacesStreet(fresh,bs),"Rebuilt buildings face streets");
        SaveService.Validate(rebuilt.World,rebuilt.Balance);
        for(int i=0;i<6000;i++){rebuilt.Step();again.Step();}
        Assert(oldSaves.CaptureSnapshot(rebuilt.World)==oldSaves.CaptureSnapshot(again.World),"Rebuilt towns replay identically");
        // Big towns build landmarks of every size, from the 3×3 police headquarters to the 8×8 stadium, each fronting a street.
        var big=New();var metro=big.Cities.CityFor(4);metro.population=13000;big.Cities.Relayout();
        Assert(metro.level==CityLevel.LargeCity&&Connected(metro),"A rebuilt 13,000-person town is a connected Large City");
        foreach(var (key,side) in new[]{("police-compound",3),("hospital-campus",4),("shopping-centre",5),("university",6),("stadium-bowl",8),("drive-in",5),("splash-pool",2),("community-pool",3),("lido",4),("water-park",5),("town-park",3),("city-park",4),("central-park",5),("zoo",6)})
        {
            var landmark=metro.buildings.Find(bs=>BuildingCatalog.Get(bs.def).key==key);
            Assert(BuildingCatalog.Valid(landmark.def)&&BuildingCatalog.Get(landmark.def).key==key,"Large City builds the "+key);
            Assert(CityLayout.FootprintCells(landmark)==side*side&&FacesStreet(metro,landmark),$"{key} covers {side}×{side} cells beside a street");
            for(int i=0;i<side*side;i++){var cell=CityLayout.FootprintCell(landmark,i);Assert(big.Cities.HasBuilding(cell)&&big.Cities.CityAt(cell)==metro&&big.Network.At(cell)==null&&!metro.roads.Exists(r=>r.cell.Equals(cell)),key+" occupies its whole footprint and no street");}
        }
        SaveService.Validate(big.World,big.Balance);
        var mall=metro.buildings.Find(bs=>BuildingCatalog.Get(bs.def).key=="shopping-centre");int bigMoney=big.World.money,bigJobs=metro.jobs;
        OK(big.Build.Bulldoze(CityLayout.FootprintCell(mall,24)));
        Assert(big.World.money==bigMoney-500*2*25&&metro.jobs==bigJobs-350,"Demolishing a 5×5 landmark costs its 25 cells and removes its jobs");
        for(int i=0;i<25;i++)Assert(!big.Cities.HasBuilding(CityLayout.FootprintCell(mall,i))&&metro.cleared.Exists(c=>c.cell.Equals(CityLayout.FootprintCell(mall,i))),"Every landmark cell is cleared and held for the cooldown");
        SaveService.Validate(big.World,big.Balance);
        var future=codec.Decode<WorldState>(codec.Encode(New().World));future.cityLayout=CityLayout.Current+1;bool refused=false;try{SaveService.Validate(future,new Balance());}catch(InvalidDataException){refused=true;}
        Assert(refused,"Saves from a newer town layout are refused");
    }
    /// <summary>Two passenger platforms on one z=46 line: Willowbrook (16,46) and Oakridge (48,46).</summary>
    static (GameSession g,int a,int b,int train) PassengerStubs(Action<GameSession> tracks)
    {
        var g=New();g.World.money=200000;tracks(g);
        int a=OK(g.Stations.Place(new Cell(16,46),4)),b=OK(g.Stations.Place(new Cell(48,46),5));
        return (g,a,b,OK(g.Trains.Buy(a,2,Cargo.Passengers)));
    }
    static void CheckRailFix()
    {
        // A branch turns the line into a turnout whose arms do not connect: the stations are joined but unreachable.
        var junction=PassengerStubs(g=>{Track(g,new Cell(14,46),new Cell(50,46));Track(g,new Cell(36,46),new Cell(36,50));});
        Assert(!junction.g.Trains.AssignRoute(junction.train,junction.a,junction.b).ok,"Turnout arms block through trains");
        var across=junction.g.Build.ValidateRoute(new List<Cell>{new Cell(35,46),new Cell(36,46),new Cell(37,46)});
        Assert(!across.valid&&across.reason.Contains("impossible turn"),"Route validation refuses a turn across turnout arms: "+across.reason);
        var fix=junction.g.Fixer.Plan(junction.train);
        Assert(fix.valid&&fix.toStationId==junction.b&&fix.build.changes.Count>0&&fix.reason.Contains("junction"),"AI explains and fixes a blocking junction: "+fix.reason);
        int money=junction.g.World.money;
        OK(junction.g.Fixer.Apply(junction.train));
        var jt=junction.g.Trains.Train(junction.train);
        Assert(jt.a==junction.a&&jt.b==junction.b&&jt.state==ServiceState.Loading,"AI fix assigns the route");
        Assert(junction.g.World.money==money-fix.build.cost,"AI fix charges its quoted cost");
        for(int i=0;i<2400;i++)junction.g.Step();
        Assert(junction.g.World.delivered>0,"Train delivers over the repaired line");
        SaveService.Validate(junction.g.World,junction.g.Balance);
        // A gap between two stubs is closed with new track.
        var gap=PassengerStubs(g=>{Track(g,new Cell(14,46),new Cell(20,46));Track(g,new Cell(46,46),new Cell(50,46));});
        var gapFix=gap.g.Fixer.Plan(gap.train);
        Assert(gapFix.valid&&gapFix.build.cost>0&&gapFix.reason.Contains("not connected"),"AI closes a gap: "+gapFix.reason);
        gap.g.World.money=gapFix.build.cost-1;
        var poor=gap.g.Fixer.Plan(gap.train);
        Assert(!poor.valid&&poor.reason.Contains("costs")&&poor.build!=null,"Unaffordable fix explains the price: "+poor.reason);
        Assert(!gap.g.Fixer.Apply(gap.train).ok&&gap.g.World.money==gapFix.build.cost-1,"Unaffordable fix changes nothing");
        gap.g.World.money=200000;
        var quoted=gap.g.Fixer.Plan(gap.train);quoted.build.cost++;int tracksBefore=gap.g.World.tracks.Count;
        Assert(!gap.g.Fixer.Apply(gap.train,quoted).ok&&gap.g.World.tracks.Count==tracksBefore,"A fix that differs from the quote is refused");
        OK(gap.g.Fixer.Apply(gap.train,gap.g.Fixer.Plan(gap.train)));
        Assert(!gap.g.Fixer.Plan(gap.train).valid,"A running train must park before the AI replans");
        // Tracks that already work cost nothing and just start the route.
        var ready=PassengerStubs(g=>Track(g,new Cell(14,46),new Cell(50,46)));
        var readyFix=ready.g.Fixer.Plan(ready.train);
        Assert(readyFix.valid&&readyFix.build.cost==0&&readyFix.build.changes.Count==0,"Working tracks need no construction");
        int readyMoney=ready.g.World.money;OK(ready.g.Fixer.Apply(ready.train));
        Assert(ready.g.World.money==readyMoney&&ready.g.Trains.Train(ready.train).a!=0,"Free fix only starts the route");
        // No compatible second station: the AI plans one at the nearest other town.
        var lonely=New();lonely.World.money=200000;Track(lonely,new Cell(14,46),new Cell(20,46));int la=OK(lonely.Stations.Place(new Cell(16,46),4));int lt=OK(lonely.Trains.Buy(la,2,Cargo.Passengers));
        var lonelyWatch=System.Diagnostics.Stopwatch.StartNew();var lonelyFix=lonely.Fixer.Plan(lt);lonelyWatch.Stop();
        Console.WriteLine($"AI new town station plan: {lonelyWatch.Elapsed.TotalMilliseconds:F0} ms · {lonelyFix.reason.Replace("\n"," | ")}");
        Assert(lonelyFix.newStation!=null&&lonelyFix.newStation.producerId==5&&lonelyFix.reason.Contains("build"),"Missing destination: the AI plans a station at the nearest other town: "+lonelyFix.reason);
        // The fix never joins another train's railway.
        var shared=PassengerStubs(g=>{Coal(g);Track(g,new Cell(14,46),new Cell(20,46));Track(g,new Cell(46,46),new Cell(50,46));});
        OK(shared.g.Fixer.Apply(shared.train));
        SaveService.Validate(shared.g.World,shared.g.Balance);
        Assert(shared.g.Network.components[shared.g.Network.At(new Cell(16,46)).id]!=shared.g.Network.components[shared.g.Network.At(new Cell(10,15)).id],"AI fix keeps railways separate");
        // The player's tangled save: Frostford's second station joins Lakewood only through turnouts.
        string fixture=Path.Combine(AppContext.BaseDirectory,"..","..","..","Fixtures","tangled-junctions.json");
        var tangledWorld=new SaveService(Path.GetTempPath(),new Codec(),new Balance()).RestoreSnapshot(File.ReadAllText(fixture));
        var tangled=new GameSession(tangledWorld,new Balance());
        var stuck=tangled.World.trains.Find(t=>t.number==2);
        // Stuck while crossings ran straight only; now it turns at the crossing at 23, 86 and the AI has nothing to build.
        var watch=System.Diagnostics.Stopwatch.StartNew();var real=tangled.Fixer.Plan(stuck.id);watch.Stop();
        Assert(real.valid&&tangled.Trains.Station(real.toStationId).name=="Lakewood Station"&&real.build.changes.Count==0,"The express reaches Lakewood by turning at a crossing: "+real.reason);
        Console.WriteLine($"AI rail fix on the tangled save: {watch.Elapsed.TotalMilliseconds:F0} ms, {real.build.changes.Count} pieces, ${real.build.cost:N0}\n{real.reason}");
        OK(tangled.Fixer.Apply(stuck.id));
        long deliveredBefore=tangled.World.delivered;
        for(int i=0;i<12000;i++)tangled.Step();
        Assert(tangled.World.delivered>deliveredBefore&&stuck.state!=ServiceState.InvalidRoute,"Repaired express runs");
        SaveService.Validate(tangled.World,tangled.Balance);
    }
    static void CheckTrainAccounts()
    {
        // Rolling windows over monthly buckets: one month is 1200 ticks, the oldest month in a window counts pro rata.
        var t=new TrainState();
        t.accounts.Record(10,1000,0);t.accounts.Record(1300,0,200);t.accounts.Record(1300,500,0);
        var partial=t.accounts.LastMonth(1300);
        Assert(partial.income==500+1000*1100/1200&&partial.cost==200,"Last month adds the unexpired share of the previous month: "+partial.income);
        var month=t.accounts.LastMonth(2400);
        Assert(month.income==500&&month.cost==200&&month.Profit==300,"Last month at a month boundary is the whole previous month");
        var year=t.accounts.LastYear(2400);
        Assert(year.income==1500&&year.cost==200&&year.Profit==1300,"Last year covers every month since purchase");
        Assert(t.accounts.LastYear(1200L*13).income==500&&t.accounts.LastYear(1200L*14).income==0,"Months older than a year leave the window");
        Assert(t.accounts.month==1,"Reading the accounts never changes them");
        t.accounts.Record(1200L*30,0,70);
        var loss=t.accounts.LastYear(1200L*30);Assert(loss.income==0&&loss.cost==70&&loss.Profit==-70,"Stale months are cleared and losses are negative");
        // A running train books its own deliveries and running costs; capital spending is not profit.
        var g=New();var r=Coal(g);var train=g.Trains.Train(r.train);long capital=g.World.totalExpenses;
        for(int i=0;i<1100;i++)g.Step();
        var booked=train.accounts.LastMonth(g.World.tick);
        Assert(g.World.delivered>0&&booked.income==g.World.totalIncome,"Delivery income is booked to the train: "+booked.income);
        Assert(booked.cost==g.World.totalExpenses-capital&&booked.cost>0&&booked.Profit>0,"Running costs are booked to the train: "+booked.cost);
        // Saves keep the accounts; saves written before accounts existed load with empty ones.
        var codec=new Codec();var restored=codec.Decode<WorldState>(codec.Encode(g.World));
        Assert(restored.trains[0].accounts.LastYear(g.World.tick).Profit==train.accounts.LastYear(g.World.tick).Profit,"Accounts survive a save round trip");
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(codec.Encode(g.World));legacy["trains"][0].AsObject().Remove("accounts");
        var old=codec.Decode<WorldState>(legacy.ToJsonString());SaveService.Validate(old,g.Balance);
        Assert(old.trains[0].accounts.LastYear(old.tick).income==0,"Old saves load with empty train accounts");
        Func<Action<TrainAccounts>,bool> rejects=mutate=>{var copy=codec.Decode<WorldState>(codec.Encode(g.World));mutate(copy.trains[0].accounts);try{SaveService.Validate(copy,g.Balance);return false;}catch(InvalidDataException){return true;}};
        Assert(rejects(a=>a.income=new int[3])&&rejects(a=>a.cost=null)&&rejects(a=>a.cost[4]=-1)&&rejects(a=>a.month=99),"Corrupt train accounts rejected on load");
    }
    static void CheckWagons()
    {
        // The standard consist keeps each model's original balance; every wagon moves price, capacity and running cost by one share.
        var b=new Balance();
        for(int m=0;m<TrainCatalog.Count;m++)
        {
            int d=TrainCatalog.DefaultWagons(m);
            Assert(b.TrainPrice(m,d)==b.trainPrice[m]&&b.Capacity(m,d)==b.capacity[m]&&b.RunningCost(m,d)==b.runningCost[m],"Standard consist keeps model "+m+" balance");
            for(int n=TrainCatalog.MinWagons;n<TrainCatalog.MaxWagons;n++)
                Assert(b.TrainPrice(m,n+1)-b.TrainPrice(m,n)==b.WagonPrice(m)&&b.Capacity(m,n+1)>b.Capacity(m,n)&&b.RunningCost(m,n+1)>b.RunningCost(m,n)&&b.Capacity(m,n)>0&&b.TrainPrice(m,n)>0,"Model "+m+" grows by one wagon at "+n);
        }
        Assert(b.WagonPrice(0)==2000&&b.WagonCapacity(0)==15&&b.WagonRunningCost(0)==5,"Small freight wagon share");
        Assert(b.Capacity(4,1)==24&&b.Capacity(4,3)==48,"Commuter coaches seat as many as its cab cars");
        // Buying: the chosen length is charged, loaded and run; lengths outside the range are refused without charging.
        var g=New();g.World.money=200000;Track(g,new Cell(8,15),new Cell(50,15));
        int a=OK(g.Stations.Place(new Cell(10,15),1)),z=OK(g.Stations.Place(new Cell(48,15),2));
        int money=g.World.money;
        Assert(!g.Trains.Buy(a,0,Cargo.Coal,0).ok&&!g.Trains.Buy(a,0,Cargo.Coal,TrainCatalog.MaxWagons+1).ok&&g.World.money==money&&g.World.trains.Count==0,"Wagon count outside the range is refused");
        int t=OK(g.Trains.Buy(a,0,Cargo.Coal,6));var train=g.Trains.Train(t);
        Assert(train.wagons==6&&b.TrainPrice(0,6)==8000+4*2000&&g.World.money==money-b.TrainPrice(0,6),"Long train is charged per wagon");
        g.Cargo.Producer(1).inventory=200;OK(g.Trains.AssignRoute(t,a,z));
        Assert(train.units==b.Capacity(0,6)&&train.units==90,"Long train loads every wagon: "+train.units);
        long expenses=g.World.totalExpenses;
        for(int i=0;i<1200;i++)g.Step();
        Assert(g.World.totalExpenses-expenses==b.RunningCost(0,6)&&b.RunningCost(0,6)==40,"Long train pays running costs per wagon: "+(g.World.totalExpenses-expenses));
        // Editing: only at a station; cargo aboard must fit; added wagons cost full price and removed ones refund half.
        for(int i=0;i<200&&train.state!=ServiceState.Travelling;i++)g.Step();
        Assert(train.state==ServiceState.Travelling&&!g.Trains.SetWagons(t,4).ok&&train.wagons==6,"A moving train keeps its wagons");
        OK(g.Trains.ReturnToStation(t));
        for(int i=0;i<6000&&train.state!=ServiceState.Parked;i++)g.Step();
        Assert(train.state==ServiceState.Parked&&train.units==0,"Train parks empty at a station");
        train.units=80;
        Assert(!g.Trains.SetWagons(t,2).ok&&train.wagons==6,"Cargo aboard must fit the shorter train");
        train.units=0;
        money=g.World.money;OK(g.Trains.SetWagons(t,2));
        Assert(train.wagons==2&&g.World.money==money+4*2000/2&&g.Balance.Capacity(train)==30,"Removed wagons refund half their price");
        money=g.World.money;OK(g.Trains.SetWagons(t,5));
        Assert(train.wagons==5&&g.World.money==money-3*2000,"Added wagons cost the wagon price");
        g.World.money=100;
        Assert(!g.Trains.SetWagons(t,8).ok&&!g.Trains.SetWagons(t,5).ok&&!g.Trains.SetWagons(t,0).ok&&!g.Trains.SetWagons(t,9).ok&&!g.Trains.SetWagons(999,3).ok&&train.wagons==5&&g.World.money==100,"Unaffordable, unchanged and out-of-range edits are refused");
        g.World.money=100000;money=g.World.money;OK(g.Trains.Sell(t));
        Assert(g.World.money==money+b.TrainPrice(0,5)/2,"Sale refunds half the price of the current length");
        // A train that is loading at a station can be rebuilt too; the standard purchase keeps the standard consist.
        int t2=OK(g.Trains.Buy(a,0,Cargo.Coal,1));var second=g.Trains.Train(t2);
        g.Cargo.Producer(1).inventory=0;OK(g.Trains.AssignRoute(t2,a,z));
        Assert(second.state==ServiceState.Loading,"Second train is loading");
        OK(g.Trains.SetWagons(t2,3));
        Assert(g.Balance.Capacity(second)==45,"Loading train rebuilt to three wagons");
        // Saves keep the wagon count; older saves without it run the standard consist; corrupt counts are rejected.
        var codec=new Codec();var saves=new SaveService(Path.GetTempPath(),codec,g.Balance);
        Assert(saves.RestoreSnapshot(saves.CaptureSnapshot(g.World)).trains[0].wagons==3,"Wagon count survives a save");
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(codec.Encode(g.World));legacy["trains"][0].AsObject().Remove("wagons");
        var old=codec.Decode<WorldState>(legacy.ToJsonString());SaveService.Validate(old,g.Balance);
        Assert(old.trains[0].wagons==0&&TrainCatalog.Wagons(old.trains[0])==2&&g.Balance.Capacity(old.trains[0])==30&&g.Balance.TrainPrice(old.trains[0])==8000,"Old saves keep the standard consist");
        var bad=codec.Decode<WorldState>(codec.Encode(g.World));bad.trains[0].wagons=TrainCatalog.MaxWagons+1;
        bool rejected=false;try{SaveService.Validate(bad,g.Balance);}catch(InvalidDataException){rejected=true;}
        Assert(rejected,"A save with too many wagons is rejected");
        var plain=New();Track(plain,new Cell(8,15),new Cell(14,15));
        Assert(plain.Trains.Train(OK(plain.Trains.Buy(OK(plain.Stations.Place(new Cell(10,15),1)),3,Cargo.Coal))).wagons==5,"Standard purchase keeps the standard consist");
    }
    static void CheckRoadLanes()
    {
        // Day 0: cars roam one east-west main street through the plaza.
        var g=New();var c=g.Cities.CityFor(4).center;var stub=new Cell(c.x+1,c.z);
        var lanes=RoadLanes.Build(g.World,g.Network);
        Assert(lanes.Exits(c)==10&&lanes.Exits(stub)==8&&!lanes.IsHighway(c,1),"Day-0 main street is one road through the plaza");
        // A highway carries traffic only once it is open; then cars can leave town from the street it joins.
        var road=new IntercityRoadState{a=4,b=5};for(int x=c.x+1;x<=c.x+8;x++)road.path.Add(new Cell(x,c.z));road.built=4;g.World.intercityRoads.Add(road);
        lanes=RoadLanes.Build(g.World,g.Network);
        Assert(lanes.Exits(stub)==8&&lanes.Exits(new Cell(c.x+2,c.z))==0,"No traffic on an unfinished highway");
        road.built=road.path.Count;lanes=RoadLanes.Build(g.World,g.Network);
        Assert(lanes.Exits(stub)==10&&lanes.IsHighway(stub,1)&&!lanes.IsHighway(stub,3)&&lanes.Exits(new Cell(c.x+4,c.z))==10,"An open highway leaves town from the street it joins");
        Assert(!lanes.SameAs(RoadLanes.Build(New().World,g.Network))&&lanes.SameAs(RoadLanes.Build(g.World,g.Network)),"Lane graphs compare by content");
        // Rails crossed at right angles make a level crossing; rails laid along the highway cut it.
        g.World.money=100000;Track(g,new Cell(c.x+5,c.z-3),new Cell(c.x+5,c.z+3));
        var crossing=g.Network.At(new Cell(c.x+5,c.z));lanes=RoadLanes.Build(g.World,g.Network);
        Assert(crossing!=null&&crossing.mask==5&&lanes.Exits(crossing.cell)==10,"Cars cross straight rails at right angles");
        Track(g,new Cell(c.x+7,c.z),new Cell(c.x+8,c.z));lanes=RoadLanes.Build(g.World,g.Network);
        Assert(lanes.Exits(new Cell(c.x+6,c.z))==8&&lanes.Exits(new Cell(c.x+7,c.z))==0,"Cars never drive along rails");
        // Crossings close under a train and ahead of a moving one; a train loading at a platform closes none ahead.
        var w=new WorldState();var near=new HashSet<int>();
        Func<ServiceState,int,int,TrainState> train=(state,step,distance)=>{var t=new TrainState{state=state,step=step,distance=distance};for(int i=0;i<12;i++)t.path.Add(new RailStep{trackId=100+i,length=1000});return t;};
        w.trains.Add(train(ServiceState.Travelling,0,0));RoadLanes.TracksNearTrains(w,near);
        Assert(near.Contains(100)&&near.Contains(108)&&!near.Contains(109),"A moving train closes the crossings it will reach soon");
        w.trains[0]=train(ServiceState.Loading,0,0);RoadLanes.TracksNearTrains(w,near);
        Assert(near.Count==1&&near.Contains(100),"A loading train closes only the track it stands on");
        w.trains[0]=train(ServiceState.Travelling,9,500);RoadLanes.TracksNearTrains(w,near);
        Assert(near.Contains(105)&&!near.Contains(104)&&near.Contains(111),"Crossings stay closed until the whole train has passed");
    }
    static void CheckStationSizes()
    {
        // A 3 x 1 station on open ground lays its own straight platform track and pays for both.
        var g=New();int money=g.World.money,tracks=g.World.tracks.Count;
        var plan=g.Stations.Plan(new Cell(10,15),1,3,1);
        Assert(plan.valid&&plan.side==0&&plan.cost==2300&&plan.track.changes.Count==3&&plan.nearby.Exists(p=>p.id==1),"Bare-ground station plan: "+plan.reason);
        int bare=OK(g.Stations.Place(plan,1));var s=g.Trains.Station(bare);
        Assert(g.World.money==money-2300&&g.World.tracks.Count==tracks+3&&s.paid==2000,"Bare-ground station charges station and track");
        foreach(var c in StationLayout.Cells(s))Assert(g.Network.At(c)!=null&&g.Network.At(c).mask==10&&BuildService.Platform(s,c),"Platform track laid straight");
        Assert(StationLayout.Length(s)==3&&StationLayout.Platforms(s)==1&&BuildService.StationFootprint(g.World,new Cell(11,16)),"Default size and strip");
        // Refusals change nothing.
        money=g.World.money;tracks=g.World.tracks.Count;int stations=g.World.stations.Count;
        Assert(!g.Stations.Plan(new Cell(20,15),1,7,1).valid&&!g.Stations.Plan(new Cell(20,15),1,3,5).valid&&!g.Stations.Plan(new Cell(20,15),1,2,1).valid,"Sizes outside 3-6 x 1-4 refused");
        Assert(!g.Stations.Plan(new Cell(31,20),0,3,1).valid,"Station on water refused");
        Assert(!g.Stations.Plan(new Cell(10,12),1,3,1).valid,"Station inside an industry refused");
        var far=g.Stations.Plan(new Cell(25,20),1,3,1);Assert(!far.valid&&far.reason.Contains("three cells"),"Station out of reach refused: "+far.reason);
        Assert(!g.Stations.Plan(new Cell(10,15),1,3,1).valid,"Station over another station refused");
        Assert(!g.Stations.Place(g.Stations.Plan(new Cell(48,15),1,3,1),1).ok,"Station for a producer out of reach refused");
        g.World.money=500;var poor=g.Stations.Plan(new Cell(48,15),1,3,1);Assert(!poor.valid&&poor.reason.Contains("money")&&!g.Stations.Place(poor,2).ok,"Unaffordable station refused");g.World.money=money;
        Assert(g.World.money==money&&g.World.tracks.Count==tracks&&g.World.stations.Count==stations,"Refused stations change nothing");
        // Bulldozing keeps the platform track as ordinary track.
        var removed=OK2(g.Build.Bulldoze(new Cell(10,15)));Assert(removed.message.Contains("track remains")&&g.World.tracks.Count==tracks&&g.World.stations.Count==stations-1&&g.World.money==money+1000,"Bulldozed station leaves its track");
        // A bigger station: 5 cells x 2 platforms, the second track beside the first, away from the building.
        var big=New();var bp=big.Stations.Plan(new Cell(10,15),1,5,2);
        Assert(bp.valid&&bp.cost==StationLayout.Cost(big.Balance,5,2)+1000&&bp.cost==5500&&bp.track.changes.Count==10,"5 x 2 station plan: "+bp.reason+" $"+bp.cost);
        var bs=big.Trains.Station(OK(big.Stations.Place(bp,1)));
        Assert(StationLayout.Cells(bs).Count==10&&StationLayout.Strip(bs).Count==5&&StationLayout.PlatformAt(bs,new Cell(12,14))==1&&StationLayout.PlatformAt(bs,new Cell(8,15))==0&&StationLayout.PlatformAt(bs,new Cell(13,15))==-1&&StationLayout.Center(bs,1).Equals(new Cell(10,14)),"5 x 2 station geometry");
        Assert(BuildService.StationFootprint(big.World,new Cell(12,16))&&!BuildService.StationFootprint(big.World,new Cell(13,16))&&big.Build.Protected(big.Network.At(new Cell(8,14)).id),"Strip and platforms follow the size");
        var evenPlan=big.Stations.Plan(new Cell(48,15),1,4,1,2);Assert(evenPlan.valid&&evenPlan.platformCells[0].Equals(new Cell(47,15))&&evenPlan.platformCells[3].Equals(new Cell(50,15)),"Even lengths reach one cell further forward");
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailStations-"+Guid.NewGuid()),new Codec(),big.Balance);
        var copy=saves.RestoreSnapshot(saves.CaptureSnapshot(big.World));Assert(copy.stations[0].length==5&&copy.stations[0].platforms==2,"Station size survives a save");
        bs.length=9;bool bad=false;try{saves.RestoreSnapshot(saves.CaptureSnapshot(big.World));}catch{bad=true;}Assert(bad,"Impossible station size rejected on load");
        bs.length=5;
        // With three platforms the sides lay different track: an unaffordable first side must not hide an affordable second one.
        var sides=New();Track(sides,new Cell(12,14),new Cell(14,14));sides.World.money=5700;
        var cheap=sides.Stations.Plan(new Cell(13,12),1,3,3,1);Assert(cheap.valid&&cheap.side==2&&cheap.cost==5600&&cheap.track.changes.Count==6,"Affordable side chosen: "+cheap.reason+" side "+cheap.side+" $"+cheap.cost);
        sides.World.money=5500;var dear=sides.Stations.Plan(new Cell(13,12),1,3,3,1);Assert(!dear.valid&&dear.reason.Contains("money")&&dear.cost==5600,"Unaffordable plan quotes the cheaper side: $"+dear.cost);
        // Two separate railways share one two-platform mine station: one train each.
        var two=New();two.World.money=200000;
        var hub=two.Stations.Plan(new Cell(13,12),0,3,2,1);Assert(hub.valid&&hub.side==1,"Two-platform mine station: "+hub.reason);
        int mine=OK(two.Stations.Place(hub,1));
        int west=OK(two.Stations.Place(two.Stations.Plan(new Cell(17,23),0,3,1,6),6));
        var lineA=new List<Cell>();for(int z=13;z<=15;z++)lineA.Add(new Cell(13,z));for(int x=14;x<=50;x++)lineA.Add(new Cell(x,15));
        var pa=two.Build.ValidateBuild(lineA);Assert(pa.valid,pa.reason);OK(two.Build.CommitBuild(pa));
        int east=OK(two.Stations.Place(new Cell(48,15),2));
        var lineB=new List<Cell>{new Cell(12,13),new Cell(12,14)};for(int x=11;x>=8;x--)lineB.Add(new Cell(x,14));for(int z=15;z<=21;z++)lineB.Add(new Cell(8,z));for(int x=9;x<=17;x++)lineB.Add(new Cell(x,21));lineB.Add(new Cell(17,22));
        var pb=two.Build.ValidateBuild(lineB);Assert(pb.valid,pb.reason);OK(two.Build.CommitBuild(pb));
        Assert(two.Network.components[two.Network.At(new Cell(13,12)).id]!=two.Network.components[two.Network.At(new Cell(12,12)).id],"The two platforms are separate railways");
        int t1=OK(two.Trains.Buy(mine,0,Cargo.Coal)),t2=OK(two.Trains.Buy(mine,0,Cargo.Coal));
        Assert(two.Trains.Train(t1).platform==0&&two.Trains.Train(t2).platform==1,"Each train takes a free platform");
        var third=two.Trains.Buy(mine,0,Cargo.Coal);Assert(!third.ok&&third.message.Contains("Every platform"),"No third train without a free platform");
        Assert(!two.Trains.AssignRoute(t2,mine,east).ok,"A train cannot leave its platform's railway");
        OK(two.Trains.AssignRoute(t1,mine,east));OK(two.Trains.AssignRoute(t2,mine,west));
        var twoSaves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailPlatforms-"+Guid.NewGuid()),new Codec(),two.Balance);
        bool reachedEast=false,reachedWest=false,backOnPlatform=false;
        for(int i=0;i<4000;i++)
        {
            two.Step();var a=two.Trains.Train(t1);var b=two.Trains.Train(t2);
            reachedEast|=a.stationId==east;reachedWest|=b.stationId==west&&b.platform==0;
            if(reachedWest&&b.stationId==mine){Assert(b.platform==1,"Back on its own platform");backOnPlatform=true;}
            if(i==700){var replay=new GameSession(twoSaves.RestoreSnapshot(twoSaves.CaptureSnapshot(two.World)),two.Balance);for(int j=0;j<300;j++){two.Step();replay.Step();i++;}Assert(twoSaves.CaptureSnapshot(two.World)==twoSaves.CaptureSnapshot(replay.World),"Platform trains replay exactly after a save");}
        }
        Assert(reachedEast&&reachedWest&&backOnPlatform&&two.World.delivered>0,"Both trains serve the shared station");
        // The AI rail fixer routes from the train's own platform to any platform of the stop.
        var ai=New();ai.World.money=200000;
        int aiMine=OK(ai.Stations.Place(ai.Stations.Plan(new Cell(13,12),0,3,2,1),1));OK(ai.Stations.Place(ai.Stations.Plan(new Cell(17,23),0,3,2,6),6));
        int aiTrain=OK(ai.Trains.Buy(aiMine,0,Cargo.Coal));var aiFix=ai.Fixer.Plan(aiTrain);
        Assert(aiFix.valid&&aiFix.build.changes.Count>0,"AI fix between multi-platform stations: "+aiFix.reason);
        OK(ai.Fixer.Apply(aiTrain));Assert(ai.Trains.Train(aiTrain).state==ServiceState.Loading,"AI fix starts the platform train");
    }
    // The bulldozer fells pines (saved in felledTrees) and picks the building or tree a tap really shows.
    static void CheckBulldozer()
    {
        var g=New();var spots=g.Scenery.Trees;
        // The forest is exactly the scatter WorldView always drew, so no existing map loses or moves a tree.
        var rng=new Random(1729);int n=0;var seen=new HashSet<int>();
        for(int i=0;i<1050;i++){var c=new Cell(rng.Next(2,MapDefinition.Size-2),rng.Next(2,MapDefinition.Size-2));bool near=false;foreach(var p in g.World.producers)if(p.kind!=ProducerKind.SkiResort&&c.Distance(p.cell)<6)near=true; // resorts came later and keep the old scatter
            if(near||MapDefinition.Water(c)||MapDefinition.Raised(c)||MapDefinition.Terrain(c)==2||!seen.Add(c.Key))continue;float h=.9f+(float)rng.NextDouble()*.65f;int shade=rng.Next(4);
            if(Coast.Beach(c))continue; // no pines on the beach sand
            Assert(n<spots.Count&&spots[n].cell.Equals(c)&&spots[n].height==h&&spots[n].shade==shade,"Tree layout matches the old scatter at "+n);n++;}
        Assert(n==spots.Count&&n>500,"Every legacy tree kept: "+n);
        // Felling a lone tree costs $50, leaves bare ground and reports a tree bulldoze event.
        Cell tree=default;for(int i=0;i<spots.Count;i++)if(g.Scenery.TreeAt(spots[i].cell)){tree=spots[i].cell;break;}
        Drain(g);int money=g.World.money,before=g.World.felledTrees.Count;var felled=g.Build.Bulldoze(tree);Assert(felled.ok&&felled.message.Contains("Tree cleared"),felled.message);
        Assert(g.World.money==money-g.Balance.treeClearCost&&g.Balance.treeClearCost==50,"Tree clearing costs $50");
        Assert(!g.Scenery.TreeAt(tree)&&g.World.felledTrees.Count==before+1&&g.World.felledTrees[before].Equals(tree),"Felled tree recorded");
        var ev=Drain(g);Assert(ev.Count==1&&ev[0].kind==GameEventKind.Bulldozed&&ev[0].aux==3&&ev[0].cell.Equals(tree),"Tree bulldoze event");
        money=g.World.money;var bare=g.Build.Bulldoze(tree);Assert(!bare.ok&&bare.message.Contains("tree")&&g.World.money==money,"Bare ground: nothing to bulldoze, no charge");
        // Too little money keeps the tree.
        Cell other=default;for(int i=0;i<spots.Count;i++)if(g.Scenery.TreeAt(spots[i].cell)){other=spots[i].cell;break;}
        g.World.money=10;Assert(!g.Build.Bulldoze(other).ok&&g.Scenery.TreeAt(other)&&g.World.money==10,"Unaffordable tree stays");g.World.money=money;
        // The felled tree survives a save round trip; standing ones stay.
        var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);var back=new GameSession(saves.RestoreSnapshot(saves.CaptureSnapshot(g.World)),new Balance());
        Assert(!back.Scenery.TreeAt(tree)&&back.Scenery.TreeAt(other),"Felled trees persist in saves");
        var dup=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));dup.felledTrees.Add(tree);bool rejected=false;try{SaveService.Validate(dup,g.Balance);}catch(InvalidDataException){rejected=true;}Assert(rejected,"Duplicate felled tree rejected");
        // Track over a tree hides it; bulldozing that track leaves bare ground instead of regrowing the pine.
        var r=New();Cell under=default;bool found=false;
        foreach(var s in r.Scenery.Trees){var c=s.cell;if(r.Scenery.TreeAt(c)&&r.Build.Placeable(c.Move(3))&&r.Build.Placeable(c)&&r.Build.Placeable(c.Move(1))&&r.Build.Preview(c.Move(3),c.Move(1)).valid){under=c;found=true;break;}}
        Assert(found,"A tree with room for track");Track(r,under.Move(3),under.Move(1));Assert(!r.Scenery.TreeAt(under),"Track covers the tree");
        OK(r.Build.Bulldoze(under));Assert(r.Network.At(under)==null&&!r.Scenery.TreeAt(under)&&r.World.felledTrees.Exists(c=>c.Equals(under)),"Removed track leaves bare ground");
        // Picking: the camera looks down 35.264° from the south-west, so a ray through a pine's crown meets the ground a cell behind it.
        var p2=New();float dx=.57735f,dy=-.57735f,dz=.57735f;Cell pine=default;found=false;
        foreach(var s in p2.Scenery.Trees){var c=s.cell;bool clear=p2.Scenery.TreeAt(c);for(int a=-1;a<=0&&clear;a++)for(int b=-1;b<=0;b++)if((a!=0||b!=0)&&(p2.Scenery.TreeAt(new Cell(c.x+a,c.z+b))||p2.Cities.HasBuilding(new Cell(c.x+a,c.z+b))))clear=false;if(clear){pine=c;found=true;break;}}
        Assert(found,"A pine with a clear line of sight");
        Assert(StructurePick.First(p2,pine.x-dx*40,1f-dy*40,pine.z-dz*40,dx,dy,dz,new Cell(pine.x+1,pine.z+1)).Equals(pine),"Tap on a pine's crown picks the pine, not the ground behind it");
        var town=p2.Cities.CityFor(4);Cell house=default;found=false;
        foreach(var bs in town.buildings){var c=bs.cell;if(!p2.Cities.HasBuilding(new Cell(c.x-1,c.z))&&!p2.Cities.HasBuilding(new Cell(c.x,c.z-1))&&!p2.Cities.HasBuilding(new Cell(c.x-1,c.z-1))){house=c;found=true;break;}}
        Assert(found,"A house with a clear line of sight");
        var hit=StructurePick.First(p2,house.x-dx*40,.5f-dy*40,house.z-dz*40,dx,dy,dz,new Cell(house.x+1,house.z+1));
        Assert(hit.Equals(house),"Tap on a house's wall picks the house: "+hit);
        // With nothing standing on the ray (the map corner has no trees or towns) the ground cell under the finger is kept.
        var corner=new Cell(0,0);Assert(StructurePick.First(p2,0,20,0,0,-1,0,corner).Equals(corner),"Empty ray keeps the ground cell");
        Assert(StructurePick.First(p2,pine.x,5,pine.z,1,0,0,corner).Equals(corner),"A level ray never picks");
        money=p2.World.money;OK(p2.Build.Bulldoze(hit));Assert(!p2.Cities.HasBuilding(house)&&p2.World.money<money,"Picked house demolished");
    }
    static Result OK2(Result r){Assert(r.ok,r.message);return r;}
    static int Bends(List<Cell> path){int n=0;for(int i=2;i<path.Count;i++)if(path[i].x-path[i-1].x!=path[i-1].x-path[i-2].x||path[i].z-path[i-1].z!=path[i-1].z-path[i-2].z)n++;return n;}
    // One-cell staircase steps: a bend followed by another bend after a single straight cell.
    static int Jogs(List<Cell> path){int n=0,last=-9;for(int i=2;i<path.Count;i++)if(path[i].x-path[i-1].x!=path[i-1].x-path[i-2].x||path[i].z-path[i-1].z!=path[i-1].z-path[i-2].z){if(i-last<=2)n++;last=i;}return n;}
    static void CheckStraightHighways()
    {
        // Highways between grown towns run in long straight stretches, never in one-cell staircase steps.
        // The map's own towns: a founded village's highway may have to swerve round an industry yard (CheckFounding covers those).
        var fast=new Balance();fast.city.basePoints=60;fast.city.foundingEnabled=false;var grown=new GameSession(WorldState.New(fast),fast);
        for(int i=0;i<90000;i++)grown.Step();
        int jogs=0,bends=0,cells=0;foreach(var r in grown.World.intercityRoads){jogs+=Jogs(r.path);bends+=Bends(r.path);cells+=r.path.Count;}
        Assert(grown.World.intercityRoads.Count>=6&&jogs==0,$"Highways run straight: {jogs} staircase steps on {grown.World.intercityRoads.Count} highways");
        // Re-planning real highways between their own ends keeps every save rule and never adds length or bends.
        grown.World.roadLayout=0;var replanned=new GameSession(grown.World,grown.Balance);SaveService.Validate(replanned.World,replanned.Balance);
        int bendsAfter=0,cellsAfter=0;foreach(var r in replanned.World.intercityRoads){bendsAfter+=Bends(r.path);cellsAfter+=r.path.Count;}
        Assert(replanned.World.roadLayout==CitySimulation.RoadLayout&&bendsAfter<=bends&&cellsAfter<=cells,$"Straightening grown highways keeps them valid: {cells}/{bends} → {cellsAfter}/{bendsAfter} cells/bends");
        // A staircase highway from an older save is straightened once when the session starts: same ends and length.
        var g=New();var c=g.Cities.CityFor(4).center;var road=new IntercityRoadState{a=4,b=5};var cell=new Cell(c.x+1,c.z);road.path.Add(cell);
        for(int i=0;i<12;i++){cell=i%2==0?new Cell(cell.x+1,cell.z):new Cell(cell.x,cell.z-1);road.path.Add(cell);}
        road.built=road.path.Count;g.World.intercityRoads.Add(road);g.World.roadLayout=0;var steps=new List<Cell>(road.path);
        var loaded=new GameSession(g.World,g.Balance);
        Assert(road.path[0].Equals(steps[0])&&road.path[road.path.Count-1].Equals(steps[steps.Count-1])&&road.path.Count==steps.Count&&road.Complete,"A straightened highway keeps its ends, length and open state");
        Assert(Jogs(steps)>0&&Bends(road.path)<=1,$"An old staircase highway is straightened on load: {Bends(steps)} bends → {Bends(road.path)}");
        Assert(loaded.World.roadLayout==CitySimulation.RoadLayout,"Straightening runs once per save");
        // A highway still under construction keeps its progress.
        road.path=steps;road.built=5;g.World.roadLayout=0;new GameSession(g.World,g.Balance);
        Assert(road.built==5&&!road.Complete&&Bends(road.path)<=1,"An unfinished highway keeps building from where it was");
    }
    static void CheckStationUpgrade()
    {
        // Willowbrook's halt on a passenger line: the AI suggests the next rung, sized to fit.
        var g=New();g.World.money=200000;Track(g,new Cell(14,46),new Cell(50,46));
        int a=OK(g.Stations.Place(new Cell(16,46),4)),z=OK(g.Stations.Place(new Cell(48,46),5));var s=g.Trains.Station(a);
        var advice=g.Upgrades.Recommend(a);
        Assert(advice.plan!=null&&advice.plan.valid&&advice.level==1&&advice.length==4&&advice.platforms==1&&advice.reason.Contains("AI pick: Town station"),"AI recommends a Town station: "+advice.reason);
        // Refusals change nothing: same size, smaller, too small for the type, unaffordable.
        int money=g.World.money,tracks=g.World.tracks.Count,revision=g.World.cityRevision;
        Assert(!g.Upgrades.Plan(a,0,3,1).valid,"Same size refused");
        Assert(!g.Upgrades.Plan(a,3,5,3).valid&&g.Upgrades.Plan(a,3,5,3).reason.Contains("needs at least"),"Grand terminal needs 6 x 3");
        Assert(!g.Upgrades.Plan(a,4,6,4).valid,"No rung above Grand terminal");
        g.World.money=100;var poor=g.Upgrades.Plan(a,1,4,1);Assert(!poor.valid&&poor.unaffordable&&poor.reason.Contains("money")&&!g.Upgrades.Apply(a,1,4,1).ok,"Unaffordable upgrade refused");g.World.money=money;
        Assert(g.World.money==money&&g.World.tracks.Count==tracks&&g.World.cityRevision==revision&&s.level==0,"Refused upgrades change nothing");
        // A Town station with three platforms flattens the houses in the way and lays two new platform tracks.
        int t=OK(g.Trains.Buy(a,2,Cargo.Passengers));OK(g.Trains.AssignRoute(t,a,z));
        money=g.World.money;tracks=g.World.tracks.Count;
        var plan=g.Upgrades.Plan(a,1,4,3);
        Assert(plan.valid&&plan.buildings.Count>0&&plan.trees.Count==0&&plan.track.changes.Count==8&&plan.cost==plan.stationPrice+plan.clearingCost+plan.trackCost,"4 x 3 plan clears houses: "+plan.reason);
        Assert(plan.stationPrice==StationLayout.Cost(g.Balance,4,3)-StationLayout.Cost(g.Balance,3,1)+StationCatalog.LevelPrice(1),"Station price is the difference plus the rung");
        var doomed=plan.buildings.ConvertAll(bs=>CityLayout.FootprintCell(bs,0));
        g.Events.Clear();OK(g.Upgrades.Apply(a,1,4,3));
        Assert(g.World.money==money-plan.cost&&s.level==1&&s.length==4&&s.platforms==3&&g.World.tracks.Count==tracks+8,"Upgrade charges the quote exactly: "+(money-g.World.money)+" vs "+plan.cost);
        foreach(var c in doomed)Assert(!g.Cities.HasBuilding(c),"Demolished building gone at "+c);
        Assert(g.Upgrades.Last.stationId==a&&g.Upgrades.Last.buildings.Count==plan.buildings.Count&&g.Upgrades.Last.oldPlatforms==1,"The show gets the demolition record");
        bool upgradedEvent=false;while(g.Events.TryDequeue(out var e))upgradedEvent|=e.kind==GameEventKind.StationUpgraded&&e.id==a;Assert(upgradedEvent,"StationUpgraded event");
        foreach(var c in StationLayout.Cells(s))Assert(g.Network.At(c)!=null&&g.Network.At(c).mask==10,"Every platform has straight track");
        // Trains keep running, and the town never rebuilds inside the new footprint, even after the demolition cooldown.
        long delivered=g.World.delivered;for(int i=0;i<7000;i++)g.Step();
        Assert(g.World.delivered>delivered,"Passengers still travel after the upgrade");
        foreach(var c in StationLayout.Strip(s))Assert(!g.Cities.Occupied(c),"Town keeps off the station strip at "+c);
        foreach(var c in StationLayout.Cells(s))Assert(!g.Cities.HasBuilding(c),"Town keeps off the platforms at "+c);
        // Bonuses: loading 15% faster per rung, town growth +5 points per rung while a train is routed here.
        Assert(StationCatalog.DwellTicks(g.Balance,s)==51&&g.Trains.Train(t).dwell<=51,"Town station loads faster");
        Assert(g.Cities.StationGrowthBonus(g.Cities.CityFor(4))==5,"Upgraded served station speeds up growth");
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailUpgrade-"+Guid.NewGuid()),new Codec(),g.Balance);
        var copy=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));Assert(copy.stations.Find(x=>x.id==a).level==1,"Station type survives a save");
        s.level=7;bool bad=false;try{saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));}catch{bad=true;}Assert(bad,"Impossible station type rejected on load");s.level=1;
        Assert(!g.Upgrades.Plan(a,0,4,3).valid&&!g.Upgrades.Plan(a,1,4,2).valid,"Upgrades never shrink or downgrade");
        // A freight yard at Pinecrest Mine: the widest station fells the pines in its way and charges for each.
        var m=New();m.World.money=200000;var sp=m.Stations.Plan(new Cell(7,12),0,3,1,1);int mine=OK(m.Stations.Place(sp,1));
        Assert(StationCatalog.MaxLevel(ProducerKind.Mine)==2&&StationCatalog.Name(ProducerKind.Mine,2)=="Freight yard","Industry ladder");
        var yard=m.Upgrades.Plan(mine,2,6,4);Assert(yard.valid&&yard.trees.Count==2&&yard.buildings.Count==0,"Freight yard plan fells trees: "+yard.reason);
        var pines=new List<Cell>(yard.trees);money=m.World.money;OK(m.Upgrades.Apply(mine,2,6,4));
        foreach(var c in pines)Assert(!m.Scenery.TreeAt(c),"Pine felled at "+c);
        Assert(m.World.money==money-yard.cost&&m.Upgrades.Last.trees.Count==2&&m.Upgrades.FullyUpgraded(m.Trains.Station(mine)),"Freight yard built and fully upgraded");
        Assert(m.Upgrades.Recommend(mine).reason.Contains("already as big"),"Nothing left to recommend");
        CheckDowntownUpgrade();
    }
    static void CheckDowntownUpgrade()
    {
        // The player's downtown Sunvale halt, hemmed in by streets and apartment towers, can always grow: the bulldozer takes
        // up the streets too, and every skyscraper costs its storeys. Only the town square, highways and the like refuse.
        var town=LoadFixture("phone-downtown.json");town.World.money=10000000;
        var sunvale=town.World.stations.Find(x=>x.cell.Equals(new Cell(75,29)));
        var grand=town.Upgrades.Plan(sunvale.id,3,6,4);int towers=0;
        foreach(var bs in grand.buildings)if(BuildingCatalog.IsSkyscraper(bs.def)){int price=town.Cities.DemolitionCost(bs);towers+=price;Assert(price>=60000,"A skyscraper costs its storeys: $"+price);}
        Assert(grand.valid&&grand.skyscrapers>=2&&grand.streets.Count>0&&grand.skyscraperCost==towers,"Downtown grand terminal clears towers and streets: "+grand.reason);
        Assert(grand.cost==grand.stationPrice+grand.clearingCost+grand.trackCost&&grand.clearingCost>=towers+grand.streets.Count*town.Balance.city.streetClearCost,"Clearing is priced per tower and street cell");
        string clears=StationUpgradeService.Clearing(grand);Assert(clears.Contains("skyscrapers")&&clears.Contains("street cell"),"The panel names towers and streets: "+clears);
        foreach(var s in town.World.stations)
        {
            var kind=town.World.producers.Find(p=>p.id==s.producerId).kind;
            for(int level=s.level;level<=StationCatalog.MaxLevel(kind);level++)for(int len=StationLayout.Length(s);len<=StationLayout.MaxLength;len++)for(int p=StationLayout.Platforms(s);p<=StationLayout.MaxPlatforms;p++)
                if(StationCatalog.Fits(level,len,p)){var plan=town.Upgrades.Plan(s.id,level,len,p);Assert(plan.valid||!plan.reason.Contains("town street"),"Streets never block an upgrade: "+plan.reason);}
        }
        bool square=false;var frost=town.World.stations.Find(x=>x.cell.Equals(new Cell(104,89)));
        for(int len=3;len<=6;len++)square|=town.Upgrades.Plan(frost.id,0,len,4).reason.StartsWith("The town square");
        Assert(square,"The town square is never bulldozed");
        int money=town.World.money;OK(town.Upgrades.Apply(sunvale.id,3,6,4));
        Assert(town.World.money==money-grand.cost&&sunvale.level==3&&StationLayout.Platforms(sunvale)==4,"The downtown upgrade charges its quote: "+(money-town.World.money)+" vs "+grand.cost);
        foreach(var c in grand.streets)Assert(!town.Cities.IsStreet(c)&&town.Upgrades.Last.cleared.Contains(c),"Street taken up at "+c);
        SaveService.Validate(town.World,town.Balance);
        // After the demolition cooldown the town still keeps its streets and buildings off the new station.
        for(int i=0;i<7000;i++)town.Step();
        foreach(var c in grand.streets)Assert(!town.Cities.IsStreet(c),"The town never re-lays a cleared street across the station at "+c);
        foreach(var c in StationLayout.Strip(sunvale))Assert(!town.Cities.Occupied(c),"Station building strip stays clear at "+c);
        SaveService.Validate(town.World,town.Balance);
        // The bulldozer tool charges the same tower price, so knocking towers down first is no cheaper.
        var lake=town.World.cities.Find(c=>c.buildings.Exists(bs=>BuildingCatalog.IsSkyscraper(bs.def)));
        var tower=lake.buildings.Find(bs=>BuildingCatalog.IsSkyscraper(bs.def));int towerPrice=town.Cities.DemolitionCost(tower);
        money=town.World.money;OK(town.Build.Bulldoze(CityLayout.FootprintCell(tower,0)));
        Assert(money-town.World.money==towerPrice&&towerPrice==town.Balance.city.demolitionCost*4+town.Balance.city.skyscraperStoreyCost*BuildingCatalog.Storeys(tower.def),"Bulldozing a tower costs its storeys: $"+(money-town.World.money));
    }
    static void CheckBridges()
    {
        var g=New();var w=g.World;var bal=g.Balance;
        Assert(BridgeCatalog.StyleAt(w,15)==BridgeCatalog.StoneArch&&BridgeCatalog.StyleAt(w,46)==BridgeCatalog.SteelTruss&&BridgeCatalog.StyleAt(w,78)==BridgeCatalog.Suspension&&BridgeCatalog.StyleAt(w,110)==BridgeCatalog.SteelArch,"Each bridge site has its own default style");
        Assert(BridgeCatalog.Cost(bal,BridgeCatalog.StoneArch)==bal.bridgeCost,"The stone arch keeps the original bridge price");
        var prices=new HashSet<int>();for(int s=0;s<BridgeCatalog.Count;s++){prices.Add(BridgeCatalog.Cost(bal,s));Assert(BridgeCatalog.Name(s).Length>0&&BridgeCatalog.Blurb(s).Length>0,"Every style is named");}
        Assert(prices.Count==BridgeCatalog.Count,"Every style has its own price");
        Assert(BridgeCatalog.SiteNear(new Cell(31,46))==46&&BridgeCatalog.SiteNear(new Cell(29,47))==46&&BridgeCatalog.SiteNear(new Cell(34,46))==0&&BridgeCatalog.SiteNear(new Cell(31,50))==0,"Taps on or beside a site find it");
        // A crossing is priced in the chosen style (or the site's own) and building it records the style.
        var plain=g.Build.Preview(new Cell(14,46),new Cell(50,46));Assert(plain.valid&&plain.bridgeStyle==-1,plain.reason);
        var timber=g.Build.Preview(new Cell(14,46),new Cell(50,46),BridgeCatalog.Timber);Assert(timber.valid&&timber.bridgeStyle==BridgeCatalog.Timber,timber.reason);
        Assert(plain.cost-timber.cost==BridgeCatalog.Cost(bal,BridgeCatalog.SteelTruss)-BridgeCatalog.Cost(bal,BridgeCatalog.Timber),"The chosen style sets the bridge price");
        int money=w.money;OK(g.Build.CommitBuild(timber));
        Assert(money-w.money==timber.cost&&BridgeCatalog.StyleAt(w,46)==BridgeCatalog.Timber&&w.bridgeStyles.Count==1,"Building a crossing records its style");
        Assert(w.tracks.FindAll(t=>t.bridge==46).Count==3&&w.tracks.Find(t=>t.cell.Equals(new Cell(30,46))).paid==BridgeCatalog.Cost(bal,BridgeCatalog.Timber),"The span carries its style's price");
        var saves=new SaveService(Path.GetTempPath(),new Codec(),bal);
        var loaded=saves.RestoreSnapshot(saves.CaptureSnapshot(w));
        Assert(BridgeCatalog.StyleAt(loaded,46)==BridgeCatalog.Timber&&BridgeCatalog.StyleAt(loaded,78)==BridgeCatalog.Suspension,"Bridge styles survive save/load");
        loaded.bridgeStyles=null;SaveService.Validate(loaded,bal);
        Assert(BridgeCatalog.StyleAt(loaded,46)==BridgeCatalog.SteelTruss,"Saves from before bridge styles show the site defaults");
        foreach(var bad in new[]{new BridgeStyleState{row=47,style=1},new BridgeStyleState{row=46,style=BridgeCatalog.Count},new BridgeStyleState{row=46,style=0}})
        {
            var copy=saves.RestoreSnapshot(saves.CaptureSnapshot(w));copy.bridgeStyles.Add(bad);bool threw=false;
            try{SaveService.Validate(copy,bal);}catch(InvalidDataException){threw=true;}
            Assert(threw,"An unknown site, style or a second entry for one site is rejected");
        }
        // Rebuilding a crossing in use: the train keeps running and the new style is charged in full.
        var r=New();Coal(r);for(int i=0;i<200;i++)r.Step();
        Assert(!r.Build.RestyleBridge(15,BridgeCatalog.StoneArch).ok,"The same style is refused");
        Assert(!r.Build.RestyleBridge(16,BridgeCatalog.Timber).ok&&!r.Build.RestyleBridge(15,BridgeCatalog.Count).ok,"An unknown site or style is refused");
        money=r.World.money;int revision=r.World.revision;OK(r.Build.RestyleBridge(15,BridgeCatalog.Suspension));
        Assert(money-r.World.money==BridgeCatalog.Cost(r.Balance,BridgeCatalog.Suspension)&&BridgeCatalog.StyleAt(r.World,15)==BridgeCatalog.Suspension&&r.World.revision>revision,"Rebuilding charges the new style and redraws");
        Assert(r.World.tracks.Find(t=>t.cell.Equals(new Cell(30,15))).paid==BridgeCatalog.Cost(r.Balance,BridgeCatalog.Suspension),"Bulldozing later refunds half the new style");
        long delivered=r.World.delivered;for(int i=0;i<1600;i++)r.Step();Assert(r.World.delivered>delivered,"Trains keep running over a rebuilt bridge");
        r.World.money=100;Assert(!r.Build.RestyleBridge(15,BridgeCatalog.Timber).ok&&BridgeCatalog.StyleAt(r.World,15)==BridgeCatalog.Suspension&&r.World.money==100,"No money, no rebuild");
        // An empty site remembers a style for free; a highway crossing is rebuilt like a railway one.
        var e=New();money=e.World.money;OK(e.Build.RestyleBridge(78,BridgeCatalog.Timber));
        Assert(e.World.money==money&&BridgeCatalog.StyleAt(e.World,78)==BridgeCatalog.Timber,"An empty site remembers a style for free");
        var highway=new IntercityRoadState();for(int x=29;x<=33;x++)highway.path.Add(new Cell(x,110));highway.built=highway.path.Count;e.World.intercityRoads.Add(highway);
        Assert(BridgeCatalog.HasHighway(e.World,110)&&!BridgeCatalog.HasRailway(e.World,110),"A built highway counts as a crossing");
        int city=e.World.cityRevision;OK(e.Build.RestyleBridge(110,BridgeCatalog.StoneArch));
        Assert(money-e.World.money==BridgeCatalog.Cost(e.Balance,BridgeCatalog.StoneArch)&&e.World.cityRevision>city,"A highway bridge is rebuilt for the new style's price");
    }
    static void Run()
    {
        CheckSkiResorts();
        CheckStationSizes();
        CheckStationUpgrade();
        CheckBulldozer();
        CheckTrainAccounts();
        CheckWagons();
        CheckRoadLanes();
        CheckStraightHighways();
        CheckRoadside();
        CheckTownFires();
        CheckLightRail();
        CheckFounding();
        CheckBigStationGrowth();
        CheckRailFix();
        CheckCargoTransfer();
        CheckRailRepair();
        CheckIndustryGuide();
        CheckGameEvents();
        CheckCrossingSignals();
        CheckCrossingTurns();
        CheckBridges();
        CheckPowerStations();
        CheckAutoDestination();
        CheckTrainCatalog();
        CheckCityLayout();
        CheckIndustries();
        CheckExpandedMap();
        CheckDonations();
        CheckTownTrams();
        CheckServiceSales();
        CheckCampsites();
        var g=New();var route=Coal(g);Assert(g.World.money==50000-(40*100+1500)-4000-8000,"Construction accounting");
        Assert(!g.Trains.Buy(route.b,0,Cargo.Coal).ok,"Second train rejected");
        var p=g.Build.Preview(new Cell(10,12),new Cell(48,12));Assert(!p.valid,"Blocked building");
        Assert(!g.Build.Bulldoze(new Cell(25,15)).ok,"Route deletion blocked");
        Assert(!g.Build.Bulldoze(new Cell(31,15)).ok,"Bridge deletion blocked");
        int initial=g.World.money;for(int i=0;i<1200;i++)g.Step();Assert(g.World.delivered>=30,"Coal delivery");Assert(g.World.money>initial,"Coal profit");
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailChecks-"+Guid.NewGuid()),new Codec(),g.Balance);
        string json=saves.CaptureSnapshot(g.World);var restored=saves.RestoreSnapshot(json);var replay=new GameSession(restored,g.Balance);
        for(int i=0;i<1300;i++){g.Step();replay.Step();}Assert(saves.CaptureSnapshot(g.World)==saves.CaptureSnapshot(replay.World),"Exact mid-route save replay");
        bool rejected=false;try{saves.RestoreSnapshot(json.Replace("green-valley","bad-map"));}catch{rejected=true;}Assert(rejected,"Tampered save rejected");
        var paused=new SimulationClock();long tick=g.World.tick;paused.Advance(30,0,g.Step);Assert(g.World.tick==tick,"Pause does not tick");
        foreach(int speed in new[]{1,2,4}){var c=new SimulationClock();int ticks=0;for(int frame=0;frame<600/speed;frame++)c.Advance(1d/30,speed,()=>ticks++);Assert(ticks==400,"Speed scheduler");}
        var catchup=new SimulationClock();int count=0;catchup.Advance(10,4,()=>count++);while(catchup.PendingSeconds>=.049999)catchup.Advance(0,4,()=>count++);Assert(count==800,"Catchup retains ticks");
        foreach(int mask in new[]{5,10,3,6,12,9,7,11,13,14})for(int a=0;a<4;a++)for(int b=0;b<4;b++)Assert(Directions.Allows(mask,a,b)==Directions.Allows(mask,b,a),"Transition symmetry");
        Assert(!Directions.Allows(7,0,2)&&Directions.Allows(7,1,0)&&Directions.Allows(7,1,2),"Turnout common stem");
        // Two independent occupied networks must not merge, even indirectly through a branch.
        var multi=New();multi.World.money=100000;Coal(multi);Track(multi,new Cell(14,46),new Cell(50,46));int pa=OK(multi.Stations.Place(new Cell(16,46),4)),pb=OK(multi.Stations.Place(new Cell(48,46),5));int passenger=OK(multi.Trains.Buy(pa,2,Cargo.Passengers));OK(multi.Trains.AssignRoute(passenger,pa,pb));
        var merge=multi.Build.Preview(new Cell(23,15),new Cell(23,46));Assert(!merge.valid,"Occupied network merge rejected");
        for(int i=0;i<1200;i++)multi.Step();Assert(multi.World.trains.Find(t=>t.id==passenger).units>0,"Passengers loaded on return trip");Assert(multi.World.delivered>=70,"Passenger deliveries counted");
        // Goods curve route.
        var goods=New();var cells=new List<Cell>();for(int z=31;z<=40;z++)cells.Add(new Cell(12,z));for(int x=13;x<=18;x++)cells.Add(new Cell(x,40));OK(goods.Build.CommitBuild(goods.Build.ValidateBuild(cells)));
        int ga=OK(goods.Stations.Place(new Cell(12,32),3)),gb=OK(goods.Stations.Place(new Cell(16,40),4));int gt=OK(goods.Trains.Buy(ga,1,Cargo.Goods));OK(goods.Trains.AssignRoute(gt,ga,gb));initial=goods.World.money;for(int i=0;i<1200;i++)goods.Step();Assert(goods.World.delivered>=45&&goods.World.money>initial,"Goods route profitable");
        // Stop, unload, clear, then permit unrelated edits and forbid occupied platform removal.
        OK(goods.Trains.ReturnToStation(gt));for(int i=0;i<1200&&goods.Trains.Train(gt).state!=ServiceState.Parked;i++)goods.Step();var train=goods.Trains.Train(gt);
        if(train.units>0){OK(goods.Trains.Resume(gt));OK(goods.Trains.ReturnToStation(gt));for(int i=0;i<1200&&train.state!=ServiceState.Parked;i++)goods.Step();}
        Assert(train.units==0,"Return unloads without reloading");OK(goods.Trains.ClearRoute(gt));Assert(!goods.Build.Bulldoze(goods.Trains.Station(train.stationId).cell).ok,"Occupied station protected");OK(goods.Trains.Sell(gt));
        var bridge=New();Track(bridge,new Cell(28,15),new Cell(34,15));int before=bridge.World.money;OK(bridge.Build.Bulldoze(new Cell(31,15)));Assert(bridge.Network.At(new Cell(30,15))==null&&bridge.Network.At(new Cell(32,15))==null,"Bridge removed atomically");Assert(bridge.World.money==before+750,"Bridge refund once");
        var broke=New();var br=Coal(broke);broke.World.money=0;broke.Step();Assert(broke.Trains.Train(br.train).state==ServiceState.InsufficientFunds,"Zero funds stop services");OK(broke.Trains.Sell(br.train));Assert(broke.World.money==4000,"Sale recovers insolvency");
        var duplicate=New();duplicate.World.producers[1].id=1;rejected=false;try{SaveService.Validate(duplicate.World,duplicate.Balance);}catch{rejected=true;}Assert(rejected,"Duplicate IDs rejected");
        var returning=New();var rr=Coal(returning);OK(returning.Trains.ReturnToStation(rr.train));
        for(int i=0;i<1200&&returning.Trains.Train(rr.train).state!=ServiceState.Parked;i++)returning.Step();
        Assert(returning.Trains.Train(rr.train).units==0&&returning.Trains.Train(rr.train).state==ServiceState.Parked,"Return requested during loading completes delivery");OK(returning.Trains.ClearRoute(rr.train));
        // Thirty minutes of authoritative simulation: no discarded cargo/state and no hot-loop allocations.
        var soak=New();Coal(soak);for(int i=0;i<2400;i++)soak.Step();
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<36000;i++)soak.Step();watch.Stop();long allocation=GC.GetAllocatedBytesForCurrentThread()-allocated;
        SaveService.Validate(soak.World,soak.Balance);Assert(soak.World.delivered>500,"Thirty-minute delivery soak");
        // Opening a highway stores its path once; nothing else in the tick loop may allocate.
        long highwayBytes=0;foreach(var road in soak.World.intercityRoads)highwayBytes+=16L*road.path.Count+256;
        Assert(allocation<1024+highwayBytes,"Simulation hot loop must not allocate per tick: "+allocation);
        Console.WriteLine($"30 simulated minutes / 1 active train: {watch.Elapsed.TotalMilliseconds:F2} ms, {allocation} bytes (includes stopwatch)");
        // Maximum supported object counts, using independent station sidings plus spare railway.
        var maximum=New();maximum.World.money=400000;
        // Keep this capacity fixture on its original seven-industry map.
        maximum.World.mapVersion=3;maximum.World.producers.RemoveAll(p=>p.id>7);maximum.World.cities.RemoveAll(c=>c.producerId>7);
        foreach(int producerId in new[]{1,3,4})
        {
            var producer=maximum.Cargo.Producer(producerId);
            for(int side=0;side<4;side++)
            {
                var center=producer.cell.Move(side).Move(side).Move(side);int axis=side%2==0?1:0;
                var start=new Cell(center.x-(axis==1?1:0),center.z-(axis==0?1:0));var end=new Cell(center.x+(axis==1?1:0),center.z+(axis==0?1:0));
                Track(maximum,start,end);int station=OK(maximum.Stations.Place(center,producerId));OK(maximum.Trains.Buy(station,producerId==4?2:0,producerId==4?Cargo.Passengers:Cargo.Coal));
            }
        }
        for(int z=0;z<64&&maximum.World.tracks.Count<1500;z++)
        {
            if((z>=11&&z<=13)||(z>=42&&z<=44))continue;int length=Math.Min(z<7?20:28,1500-maximum.World.tracks.Count);if(length<2)break;
            Track(maximum,new Cell(35,z),new Cell(35+length-1,z));
        }
        Assert(maximum.World.tracks.Count==1500&&maximum.World.trains.Count==12,"Maximum scope fixture");
        SaveService.Validate(maximum.World,maximum.Balance);var limitPlan=maximum.Build.Preview(new Cell(1,1),new Cell(2,1));Assert(!limitPlan.valid,"Track cap enforced");
        for(int i=0;i<36000;i++)maximum.Step();SaveService.Validate(maximum.World,maximum.Balance);
        // Construction and purchases are capital: they must not appear as a per-minute expense. Running costs must.
        var ledger=New();var lr=Coal(ledger);EconomyService.Recent(ledger.World,out int income0,out int expense0);Assert(income0==0&&expense0==0,"Capital spending is not a running expense");
        for(int i=0;i<1200;i++)ledger.Step();EconomyService.Recent(ledger.World,out int income1,out int expense1);Assert(expense1==ledger.Balance.runningCost[0]&&income1>0,"Running costs and income enter the ledger: "+expense1);
        // Fleet numbers are player-facing and sequential, independent of the shared id counter.
        Assert(ledger.Trains.Train(lr.train).number==1&&lr.train>1,"First train is fleet number 1");
        // Both route legs protect their tracks; the legs swap at every stop.
        var only=ledger.Trains.Train(lr.train);var forward=only.path;only.path=new List<RailStep>();
        Assert(!ledger.Build.Bulldoze(new Cell(25,15)).ok,"Return leg protected from bulldozing");
        var branch=ledger.Build.Preview(new Cell(25,15),new Cell(25,18));Assert(!branch.valid&&branch.reason.StartsWith("Park"),"Return leg protected from junction conversion");
        only.path=forward;
        // The guided steps follow world state, fast-forward through completed work, and end after the first deliveries.
        var tut=New();Assert(tut.World.tutorialStep==1&&Tutorial.Text(tut.World).StartsWith("1/5"),"Tutorial starts at step 1");
        Coal(tut);Tutorial.Advance(tut.World);Assert(tut.World.tutorialStep==5,"Tutorial fast-forwards through completed steps: "+tut.World.tutorialStep);
        for(int i=0;i<1200;i++)tut.Step();Tutorial.Advance(tut.World);Assert(tut.World.tutorialStep==6&&Tutorial.Text(tut.World).StartsWith("Delivered"),"First delivery completes the guided steps");
        tut.World.delivered=100;Tutorial.Advance(tut.World);Assert(tut.World.tutorialStep==0&&Tutorial.Text(tut.World)==null,"Tutorial ends and has no text");
        // Milestone 5 acceptance: invalid station placements and route assignments are refused without side effects.
        var st=New();Track(st,new Cell(8,15),new Cell(50,15));OK(st.Build.CommitBuild(st.Build.ValidateBuild(new List<Cell>{new Cell(6,13),new Cell(7,13),new Cell(7,14),new Cell(7,15)})));int stMoney=st.World.money;
        Assert(!st.Stations.Place(new Cell(31,15),2).ok,"Station on a bridge rejected");
        Assert(!st.Stations.Place(new Cell(25,15),1).ok,"Station outside catchment rejected");
        Assert(!st.Stations.Place(new Cell(9,20),1).ok,"Station without track rejected");
        Assert(!st.Stations.Place(new Cell(7,13),1).ok,"Station on a curve rejected");
        Assert(st.World.money==stMoney&&st.World.stations.Count==0,"Rejected stations leave state unchanged");
        int sa=OK(st.Stations.Place(new Cell(10,15),1)),sb=OK(st.Stations.Place(new Cell(48,15),2));int stTrain=OK(st.Trains.Buy(sa,0,Cargo.Coal));
        Assert(!st.Trains.AssignRoute(stTrain,sa,sa).ok,"Same-stop route rejected");
        Track(st,new Cell(8,9),new Cell(14,9));int sc=OK(st.Stations.Place(new Cell(10,9),1));
        Assert(!st.Trains.AssignRoute(stTrain,sa,sc).ok,"Route between two mine stations rejected");
        OK(st.Trains.AssignRoute(stTrain,sa,sb));Assert(!st.Trains.AssignRoute(stTrain,sa,sb).ok,"Route change while loading rejected");
        var pax=New();pax.World.money=100000;Track(pax,new Cell(14,46),new Cell(20,46));Track(pax,new Cell(46,46),new Cell(50,46));int pa1=OK(pax.Stations.Place(new Cell(16,46),4)),pb1=OK(pax.Stations.Place(new Cell(48,46),5));int pt=OK(pax.Trains.Buy(pa1,2,Cargo.Passengers));
        Assert(!pax.Trains.AssignRoute(pt,pa1,pb1).ok,"Unreachable stop rejected");
        Track(pax,new Cell(20,46),new Cell(46,46));OK(pax.Trains.AssignRoute(pt,pa1,pb1));int paxMoney=pax.World.money;for(int i=0;i<2400;i++)pax.Step();Assert(pax.World.money>paxMoney&&pax.World.delivered>0,"Passenger route profitable");
        // Milestone 6/7: cargo is conserved and payout follows producer distance, not track length.
        var cons=New();Coal(cons);for(int i=0;i<1200;i++)cons.Step();Assert(cons.Cargo.Producer(1).inventory+cons.World.trains[0].units+cons.World.delivered==80,"Cargo conserved (60 initial + 20 produced)");
        var pay=New();Coal(pay);for(int i=0;i<1200&&pay.World.delivered==0;i++)pay.Step();Assert(pay.World.delivered==30&&pay.World.totalIncome==30*pay.Balance.rate[0]*38,"Payout is units × rate × producer distance, independent of track length");
        // Milestone 9: manual and autosave slots are independent files; structural corruption is rejected branch by branch.
        var slots=New();string slotDir=Path.Combine(Path.GetTempPath(),"ValleyRailSlots-"+Guid.NewGuid());var slotSaves=new SaveService(slotDir,new Codec(),slots.Balance);
        slotSaves.Save(slots.World,false);slots.Step();slots.Step();slotSaves.Save(slots.World,true);Assert(slotSaves.Load(false).tick==0&&slotSaves.Load(true).tick==2,"Manual and autosave slots are independent");Directory.Delete(slotDir,true);
        var vb=New();Coal(vb);for(int i=0;i<100;i++)vb.Step();var vcodec=new Codec();
        Func<Action<WorldState>,bool> rejects=mutate=>{var copy=vcodec.Decode<WorldState>(vcodec.Encode(vb.World));mutate(copy);try{SaveService.Validate(copy,vb.Balance);return false;}catch(InvalidDataException){return true;}};
        Assert(rejects(w=>w.tracks[5].mask=16),"Out-of-range port mask rejected");
        Assert(rejects(w=>w.tracks.RemoveAll(t=>t.cell.x==31&&t.cell.z==15)),"Incomplete bridge rejected");
        Assert(rejects(w=>w.stations[0].cell=new Cell(25,15)),"Station outside catchment rejected on load");
        Assert(rejects(w=>{var extra=vcodec.Decode<TrainState>(vcodec.Encode(w.trains[0]));extra.id=w.nextId++;w.trains.Add(extra);}),"Second train in one network rejected on load");
        Assert(rejects(w=>w.trains[0].path[2].trackId=w.trains[0].path[4].trackId),"Disconnected route rejected on load");
        Assert(rejects(w=>w.trains[0].units=999),"Over-capacity cargo rejected on load");
        Assert(rejects(w=>w.cameraTurn=4)&&rejects(w=>w.cameraTurn=-1),"Out-of-range camera turn rejected on load");
        Assert(!rejects(w=>w.cameraTurn=3),"Quarter-turned camera validates");
        Assert(!rejects(w=>{}),"Untouched snapshot still validates");
        // Command execution must not depend on an event subscriber.
        var command=New();command.Enqueue(new BuildCommand(command.Build.Preview(new Cell(4,10),new Cell(8,10))));command.FlushCommands();Assert(command.World.tracks.Count==5,"Commands work without listeners");
        // City growth: towns start as 420-population villages producing 20 passengers/min; a passenger service grows them far faster than isolation.
        var served=New();served.World.money=100000;Track(served,new Cell(14,46),new Cell(50,46));int ca=OK(served.Stations.Place(new Cell(16,46),4)),cz=OK(served.Stations.Place(new Cell(48,46),5));int ct=OK(served.Trains.Buy(ca,2,Cargo.Passengers));OK(served.Trains.AssignRoute(ct,ca,cz));
        var idle=New();var willow=served.Cities.CityFor(4);Assert(willow!=null&&willow.population==420&&willow.level==CityLevel.SmallVillage&&willow.buildings.Count==6&&willow.roads.Count==3&&served.Cargo.Producer(4).production==20&&served.Cargo.Producer(4).storage==252,"Town starts at 420 population, 20 passengers/min");
        for(int i=0;i<12000;i++){served.Step();idle.Step();}
        int grown=willow.buildings.Count+willow.roads.Count-9,still=idle.Cities.CityFor(4).buildings.Count+idle.Cities.CityFor(4).roads.Count-9;
        Assert(grown>=8&&willow.buildings.Count>=11&&grown>=4*Math.Max(1,still),$"Served town grows faster than an isolated one: {grown} actions vs {still}");
        Assert(willow.level>=CityLevel.Village&&willow.roads.Count>=4&&served.Cargo.Producer(4).production>20,"Service raises the level, lays streets and increases passenger production");
        Assert(served.Cities.Notifications.Count>0,"Level changes notify");
        SaveService.Validate(served.World,served.Balance);SaveService.Validate(idle.World,idle.Balance);
        string cityJson=saves.CaptureSnapshot(served.World);var cityReplay=new GameSession(saves.RestoreSnapshot(cityJson),served.Balance);
        for(int i=0;i<6000;i++){served.Step();cityReplay.Step();}Assert(saves.CaptureSnapshot(served.World)==saves.CaptureSnapshot(cityReplay.World),"City growth replays exactly from a snapshot");
        foreach(var city in served.World.cities)foreach(var bs in city.buildings)Assert(served.Network.At(bs.cell)==null&&!MapDefinition.Water(bs.cell)&&Math.Abs(bs.cell.x-city.center.x)<=13&&Math.Abs(bs.cell.z-city.center.z)<=13,"Buildings avoid tracks and water and stay near the town");
        // Intercity construction is gradual, persistent, and diverts only competing roadPassengers.
        var roads = New();
        for (int i = 0; i < 400; i++) roads.Step();
        Assert(roads.World.intercityRoads.Count == 0, "Small villages do not start highways");
        foreach (int id in new[] { 4, 5 })
        {
            var town = roads.Cities.CityFor(id);
            var houseState = town.buildings[0]; houseState.def = 2; town.buildings[0] = houseState;
            CitySimulation.Recount(town, roads.Cargo.Producer(id), roads.Balance);
        }
        for (int i = 0; i < 200; i++) roads.Step();
        var highway = roads.Cities.RoadBetween(4, 5);
        Assert(highway != null && highway.built == 1 && !highway.Complete, "Growing towns begin one road section at a time");
        var roadReplay = new GameSession(saves.RestoreSnapshot(saves.CaptureSnapshot(roads.World)), roads.Balance);
        var roadPassenger = new TrainState { model = 2, cargo = Cargo.Passengers };
        var source = new StationState { producerId = 4 }; var target = new StationState { producerId = 5 };
        roads.Cargo.Producer(4).inventory = 40; roads.Cargo.Service(roadPassenger, source, target);
        Assert(roadPassenger.units == 40, "Unfinished roads do not reduce rail roadPassengers");
        // Resume the identical saved state for deterministic construction checks.
        roads = new GameSession(saves.RestoreSnapshot(saves.CaptureSnapshot(roadReplay.World)), roads.Balance);
        for (int i = 0; i < 16000; i++) { roads.Step(); roadReplay.Step(); }
        Assert(roads.Cities.RoadBetween(4, 5).Complete, "Road reaches the other town");
        Assert(saves.CaptureSnapshot(roads.World) == saves.CaptureSnapshot(roadReplay.World), "Road progress replays after saving");
        roadPassenger.units = 0; roads.Cargo.Producer(4).inventory = 40;
        roads.Cargo.Service(roadPassenger, source, target);
        Assert(roadPassenger.units == 26 && roads.Cargo.Producer(4).inventory == 0, "Completed road diverts 35 percent of travelers");
        roadPassenger.units = 0; roads.Cargo.Producer(4).inventory = 40;
        roads.Cargo.Service(roadPassenger, source, new StationState { producerId = 14 });
        Assert(roadPassenger.units == 40, "Unlinked destinations retain full roadPassenger demand");
        SaveService.Validate(roads.World, roads.Balance);
        // Legacy map-version-1 saves without cities migrate to the day-0 layout.
        var legacy=vcodec.Decode<WorldState>(vcodec.Encode(New().World));legacy.cities.Clear();legacy.producers.RemoveAll(p=>p.id>5);legacy.mapVersion=1;foreach(var lp in legacy.producers){lp.production=0;lp.storage=0;}
        string legacyPayload=vcodec.Encode(legacy);var migrated=saves.RestoreSnapshot(vcodec.Encode(new SaveEnvelope{payload=legacyPayload,checksum=SaveService.Hash(legacyPayload)}));
        Assert(migrated.mapVersion==2&&migrated.cities.Count==2&&migrated.cities[0].population==420&&migrated.producers[3].production==20,"Legacy save gains day-0 cities on load");
        // Demolition costs money, frees the cell for track, and a house blocks track until then.
        var bull=New();var house=bull.Cities.CityFor(4).buildings[0].cell;Assert(!bull.Build.ValidateBuild(new List<Cell>{house.Move(3),house,house.Move(1)}).valid,"Track through a house rejected");
        int bullMoney=bull.World.money;OK(bull.Build.Bulldoze(house));Assert(bull.World.money==bullMoney-500&&bull.Cities.CityFor(4).population==380&&bull.Cargo.Producer(4).production==19,"Demolition costs $500 per level and removes 40 residents");
        Assert(bull.Build.ValidateBuild(new List<Cell>{house,house.Move(0)}).valid,"Cleared cell accepts track");
        for(int i=0;i<3600;i++)bull.Step();Assert(!bull.Cities.HasBuilding(house),"Town respects the demolition cooldown");
        // Town stations still bind within three cells of the plaza, and the station strip cannot cover a house.
        var bind=New();Track(bind,new Cell(14,46),new Cell(20,46));Assert(bind.Stations.Nearby(new Cell(16,46),1).Exists(np=>np.id==4),"Catchment reaches the town");
    }
}
