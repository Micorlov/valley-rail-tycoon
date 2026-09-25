using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static void CheckTownFires()
    {
        // Homes and businesses burn; towers, high-rises and civic buildings never do.
        Assert(TownFires.Burns(0)&&TownFires.Burns(8)&&TownFires.Burns(17)&&TownFires.Burns(19),"Houses, cottages, cafés and hotels can catch fire");
        Assert(!TownFires.Burns(3)&&!TownFires.Burns(7)&&!TownFires.Burns(16)&&!TownFires.Burns(BuildingCatalog.TownHall)&&!TownFires.Burns(-1),"Towers, high-rises, civic buildings and bad indices never burn");
        // A new valley has no fire station yet, so nothing burns.
        var young=new GameSession(WorldState.New(new Balance()),new Balance());
        Assert(TownFires.Plan(young.World,young.Network,RoadLanes.Build(young.World,young.Network),new Random(1))==null,"No fire breaks out before a town has a fire station");
        // Small towns build fire stations (about 40000 ticks at this pace); their engines reach fires along streets and highways.
        var fast=new Balance();fast.city.basePoints=400;var g=new GameSession(WorldState.New(fast),fast);
        for(int i=0;i<40000;i++)g.Step();
        int stations=0;foreach(var c in g.World.cities)foreach(var b in c.buildings)if(TownFires.IsFireStation(b.def))stations++;
        Assert(stations>=2,$"Grown towns build fire stations ({stations})");
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailFires-"+Guid.NewGuid()),new Codec(),fast);
        string before=saves.CaptureSnapshot(g.World);
        var lanes=RoadLanes.Build(g.World,g.Network);var streets=TownFires.Streets(g.World);var random=new Random(7);
        var burnt=new HashSet<int>();var towns=new HashSet<int>();int longest=0,aheadHome=0,turned=0;
        for(int k=0;k<200;k++)
        {
            var call=TownFires.Plan(g.World,g.Network,lanes,random);
            Assert(call!=null,"A grown valley always has a building a fire engine can reach");
            var city=g.World.cities.Find(c=>c.id==call.cityId);
            Assert(city!=null&&city.buildings.Contains(call.building)&&TownFires.Burns(call.building.def),"The fire is in a home or business of its town");
            Assert(call.street.Equals(call.building.cell.Move(call.facing))&&streets.Contains(call.street.Key)&&g.Network.At(call.street)==null,"The engine parks on the town street the building faces, never on a level crossing");
            Assert(TownFires.IsFireStation(call.station.def)&&call.stationStreet.Equals(call.station.cell.Move(call.stationFacing)),"The engine comes out of a fire station onto the street it faces");
            Assert(call.route[0].Equals(call.stationStreet)&&call.route[call.route.Count-1].Equals(call.street)&&call.route.Count<=TownFires.FarthestCall+1,"The route runs from the station's street to the fire's");
            Drivable(lanes,call.route,"to the fire");
            // No other fire station is nearer by road.
            var nearest=TownFires.Route(lanes,call.street,c=>{foreach(var t in g.World.cities)foreach(var b in t.buildings)if(TownFires.IsFireStation(b.def)&&TownFires.Facing(streets,b.cell)>=0&&b.cell.Move(TownFires.Facing(streets,b.cell)).Equals(c))return true;return false;},-1,TownFires.FarthestCall);
            Assert(nearest!=null&&nearest.Count==call.route.Count,"The nearest fire station answers");
            int heading=TownFires.ParkHeading(lanes,call);
            Assert(heading%2!=call.facing%2,"The engine parks along the street, not facing the building");
            var home=TownFires.HomeRoute(lanes,call,heading);
            Assert(home[0].Equals(call.street)&&home[home.Count-1].Equals(call.stationStreet)&&home.Count<call.route.Count+TownFires.LongestDetour,"The engine drives home to its station, never far out of its way");
            Drivable(lanes,home,"home");
            if(home.Count>1&&Directions.Between(home[0],home[1])==heading)aheadHome++;else turned++;
            burnt.Add(call.building.cell.Key);towns.Add(call.cityId);longest=Math.Max(longest,call.route.Count);
        }
        Assert(burnt.Count>=40&&towns.Count>=2,$"Fires break out all over the valley: {burnt.Count} buildings in {towns.Count} towns");
        Assert(saves.CaptureSnapshot(g.World)==before,"Planning fires never changes the world");
        Console.WriteLine($"Fires: {stations} fire stations, 200 fires in {burnt.Count} buildings of {towns.Count} towns, longest drive {longest} cells, home ahead {aheadHome} / turned round {turned}");
        // A particular building: its own town's station answers when it is the nearest; a building under construction does not burn.
        var some=g.World.cities.Find(c=>c.buildings.Exists(b=>TownFires.Burns(b.def)));var target=some.buildings.Find(b=>TownFires.Burns(b.def));
        var forced=TownFires.Call(g.World,g.Network,lanes,some,target);
        Assert(forced==null||forced.building.cell.Equals(target.cell),"A forced fire burns the building asked for");
        Assert(TownFires.Call(g.World,g.Network,lanes,some,target,b=>!b.cell.Equals(target.cell))==null,"A building still under construction does not burn");
        Assert(TownFires.Plan(g.World,g.Network,lanes,new Random(3),b=>!TownFires.IsFireStation(b.def))==null,"Without a finished fire station nothing burns");
    }
    static void Drivable(RoadLanes lanes,List<Cell> route,string what)
    {
        for(int i=0;i+1<route.Count;i++)
            Assert(route[i].Distance(route[i+1])==1&&(lanes.Exits(route[i])&1<<Directions.Between(route[i],route[i+1]))!=0,$"Every step of the drive {what} follows an open road ({route[i]} → {route[i+1]})");
    }
}
