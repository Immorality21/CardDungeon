using UnityEngine;

namespace ImmoralityGaming.Extensions
{
    public static class Vector3Extension
    {
        public static float ToFloat(this Vector3 vector3)
        {
            float f = vector3.x + vector3.y + vector3.z;
            return f;
        }

        public static int ToInt(this Vector3 vector3)
        {
            float f = vector3.x + vector3.y + vector3.z;
            return Mathf.RoundToInt(f);
        }
		
        public static Vector3 RoundToNearest(this Vector3 vector3)
        {
            var x = Mathf.Round(vector3.x);
            var y = Mathf.Round(vector3.y);
            var z = Mathf.Round(vector3.z);

            return new Vector3 { x = x, y = y, z = z };
        }
    }
}
