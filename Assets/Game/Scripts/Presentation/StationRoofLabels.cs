using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Each station's queue on a navy name board standing on its roof: on the canopy of a halt, or on the hall once an upgrade
    /// has built one. The count is big and "waiting" small beside it; the letters turn amber, then red, as the platform fills
    /// up, and the board stays square to the camera as the view turns. A standing board can be much taller than the narrow
    /// canopy is wide, so even a short one-platform halt gets a sign that reads at the usual zoom.
    /// </summary>
    public sealed partial class WorldView
    {
        // DrawStation's canopy is centred .15 + .95 above the ground, .14 thick and .9 wide.
        const float CanopyTop = .15f + .95f + .07f, CanopyWidth = .9f, RoofTextEnds = .3f;
        // Largest letters (a tenth of the font size is their em in world units), and how far past the roof's ends the sign may
        // reach, seen from the camera. Copies stacked behind the face give the letters their depth.
        const float RoofSignMaxSize = 11, RoofSignOverhang = .6f, RoofSignDepth = .05f;
        const int RoofSignDepthLayers = 2;
        // The board behind the letters: its margin around them and its thickness.
        const float SignBoardMargin = .1f, SignBoardThickness = .06f;
        const string StationSignBoard = "Sign board";
        // Queues below this share one letter size, so a sign does not jump as passengers come and go.
        const int RoofSignSteadyCount = 888;
        const string SmallWords = "<size=55%>";
        // StationArt stands a Grand terminal's .36-wide clock tower .2 in from the hall's end; the sign stops this far short of it.
        const float TowerInset = .2f, TowerHalfWidth = .18f, RoofTextTowerGap = .08f;
        Material roofSignMaterial;
        readonly List<TextMeshPro> roofSignLayers = new List<TextMeshPro>();

        TextMeshPro RoofLabel(StationState s)
        {
            int length = StationLayout.Length(s);
            var along = StationAlong(s);
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            float offset = RoofTextSpan(s, length).offset;
            var face = RoofSignLayer("Station status " + s.id, stationRoot);
            face.transform.localPosition = new Vector3(s.cell.x, StationRoofTop(s), s.cell.z) + along * (StationLayout.Middle(length) + offset) + across;
            face.transform.rotation = Quaternion.Euler(0, labelYaw, 0);
            face.fontSharedMaterial = RoofSignMaterial(face.fontSharedMaterial);
            for (int i = 1; i <= RoofSignDepthLayers; i++)
                RoofSignLayer("Letter depth " + i, face.transform).transform.localPosition = Vector3.forward * (i * RoofSignDepth / RoofSignDepthLayers);
            // Sized to the words each time they change.
            Box(StationSignBoard, Vector3.forward * (RoofSignDepth + SignBoardThickness), Vector3.zero, Navy, face.transform);
            return face;
        }

        TextMeshPro RoofSignLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            var layer = go.GetComponent<TextMeshPro>();
            layer.fontStyle = FontStyles.Bold;
            // The first line's baseline sits on the layer's origin, so the letters stand on the roof.
            layer.alignment = TextAlignmentOptions.Baseline;
            layer.enableWordWrapping = false;
            layer.rectTransform.sizeDelta = new Vector2(20, 2);
            return layer;
        }

        /// <summary>Shows <paramref name="station"/>'s queue on its roof sign; a sign whose words and colour still match is left alone.</summary>
        void ShowStationQueue(StationState station, TextMeshPro face, StationLoad load)
        {
            string text = StationRoofText(station, load, load.waiting);
            var color = StationCrowds.LoadColor(load.Level);
            if (face.text == text && face.color == color)
                return;
            face.fontSize = 10;
            float size = Mathf.Min(RoofSignMaxSize, 10 * RoofSignRoom(station) / face.GetPreferredValues(StationRoofText(station, load, Mathf.Max(load.waiting, RoofSignSteadyCount))).x);
            face.GetComponentsInChildren(roofSignLayers);
            foreach (var layer in roofSignLayers)
            {
                layer.text = text;
                layer.fontSize = size;
                layer.color = layer == face ? color : Color.Lerp(color, Navy, .45f);
            }
            face.ForceMeshUpdate();
            var words = face.textBounds;
            var board = face.transform.Find(StationSignBoard);
            board.localPosition = new Vector3(words.center.x, words.center.y, board.localPosition.z);
            board.localScale = new Vector3(words.size.x + 2 * SignBoardMargin, words.size.y + 2 * SignBoardMargin, SignBoardThickness);
        }

        /// <summary>The count in big letters and what is waiting in small ones; a station that only accepts cargo says what it takes.</summary>
        string StationRoofText(StationState station, StationLoad load, int count) =>
            !load.cargo.HasValue ? SmallWords + "Accepts " + IndustryCatalog.Inputs(game.Cargo.Producer(station.producerId).kind)
            : load.Passengers ? $"{count:N0}{SmallWords} waiting"
            : $"{count:N0}{SmallWords} {IndustryCatalog.CargoName(load.cargo.Value).ToLowerInvariant()} waiting";

        /// <summary>How wide the sign may be: the roof's width across the screen from the usual diagonal view, plus a little overhang.</summary>
        float RoofSignRoom(StationState s) => (RoofTextSpan(s, StationLayout.Length(s)).width + CanopyWidth) * Mathf.Sqrt(.5f) + RoofSignOverhang;

        /// <summary>Keeps every roof sign square to the camera while the view turns.</summary>
        void OrientStationLabels()
        {
            var facing = Quaternion.Euler(0, labelYaw, 0);
            foreach (var label in stationLabels.Values)
                if (label)
                    label.transform.rotation = facing;
        }

        static Vector3 StationAlong(StationState s) => s.axis == 1 ? Vector3.right : Vector3.forward;

        /// <summary>
        /// The stretch of roof the sign stands on, as an offset from the station's middle and a length: clear of both ends,
        /// and on a Grand terminal only up to its clock tower, which would otherwise stand in the letters.
        /// </summary>
        (float offset, float width) RoofTextSpan(StationState s, int length)
        {
            float half = (length - RoofTextEnds) / 2;
            if (s.level < 3 || !StationCatalog.Town(game.Cargo.Producer(s.producerId).kind))
                return (0, half * 2);
            float run = Vector3.Dot(StationHall(s).size, StationAlong(s));
            float end = Mathf.Min(half, run / 2 - TowerInset - TowerHalfWidth - RoofTextTowerGap);
            return ((end - half) / 2, end + half);
        }

        /// <summary>
        /// Top of the roof over the main platform. An upgraded station's hall rises through the canopy; the roof heights on
        /// top of its walls match StationArt: a .3 pitched roof on a town station, a .06 cornice on the bigger halls and a
        /// .22 shed roof on freight depots.
        /// </summary>
        float StationRoofTop(StationState s)
        {
            if (s.level <= 0)
                return CanopyTop;
            var hall = StationHall(s);
            bool town = StationCatalog.Town(game.Cargo.Producer(s.producerId).kind);
            float roof = town ? (s.level == 1 ? .3f : .06f) : .22f;
            return Mathf.Max(CanopyTop, hall.center.y + hall.size.y / 2 + roof);
        }

        /// <summary>One shared outlined copy of the font material for the signs' faces, so they stand out without a material each.</summary>
        Material RoofSignMaterial(Material font)
        {
            if (!roofSignMaterial)
            {
                roofSignMaterial = new Material(font) { name = "Station roof sign" };
                roofSignMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .2f);
                roofSignMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Navy);
            }
            return roofSignMaterial;
        }
    }
}
