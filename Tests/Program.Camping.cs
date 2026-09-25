using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    /// <summary>A new game with an open highway running straight east from Oakridge's easternmost street, as the PlayMode roadside tests lay it.</summary>
    static (GameSession game, IntercityRoadState road) OakridgeHighway(int length = 24)
    {
        var g=New();var town=g.Cities.CityFor(5);int east=town.center.x;
        foreach(var r in town.roads)if(r.cell.z==town.center.z)east=Math.Max(east,r.cell.x);
        var road=new IntercityRoadState{a=town.producerId,b=14};
        for(int x=east;x<=east+length;x++)road.path.Add(new Cell(x,town.center.z));
        road.built=road.path.Count;g.World.intercityRoads.Add(road);g.Cities.Rebuild();
        return (g,road);
    }
    static void CheckServiceSales()
    {
        // The price is always above a million dollars, whatever the style or the traffic.
        foreach(int kind in new[]{-1,0,1,2,3,99})
            foreach(int people in new[]{int.MinValue,-5,0,1,999,25000,400000,int.MaxValue})
            {
                Assert(ServiceSales.Price(kind,people)>ServiceSales.MinPrice,$"A service area costs more than a million: kind {kind}, {people} people");
                Assert(ServiceSales.Income(kind,people)>0,"An owned station earns something");
            }
        Assert(ServiceSales.Price(0,30000)>ServiceSales.Price(0,1000),"More people on the highway raise the price");
        Assert(ServiceSales.Price(2,1000)>ServiceSales.Price(0,1000),"A truck stop costs more than a plain filling station");
        var (g,road)=OakridgeHighway();
        var w=g.World;
        Assert(!g.Cities.BuyService(road).ok,"Nothing to buy before a service area is planned");
        for(int i=0;i<200&&road.service==0;i++)g.Step();
        Assert(Roadside.Planned(road)&&!Roadside.Open(road),"The highway plans its service area");
        int money=w.money=5_000_000;
        var early=g.Cities.BuyService(road);
        Assert(!early.ok&&w.money==money&&!road.serviceOwned,"A station under construction is not for sale: "+early.message);
        for(int i=0;i<2000&&!Roadside.Open(road);i++)g.Step();
        Assert(Roadside.Open(road),"The service area opens");
        // Not enough money: nothing changes.
        w.money=900_000;var poor=g.Cities.BuyService(road);
        Assert(!poor.ok&&w.money==900_000&&!road.serviceOwned&&poor.message.Contains("$"),"Buying needs the full price: "+poor.message);
        // Bought at today's price, which is over a million.
        w.money=money;int price=g.Cities.ServicePrice(road),revision=w.cityRevision;long spent=w.totalExpenses;
        var bought=g.Cities.BuyService(road);
        Assert(bought.ok&&road.serviceOwned&&w.money==money-price&&price>ServiceSales.MinPrice&&w.totalExpenses==spent+price,$"The station is bought for ${price:N0}: {bought.message}");
        Assert(w.cityRevision>revision,"Buying redraws the towns (the owner's flag)");
        Assert(!g.Cities.BuyService(road).ok&&w.money==money-price,"A station is bought only once");
        Assert(g.Cities.OwnedServices()==1,"One station owned");
        // It pays once a game minute, as income.
        long income=w.totalIncome;int before=w.money;
        for(int i=0;i<ServiceSales.PayPeriod;i++)g.Step();
        int gain=w.money-before;
        Assert(gain>0&&gain==road.serviceEarned&&w.totalIncome==income+gain,$"An owned station pays every minute: +${gain:N0}, earned {road.serviceEarned:N0}");
        Assert(gain>=ServiceSales.Income(road.serviceKind,0)&&gain*ServiceSales.PaybackMinutes<=price*11/10,"It earns about a hundredth of its price a minute");
        EconomyService.Recent(w,out int recent,out _);Assert(recent>=gain,"The takings show as income per minute");
        // Paid even while the towns stand still.
        g.Balance.city.growthEnabled=false;before=w.money;
        for(int i=0;i<ServiceSales.PayPeriod;i++)g.Step();
        Assert(w.money>before,"An owned station pays with town growth off");
        g.Balance.city.growthEnabled=true;
        // In a grown valley: saves keep ownership and takings; a bought station that is not open, or negative takings, is rejected.
        var valley=GrownValley();var vw=valley.World;vw.money=10_000_000;
        var station=vw.intercityRoads.Find(r=>Roadside.Open(r));int index=vw.intercityRoads.IndexOf(station);
        OK(valley.Cities.BuyService(station));
        for(int i=0;i<ServiceSales.PayPeriod;i++)valley.Step();
        Assert(station.serviceEarned>0,"The grown valley's station pays");
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailServiceSales-"+Guid.NewGuid()),new Codec(),valley.Balance);
        string json=saves.CaptureSnapshot(vw);var restored=saves.RestoreSnapshot(json);SaveService.Validate(restored,valley.Balance);
        Assert(restored.intercityRoads[index].serviceOwned&&restored.intercityRoads[index].serviceEarned==station.serviceEarned,"Ownership survives a save");
        foreach(var tamper in new Action<IntercityRoadState>[]{r=>r.service=2,r=>r.serviceEarned=-1})
        {
            var t=saves.RestoreSnapshot(json);tamper(t.intercityRoads[index]);
            bool rejected=false;try{SaveService.Validate(t,valley.Balance);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"A tampered owned station is rejected");
        }
        var replay=new GameSession(saves.RestoreSnapshot(json),valley.Balance);
        for(int i=0;i<2*ServiceSales.PayPeriod;i++){valley.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(vw)==saves.CaptureSnapshot(replay.World),"Owned stations replay identically");
        Console.WriteLine($"Service sales: bought for ${price:N0}, paying ${gain:N0}/min");
    }
    static void CheckCampsites()
    {
        // A new highway gets its town a campsite just outside the last street, built in stages.
        var (g,road)=OakridgeHighway();
        var w=g.World;bool opened=false;
        for(int i=0;i<3000&&!Campsites.Open(road);i++){g.Step();while(g.Cities.Notifications.Count>0)if(g.Cities.Notifications.Dequeue().kind==Notification.CampOpened)opened=true;}
        Assert(Campsites.Open(road)&&opened,"The highway's town opens a campsite");
        Assert(Campsites.Shaped(road)&&Campsites.Town(road)==5,"Oakridge's campsite stands beside a straight stretch in Oakridge's half of the road");
        int edge=0;while(edge<road.path.Count/2&&NearStreet(g,road.path[edge]))edge++;
        Assert(road.campAt-1>=edge&&road.campAt-1<=edge+Campsites.Reach,$"It stands just outside town: frontage starts {road.campAt-1} cells along the highway, the town ends at {edge}");
        Assert(!Roadside.Planned(road)||Math.Abs(road.campAt-road.serviceAt)>=Campsites.ServiceGap,"The campsite keeps clear of the service area");
        var town=g.Cities.CityFor(5);var highway=new HashSet<int>();foreach(var r in w.intercityRoads)foreach(var c in r.path)highway.Add(c.Key);
        for(int i=0;i<Campsites.Cells;i++)
        {
            var c=Campsites.SiteCell(road,i);
            Assert(!highway.Contains(c.Key)&&g.Network.At(c)==null&&!MapDefinition.Water(c)&&!MapDefinition.Raised(c)&&g.Cities.CityAt(c)==null,"Campsite on open ground at "+c);
            Assert(g.Cities.BlocksTrack(c)&&!g.Build.Placeable(c),"Track may not be laid through a campsite at "+c);
            Assert(!g.Scenery.TreeAt(c),"No pine stands inside a campsite at "+c);
            Assert(Campsites.At(w,c)==road,"A tap on the campsite finds it");
            if(Roadside.Planned(road))for(int k=0;k<Roadside.Cells;k++)Assert(!Roadside.SiteCell(road,k).Equals(c),"Campsite and service area never share a cell");
        }
        // It keeps the town's streets out; after a long spell of growth no street or building stands on it.
        g.Balance.city.basePoints=400;
        for(int i=0;i<12000;i++)g.Step();
        for(int i=0;i<Campsites.Cells;i++)Assert(g.Cities.CityAt(Campsites.SiteCell(road,i))==null,"The town grows round its campsite");
        // Across a grown valley: one campsite per town, every style, never on a beach or ski road.
        var grown=GrownValley();
        var owners=new Dictionary<int,int>();int planned=0;var kinds=new HashSet<int>();
        foreach(var r in grown.World.intercityRoads)
        {
            Assert(!(r.ToSki&&Campsites.Planned(r))&&!(r.ToBeach&&Campsites.Planned(r)&&Campsites.Town(r)!=r.a),"No campsite on a ski road; a beach road's belongs to its town");
            if(!Campsites.Planned(r))continue;
            planned++;kinds.Add(r.campKind);
            int owner=Campsites.Town(r);owners.TryGetValue(owner,out int n);owners[owner]=n+1;
            Assert(owners[owner]==1,"A town has one campsite: "+grown.Cargo.Producer(owner).name);
            Assert(Campsites.Shaped(r)&&r.Complete&&r.path.Count>=Campsites.MinRoad,"A campsite stands beside an open highway");
            for(int i=0;i<Campsites.Cells;i++)Assert(!Coast.Beach(Campsites.SiteCell(r,i))&&!MapDefinition.Water(Campsites.SiteCell(r,i)),"No campsite on the sand");
        }
        Assert(planned>=3&&kinds.Count==Math.Min(planned,Campsites.Kinds),$"Grown towns get campsites in every style: {planned} planned, {kinds.Count} styles");
        // Saves keep them; tampered sites are rejected; a replay plans the same ones.
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailCamps-"+Guid.NewGuid()),new Codec(),grown.Balance);
        string json=saves.CaptureSnapshot(grown.World);var restored=saves.RestoreSnapshot(json);SaveService.Validate(restored,grown.Balance);
        var camp=grown.World.intercityRoads.Find(r=>Campsites.Open(r));int index=grown.World.intercityRoads.IndexOf(camp);
        Assert(restored.intercityRoads[index].camp==camp.camp&&restored.intercityRoads[index].campAt==camp.campAt&&restored.intercityRoads[index].campSide==camp.campSide,"A campsite survives a save");
        foreach(var tamper in new Action<IntercityRoadState>[]{r=>r.campSide=Campsites.Along(r),r=>r.camp=Campsites.Steps+1,r=>r.campAt=0,r=>r.campSide=7,r=>r.campKind=Campsites.Kinds,r=>r.camp=-1})
        {
            var t=saves.RestoreSnapshot(json);tamper(t.intercityRoads[index]);
            bool rejected=false;try{SaveService.Validate(t,grown.Balance);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"A misplaced campsite is rejected");
        }
        var replay=new GameSession(saves.RestoreSnapshot(json),grown.Balance);
        for(int i=0;i<2400;i++){grown.Step();replay.Step();}
        Assert(saves.CaptureSnapshot(grown.World)==saves.CaptureSnapshot(replay.World),"Campsites replay identically");
        // A save from before campsites: its towns plan them again.
        var old=saves.RestoreSnapshot(json);foreach(var r in old.intercityRoads)r.camp=r.campAt=r.campSide=r.campKind=0;
        var reopened=new GameSession(old,grown.Balance);SaveService.Validate(reopened.World,reopened.Balance);
        for(int i=0;i<200*(planned+Campsites.Steps+2);i++)reopened.Step();
        int again=0;foreach(var r in reopened.World.intercityRoads)if(Campsites.Planned(r))again++;
        Assert(again>=planned-1,$"An older save plans its campsites: {again} (was {planned})");
        SaveService.Validate(reopened.World,reopened.Balance);
        Console.WriteLine($"Campsites: {planned} planned on {grown.World.intercityRoads.Count} roads; Oakridge's at road cell {road.campAt}");
    }
    /// <summary>True when a town street or building lies within Roadside.TownGap cells, as the campsite planner tests it.</summary>
    static bool NearStreet(GameSession g,Cell c)
    {
        for(int dz=-Roadside.TownGap;dz<=Roadside.TownGap;dz++)for(int dx=-Roadside.TownGap;dx<=Roadside.TownGap;dx++)
        {var n=new Cell(c.x+dx,c.z+dz);if(MapDefinition.InBounds(n)&&g.Cities.CityAt(n)!=null)return true;}
        return false;
    }
    static string grownValley;
    /// <summary>A fresh copy of the valley after 40000 ticks of fast growth: towns linked by highways, with service areas and campsites. Grown once.</summary>
    static GameSession GrownValley()
    {
        var fast=new Balance();fast.city.basePoints=400;
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailGrown-"+Guid.NewGuid()),new Codec(),fast);
        if(grownValley==null)
        {
            var g=new GameSession(WorldState.New(fast),fast);
            for(int i=0;i<40000;i++){g.Step();g.Cities.Notifications.Clear();}
            grownValley=saves.CaptureSnapshot(g.World);
        }
        return new GameSession(saves.RestoreSnapshot(grownValley),fast);
    }
}
