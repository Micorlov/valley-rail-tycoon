using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// The working parts of the map's industries (pumpjacks, a mine hoist, a sawmill log deck, a refinery flare, furnace
    /// mouths and smoking stacks), drawn as loose parts and handed to <see cref="IndustryMotion"/>, which moves them.
    /// </summary>
    public sealed partial class WorldView
    {
        // Literal colours only: statics of other partial files may not be initialised yet when this file's run.
        static readonly Color MachineSteel = new Color(.34f, .46f, .51f), MachineRust = new Color(.69f, .32f, .2f),
            MachineDark = new Color(.055f, .12f, .16f), MachineGold = new Color(.95f, .65f, .24f), SawlogBrown = new Color(.55f, .32f, .15f);
        IndustryMotion industry;

        void CreateIndustryMotion()
        {
            industry = Root("Industry motion").gameObject.AddComponent<IndustryMotion>();
            industry.Initialize(this);
        }
        void DrawOilWells(Transform root)
        {
            float phase = IndustryMotion.Seed(root.position);
            for (int i = 0; i < 2; i++)
                DrawPumpJack(root, -.7f + i * 1.4f, phase + i * .37f);
        }
        /// <summary>A beam pump: the crank turns, the pitman arms rock the walking beam, and the horse head lifts the rod.</summary>
        void DrawPumpJack(Transform yard, float x, float phase)
        {
            var pump = IndustryPivot("Pumpjack", new Vector3(x, 0, 0), yard);
            Box("Pump skid", new Vector3(0, .08f, -.05f), new Vector3(.62f, .12f, 2.1f), MachineDark, pump);
            // The Samson post: four legs leaning in to the saddle bearing the beam rocks on.
            var saddle = IndustryMotion.BeamPivot - new Vector3(0, .05f, 0);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, end = i < 2 ? -1 : 1;
                IndustryStrut("Samson post leg", new Vector3(side * .15f, .14f, end * .36f), saddle + new Vector3(side * .05f, 0, 0), .07f, MachineSteel, pump);
            }
            Box("Saddle bearing", saddle, new Vector3(.2f, .08f, .14f), MachineDark, pump);
            var beam = IndustryPivot("Walking beam", IndustryMotion.BeamPivot, pump);
            Box("Beam", new Vector3(0, .07f, -.12f), new Vector3(.14f, .14f, 1.9f), MachineGold, beam);
            Box("Horse head", new Vector3(0, -.08f, .11f - IndustryMotion.HeadArm), new Vector3(.2f, .62f, .22f), MachineRust, beam);
            Box("Equalizer", new Vector3(0, -.02f, IndustryMotion.TailArm), new Vector3(.52f, .07f, .08f), MachineDark, beam);
            Box("Gear reducer", new Vector3(0, .38f, IndustryMotion.CrankAxle.z), new Vector3(.26f, .36f, .34f), MachineSteel, pump);
            var crank = IndustryPivot("Crank", IndustryMotion.CrankAxle, pump);
            Box("Crank shaft", Vector3.zero, new Vector3(.44f, .05f, .05f), MachineDark, crank);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                Box("Crank arm", new Vector3(side * .2f, 0, -.1f), new Vector3(.04f, .1f, .44f), MachineSteel, crank);
                Box("Counterweight", new Vector3(side * .165f, 0, -.19f), new Vector3(.05f, .3f, .24f), MachineDark, crank);
            }
            Box("Wellhead", new Vector3(0, .2f, -IndustryMotion.HeadArm), new Vector3(.18f, .26f, .18f), MachineSteel, pump);
            Box("Stuffing box", new Vector3(0, .37f, -IndustryMotion.HeadArm), new Vector3(.1f, .1f, .1f), MachineDark, pump);
            Box("Flow line", new Vector3(-Mathf.Sign(x) * .25f, .1f, -IndustryMotion.HeadArm), new Vector3(.36f, .05f, .05f), MachineDark, pump);
            var carrier = IndustryPivot("Rod carrier", Vector3.zero, pump);
            Box("Carrier bar", Vector3.zero, new Vector3(.16f, .04f, .06f), MachineDark, carrier);
            Box("Polished rod", new Vector3(0, -.5f, 0), new Vector3(.035f, 1, .035f), MachineSteel, carrier);
            var pitmans = new[] { MovingPart("Pitman arm", MachineSteel, pump), MovingPart("Pitman arm", MachineSteel, pump) };
            var bridles = new[] { MovingPart("Bridle", MachineDark, pump), MovingPart("Bridle", MachineDark, pump) };
            industry.AddPumpJack(beam, crank, carrier, pitmans, bridles, phase);
        }
        /// <summary>A winding wheel over the shaft; the cage rides up and down on its rope.</summary>
        void DrawHoist(Transform root)
        {
            const float r = IndustryMotion.SheaveRadius;
            var hub = new Vector3(.8f, 2.2f, .5f);
            Box("Headframe cap", new Vector3(.8f, 2.02f, .5f), new Vector3(.75f, .1f, .3f), MachineDark, root);
            for (int i = 0; i < 2; i++)
                Box("Sheave post", new Vector3(.68f + i * .24f, 2.13f, .5f), new Vector3(.04f, .16f, .05f), MachineDark, root);
            Box("Sheave axle", hub, new Vector3(.3f, .04f, .04f), MachineDark, root);
            var wheel = IndustryPivot("Sheave wheel", hub, root);
            const int rim = 10;
            for (int k = 0; k < rim; k++)
                IndustryStrut("Sheave rim", Around(r, k * 360f / rim), Around(r, (k + 1) * 360f / rim), .05f, MachineGold, wheel);
            for (int k = 0; k < 2; k++)
                IndustryStrut("Sheave spoke", Around(r, 45 + k * 90), Around(r, 225 + k * 90), .035f, MachineGold, wheel);
            // The cage hangs from the front of the wheel; the back rope runs down to the winding house.
            var ropeTop = hub - new Vector3(0, 0, r);
            Box("Shaft collar", new Vector3(.8f, .09f, ropeTop.z), new Vector3(.34f, .04f, .28f), MachineDark, root);
            var cage = Box("Hoist cage", new Vector3(.8f, IndustryMotion.HoistMiddle, ropeTop.z), new Vector3(.26f, .3f, .2f), MachineRust, root).transform;
            Box("Winding house", new Vector3(.8f, .3f, 1.2f), new Vector3(.55f, .5f, .4f), MachineSteel, root);
            Box("Winding house roof", new Vector3(.8f, .59f, 1.2f), new Vector3(.62f, .08f, .47f), MachineDark, root);
            IndustryStrut("Winding rope", hub + new Vector3(0, 0, r), new Vector3(.8f, .6f, 1.05f), .025f, MachineDark, root);
            industry.AddHoist(wheel, cage, MovingPart("Hoist rope", MachineDark, root), ropeTop, IndustryMotion.Seed(root.position));
        }
        /// <summary>Logs slide along a deck into the back of the sawmill; sawdust puffs from the saw vent.</summary>
        void DrawLogDeck(Transform root)
        {
            Box("Log deck", new Vector3(-.4f, .12f, 1.05f), new Vector3(.5f, .08f, .8f), MachineDark, root);
            var logs = new Transform[2];
            for (int i = 0; i < logs.Length; i++)
                logs[i] = Box("Sawlog", new Vector3(-.4f, .245f, 1), new Vector3(.17f, .17f, IndustryMotion.LogLength), SawlogBrown, root).transform;
            industry.AddLogFeed(logs, IndustryMotion.Seed(root.position));
            industry.AddStack(root, new Vector3(-.4f, 1.76f, .3f), IndustryMotion.Plume.Sawdust, .75f);
        }
        /// <summary>A flare stack burning off gas behind the refinery, and steam from the taller column.</summary>
        void DrawFlare(Transform root)
        {
            var foot = new Vector3(-1.02f, 0, 1.08f);
            Box("Flare stack", foot + new Vector3(0, 1f, 0), new Vector3(.1f, 2f, .1f), MachineSteel, root);
            Box("Flare tip", foot + new Vector3(0, 2.02f, 0), new Vector3(.16f, .06f, .16f), MachineDark, root);
            var flame = IndustryPivot("Flare flame", foot + new Vector3(0, 2.05f, 0), root);
            flame.localScale = new Vector3(.3f, .75f, .3f);
            Box("Flame", new Vector3(0, .5f, 0), new Vector3(.75f, 1, .75f), new Color(1, .5f, .13f), flame);
            Box("Flame core", new Vector3(0, .28f, 0), new Vector3(1, .56f, 1), new Color(1, .85f, .38f), flame, Quaternion.Euler(0, 45, 0));
            industry.AddFlame(flame, IndustryMotion.Seed(root.position));
            industry.AddStack(root, new Vector3(.9f, 2.2f, .85f), IndustryMotion.Plume.Steam, .8f);
        }
        /// <summary>Glowing tap holes at the foot of each blast furnace, and dark smoke from their tops.</summary>
        void DrawFurnaceMouths(Transform root)
        {
            float phase = IndustryMotion.Seed(root.position);
            for (int i = 0; i < 2; i++)
            {
                float z = -.65f + i * 1.5f;
                var mouth = Box("Furnace mouth", new Vector3(.9f, .3f, z - .335f), new Vector3(.3f, .26f, .03f), new Color(1, .6f, .2f), root);
                industry.AddGlow(mouth.GetComponent<Renderer>(), phase + i * .5f);
                industry.AddStack(root, new Vector3(.9f, 2f + i * .2f, z), IndustryMotion.Plume.Soot);
            }
        }

        static Vector3 Around(float radius, float degrees) =>
            new Vector3(0, radius * Mathf.Sin(degrees * Mathf.Deg2Rad), radius * Mathf.Cos(degrees * Mathf.Deg2Rad));
        static Transform IndustryPivot(string name, Vector3 at, Transform parent)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = at;
            return pivot;
        }
        Transform IndustryStrut(string name, Vector3 a, Vector3 b, float thickness, Color color, Transform parent)
        {
            var part = MovingPart(name, color, parent);
            IndustryMotion.Strut(part, a, b, thickness);
            return part;
        }
        // A unit cube that IndustryMotion stretches into place every frame.
        Transform MovingPart(string name, Color color, Transform parent) => Box(name, Vector3.zero, Vector3.one, color, parent).transform;
    }
}
