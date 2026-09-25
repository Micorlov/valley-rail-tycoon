using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// The flashing beacons of the police cars, ambulances and fire engines in <see cref="CityTraffic"/>, and which of them
    /// are out on an emergency call with their siren on (<see cref="SoundEffects"/>). The shared body mesh paints each lens
    /// dark; over them a vehicle carries one lamp per beat and colour, a small mesh shared by every vehicle of its model,
    /// shown on its beat. The beacons flash in real time like the level-crossing lamps, so a paused town still shows them,
    /// and every vehicle keeps its own rhythm. Each vehicle drives one trip after another, and only CallChance of the trips
    /// are calls.
    /// </summary>
    public sealed class EmergencyLights
    {
        // Seconds each of the two beats lasts; a lamp overhangs its lens by Glow on every side.
        const float Beat = .22f, Glow = .008f;
        // A trip lasts ShortestTrip..LongestTrip seconds of driving (game time); CallChance of them are emergency calls.
        const float CallChance = .05f, ShortestTrip = 20, LongestTrip = 40;
        sealed class Unit
        {
            public Transform vehicle;
            public float tripLeft;
            public bool onCall;
        }
        readonly List<(Renderer lamp, int beat, float offset)> lamps = new List<(Renderer, int, float)>();
        readonly List<Unit> units = new List<Unit>();
        readonly List<Transform> sirens = new List<Transform>();
        readonly Dictionary<VehicleModel, List<(Mesh mesh, Color color, int beat)>> meshes = new Dictionary<VehicleModel, List<(Mesh, Color, int)>>();
        readonly System.Random random = new System.Random(112);

        /// <summary>The emergency vehicles out on a call right now, siren on; one that left is dropped by the next <see cref="Drive"/>.</summary>
        public IReadOnlyList<Transform> Sirens => sirens;

        /// <summary>
        /// Adds the lamps of <paramref name="model"/> to a vehicle; nothing for a vehicle without beacons. A vehicle that
        /// <paramref name="roams"/> drives trips, now and then on a call; any other sounds its siren through <see cref="Sound"/>.
        /// </summary>
        public void Fit(WorldView world, Transform vehicle, VehicleModel model, bool roams = true)
        {
            if (model.beacons.Length == 0)
                return;
            if (!meshes.TryGetValue(model, out var groups))
                meshes[model] = groups = BuildLamps(model);
            float offset = (float)random.NextDouble() * Beat * 2;
            foreach (var (mesh, color, beat) in groups)
            {
                var lamp = new GameObject("Beacon", typeof(MeshFilter), typeof(MeshRenderer));
                lamp.transform.SetParent(vehicle, false);
                lamp.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = lamp.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = world.Mat(color);
                renderer.enabled = false;
                lamps.Add((renderer, beat, offset));
            }
            if (!roams)
                return;
            var unit = new Unit { vehicle = vehicle };
            units.Add(unit);
            StartTrip(unit, random.NextDouble() < CallChance);
            // Join partway through the first trip, so the vehicles' trips do not all end together.
            unit.tripLeft *= (float)random.NextDouble();
        }

        /// <summary>Drives every vehicle's trip on by <paramref name="seconds"/> of game time and starts the next trip where one ends.</summary>
        public void Drive(float seconds)
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var unit = units[i];
                if (!unit.vehicle)
                {
                    units.RemoveAt(i);
                    continue;
                }
                unit.tripLeft -= seconds;
                if (unit.tripLeft <= 0)
                    StartTrip(unit, random.NextDouble() < CallChance);
            }
            sirens.RemoveAll(vehicle => !vehicle);
        }

        /// <summary>Starts a new trip for <paramref name="vehicle"/>: an emergency call with its siren on, or an ordinary drive.</summary>
        public void Dispatch(Transform vehicle, bool call)
        {
            foreach (var unit in units)
                if (unit.vehicle == vehicle)
                    StartTrip(unit, call);
        }
        /// <summary>Turns the siren of a vehicle fitted without trips (a fire engine on its way to a fire) on or off.</summary>
        public void Sound(Transform vehicle, bool on)
        {
            if (!on)
                sirens.Remove(vehicle);
            else if (!sirens.Contains(vehicle))
                sirens.Add(vehicle);
        }
        void StartTrip(Unit unit, bool call)
        {
            unit.tripLeft = Mathf.Lerp(ShortestTrip, LongestTrip, (float)random.NextDouble());
            if (call && !unit.onCall)
                sirens.Add(unit.vehicle);
            else if (!call && unit.onCall)
                sirens.Remove(unit.vehicle);
            unit.onCall = call;
        }

        /// <summary>Lights each lamp on its beat at <paramref name="time"/> seconds and forgets the lamps of vehicles that left.</summary>
        public void Flash(float time)
        {
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var (lamp, beat, offset) = lamps[i];
                if (!lamp)
                {
                    lamps.RemoveAt(i);
                    continue;
                }
                bool on = (int)((time + offset) / Beat) % 2 == beat;
                if (lamp.enabled != on)
                    lamp.enabled = on;
            }
        }

        /// <summary>Destroys the shared lamp meshes; the lamps themselves go with their vehicles.</summary>
        public void Release()
        {
            foreach (var groups in meshes.Values)
                foreach (var group in groups)
                    Object.Destroy(group.mesh);
            meshes.Clear();
        }

        /// <summary>One lamp mesh per beat and lit colour, each holding the glowing boxes over that beat's lenses.</summary>
        static List<(Mesh mesh, Color color, int beat)> BuildLamps(VehicleModel model)
        {
            var groups = new List<(Mesh, Color, int)>();
            for (int beat = 0; beat <= 1; beat++)
            {
                var colors = new List<Color>();
                foreach (var lens in model.beacons)
                    if (lens.beat == beat && !colors.Contains(lens.flash))
                        colors.Add(lens.flash);
                foreach (var color in colors)
                {
                    var boxes = new List<VehiclePart>();
                    foreach (var lens in model.beacons)
                        if (lens.beat == beat && lens.flash == color)
                            boxes.Add(lens);
                    groups.Add((Boxes(model.name + " beacon", boxes), color, beat));
                }
            }
            return groups;
        }

        /// <summary>A mesh of boxes, each its lens grown by Glow, with flat faces wound clockwise as Unity draws front faces.</summary>
        static Mesh Boxes(string name, List<VehiclePart> lenses)
        {
            var vertices = new List<Vector3>(lenses.Count * 24);
            var normals = new List<Vector3>(lenses.Count * 24);
            var triangles = new List<int>(lenses.Count * 36);
            foreach (var lens in lenses)
            {
                var half = lens.size / 2 + Vector3.one * Glow;
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 normal = Vector3.zero, u = Vector3.zero, v = Vector3.zero;
                        normal[axis] = sign;
                        u[(axis + 1) % 3] = 1;
                        v[(axis + 2) % 3] = 1;
                        int start = vertices.Count;
                        for (int corner = 0; corner < 4; corner++)
                        {
                            float a = corner == 1 || corner == 2 ? 1 : -1, b = corner >= 2 ? 1 : -1;
                            vertices.Add(lens.center + Vector3.Scale(normal + u * a + v * b, half));
                            normals.Add(normal);
                        }
                        if (sign > 0)
                            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                        else
                            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
