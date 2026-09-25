using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Where everything of one ski resort (<see cref="SkiResorts"/>) goes, in world units, shared by the art (SkiArt) and
    /// the life (SkiLife) so cabins run on the drawn cables and skiers stay on the drawn pistes. The base uses a frame
    /// whose +f runs up the mountain and +a to its right: the car park fills the front (f &lt; 0) with the gate at the
    /// front edge, the lodge stands back left and the gondola's valley station back right. The gondola climbs from there
    /// to a top station just below the summit, on towers tall enough to keep every cabin clear of the slope, and three
    /// pistes wind down from the top station to the plaza between the lodge and the valley station.
    /// </summary>
    public sealed class SkiLayout
    {
        public struct Bay
        {
            public Vector3 at, facing;
            /// <summary>The point on the aisle in front of the bay, where a car turns in and out.</summary>
            public Vector3 aisle;
        }
        public struct Piste
        {
            public List<Vector3> line;
            public float width;
            /// <summary>0 easy (blue), 1 intermediate (red), 2 expert (black).</summary>
            public int grade;
        }
        public const float Deck = .03f, CabinDrop = .3f;
        /// <summary>The car park's aisle runs across the base at this f; the gate is on the front edge.</summary>
        public const float AisleF = -1.4f, FrontF = -SkiResorts.Half - .5f, LaneA = .11f;
        const float TowerHeight = 1f, ValleyCable = .95f, TopCable = .42f, Clearance = .18f, LineGap = .13f, SpanTarget = 2.6f;

        public readonly ProducerState resort;
        public readonly int peak, upDirection;
        public readonly Vector3 center, up, right;
        public readonly Quaternion facing;
        /// <summary>Cable ends at the two bullwheels, and the top station's floor.</summary>
        public readonly Vector3 valleyWheel, topWheel;
        public readonly float topFloor, topFoot;
        /// <summary>The top hall's centre lies this far up the line from the top bullwheel.</summary>
        public const float TopHall = .1f;
        /// <summary>Tower saddle points along the centre of the line (between the two cables), valley first.</summary>
        public readonly List<Vector3> towers = new List<Vector3>();
        /// <summary>The up and down cables: the valley wheel, every tower saddle and the top wheel, offset to either side.</summary>
        public readonly List<Vector3> upCable = new List<Vector3>(), downCable = new List<Vector3>();
        public readonly List<Piste> pistes = new List<Piste>();
        public readonly List<Bay> bays = new List<Bay>();
        /// <summary>Where arriving skiers queue: in front of the valley station's doors.</summary>
        public readonly Vector3 liftDoor, lodgeDoor, gate, plaza;

        public SkiLayout(ProducerState p)
        {
            resort = p;
            peak = SkiResorts.PeakOf(p.id);
            upDirection = SkiResorts.Up(p);
            center = new Vector3(p.cell.x, 0, p.cell.z);
            up = new Vector3(Directions.Dx[upDirection], 0, Directions.Dz[upDirection]);
            right = new Vector3(Directions.Dx[(upDirection + 1) % 4], 0, Directions.Dz[(upDirection + 1) % 4]);
            facing = Quaternion.LookRotation(up);
            // The lift: from the valley station (back right of the base) to just below the summit.
            valleyWheel = At(1.45f, 1.45f, ValleyCable);
            var summit = new Vector3(SkiResorts.Peaks[peak].x, 0, SkiResorts.Peaks[peak].z);
            var flat = summit - new Vector3(valleyWheel.x, 0, valleyWheel.z);
            var along = flat.normalized;
            var topXZ = summit - along * 1.2f;
            // The hall sits on the slope's uphill edge, a little dug in; its plinth goes down to the lowest corner.
            var across = Vector3.Cross(Vector3.up, along);
            float highest = float.MinValue, lowest = float.MaxValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var c = topXZ + along * (TopHall + (corner < 2 ? -.68f : .68f)) + across * (corner % 2 == 0 ? -.63f : .63f);
                float g = Ground(c.x, c.z);
                highest = Mathf.Max(highest, g);
                lowest = Mathf.Min(lowest, g);
            }
            topFloor = highest - .15f;
            topFoot = lowest;
            topWheel = new Vector3(topXZ.x, topFloor + TopCable, topXZ.z);
            BuildLine(along);
            liftDoor = At(1.45f, .35f, 0);
            lodgeDoor = At(-1.4f, .25f, 0);
            gate = At(0, FrontF, Deck);
            plaza = At(.05f, 1.9f, 0);
            BuildPistes(along);
            BuildBays();
        }
        /// <summary>A point in the base's frame: a to the right, f up the mountain, y above the ground.</summary>
        public Vector3 At(float a, float f, float y) => center + right * a + up * f + Vector3.up * y;
        /// <summary>Horizontal direction of the lift line, valley to top.</summary>
        public Vector3 LineDirection
        {
            get
            {
                var d = topWheel - valleyWheel;
                d.y = 0;
                return d.normalized;
            }
        }

        /// <summary>The height of the drawn terrain at a point: the same two triangles per cell as WorldView.BuildLand.</summary>
        public static float Ground(float x, float z)
        {
            int cx = Mathf.RoundToInt(x), cz = Mathf.RoundToInt(z);
            float y0 = MapDefinition.Height(cx - .5f, cz - .5f), y1 = MapDefinition.Height(cx - .5f, cz + .5f);
            float y2 = MapDefinition.Height(cx + .5f, cz + .5f), y3 = MapDefinition.Height(cx + .5f, cz - .5f);
            float u = Mathf.Clamp01(x - (cx - .5f)), v = Mathf.Clamp01(z - (cz - .5f));
            return v >= u ? y0 + v * (y1 - y0) + u * (y2 - y1) : y0 + u * (y3 - y0) + v * (y2 - y3);
        }
        public static Vector3 OnGround(Vector3 p, float lift = 0) => new Vector3(p.x, Ground(p.x, p.z) + lift, p.z);

        /// <summary>
        /// Towers at even spacing between the stations, then raised span by span until the lowest point of every cabin
        /// (the cable sags a little mid-span) clears the ground under it.
        /// </summary>
        void BuildLine(Vector3 along)
        {
            var from = new Vector3(valleyWheel.x, 0, valleyWheel.z);
            float length = Vector3.Distance(from, new Vector3(topWheel.x, 0, topWheel.z));
            int spans = Mathf.Max(2, Mathf.CeilToInt(length / SpanTarget));
            var heights = new float[spans + 1];
            var points = new Vector3[spans + 1];
            for (int i = 0; i <= spans; i++)
            {
                points[i] = from + along * (length * i / spans);
                heights[i] = i == 0 ? valleyWheel.y : i == spans ? topWheel.y : Ground(points[i].x, points[i].z) + TowerHeight;
            }
            for (int pass = 0; pass < 12; pass++)
            {
                bool raised = false;
                for (int i = 0; i < spans; i++)
                {
                    float spanLength = length / spans;
                    for (int k = 1; k < 16; k++)
                    {
                        float s = k / 16f;
                        var p = Vector3.Lerp(points[i], points[i + 1], s);
                        float cabinBottom = Mathf.Lerp(heights[i], heights[i + 1], s) - Sag(s, spanLength) - CabinDrop - .12f;
                        float short_ = Ground(p.x, p.z) + Clearance - cabinBottom;
                        if (short_ <= 0)
                            continue;
                        // Lift whichever ends are towers, more at the nearer one when the other is a station.
                        bool left = i > 0, rightTower = i + 1 < spans;
                        if (left && rightTower) { heights[i] += short_; heights[i + 1] += short_; }
                        else if (left) heights[i] += short_ / Mathf.Max(.2f, 1 - s);
                        else if (rightTower) heights[i + 1] += short_ / Mathf.Max(.2f, s);
                        raised = true;
                        break;
                    }
                }
                if (!raised)
                    break;
            }
            var side = Vector3.Cross(Vector3.up, along) * LineGap;
            for (int i = 0; i <= spans; i++)
            {
                var saddle = new Vector3(points[i].x, heights[i], points[i].z);
                if (i > 0 && i < spans)
                    towers.Add(saddle);
                upCable.Add(saddle + side);
                downCable.Add(saddle - side);
            }
        }
        /// <summary>How far the cable hangs below the straight line between two supports, s along the span.</summary>
        public static float Sag(float s, float spanLength) => .05f * spanLength / SpanTarget * 4 * s * (1 - s);
        /// <summary>A point on a cable (a list of supports), <paramref name="distance"/> along it horizontally, with the sag.</summary>
        public static Vector3 OnCable(List<Vector3> cable, float distance, out Vector3 heading)
        {
            for (int i = 0; i + 1 < cable.Count; i++)
            {
                Vector3 a = cable[i], b = cable[i + 1];
                var flat = new Vector3(b.x - a.x, 0, b.z - a.z);
                float span = flat.magnitude;
                if (distance > span && i + 2 < cable.Count)
                {
                    distance -= span;
                    continue;
                }
                float s = span > 1e-4f ? Mathf.Clamp01(distance / span) : 1;
                heading = span > 1e-4f ? flat / span : Vector3.forward;
                var p = Vector3.Lerp(a, b, s);
                p.y -= Sag(s, span);
                return p;
            }
            heading = Vector3.forward;
            return cable[cable.Count - 1];
        }
        public static float CableLength(List<Vector3> cable)
        {
            float length = 0;
            for (int i = 0; i + 1 < cable.Count; i++)
                length += new Vector2(cable[i + 1].x - cable[i].x, cable[i + 1].z - cable[i].z).magnitude;
            return length;
        }

        /// <summary>
        /// Three runs from beside the top station down to the plaza: a wide easy one that swings out in two big turns, an
        /// intermediate one with three turns on the other side and a short, straight expert run.
        /// </summary>
        void BuildPistes(Vector3 along)
        {
            var across = Vector3.Cross(Vector3.up, along);
            var top = new Vector3(topWheel.x, 0, topWheel.z);
            float length = Vector3.Distance(new Vector3(valleyWheel.x, 0, valleyWheel.z), top);
            // start offset across the line, end a in the base frame, swing amplitude (signed), turns, width, grade
            AddPiste(top - along * .7f - across * .75f, -.15f, -.12f * length, 2, .95f, 0);
            AddPiste(top - along * .7f + across * .45f, .25f, .09f * length, 3, .8f, 1);
            AddPiste(top - along * .5f - across * .15f, .05f, -.03f * length, 1, .6f, 2);
        }
        void AddPiste(Vector3 start, float endA, float swing, int turns, float width, int grade)
        {
            var end = At(endA, 1.6f, 0);
            end.y = 0;
            var direction = end - start;
            float length = direction.magnitude;
            var across = Vector3.Cross(Vector3.up, direction / length);
            int samples = Mathf.CeilToInt(length / .15f);
            var line = new List<Vector3>(samples + 1);
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                // Turns fade out at both ends so the run leaves the station and reaches the plaza head on.
                var p = start + direction * t + across * (swing * Mathf.Sin(Mathf.PI * turns * t) * Mathf.Sin(Mathf.PI * t));
                line.Add(OnGround(p));
            }
            pistes.Add(new Piste { line = line, width = width, grade = grade });
        }
        /// <summary>A point on a piste: t from the top (0) to the plaza (1), side from -1 (left edge) to 1 (right edge).</summary>
        public Vector3 OnPiste(int index, float t, float side, out Vector3 downhill)
        {
            var line = pistes[index].line;
            float x = Mathf.Clamp01(t) * (line.Count - 1);
            int i = Mathf.Min(line.Count - 2, (int)x);
            var a = line[i];
            var b = line[i + 1];
            downhill = b - a;
            var flat = new Vector3(downhill.x, 0, downhill.z).normalized;
            var p = Vector3.Lerp(a, b, x - i) + Vector3.Cross(Vector3.up, flat) * (side * pistes[index].width * .5f);
            return OnGround(p);
        }

        /// <summary>
        /// Two rows of bays either side of an aisle across the front of the base: the front row leaves a gap for the lane
        /// from the gate. Cars park nose in, facing away from the aisle.
        /// </summary>
        void BuildBays()
        {
            const float pitch = .42f, frontF = -2.02f, backF = -.8f;
            for (int i = 0; i < 11; i++)
            {
                float a = -2.1f + i * pitch;
                if (Mathf.Abs(a) > .35f)
                    bays.Add(new Bay { at = At(a, frontF, Deck), facing = -up, aisle = At(a, AisleF, Deck) });
            }
            for (int i = 0; i < 11; i++)
            {
                float a = -2.1f + i * pitch;
                bays.Add(new Bay { at = At(a, backF, Deck), facing = up, aisle = At(a, AisleF, Deck) });
            }
        }
    }
}
