using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Measurements of a stadium (the Arena landmark) in its lot frame: x runs between the goals, z across the pitch and
    /// the origin is the centre spot. The building art raises the stands from these numbers and the live match seats its
    /// crowd on the very same rows, so the two always line up.
    /// </summary>
    public readonly struct ArenaLayout
    {
        public const float PitchTop = .06f;
        const float RowRise = .176f; // target riser height per spectator scale unit
        public readonly float w, d, h;
        public readonly int size;
        public ArenaLayout(float w, float d, float h, int size)
        {
            this.w = w;
            this.d = d;
            this.h = h;
            this.size = size;
        }
        public float PitchLength => w * .6f;
        public float PitchWidth => d * .48f;
        /// <summary>Figure scale of a seated fan (1 is a pavement walker).</summary>
        public float Spectator => .17f * size;
        /// <summary>Figure scale of a player on the pitch.</summary>
        public float Player => Mathf.Clamp(.2f * size, .5f, 1.7f);
        public float GoalWidth => PitchWidth * .26f;
        public float GoalHeight => .2f * Player;
        public float GoalDepth => .12f * Player;
        public float Line => Mathf.Max(.012f, .004f * size);
        /// <summary>Top of the roof over the touchline stands.</summary>
        public float RoofTop => h * (size >= 4 ? 1.75f : 1f) + .14f;
        /// <summary>The scoreboard standing on the -z touchline roof (mirror z for the +z one): centre and size.</summary>
        public Vector3 BoardCentre => new Vector3(0, RoofTop + BoardSize.y / 2 + .02f, -d * .44f);
        public Vector3 BoardSize => new Vector3(w * .24f, h * .6f, .06f);
        /// <summary>The stand on one side: 0 and 2 run along the touchlines, 1 and 3 stand behind the goals.</summary>
        public Stand Stand(int side)
        {
            bool alongX = side % 2 == 0;
            var normal = new Vector3(Directions.Dx[side], 0, Directions.Dz[side]);
            float height = alongX ? h : h * .75f, thick = .13f * size;
            // Big grounds keep the back of each stand for the upper tier; small ones rake almost all the way back.
            float rake = thick * (size >= 4 ? .55f : .8f);
            int rows = Mathf.Clamp(Mathf.RoundToInt(height / (RowRise * Spectator)), 3, 8);
            return new Stand(new Vector3(normal.x * w * .41f, 0, normal.z * d * .37f), normal, alongX, height, thick, alongX ? w * .9f : d * .6f, rake, rows);
        }
    }

    /// <summary>One stand: a raked block of seat rows rising away from the pitch, with a solid back behind the rake.</summary>
    public readonly struct Stand
    {
        public readonly Vector3 centre, normal;
        public readonly bool alongX;
        public readonly float height, thick, length, rake;
        public readonly int rows;
        public Stand(Vector3 centre, Vector3 normal, bool alongX, float height, float thick, float length, float rake, int rows)
        {
            this.centre = centre;
            this.normal = normal;
            this.alongX = alongX;
            this.height = height;
            this.thick = thick;
            this.length = length;
            this.rake = rake;
            this.rows = rows;
        }
        /// <summary>Unit vector along the rows.</summary>
        public Vector3 Along => alongX ? Vector3.right : Vector3.forward;
        public float Step => rake / rows;
        public float Rise => height / rows;
        /// <summary>Distance from the stand centre, outwards, of the pitch-side face.</summary>
        public float Front => -thick / 2;
        public float RowTop(int row) => (row + 1) * Rise;
        /// <summary>Rows left standing when the stand is cut away so the camera can see over it.</summary>
        public int CutRows => Mathf.Min(2, rows);
        /// <summary>Middle of a row's tread, where its fans sit.</summary>
        public Vector3 RowCentre(int row) => centre + normal * (Front + (row + .5f) * Step) + Vector3.up * RowTop(row);
        /// <summary>A box size with <paramref name="depth"/> measured away from the pitch and <paramref name="run"/> along the rows.</summary>
        public Vector3 Span(float depth, float up, float run) => alongX ? new Vector3(run, up, depth) : new Vector3(depth, up, run);
    }
}
