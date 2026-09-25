using UnityEngine;

namespace ValleyRail
{
    /// <summary>The flag a service area flies once the player has bought it (ServiceSales), at the downstream end of its verge.</summary>
    public sealed partial class WorldView
    {
        static readonly Color OwnerFlag = new Color(.55f, .84f, .59f);

        void DrawOwnerFlag(in RoadsideLayout f)
        {
            const float U = 1.47f, V = .56f, Top = 1.15f;
            Part(f, "Owner's flagpole", U, Top / 2, V, .03f, Top, .03f, Forecourt.Post);
            Part(f, "Owner's flagpole finial", U, Top + .02f, V, .05f, .05f, .05f, Gold);
            // A broad flag with a cream band, flying back over the forecourt from the top of the pole.
            Part(f, "Owner's flag", U - .19f, Top - .13f, V + .02f, .36f, .22f, .016f, OwnerFlag);
            Part(f, "Owner's flag band", U - .19f, Top - .13f, V + .02f, .364f, .06f, .02f, Cream);
            Part(f, "Owner's flag fly", U - .39f, Top - .13f, V + .02f, .05f, .16f, .016f, OwnerFlag);
        }
    }
}
