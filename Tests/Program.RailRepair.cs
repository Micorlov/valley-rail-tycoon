using System;
using System.Collections.Generic;
using System.IO;
using ValleyRail.Core;
partial class Program
{
    static int MaskAt(GameSession g,Cell c)=>g.Network.At(c)?.mask??0;
    /// <summary>Length of the best train route between two pieces, or -1 when none exists.</summary>
    static int RouteLength(GameSession g,Cell a,Cell b){var p=g.Pathfinder.FindPath(a,b);if(p==null)return -1;int n=0;foreach(var s in p)n+=s.length;return n;}
    static bool Changed(TrackRepair r,Cell c,out int mask){mask=-1;if(r.build==null)return false;var t=r.build.changes.Find(x=>x.cell.Equals(c));if(t!=null)mask=t.mask;return t!=null;}
    static string Changes(TrackRepair r){if(r.build==null)return "";var keys=new List<string>();foreach(var t in r.build.changes)keys.Add(t.cell.Key+":"+t.mask);keys.Sort(string.CompareOrdinal);return string.Join(",",keys);}
    static void Build(GameSession g,params Cell[] cells){var p=g.Build.ValidateBuild(new List<Cell>(cells));Assert(p.valid,p.reason);OK(g.Build.CommitBuild(p));}
    static Cell[] Line(Cell a,Cell b){var cells=new List<Cell>();int dx=Math.Sign(b.x-a.x),dz=Math.Sign(b.z-a.z);for(var c=a;;c=new Cell(c.x+dx,c.z+dz)){cells.Add(c);if(c.Equals(b))break;}return cells.ToArray();}
    static GameSession Open(){var g=New();g.World.money=200000;return g;}
    static GameSession LoadFixture(string name)
    {
        string path=Path.Combine(AppContext.BaseDirectory,"..","..","..","Fixtures",name);
        return new GameSession(new SaveService(Path.GetTempPath(),new Codec(),new Balance()).RestoreSnapshot(File.ReadAllText(path)),new Balance());
    }
    static void CheckRailRepair()
    {
        // Two line ends stop one cell short of each other: the AI lays the missing straight.
        var gap=Open();Track(gap,new Cell(14,36),new Cell(18,36));Track(gap,new Cell(20,36),new Cell(24,36));
        Assert(RouteLength(gap,new Cell(14,36),new Cell(24,36))<0,"Gap starts disconnected");
        var gapFix=gap.Repairs.Plan();
        Assert(gapFix.valid&&Changed(gapFix,new Cell(19,36),out int gapMask)&&gapMask==10&&gapFix.build.changes.Count==1&&gapFix.build.cost==100,"AI fills a one-cell gap with a straight: "+gapFix.reason+" "+Changes(gapFix));
        Assert(gapFix.items.Exists(i=>i.kind==RepairKind.Gap&&i.repaired),"The gap is reported as fixed");
        int money=gap.World.money;OK(gap.Repairs.Apply(gapFix));
        Assert(gap.World.money==money-100&&RouteLength(gap,new Cell(14,36),new Cell(24,36))>0,"Applied gap fix charges its quote and joins the lines");
        var again=gap.Repairs.Plan();
        Assert(!again.valid&&(again.build==null||again.build.changes.Count==0),"Nothing left to fix: "+again.reason);
        SaveService.Validate(gap.World,gap.Balance);
        // Ends offset by one row: joined with an S-bend.
        var bend=Open();Track(bend,new Cell(14,34),new Cell(18,34));Track(bend,new Cell(20,35),new Cell(24,35));
        var bendFix=bend.Repairs.Plan();OK(bend.Repairs.Apply(bendFix));
        Assert(RouteLength(bend,new Cell(14,34),new Cell(24,35))>0,"AI joins offset line ends: "+bendFix.reason);
        // Parallel line ends (double track, platform ends) are left apart.
        var parallel=Open();Track(parallel,new Cell(14,32),new Cell(18,32));Track(parallel,new Cell(14,33),new Cell(18,33));
        var parallelFix=parallel.Repairs.Plan();
        Assert(!parallelFix.valid&&(parallelFix.build==null||parallelFix.build.changes.Count==0),"Parallel tracks are not joined: "+Changes(parallelFix));
        // A line ends against the end of another line: that end turns to meet it.
        var end=Open();Track(end,new Cell(14,36),new Cell(18,36));Build(end,Line(new Cell(19,40),new Cell(19,36)));
        Assert(MaskAt(end,new Cell(19,36))==5,"The second line ends in a stub past the first");
        var endFix=end.Repairs.Plan();
        Assert(endFix.valid&&Changed(endFix,new Cell(19,36),out int endMask)&&endMask==9,"The line end becomes a curve: "+endFix.reason+" "+Changes(endFix));
        Assert(endFix.items.Exists(i=>i.kind==RepairKind.LooseJoin&&i.repaired),"Reported as a loose end");
        OK(end.Repairs.Apply(endFix));Assert(RouteLength(end,new Cell(14,36),new Cell(19,40))>0,"Loose end joined");
        // A line ends against a curve: the curve becomes a junction and still turns as before.
        var curve=Open();Build(curve,new Cell(19,40),new Cell(19,39),new Cell(19,38),new Cell(19,37),new Cell(19,36),new Cell(20,36),new Cell(21,36),new Cell(22,36));Track(curve,new Cell(14,36),new Cell(18,36));
        var curveFix=curve.Repairs.Plan();OK(curve.Repairs.Apply(curveFix));
        Assert(MaskAt(curve,new Cell(19,36))==11,"Curve plus branch becomes a wye: "+curveFix.reason);
        Assert(RouteLength(curve,new Cell(14,36),new Cell(19,40))>0&&RouteLength(curve,new Cell(19,40),new Cell(22,36))>0,"Branch joins and the old curve still runs");
        // A line ends against the side of a straight line: a proper junction is built without cutting the line.
        var side=Open();Track(side,new Cell(19,31),new Cell(19,41));int through=RouteLength(side,new Cell(19,31),new Cell(19,41));
        Track(side,new Cell(14,36),new Cell(18,36));
        Assert(MaskAt(side,new Cell(18,36))==10&&MaskAt(side,new Cell(19,36))==5,"The new line points into the side of the straight");
        var sideFix=side.Repairs.Plan();OK(side.Repairs.Apply(sideFix));
        int after=RouteLength(side,new Cell(19,31),new Cell(19,41));
        Assert(after>0&&after<=through+RailRepairPlanner.MaxDetour,"The straight still runs through: "+after+" vs "+through+" "+sideFix.reason);
        Assert(RouteLength(side,new Cell(14,36),new Cell(19,31))>0||RouteLength(side,new Cell(14,36),new Cell(19,41))>0,"The side line reaches the straight");
        foreach(var t in sideFix.build.changes)if(t.mask==0)Assert(side.Network.At(t.cell)==null&&!side.Scenery.TreeAt(t.cell),"Removed track leaves bare ground");
        Assert(sideFix.doomed.Count>0,"Removed or trimmed track is marked for the preview");
        SaveService.Validate(side.World,side.Balance);
        // Rail over rail: a branch dragged off the middle of a line turns it into a wye that cuts the line.
        var cut=PassengerStubs(g=>{Track(g,new Cell(14,46),new Cell(50,46));Track(g,new Cell(36,46),new Cell(36,50));});
        Assert(!cut.g.Trains.AssignRoute(cut.train,cut.a,cut.b).ok,"The branch cut the main line");
        int north=RouteLength(cut.g,new Cell(36,50),new Cell(16,46)),south=RouteLength(cut.g,new Cell(36,50),new Cell(48,46));
        var cutFix=cut.g.Repairs.Plan();
        Assert(cutFix.valid&&cutFix.items.Exists(i=>i.kind==RepairKind.CutLine&&i.repaired),"AI rebuilds the cut junction: "+cutFix.reason);
        OK(cut.g.Repairs.Apply(cutFix));
        OK(cut.g.Trains.AssignRoute(cut.train,cut.a,cut.b));
        int north2=RouteLength(cut.g,new Cell(36,50),new Cell(16,46)),south2=RouteLength(cut.g,new Cell(36,50),new Cell(48,46));
        Assert(north2>0&&south2>0&&north2<=north+RailRepairPlanner.MaxDetour&&south2<=south+RailRepairPlanner.MaxDetour,"Both branch directions still run");
        for(int i=0;i<2400;i++)cut.g.Step();
        Assert(cut.g.World.delivered>0,"Train delivers through the rebuilt junction");
        SaveService.Validate(cut.g.World,cut.g.Balance);
        // A junction whose stem leads nowhere: dropping the stub reopens the line for free.
        var stem=Open();Track(stem,new Cell(14,38),new Cell(24,38));Track(stem,new Cell(19,38),new Cell(19,40));
        OK(stem.Build.Bulldoze(new Cell(19,39)));OK(stem.Build.Bulldoze(new Cell(19,40)));
        Assert(MaskAt(stem,new Cell(19,38))==11&&RouteLength(stem,new Cell(14,38),new Cell(24,38))<0,"A dead stem cuts the line");
        var stemFix=stem.Repairs.Plan();
        Assert(stemFix.valid&&stemFix.build.cost==0&&Changed(stemFix,new Cell(19,38),out int stemMask)&&stemMask==10,"Dead stem removed for free: "+stemFix.reason+" "+Changes(stemFix));
        OK(stem.Repairs.Apply(stemFix));Assert(RouteLength(stem,new Cell(14,38),new Cell(24,38))>0,"Line runs through again");
        // A running train's route is never touched; the player is told to park it.
        var busy=PassengerStubs(g=>Track(g,new Cell(14,46),new Cell(50,46)));OK(busy.g.Trains.AssignRoute(busy.train,busy.a,busy.b));
        Track(busy.g,new Cell(38,40),new Cell(38,45));
        Assert(MaskAt(busy.g,new Cell(38,45))==5,"A line ends against the running route");
        var busyFix=busy.g.Repairs.Plan();
        Assert(!Changed(busyFix,new Cell(38,46),out _)&&busyFix.items.Exists(i=>!i.repaired&&i.reason.Contains("park")),"Route track is left alone with advice: "+busyFix.reason);
        // Streets and highways keep their level crossings: the AI never builds on them.
        var street=Open();var road=new IntercityRoadState{a=4,b=5};for(int x=40;x<=44;x++)road.path.Add(new Cell(x,36));road.built=road.path.Count;street.World.intercityRoads.Add(road);street.Cities.Rebuild();
        Track(street,new Cell(42,32),new Cell(42,35));Track(street,new Cell(42,40),new Cell(42,37));
        var lanes=RoadLanes.Build(street.World,street.Network);var streetFix=street.Repairs.Plan();
        Assert(streetFix.build==null||!streetFix.build.changes.Exists(t=>street.Cities.Occupied(t.cell)),"No repair on a street cell: "+Changes(streetFix));
        if(streetFix.valid)OK(street.Repairs.Apply(streetFix));
        Assert(lanes.SameAs(RoadLanes.Build(street.World,street.Network)),"Road lanes are unchanged");
        Assert(!street.Build.ValidateRepair(new List<TrackPieceState>{new TrackPieceState{cell=new Cell(42,36),mask=5}}).valid,"Repairs refuse street cells");
        // Quotes: a changed quote or a player who cannot pay changes nothing.
        var quote=Open();Track(quote,new Cell(14,36),new Cell(18,36));Track(quote,new Cell(20,36),new Cell(24,36));
        var tampered=quote.Repairs.Plan();tampered.build.cost++;int tracks=quote.World.tracks.Count;
        Assert(!quote.Repairs.Apply(tampered).ok&&quote.World.tracks.Count==tracks,"A repair that differs from its quote is refused");
        quote.World.money=50;var poor=quote.Repairs.Plan();
        Assert(!poor.valid&&poor.build!=null&&poor.build.unaffordable&&poor.reason.Contains("$"),"An unaffordable repair explains its price: "+poor.reason);
        Assert(!quote.Repairs.Apply(poor).ok&&quote.World.money==50&&quote.World.tracks.Count==tracks,"An unaffordable repair changes nothing");
        // Repair validation: masks need two ports, costs are never negative and removals count against the track cap.
        var v=Open();Track(v,new Cell(14,36),new Cell(18,36));
        Assert(!v.Build.ValidateRepair(new List<TrackPieceState>{new TrackPieceState{cell=new Cell(16,36),mask=2}}).valid,"A one-port piece is refused");
        var trim=v.Build.ValidateRepair(new List<TrackPieceState>{new TrackPieceState{cell=new Cell(18,36),mask=0}});
        Assert(trim.valid&&trim.cost==0,"Removing track costs nothing: "+trim.reason);
        for(int z=100;v.World.tracks.Count<1500;z++)for(int x=0;x<MapDefinition.Size&&v.World.tracks.Count<1500;x++){var c=new Cell(x,z);if(v.Network.At(c)==null)v.World.tracks.Add(new TrackPieceState{id=v.World.nextId++,cell=c,mask=10,paid=100});}
        v.Network.Rebuild(v.World);
        Assert(v.Build.ValidateRepair(new List<TrackPieceState>{new TrackPieceState{cell=new Cell(18,36),mask=0},new TrackPieceState{cell=new Cell(19,36),mask=10}}).valid,"A swap within the track cap is allowed");
        Assert(!v.Build.ValidateRepair(new List<TrackPieceState>{new TrackPieceState{cell=new Cell(19,36),mask=10}}).valid,"The track cap still holds");
        // The player's tangled save: repairs keep every working route and never merge two trains' railways.
        var tangled=LoadFixture("tangled-junctions.json");
        var platforms=new List<Cell>();foreach(var s in tangled.World.stations)for(int p=0;p<StationLayout.Platforms(s);p++)platforms.Add(StationLayout.Center(s,p));
        var reachable=new List<(Cell,Cell)>();foreach(var a in platforms)foreach(var b in platforms)if(!a.Equals(b)&&RouteLength(tangled,a,b)>0)reachable.Add((a,b));
        var watch=System.Diagnostics.Stopwatch.StartNew();var tangledFix=tangled.Repairs.Plan();watch.Stop();
        Console.WriteLine($"AI track repair on the tangled save: {watch.Elapsed.TotalMilliseconds:F0} ms, {tangledFix.build?.changes.Count??0} cells, ${tangledFix.build?.cost??0:N0}\n{tangledFix.reason}");
        Assert(tangledFix.valid,"The tangled save has something to fix: "+tangledFix.reason);
        Assert(tangledFix.items.Exists(i=>!i.repaired&&i.reason.Contains("two trains")),"The Frostford near miss would join two trains' railways");
        var twin=LoadFixture("tangled-junctions.json");
        Assert(Changes(twin.Repairs.Plan())==Changes(tangledFix),"Repair plans are deterministic");
        OK(tangled.Repairs.Apply(tangledFix));
        foreach(var (a,b) in reachable)Assert(RouteLength(tangled,a,b)>0,$"Route {a} → {b} survives the repair");
        SaveService.Validate(tangled.World,tangled.Balance);
        var second=tangled.Repairs.Plan();
        Assert(second.build==null||second.build.changes.Count==0,"A second AI FIX finds nothing new: "+Changes(second)+"\n"+second.reason);
        var stuck=tangled.World.trains.Find(t=>t.number==2);
        OK(tangled.Trains.AutoDestination(stuck.id));
        Assert(stuck.b!=0&&tangled.Trains.Station(stuck.b).name=="Lakewood Station","The repaired junction lets the stuck express reach Lakewood");
        // The player's phone save: the one-cell gap south of Sunvale lies between two trains' railways, so it stays.
        var phone=LoadFixture("phone-gap.json");var phoneFix=phone.Repairs.Plan();
        Assert(!Changed(phoneFix,new Cell(75,26),out _)&&phoneFix.items.Exists(i=>i.kind==RepairKind.Gap&&i.cell.Equals(new Cell(75,26))&&i.reason.Contains("two trains")),"The phone save's gap would join two trains' railways: "+phoneFix.reason+" "+Changes(phoneFix));
        Console.WriteLine($"AI track repair on the phone save: {phoneFix.build?.changes.Count??0} cells, ${phoneFix.build?.cost??0:N0}\n{phoneFix.reason}");
        foreach(var i in phoneFix.items)Console.WriteLine($"  {i.kind} at {i.cell}: {(i.repaired?"fixed":i.reason)}");
        foreach(var i in tangledFix.items)Console.WriteLine($"  tangled {i.kind} at {i.cell}: {(i.repaired?"fixed":i.reason)}");
    }
}
