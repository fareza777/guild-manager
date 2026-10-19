using UnityEngine;

namespace Emberhall
{
    /// <summary>A pedestrian ring: neighboring segments stay outside the fountain's stone basin.</summary>
    public static class HallPath
    {
        public const int Nodes = 12;
        public static Vector2 Point(Vector2 center, Vector2 radius, int index)
        {
            float angle = (index % Nodes) * Mathf.PI * 2 / Nodes;
            return center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
        }
        public static int Next(int index, int direction) => (index + direction + Nodes) % Nodes;
    }
}
