using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static int StadiumDef=>Array.FindIndex(BuildingCatalog.Defaults,d=>d.key=="stadium-bowl");
    // Oakridge gets a railway station on the z 46 line and a stadium on open ground to its east.
    static GameSession TramGame(out int station,out TramVenue stadium)
    {
        var g=New();g.World.money=500000;
        Track(g,new Cell(14,46),new Cell(50,46));
        station=OK(g.Stations.Place(new Cell(48,46),5));
        var city=g.World.cities.Find(c=>c.producerId==5);
        city.buildings.Add(new BuildingState{cell=new Cell(53,34),def=StadiumDef});
        CitySimulation.Recount(city,g.Cargo.Producer(5),g.Balance);
        g.Cities.Rebuild();
        stadium=TramVenues.All(g.World).Find(v=>v.kind==TramVenueKind.Stadium);
        return g;
    }
    static bool Rejected(GameSession g,Action<WorldState> mutate)
    {
        var codec=new Codec();var copy=codec.Decode<WorldState>(codec.Encode(g.World));mutate(copy);
        try{SaveService.Validate(copy,g.Balance);return false;}catch(InvalidDataException){return true;}
    }
    // Light rail: venues, the route planner, building, the tram simulation, the heavy-rail rules and saves.
    static void CheckLightRail()
    {
        Assert(TramCatalog.Fare(new Balance(),3)==16&&TramCatalog.Fare(new Balance(),13)==24&&TramCatalog.Fare(new Balance(),90)==64,"Tram fares grow with the line and are clamped");
        Assert(TramCatalog.TramsFor(5)==1&&TramCatalog.TramsFor(12)==3&&TramCatalog.TramsFor(64)==TramCatalog.MaxTrams,"Longer lines run more trams");
        var g=TramGame(out int stationId,out var stadium);
        var station=g.Trains.Station(stationId);
        // Venues: the stadium, and every ski resort from day 0.
        var venues=TramVenues.All(g.World);
        Assert(stadium.kind==TramVenueKind.Stadium&&stadium.open&&stadium.id==5&&stadium.name=="Oakridge Stadium","The stadium is a light rail venue");
        Assert(venues.FindAll(v=>v.kind==TramVenueKind.Ski&&v.open).Count==SkiResorts.Count,"Every ski resort is a light rail venue");
        var stations=g.Trams.Planner.Stations(stadium);
        Assert(stations.Count==1&&stations[0].id==stationId,"The town's station is offered for the stadium line");
        // The plan: forecourt start, straight ends, ends beside the stadium, clear of buildings and stations.
        var plan=g.Trams.Planner.Plan(stadium,stationId);
        Assert(plan.valid,"Stadium line plans: "+plan.reason);
        var cells=plan.cells;int n=cells.Count;
        Console.WriteLine($"Light rail to {stadium.name}: {n} cells, ${plan.cost:N0} ({plan.streetCells} street, {plan.trees.Count} trees)");
        Assert(n>=TramCatalog.MinCells&&n<=TramCatalog.MaxCells,"Line length is in range");
        int across=(cells[0].x-station.cell.x)*Directions.Dx[station.side]+(cells[0].z-station.cell.z)*Directions.Dz[station.side];
        Assert(across==2,"The transfer stop stands on the forecourt, two cells out on the building's side");
        Assert(Directions.Between(cells[0],cells[1])==Directions.Between(cells[1],cells[2])&&Directions.Between(cells[n-3],cells[n-2])==Directions.Between(cells[n-2],cells[n-1]),"Both stops are straight");
        Assert(Directions.Between(cells[0],cells[1])!=Directions.Opp(station.side),"The line leaves the forecourt away from the platforms");
        var bowl=g.World.cities.Find(c=>c.producerId==5).buildings.Find(bs=>bs.def==StadiumDef);
        bool beside=false;for(int d=0;d<4;d++)beside|=CityLayout.Covers(bowl,cells[n-1].Move(d));
        Assert(beside&&!CityLayout.Covers(bowl,cells[n-1]),"The venue stop is right beside the stadium");
        var seen=new HashSet<int>();
        for(int i=0;i<n;i++)
        {
            var c=cells[i];
            Assert(seen.Add(c.Key)&&(i==0||c.Distance(cells[i-1])==1),"The line is one connected run of cells");
            Assert(!MapDefinition.Raised(c)&&!MapDefinition.Blocked(c,g.World)&&!MapDefinition.Water(c)&&!g.Cities.BlocksTrack(c)&&!BuildService.StationFootprint(g.World,c)&&StationLayout.PlatformAt(station,c)<0,"The line keeps off hills, buildings and the station: "+c);
        }
        Assert(plan.cost==plan.trackCost+plan.stopCost+plan.clearingCost+plan.tramCost&&plan.tramCost==TramCatalog.Price&&plan.stopCost==TramCatalog.TransferStop+TramCatalog.VenueStop,"The price adds up");
        // Too little money: refused but still quoted.
        int money=g.World.money;g.World.money=100;
        var poor=g.Trams.Planner.Plan(stadium,stationId);
        Assert(!poor.valid&&poor.unaffordable&&poor.cost==plan.cost,"An unaffordable line is still priced");
        Assert(g.Trams.Planner.Plan(stadium,stationId,quote:true).valid,"A quote ignores funds");
        g.World.money=money;
        // Only town stations host a transfer stop.
        var coal=New();coal.World.money=500000;var (mineStation,_,_)=Coal(coal);
        var coalStadium=stadium;Assert(!coal.Trams.Planner.Plan(coalStadium,mineStation).valid,"A freight station cannot host a transfer stop");
        // Build.
        var trees=new List<Cell>(plan.trees);
        long incomeBefore=g.World.totalIncome;
        int lineId=OK(g.Trams.Build(plan));
        var line=g.Trams.Line(lineId);
        Assert(g.World.money==money-plan.cost,"Building charges exactly the quoted price");
        Assert(line.trams.Count==1&&line.cells.Count==n&&line.stationId==stationId&&line.venueKind==(int)TramVenueKind.Stadium,"The line opens with one tram");
        foreach(var t in trees)Assert(!g.Scenery.TreeAt(t),"Trees on the route are felled");
        foreach(var c in cells)Assert(g.Cities.HasTram(c)&&!g.Cities.BlocksTrack(c),"Tram cells are marked and still open to crossing tracks");
        Assert((g.Cities.TramMask(cells[0])&TramRules.Stop)!=0&&(g.Cities.TramMask(cells[n-1])&TramRules.Stop)!=0,"Both ends are stops");
        Assert(!g.Trams.Planner.Plan(stadium,stationId).valid,"A venue takes one line");
        Assert(TramVenues.All(g.World).Find(v=>v.kind==TramVenueKind.Stadium).lineId==lineId,"The venue knows its line");
        // Heavy rail meets light rail only at a straight crossing at right angles, away from the stops.
        int mid=-1;
        for(int i=4;i<n-4&&mid<0;i++)
        {
            int d=Directions.Between(cells[i-1],cells[i]);
            if(d!=Directions.Between(cells[i],cells[i+1]))continue;
            var side=cells[i].Move((d+1)%4);var other=cells[i].Move((d+3)%4);
            if(g.Build.Placeable(side)&&g.Build.Placeable(other)&&!g.Cities.HasTram(side)&&!g.Cities.HasTram(other)&&g.Network.At(side)==null&&g.Network.At(other)==null)mid=i;
        }
        if(mid>0)
        {
            int d=Directions.Between(cells[mid-1],cells[mid]);
            var along=g.Build.ValidateBuild(new List<Cell>{cells[mid-1],cells[mid],cells[mid+1]});
            Assert(!along.valid,"Track along a light rail line is refused");
            var over=g.Build.ValidateBuild(new List<Cell>{cells[mid].Move((d+1)%4),cells[mid],cells[mid].Move((d+3)%4)});
            Assert(over.valid,"Track straight across a light rail line is allowed: "+over.reason);
        }
        else Console.WriteLine("Light rail: no free straight cell to test a crossing on this route");
        var stopCross=g.Build.ValidateBuild(new List<Cell>{cells[0].Move((Directions.Between(cells[0],cells[1])+1)%4),cells[0],cells[0].Move((Directions.Between(cells[0],cells[1])+3)%4)});
        Assert(!stopCross.valid,"No track through a tram stop");
        // Stations and bulldozing leave the line alone.
        var onLine=cells[n/2];int axis=Directions.Between(cells[n/2-1],cells[n/2])%2==1?1:0;
        Assert(!g.Stations.Plan(onLine,axis,3,1).valid,"No station on light rail cells");
        money=g.World.money;int stationsBefore=g.World.stations.Count;
        Assert(!g.Build.Bulldoze(StationLayout.Center(station,0)).ok&&g.World.stations.Count==stationsBefore,"A station with a light rail line stays");
        Assert(!g.Build.Bulldoze(CityLayout.FootprintCell(bowl,10)).ok&&g.World.cities.Find(c=>c.producerId==5).buildings.Exists(bs=>bs.def==StadiumDef),"A stadium with a light rail line stays");
        if(g.Network.At(onLine)==null)Assert(!g.Build.Bulldoze(onLine).ok&&g.Cities.HasTram(onLine),"The bulldozer leaves tram track to the line panel");
        Assert(g.World.money==money,"Refused demolitions cost nothing");
        // Riders: a quarter of the train passengers who arrive change to the tram, remainder carried.
        int before=line.waitingOut;
        TramLines.Arrive(g.World,station,Cargo.Passengers,10);Assert(line.waitingOut==before+2&&line.transferRemainder==50,"25% of arrivals change to the tram");
        TramLines.Arrive(g.World,station,Cargo.Passengers,10);Assert(line.waitingOut==before+5&&line.transferRemainder==0,"The rounding carries over");
        TramLines.Arrive(g.World,station,Cargo.Coal,40);Assert(line.waitingOut==before+5,"Freight never boards a tram");
        // Running: fares both ways, visitors, trams add up, costs.
        g.World.cities.ForEach(c=>{});g.Balance.city.growthEnabled=false;
        var town=g.Cargo.Producer(5);
        long income0=g.World.totalIncome;
        for(int i=0;i<3600;i++)
        {
            g.Step();
            Assert(town.inventory<=town.storage&&line.waitingOut<=TramCatalog.QueueCap&&line.waitingBack<=TramCatalog.QueueCap,"Queues stay within their caps");
        }
        var profit=line.accounts.LastYear(g.World.tick);
        Assert(profit.income>0&&g.World.totalIncome-income0==profit.income,"Tram fares are income, booked to the line: "+profit.income);
        Assert(line.visitors>0||line.waitingBack>0,"Riders reach the venue");
        Assert(profit.cost==3*TramCatalog.RunningCost,"One tram costs its running cost a minute: "+profit.cost);
        SaveService.Validate(g.World,g.Balance);
        int max=TramCatalog.TramsFor(n);
        for(int k=1;k<max;k++)OK(g.Trams.AddTram(lineId));
        Assert(!g.Trams.AddTram(lineId).ok&&line.trams.Count==max,"A line runs at most one tram per four cells");
        var route=g.Trams.Route(line);
        for(int i=0;i<6000;i++)
        {
            g.Step();
            foreach(var a in line.trams)foreach(var z in line.trams)
                if(a!=z&&a.position>=0&&z.position>=0)Assert(route.Ahead(a.position,z.position)>=TramCatalog.Gap||route.Ahead(z.position,a.position)>=TramCatalog.Gap,"Trams keep a safe gap");
            if(i%1000==0)SaveService.Validate(g.World,g.Balance);
        }
        Assert(line.trams.TrueForAll(t=>t.position>=0),"Every tram entered service");
        // No money: the trams stand still.
        money=g.World.money;g.World.money=0;
        var positions=line.trams.ConvertAll(t=>t.position);
        for(int i=0;i<100;i++)g.Step();
        Assert(line.trams.ConvertAll(t=>t.position).TrueForAll(p=>positions.Contains(p)),"Trams stop without operating funds");
        Assert(g.Trams.Status(line)==TramLineStatus.NoFunds,"The line says it needs funds");
        g.World.money=money;
        // Saves: round trip, older saves, tampered lines, a missing station, replays.
        var codec=new Codec();var saves=new SaveService(Path.GetTempPath(),codec,g.Balance);
        var restored=saves.RestoreSnapshot(saves.CaptureSnapshot(g.World));
        Assert(codec.Encode(restored.tramLines)==codec.Encode(g.World.tramLines),"Light rail survives a save");
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(codec.Encode(g.World));legacy.AsObject().Remove("tramLines");
        var old=codec.Decode<WorldState>(legacy.ToJsonString());
        Assert(old.tramLines!=null&&old.tramLines.Count==0,"Older saves load with no light rail");
        Assert(Rejected(g,w=>w.tramLines[0].cells.RemoveAt(3))&&Rejected(g,w=>w.tramLines[0].trams[0].units=999)&&Rejected(g,w=>w.tramLines[0].trams[0].position=99999)
            &&Rejected(g,w=>w.tramLines[0].venueKind=9)&&Rejected(g,w=>w.tramLines[0].waitingOut=-1)&&Rejected(g,w=>w.tramLines[0].trams.Clear())
            &&Rejected(g,w=>w.tramLines[0].trams[0].id=w.tramLines[0].id)&&Rejected(g,w=>w.tramLines.Add(w.tramLines[0])),"Corrupt light rail is rejected on load");
        var orphan=codec.Decode<WorldState>(codec.Encode(g.World));orphan.tramLines[0].stationId=999999;orphan.nextId=Math.Max(orphan.nextId,1000000);
        SaveService.Validate(orphan,g.Balance);
        var orphanGame=new GameSession(orphan,g.Balance);
        var orphanLine=orphan.tramLines[0];var still=orphanLine.trams.ConvertAll(t=>t.position);
        for(int i=0;i<200;i++)orphanGame.Step();
        Assert(orphanGame.Trams.Status(orphanLine)==TramLineStatus.StationGone&&orphanLine.trams.ConvertAll(t=>t.position).TrueForAll(p=>still.Contains(p)),"A line whose station is gone idles");
        var replay=new GameSession(saves.RestoreSnapshot(saves.CaptureSnapshot(g.World)),g.Balance);
        for(int i=0;i<3000;i++){g.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(g.World)==saves.CaptureSnapshot(replay.World),"Light rail replays exactly after a save");
        // Removal refunds half and frees the cells.
        money=g.World.money;int refund=line.paid/2+line.trams.Count*(TramCatalog.Price/2);
        OK(g.Trams.Remove(lineId));
        Assert(g.World.money==money+refund&&g.World.tramLines.Count==0,"Removing a line refunds half");
        foreach(var c in cells)Assert(!g.Cities.HasTram(c)&&g.Cities.TramMask(c)==0,"Removed lines free their cells");
        CheckLightRailBeachAndSki();
        CheckLightRailCrossing();
        CheckLightRailTownGrowth();
    }
    static void CheckLightRailBeachAndSki()
    {
        var g=New();g.World.money=500000;
        // Sunvale's beach: a finished road along row 28 to the coast with its car park open.
        var path=new List<Cell>();for(int x=79;x<=Coast.EntranceX;x++)path.Add(new Cell(x,28));
        var road=new IntercityRoadState{a=14,b=Coast.Resort,path=path,built=path.Count,park=Coast.ParkSteps-1};
        g.World.intercityRoads.Add(road);g.Cities.Rebuild();
        var beach=TramVenues.All(g.World).Find(v=>v.kind==TramVenueKind.Beach);
        Assert(beach.name=="Sunvale Beach"&&!beach.open&&beach.status.Contains("stage"),"A beach waits for its car park");
        Track(g,new Cell(70,31),new Cell(90,31));int sunvale=OK(g.Stations.Place(new Cell(80,31),14));
        Assert(!g.Trams.Planner.Plan(beach,sunvale).valid,"No line to a beach that is not open");
        road.park=Coast.ParkSteps;beach=TramVenues.All(g.World).Find(v=>v.kind==TramVenueKind.Beach);
        var plan=g.Trams.Planner.Plan(beach,sunvale);
        Assert(plan.valid,"Beach line plans: "+plan.reason);
        Console.WriteLine($"Light rail to {beach.name}: {plan.cells.Count} cells, ${plan.cost:N0} ({plan.streetCells} on the road)");
        Assert(plan.streetCells>plan.cells.Count/2,"The beach line runs mostly along the beach road");
        var last=plan.cells[plan.cells.Count-1];
        bool byPark=false;for(int i=0;i<Coast.ParkCells;i++)byPark|=Coast.ParkCell(beach.cell,i).Distance(last)==1;
        Assert(byPark&&!last.Equals(beach.cell),"The beach stop is beside the car park");
        OK(g.Trams.Build(plan));
        SaveService.Validate(g.World,g.Balance);
        // A ski resort from the nearest town station.
        var granite=TramVenues.All(g.World).Find(v=>v.kind==TramVenueKind.Ski&&v.id==SkiResorts.FirstId);
        var skiStations=g.Trams.Planner.Stations(granite);
        Assert(skiStations.Exists(s=>s.id==sunvale),"Sunvale's station is offered for Granite Ridge");
        var ski=g.Trams.Planner.Plan(granite,sunvale);
        Console.WriteLine($"Light rail to {granite.name}: {(ski.valid?ski.cells.Count+" cells, $"+ski.cost.ToString("N0"):ski.reason)}");
        if(ski.valid)
        {
            var p=g.Cargo.Producer(granite.id);
            Assert(SkiResorts.Distance(p,ski.cells[ski.cells.Count-1])==1,"The ski stop is beside the resort's base");
            OK(g.Trams.Build(ski));
            SaveService.Validate(g.World,g.Balance);
            for(int i=0;i<2400;i++)g.Step();
            Assert(g.World.tramLines.TrueForAll(l=>l.accounts.LastYear(g.World.tick).income>0),"Beach and ski trams earn fares");
        }
    }
    // A tram line crossing a busy coal railway: trams never enter the crossing while a train is near.
    static void CheckLightRailCrossing()
    {
        var g=TramGame(out int stationId,out var stadium);
        var coal=Coal(g);
        var cells=new List<Cell>();for(int z=9;z<=21;z++)cells.Add(new Cell(25,z));
        var line=new TramLineState{id=g.World.nextId++,stationId=stationId,venueKind=(int)stadium.kind,venueId=stadium.id,venueCell=stadium.cell,cells=cells};
        line.trams.Add(new TramState{id=g.World.nextId++});line.trams.Add(new TramState{id=g.World.nextId++});line.trams.Add(new TramState{id=g.World.nextId++});
        g.World.tramLines.Add(line);g.Cities.Rebuild();g.World.revision++;
        SaveService.Validate(g.World,g.Balance);
        var route=g.Trams.Route(line);int i15=cells.FindIndex(c=>c.z==15);int track=g.Network.At(new Cell(25,15)).id;
        g.Cargo.Producer(1).inventory=200;
        int crossed=0;bool held=false;
        for(int tick=0;tick<4000;tick++)
        {
            var before=line.trams.ConvertAll(t=>t.position);
            g.Step();
            bool near=g.Trams.TrainNear(track);
            held|=g.Trams.Status(line)==TramLineStatus.HeldAtCrossing;
            for(int k=0;k<line.trams.Count;k++)
                for(int back=0;back<2;back++)
                {
                    route.Span(i15,back==1,out int enter,out int leave);
                    int p0=before[k],p1=line.trams[k].position;
                    if(p0>=0&&p0<=enter&&p1>enter){crossed++;Assert(!near,"A tram never enters a crossing a train is near");}
                }
        }
        Assert(crossed>0,"Trams cross the railway between trains: "+crossed);
        Assert(held,"Trams wait for trains at the crossing");
        Assert(g.Trains.Train(coal.train).accounts.LastYear(g.World.tick).income>0,"The coal train keeps running");
    }
    // Town growth never builds on tram cells; streets may still grow over them.
    static void CheckLightRailTownGrowth()
    {
        var g=TramGame(out int stationId,out var stadium);
        g.Balance.city.basePoints=400;
        var plan=g.Trams.Planner.Plan(stadium,stationId);OK(g.Trams.Build(plan));
        for(int i=0;i<20000;i++)
        {
            g.Step();
            if(i%2000==0)SaveService.Validate(g.World,g.Balance);
        }
        foreach(var c in plan.cells)
            foreach(var city in g.World.cities)
            {
                Assert(!city.buildings.Exists(bs=>CityLayout.Covers(bs,c)),"No building on a tram cell: "+c);
                Assert(!city.roads.Exists(r=>r.cell.Equals(c)&&r.cell.Equals(city.center)),"No town square on a tram cell");
            }
        Console.WriteLine($"Light rail town growth: Oakridge {g.World.cities.Find(c=>c.producerId==5).population} people, {plan.cells.FindAll(c=>(g.Cities.Bits(c)&CitySimulation.Road)!=0).Count} tram cells now in streets");
    }
}
