using System;
namespace ValleyRail.Core
{
    /// <summary>
    /// What a tap on the tilted map really points at. The finger's ray meets the ground well behind a tall building (about
    /// 1.4 cells back per cell of height), so the bulldozer walks the ray down from above the tallest tower and takes the
    /// first town building or tree it passes through, falling back to the ground cell under the finger.
    /// </summary>
    public static class StructurePick
    {
        // Top clears the tallest tower (6.2 cells, drawn up to 20% taller) and its roof.
        const float Top = 8.5f, Step = .05f, TowerVariant = 1.2f, RoofAllowance = .3f;
        /// <param name="ox">Ray origin and (normalised) direction in world units, one unit per cell.</param>
        public static Cell First(GameSession game, float ox, float oy, float oz, float dx, float dy, float dz, Cell ground)
        {
            if (dy > -.01f)
                return ground;
            float start = Math.Max(0, (oy - Top) / -dy), end = oy / -dy;
            for (float t = start; t <= end; t += Step)
            {
                float x = ox + dx * t, y = oy + dy * t, z = oz + dz * t;
                if (y < MapDefinition.Height(x, z))
                    break;
                var cell = new Cell((int)Math.Round(x), (int)Math.Round(z));
                if (game.Cities.HasBuilding(cell) && y <= BuildingTop(game.World, cell))
                    return cell;
                if (InTree(game.Scenery, x, y, z, out var tree))
                    return tree;
            }
            return ground;
        }
        static float BuildingTop(WorldState w, Cell c)
        {
            foreach (var city in w.cities)
                foreach (var bs in city.buildings)
                    if (CityLayout.Covers(bs, c))
                        return BuildingCatalog.Get(bs.def).height / 100f * TowerVariant + RoofAllowance;
            return 0;
        }
        /// <summary>A pine's foliage is a cone wider than its cell, so the neighbouring cells' trees are tested too.</summary>
        static bool InTree(Scenery scenery, float x, float y, float z, out Cell tree)
        {
            int cx = (int)Math.Round(x), cz = (int)Math.Round(z);
            for (int ddx = -1; ddx <= 1; ddx++)
                for (int ddz = -1; ddz <= 1; ddz++)
                {
                    tree = new Cell(cx + ddx, cz + ddz);
                    int index = scenery.TreeIndex(tree);
                    if (index < 0 || !scenery.Standing(index))
                        continue;
                    var spot = scenery.Trees[index];
                    if (y < TreeSpot.Crown || y > spot.Top)
                        continue;
                    float radius = TreeSpot.Radius * (1 - (y - TreeSpot.Crown) / spot.height);
                    float ex = x - tree.x, ez = z - tree.z;
                    if (ex * ex + ez * ez <= radius * radius)
                        return true;
                }
            tree = default;
            return false;
        }
    }
}
