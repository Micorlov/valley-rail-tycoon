using System;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static void CheckCargoTransfer()
    {
        // A town holds freight it does not use; town to town is never a freight route.
        Assert(CargoTransfer.Holds(ProducerKind.Town,Cargo.Oil)&&!CargoTransfer.Holds(ProducerKind.Town,Cargo.Goods)&&!CargoTransfer.Holds(ProducerKind.Town,Cargo.Passengers)&&!CargoTransfer.Holds(ProducerKind.Refinery,Cargo.Oil),"Only towns hold freight they do not use");
        Assert(CargoTransfer.Feeds(ProducerKind.OilWells,ProducerKind.Town,Cargo.Oil)&&CargoTransfer.Delivers(ProducerKind.Town,ProducerKind.Refinery,Cargo.Oil),"Wells feed a city and the city forwards to a refinery");
        Assert(!CargoTransfer.Delivers(ProducerKind.Town,ProducerKind.Town,Cargo.Oil)&&!CargoTransfer.Feeds(ProducerKind.Town,ProducerKind.Town,Cargo.Oil),"Oil never runs town to town");
        Assert(CargoTransfer.Delivers(ProducerKind.Town,ProducerKind.Town,Cargo.Passengers)&&!CargoTransfer.Feeds(ProducerKind.Town,ProducerKind.Town,Cargo.Passengers),"Passenger routes are unchanged");
        CheckTransferLegs();
        CheckTransferFix();
        CheckNewStationFix();
    }
    /// <summary>Eastbank Oil Wells (10) → Oakridge (5) → Riverside Refinery (11), one Service call per stop.</summary>
    static void CheckTransferLegs()
    {
        var g=New();var wells=g.Cargo.Producer(10);var town=g.Cargo.Producer(5);var refinery=g.Cargo.Producer(11);
        var atWells=new StationState{producerId=10};var atTown=new StationState{producerId=5};var atRefinery=new StationState{producerId=11};
        Assert(g.Trains.CanDeliver(atWells,atTown,Cargo.Oil)&&g.Trains.CanDeliver(atTown,atRefinery,Cargo.Oil)&&!g.Trains.Delivers(atWells,atTown,Cargo.Oil)&&g.Trains.Delivers(atTown,atRefinery,Cargo.Oil),"Stations follow the transfer rules");
        Assert(!g.Trains.CanDeliver(atTown,new StationState{producerId=4},Cargo.Oil),"Two city stations never form an oil route");
        wells.inventory=200;
        var feeder=new TrainState{model=0,cargo=Cargo.Oil};
        g.Cargo.Service(feeder,atWells,atTown);
        Assert(feeder.units==30&&feeder.cargoDestination==5,"Oil boards for the city");
        int rate=g.Balance.CargoRate(Cargo.Oil),first=wells.cell.Distance(town.cell),whole=wells.cell.Distance(refinery.cell);
        int money=g.World.money;long delivered=g.World.delivered;
        g.Cargo.Service(feeder,atTown,atWells);
        int share=30*rate*first*CargoTransfer.Share/100;
        Assert(feeder.units==0&&feeder.prepaid==0&&CargoTransfer.Held(town,Cargo.Oil)==30&&g.World.money==money+share&&g.World.delivered==delivered,"The city holds the oil and pays half the first leg: "+(g.World.money-money));
        Assert(town.inventory==40,"Held oil is not counted as waiting passengers");
        var onward=new TrainState{model=1,cargo=Cargo.Oil};
        g.Cargo.Service(onward,atTown,atRefinery);
        Assert(onward.units==30&&onward.origin==10&&onward.prepaid==share&&CargoTransfer.Held(town,Cargo.Oil)==0,"A second train takes the oil on with its credit");
        money=g.World.money;
        g.Cargo.Service(onward,atRefinery,atTown);
        Assert(onward.units==0&&onward.prepaid==0&&refinery.inventory==30&&g.World.money==money+Math.Max(0,30*rate*whole-share)&&g.World.delivered==delivered+30,"Final delivery pays the rest of the whole fare");
        Assert(onward.units==0,"Nothing boards at the refinery for the city");
        // Partial pickups split the credit, and the city's stock is capped at storage.
        CargoTransfer.Store(town,Cargo.Oil,10,40,g.Balance.storage,out var stock);stock.credit=400;
        var small=new TrainState{model=0,cargo=Cargo.Oil};g.Cargo.Service(small,atTown,atRefinery);
        Assert(small.units==30&&small.prepaid==300&&stock.units==10&&stock.credit==100,"A partial pickup takes its share of the credit");
        Assert(CargoTransfer.Store(town,Cargo.Oil,10,500,g.Balance.storage,out _)==g.Balance.storage-10&&CargoTransfer.Held(town,Cargo.Oil)==g.Balance.storage,"City stock is capped at storage");
        Assert(CargoTransfer.Describe(town)==$"{g.Balance.storage} oil"&&CargoTransfer.Describe(wells)==null,"Describe lists held freight");
        Assert(CargoTransfer.Valid(town,g.World.producers,g.Balance.storage),"Stored stock is valid");
        town.transfers[0].units=-1;Assert(!CargoTransfer.Valid(town,g.World.producers,g.Balance.storage),"Negative stock is refused");
        wells.transfers.Add(new TransferStock{cargo=Cargo.Oil,origin=10,units=1});Assert(!CargoTransfer.Valid(wells,g.World.producers,g.Balance.storage),"Only towns hold stock");
    }
    /// <summary>Oil wells station plus an Oakridge station and no refinery station: the AI makes Oakridge a transfer stop.</summary>
    static void CheckTransferFix()
    {
        var g=New();g.World.money=500000;
        var wc=new Cell(39,10);Track(g,wc.Move(3),wc.Move(1));int ws=OK(g.Stations.Place(wc,10));
        Track(g,new Cell(46,46),new Cell(50,46));int ts=OK(g.Stations.Place(new Cell(48,46),5));
        int oil=OK(g.Trains.Buy(ws,0,Cargo.Oil));
        var fix=g.Fixer.Plan(oil);
        Assert(fix.valid&&fix.transfer&&fix.toStationId==ts&&fix.newStation==null&&fix.cost==fix.build.cost&&fix.reason.Contains("transfer"),"With no refinery station the AI routes oil to a city transfer station: "+fix.reason);
        OK(g.Fixer.Apply(oil,fix));
        var town=g.Cargo.Producer(5);var train=g.Trains.Train(oil);
        var saves=new SaveService(Path.GetTempPath(),new Codec(),g.Balance);
        bool savedLoaded=false;
        for(int i=0;i<8000&&CargoTransfer.Held(town,Cargo.Oil)==0;i++)
        {
            g.Step();
            // Mid-route with oil aboard for the city: the manifest must validate.
            if(!savedLoaded&&train.units>0&&train.state==ServiceState.Travelling&&train.cargoDestination==5){SaveService.Validate(g.World,g.Balance);savedLoaded=true;}
        }
        Assert(savedLoaded&&CargoTransfer.Held(town,Cargo.Oil)>0,"The oil train fills Oakridge's transfer stock");
        SaveService.Validate(g.World,g.Balance);
        var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
        Assert(CargoTransfer.Held(restored.producers.Find(p=>p.id==5),Cargo.Oil)==CargoTransfer.Held(town,Cargo.Oil),"Transfer stock survives save/load");
    }
    /// <summary>Only an oil wells station: the AI builds a station at Riverside Refinery and the route to it.</summary>
    static void CheckNewStationFix()
    {
        var g=New();g.World.money=500000;
        var wc=new Cell(39,10);Track(g,wc.Move(3),wc.Move(1));int ws=OK(g.Stations.Place(wc,10));
        int oil=OK(g.Trains.Buy(ws,0,Cargo.Oil));
        int stations=g.World.stations.Count,tracks=g.World.tracks.Count,next=g.World.nextId;
        var watch=System.Diagnostics.Stopwatch.StartNew();var fix=g.Fixer.Plan(oil);watch.Stop();
        Console.WriteLine($"AI new-station plan: {watch.Elapsed.TotalMilliseconds:F0} ms · {fix.reason.Replace("\n"," | ")}");
        Assert(fix.valid&&fix.newStation!=null&&fix.newStation.producerId==11&&fix.cost==fix.build.cost+fix.newStation.cost&&fix.reason.Contains("Riverside Refinery"),"With no stop at all the AI plans a refinery station: "+fix.reason);
        Assert(g.World.stations.Count==stations&&g.World.tracks.Count==tracks&&g.World.nextId==next,"Planning a new station changes nothing live");
        var stale=g.Fixer.Plan(oil);stale.cost++;
        Assert(!g.Fixer.Apply(oil,stale).ok&&g.World.stations.Count==stations&&g.World.tracks.Count==tracks,"A changed station quote is refused and builds nothing");
        g.World.money=fix.cost-1;
        var poor=g.Fixer.Plan(oil);
        Assert(!poor.valid&&poor.newStation!=null&&poor.reason.Contains("costs"),"An unaffordable new station is quoted: "+poor.reason);
        g.World.money=500000;
        int before=g.World.money;
        OK(g.Fixer.Apply(oil,g.Fixer.Plan(oil)));
        var built=g.World.stations[g.World.stations.Count-1];var train=g.Trains.Train(oil);
        Assert(g.World.stations.Count==stations+1&&built.producerId==11&&g.World.money==before-fix.cost,"AI builds the station and its route for exactly the quote");
        Assert(train.b==built.id&&train.state==ServiceState.Loading,"The train runs to the new station");
        long delivered=g.World.delivered;
        for(int i=0;i<8000&&g.World.delivered==delivered;i++)g.Step();
        Assert(g.World.delivered>delivered&&g.Cargo.Producer(11).inventory>0,"Oil reaches the new refinery station");
        SaveService.Validate(g.World,g.Balance);
    }
}
