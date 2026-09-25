using System;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static void CheckDonations()
    {
        // Twin valleys: Willowbrook gets a gift in one, nothing in the other.
        var plain=New();var gifted=New();plain.World.money=gifted.World.money=300000;
        var twin=plain.Cities.CityFor(4);var town=gifted.Cities.CityFor(4);
        Assert(!gifted.Cities.Donate(4,0).ok&&!gifted.Cities.Donate(4,400000).ok&&!gifted.Cities.Donate(1,10000).ok,"A gift needs an amount, the money for it and a town");
        Assert(gifted.World.money==300000&&town.fund==0,"A refused gift costs nothing");
        int price=gifted.Cities.FundBuildingPrice(town),revision=gifted.World.cityRevision;long spent=gifted.World.totalExpenses;
        Assert(price==gifted.Cities.ActionCost(town)*gifted.Balance.city.fundPointPrice,"A funded building costs its growth points in dollars");
        var r=gifted.Cities.Donate(4,50000);Assert(r.ok,r.message);
        Assert(gifted.World.money==250000&&gifted.World.totalExpenses==spent+50000,"The gift is paid from the player's money");
        Assert(50000-town.fund>=CitySimulation.GiftStarts*price&&gifted.World.cityRevision>=revision+CitySimulation.GiftStarts,"A gift breaks ground on three buildings at once: "+r.message);
        int fund=town.fund;
        for(int i=0;i<6000;i++){plain.Step();gifted.Step();}
        Console.WriteLine($"Donations: after 5 game min Willowbrook has {twin.population} people without a gift and {town.population} with $50,000 (fund left ${town.fund:N0}, first building ${price:N0})");
        Assert(town.fund<fund-10*price/2,"The fund pays for extra buildings every minute");
        Assert(town.population>twin.population*3/2,$"A funded town clearly outgrows its twin: {town.population} vs {twin.population}");
        for(int i=0;i<60000&&town.fund>0;i++)gifted.Step();
        Assert(town.fund==0,"The fund runs out, and its last dollars become growth points");
        // A town with no room keeps its fund.
        var stuck=New();stuck.World.money=100000;var full=stuck.Cities.CityFor(4);full.blockedEvaluations=1;
        OK(stuck.Cities.Donate(4,10000));Assert(full.fund==10000,"A town with no room to build keeps the whole gift");
        // The fund survives a save; a negative fund is refused.
        var saves=new SaveService(Path.Combine(Path.GetTempPath(),"ValleyRailDonations-"+Guid.NewGuid()),new Codec(),stuck.Balance);
        var loaded=saves.RestoreSnapshot(saves.CaptureSnapshot(stuck.World));
        Assert(loaded.cities.Find(c=>c.producerId==4).fund==10000,"The fund is saved");
        full.fund=-1;bool refused=false;try{saves.RestoreSnapshot(saves.CaptureSnapshot(stuck.World));}catch(InvalidDataException){refused=true;}
        Assert(refused,"A negative fund is an invalid save");
        full.fund=10000;
        // Give as often as you like: gifts add up far past the old $5M ceiling; maxFund only guards the int.
        var generous=new Balance();generous.city.growthEnabled=false;var rich=new GameSession(WorldState.New(generous),generous);rich.World.money=10000000;
        for(int k=0;k<8;k++)OK(rich.Cities.Donate(4,1000000));for(int k=0;k<5;k++)OK(rich.Cities.Donate(4,CitySimulation.Gifts[0]));
        var patron=rich.Cities.CityFor(4);Assert(patron.fund==8050000&&rich.World.money==1950000,"Repeated gifts add up in the fund: "+patron.fund);
        patron.fund=generous.city.maxFund-5000;Assert(!rich.Cities.Donate(4,10000).ok&&patron.fund==generous.city.maxFund-5000,"The fund never overflows");
    }
}
