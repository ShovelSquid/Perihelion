// Minimal shim so the real Fixed.cs (which has a single view-only FixedVec2.ToWorld returning
// UnityEngine.Vector3) compiles in this headless console. Never used by the sim itself.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
