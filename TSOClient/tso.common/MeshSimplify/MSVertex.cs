using Microsoft.Xna.Framework;

namespace FSO.Common.MeshSimplify
{
    public struct MSVertex
    {
        public Vector3 p;
        public Vector2 t; //texcoord
        public int tstart, tcount;
        public SymmetricMatrix q;
        public bool border;
    }
}
