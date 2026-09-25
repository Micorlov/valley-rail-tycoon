using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    [CreateAssetMenu(menuName = "Valley Rail/Game Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        public Balance values = new Balance();
    }
}
