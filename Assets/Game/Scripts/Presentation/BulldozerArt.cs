using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// Site machinery for the station upgrade show, built from live boxes facing +z: the yellow crawler bulldozer (tracks,
    /// engine deck, glazed cab with an amber beacon, exhaust stack, push blade on two arms), an orange dump truck that hauls
    /// the rubble away, and a cement mixer with a turning drum.
    /// </summary>
    public sealed partial class WorldView
    {
        const float DozerScale = 1.35f;
        sealed class Dozer
        {
            public Transform root, blade;
            public Renderer beacon;
        }
        Dozer BuildBulldozer(Transform parent)
        {
            // Local colours: statics declared in other partial files may not be initialised yet when a static here reads them.
            Color yellow = new Color(.98f, .74f, .1f), dark = new Color(.16f, .16f, .17f), steel = new Color(.42f, .42f, .44f), glass = new Color(.2f, .36f, .44f);
            var dozer = new Dozer { root = new GameObject("Bulldozer").transform };
            dozer.root.SetParent(parent, false);
            dozer.root.localScale = Vector3.one * DozerScale;
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Crawler track", new Vector3(side * .2f, .08f, 0), new Vector3(.12f, .15f, .62f), dark, dozer.root);
                Box("Track guard", new Vector3(side * .2f, .165f, 0), new Vector3(.13f, .025f, .56f), yellow, dozer.root);
                Box("Blade arm", new Vector3(side * .2f, .16f, .3f), new Vector3(.04f, .045f, .3f), dark, dozer.root);
            }
            Box("Chassis", new Vector3(0, .2f, -.02f), new Vector3(.3f, .12f, .5f), yellow, dozer.root);
            Box("Engine deck", new Vector3(0, .31f, -.14f), new Vector3(.28f, .1f, .24f), yellow, dozer.root);
            Box("Radiator grille", new Vector3(0, .31f, -.265f), new Vector3(.2f, .07f, .01f), dark, dozer.root);
            Box("Cab glass", new Vector3(0, .41f, .08f), new Vector3(.24f, .2f, .22f), glass, dozer.root);
            for (int i = 0; i < 4; i++)
                Box("Cab post", new Vector3((i % 2 == 0 ? -1 : 1) * .12f, .41f, .08f + (i < 2 ? -1 : 1) * .11f), new Vector3(.025f, .2f, .025f), yellow, dozer.root);
            Box("Cab roof", new Vector3(0, .525f, .08f), new Vector3(.3f, .03f, .28f), yellow, dozer.root);
            Box("Exhaust stack", new Vector3(.09f, .42f, -.2f), new Vector3(.035f, .16f, .035f), dark, dozer.root);
            dozer.beacon = Box("Beacon", new Vector3(0, .56f, .08f), new Vector3(.06f, .045f, .06f), new Color(1f, .55f, .08f), dozer.root).GetComponent<Renderer>();
            dozer.blade = Box("Blade", new Vector3(0, .13f, .47f), new Vector3(.54f, .2f, .05f), yellow, dozer.root, Quaternion.Euler(-12, 0, 0)).transform;
            Box("Blade edge", new Vector3(0, .035f, .49f), new Vector3(.54f, .03f, .05f), steel, dozer.root);
            return dozer;
        }
        /// <summary>The beacon blinks and the blade bobs while the engine works.</summary>
        void AnimateBulldozer(Dozer dozer, float time, bool pushing)
        {
            bool lit = Mathf.Repeat(time * 3f, 1f) < .5f;
            dozer.beacon.sharedMaterial = Mat(lit ? new Color(1f, .62f, .1f) : new Color(.45f, .22f, .05f));
            float lift = pushing ? .02f : .06f + Mathf.Sin(time * 9f) * .01f;
            dozer.blade.localPosition = new Vector3(0, .07f + lift, .47f);
        }
        /// <summary>Three axles and a tyre at each end of each: shared by both trucks.</summary>
        void TruckChassis(Transform root, Color cab, Color dark, Color glass)
        {
            Box("Chassis", new Vector3(0, .12f, -.02f), new Vector3(.28f, .06f, .78f), dark, root);
            foreach (float z in new[] { .27f, -.1f, -.27f })
                for (int side = -1; side <= 1; side += 2)
                    Box("Tyre", new Vector3(side * .16f, .065f, z), new Vector3(.07f, .13f, .13f), new Color(.1f, .1f, .11f), root);
            Box("Cab", new Vector3(0, .28f, .27f), new Vector3(.3f, .22f, .2f), cab, root);
            Box("Windscreen", new Vector3(0, .32f, .375f), new Vector3(.26f, .09f, .012f), glass, root);
            Box("Bumper", new Vector3(0, .13f, .385f), new Vector3(.3f, .05f, .03f), dark, root);
            Box("Roof lamp", new Vector3(0, .405f, .27f), new Vector3(.08f, .03f, .05f), new Color(1f, .62f, .1f), root);
        }
        /// <summary>An orange tipper; <paramref name="load"/> is the rubble heap in its bed, hidden until the rubble is loaded.</summary>
        Transform BuildDumpTruck(Transform parent, out Transform load)
        {
            Color body = new Color(.93f, .45f, .12f), dark = new Color(.16f, .16f, .17f), glass = new Color(.2f, .36f, .44f);
            var root = new GameObject("Dump truck").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * DozerScale;
            TruckChassis(root, body, dark, glass);
            Box("Bed floor", new Vector3(0, .19f, -.12f), new Vector3(.32f, .03f, .46f), body, root);
            for (int side = -1; side <= 1; side += 2)
                Box("Bed side", new Vector3(side * .155f, .27f, -.12f), new Vector3(.02f, .14f, .46f), body, root);
            Box("Tailgate", new Vector3(0, .27f, -.355f), new Vector3(.32f, .14f, .02f), body, root);
            Box("Headboard", new Vector3(0, .3f, .115f), new Vector3(.32f, .2f, .02f), dark, root);
            load = new GameObject("Rubble load").transform;
            load.SetParent(root, false);
            Box("Rubble", new Vector3(0, .27f, -.12f), new Vector3(.28f, .12f, .42f), new Color(.52f, .42f, .33f), load);
            Box("Rubble", new Vector3(.04f, .35f, -.08f), new Vector3(.16f, .08f, .22f), new Color(.66f, .38f, .28f), load, Quaternion.Euler(0, 20, 8));
            load.gameObject.SetActive(false);
            return root;
        }
        /// <summary>A white mixer lorry; <paramref name="drum"/> turns about its long axis while the concrete is poured.</summary>
        Transform BuildMixer(Transform parent, out Transform drum)
        {
            Color white = new Color(.92f, .92f, .9f), dark = new Color(.16f, .16f, .17f), glass = new Color(.2f, .36f, .44f), stripe = new Color(.93f, .45f, .12f);
            var root = new GameObject("Cement mixer").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * DozerScale;
            TruckChassis(root, white, dark, glass);
            var mount = new GameObject("Drum mount").transform;
            mount.SetParent(root, false);
            mount.localPosition = new Vector3(0, .34f, -.12f);
            mount.localRotation = Quaternion.Euler(-12, 0, 0);
            drum = new GameObject("Drum").transform;
            drum.SetParent(mount, false);
            Box("Drum shell", Vector3.zero, new Vector3(.25f, .25f, .44f), white, drum);
            Box("Drum stripe", new Vector3(0, .1f, 0), new Vector3(.27f, .06f, .46f), stripe, drum);
            Box("Drum stripe", new Vector3(0, -.1f, 0), new Vector3(.27f, .06f, .46f), stripe, drum);
            Box("Chute", new Vector3(0, .24f, -.4f), new Vector3(.06f, .04f, .16f), dark, root, Quaternion.Euler(25, 0, 0));
            return root;
        }
    }
}
