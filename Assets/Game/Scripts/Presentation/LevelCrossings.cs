using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Barriers at every level crossing, where a road crosses a straight track at right angles. The road runs over the rails
    /// on a deck, ramping up to it from the street on either side. On each road approach a post carries a crossbuck and a
    /// pair of red lamps facing each way, and a red-and-white boom closes the lane into the crossing. While a train is near
    /// (<see cref="RoadLanes.TracksNearTrains"/>) the lamps flash and the booms come down; <see cref="CityTraffic"/> waits
    /// until they are back up. Cosmetic only: trains never wait for a barrier.
    /// </summary>
    public sealed class LevelCrossings : MonoBehaviour
    {
        sealed class Crossing
        {
            public int trackId;
            public CrossingBarrier barrier;
            // Each boom's pivot and its turn with the arm lying across the road.
            public readonly List<(Transform pivot, Quaternion down)> booms = new List<(Transform, Quaternion)>();
            // Lamps in side-by-side pairs, one pair on each face of every post; each pair flashes in turn.
            public readonly List<Renderer> lamps = new List<Renderer>();
        }
        /// <summary>Top of the road deck over the rails, just under the rail heads; cars ride up onto it.</summary>
        public const float DeckTop = .15f;
        // Distances from the crossing's centre: "along" towards the road a barrier closes, "across" to the kerb on the
        // approaching driver's right (cars keep right). The boom stands on the track side of its post and, lowered, reaches
        // just past the middle of the road: it closes the lane into the crossing and leaves the lane out of it free.
        const float PostAlong = .44f, PostAcross = .44f, PostHeight = .52f, BoomAlong = .38f, BoomHeight = .27f, BoomLength = .56f;
        const float DeckWidth = .8f, RampFoot = .03f, RampDepth = .16f, RaisedDegrees = 84f, FlashPeriod = .9f;
        const int BoomStripes = 6;
        // Deck slabs along the road: outside each rail and between them, leaving a flangeway at each rail (rails at ±.2).
        static readonly (float from, float to)[] Slabs = { (-.5f, -.23f), (-.17f, .17f), (.23f, .5f) };
        static readonly Color Asphalt = new Color(.3f, .3f, .32f), Steel = new Color(.28f, .29f, .31f), Board = new Color(.08f, .08f, .09f),
            White = new Color(.96f, .96f, .94f), Red = new Color(.86f, .11f, .09f), LampOn = new Color(1f, .3f, .18f), LampOff = new Color(.26f, .07f, .06f);
        readonly List<Crossing> crossings = new List<Crossing>();
        readonly Dictionary<int, Crossing> byTrack = new Dictionary<int, Crossing>();
        readonly HashSet<int> near = new HashSet<int>();
        readonly List<Mesh> artMeshes = new List<Mesh>();
        Mesh cube, boom;
        Material[] boomFinish;
        WorldView world;
        GameSession game;
        int revision = -1, cityRevision = -1;
        float flash;

        /// <summary>How many level crossings have a barrier.</summary>
        public int Count => crossings.Count;
        /// <summary>Whether cars may drive onto this track piece: no barrier there, or its booms are fully up with no train near.</summary>
        public bool Open(int trackId) => !byTrack.TryGetValue(trackId, out var crossing) || crossing.barrier.Open;
        /// <summary>How far the booms on this track piece are down: 0 up (or no barrier), 1 across the road.</summary>
        public float Lowered(int trackId) => byTrack.TryGetValue(trackId, out var crossing) ? crossing.barrier.lowered : 0;

        public void Refresh(WorldView view, GameSession session)
        {
            if (game == session && revision == session.World.revision && cityRevision == session.World.cityRevision)
                return;
            // Track ids start over in every game, so a new or loaded game never inherits a barrier's swing.
            if (game != session)
                crossings.Clear();
            world = view;
            game = session;
            revision = session.World.revision;
            cityRevision = session.World.cityRevision;
            if (!cube)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = primitive.GetComponent<MeshFilter>().sharedMesh;
                primitive.SetActive(false);
                Destroy(primitive);
            }
            if (!boom)
                BuildBoom();
            Rebuild(RoadLanes.Build(session.World, session.Network));
        }
        /// <summary>Moves the booms by <paramref name="seconds"/> of game time (0 while paused); the lamps flash in real time.</summary>
        public void Animate(float seconds, float realSeconds)
        {
            if (game == null || crossings.Count == 0)
                return;
            RoadLanes.TracksNearTrains(game.World, near);
            flash += realSeconds;
            foreach (var crossing in crossings)
            {
                crossing.barrier = crossing.barrier.Advance(near.Contains(crossing.trackId), seconds);
                Pose(crossing);
            }
        }
        /// <summary>Redraws every crossing. A barrier on the same track piece keeps its state, so a redraw never jerks a boom.</summary>
        void Rebuild(RoadLanes lanes)
        {
            var before = new Dictionary<int, CrossingBarrier>();
            foreach (var crossing in crossings)
                before[crossing.trackId] = crossing.barrier;
            Clear();
            RoadLanes.TracksNearTrains(game.World, near);
            var art = new Dictionary<Material, List<CombineInstance>>();
            foreach (var track in game.World.tracks)
            {
                // Only a straight track crossed at right angles carries a road (RoadLanes.Crosses).
                int exits = lanes.Exits(track.cell);
                if (exits == 0)
                    continue;
                var crossing = new Crossing
                {
                    trackId = track.id,
                    barrier = before.TryGetValue(track.id, out var kept) ? kept : CrossingBarrier.Settled(near.Contains(track.id)),
                };
                var root = new GameObject("Level crossing " + track.id).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(track.cell.x, 0, track.cell.z);
                Deck(art, track.cell, exits);
                for (int d = 0; d < 4; d++)
                    if ((exits & 1 << d) != 0)
                        Approach(art, crossing, root, d);
                crossings.Add(crossing);
                byTrack[track.id] = crossing;
                Pose(crossing);
            }
            if (art.Count == 0)
                return;
            var (mesh, finish) = Merge(art, "Level crossing art");
            artMeshes.Add(mesh);
            var go = new GameObject("Level crossing art", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = finish;
        }
        void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            foreach (var mesh in artMeshes)
                Destroy(mesh);
            artMeshes.Clear();
            crossings.Clear();
            byTrack.Clear();
        }
        /// <summary>The road over the rails, and a ramp up to it on every side where a street (not another track) meets it.</summary>
        void Deck(Dictionary<Material, List<CombineInstance>> art, Cell cell, int exits)
        {
            var center = new Vector3(cell.x, 0, cell.z);
            var along = (exits & 5) != 0 ? Vector3.forward : Vector3.right;
            var across = Vector3.one - Vector3.up - along;
            foreach (var (from, to) in Slabs)
                Part(art, center + along * ((from + to) / 2) + Vector3.up * (DeckTop / 2), along * (to - from) + across * DeckWidth + Vector3.up * DeckTop, Asphalt);
            for (int d = 0; d < 4; d++)
            {
                if ((exits & 1 << d) == 0 || game.Network.At(cell.Move(d)) != null)
                    continue;
                // Sloping from the middle of the street beside up to the deck's edge; its underside sinks into the ground.
                var toward = Direction(d);
                Vector3 low = center + toward + Vector3.up * RampFoot, high = center + toward * .5f + Vector3.up * DeckTop;
                var slope = Quaternion.LookRotation(high - low);
                Part(art, (low + high) / 2 - slope * Vector3.up * (RampDepth / 2), new Vector3(DeckWidth, RampDepth, Vector3.Distance(low, high)), Asphalt, slope);
            }
        }
        /// <summary>One road approach: the post with its crossbuck and lamps, and the boom beside it.</summary>
        void Approach(Dictionary<Material, List<CombineInstance>> art, Crossing crossing, Transform root, int d)
        {
            var center = root.localPosition;
            var along = Direction(d);
            var heading = -along; // an approaching car drives this way
            var right = Vector3.Cross(Vector3.up, heading);
            var facing = Quaternion.LookRotation(along); // the signs face the approaching driver
            var post = along * PostAlong + right * PostAcross;
            Part(art, center + post + Vector3.up * (PostHeight / 2), new Vector3(.045f, PostHeight, .045f), Steel);
            // A crossbuck on top: two white boards edged in red.
            var buck = center + post + Vector3.up * (PostHeight - .05f) + along * .03f;
            for (int s = -1; s <= 1; s += 2)
            {
                var turn = facing * Quaternion.Euler(0, 0, s * 40);
                Part(art, buck, new Vector3(.25f, .05f, .01f), Red, turn);
                Part(art, buck + along * .006f, new Vector3(.23f, .03f, .01f), White, turn);
            }
            // A black board well below the crossbuck with a pair of lamps on each face, so the flashing shows from any view.
            var board = post + Vector3.up * (PostHeight - .23f) + along * .03f;
            Part(art, center + board, new Vector3(.21f, .095f, .015f), Board, facing);
            for (int face = 1; face >= -1; face -= 2)
                for (int s = -1; s <= 1; s += 2)
                    crossing.lamps.Add(Lamp(root, board + along * (face * .012f) + right * (s * .06f), facing));
            // The boom's drive housing, then the boom itself pivoting on top of it.
            var pivot = along * BoomAlong + right * PostAcross;
            Part(art, center + pivot + right * .02f + Vector3.up * (BoomHeight / 2), new Vector3(.08f, BoomHeight, .08f), Steel);
            var arm = new GameObject("Barrier boom", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            arm.SetParent(root, false);
            arm.localPosition = pivot + Vector3.up * BoomHeight;
            arm.GetComponent<MeshFilter>().sharedMesh = boom;
            arm.GetComponent<MeshRenderer>().sharedMaterials = boomFinish;
            crossing.booms.Add((arm, Quaternion.LookRotation(heading)));
        }
        Renderer Lamp(Transform root, Vector3 local, Quaternion turn)
        {
            var lamp = new GameObject("Crossing lamp", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            lamp.SetParent(root, false);
            lamp.localPosition = local;
            lamp.localRotation = turn;
            lamp.localScale = new Vector3(.07f, .07f, .012f);
            lamp.GetComponent<MeshFilter>().sharedMesh = cube;
            var renderer = lamp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = world.Mat(LampOff);
            return renderer;
        }
        /// <summary>Swings the booms (easing into each end) and flashes the lamps in turn.</summary>
        void Pose(Crossing crossing)
        {
            // In the pivot's frame the arm lies along -x, towards the middle of the road; raising turns it up about the heading.
            var lift = Quaternion.Euler(0, 0, -RaisedDegrees * (1 - Mathf.SmoothStep(0, 1, crossing.barrier.lowered)));
            foreach (var (pivot, down) in crossing.booms)
                pivot.localRotation = down * lift;
            bool first = Mathf.Repeat(flash, FlashPeriod) < FlashPeriod / 2;
            for (int i = 0; i < crossing.lamps.Count; i++)
                crossing.lamps[i].sharedMaterial = world.Mat(crossing.barrier.Flashing && (i % 2 == 0) == first ? LampOn : LampOff);
        }
        /// <summary>The boom in its pivot's frame: a striped arm along -x with a red tip, the hinge and a counterweight behind it.</summary>
        void BuildBoom()
        {
            var parts = new Dictionary<Material, List<CombineInstance>>();
            float stripe = BoomLength / BoomStripes;
            for (int i = 0; i < BoomStripes; i++)
                Part(parts, new Vector3(-(i + .5f) * stripe, 0, 0), new Vector3(stripe, .05f, .04f), (BoomStripes - 1 - i) % 2 == 0 ? Red : White);
            Part(parts, Vector3.zero, new Vector3(.06f, .06f, .07f), Steel);
            Part(parts, new Vector3(.07f, 0, 0), new Vector3(.08f, .07f, .05f), Steel);
            (boom, boomFinish) = Merge(parts, "Barrier boom");
        }
        void Part(Dictionary<Material, List<CombineInstance>> parts, Vector3 at, Vector3 size, Color color, Quaternion? turn = null)
        {
            var material = world.Mat(color);
            if (!parts.TryGetValue(material, out var list))
                parts[material] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(at, turn ?? Quaternion.identity, size) });
        }
        /// <summary>One mesh with a submesh per material.</summary>
        static (Mesh mesh, Material[] finish) Merge(Dictionary<Material, List<CombineInstance>> parts, string name)
        {
            var pieces = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var pair in parts)
            {
                var piece = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                piece.CombineMeshes(pair.Value.ToArray(), true, true);
                pieces.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity });
                materials.Add(pair.Key);
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(pieces.ToArray(), false, true);
            foreach (var piece in pieces)
                Destroy(piece.mesh);
            return (mesh, materials.ToArray());
        }
        static Vector3 Direction(int d) => new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
        void OnDestroy()
        {
            foreach (var mesh in artMeshes)
                if (mesh)
                    Destroy(mesh);
            if (boom)
                Destroy(boom);
        }
    }
}
